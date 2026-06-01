using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers.Cdcl;

namespace DeepSigma.LogicEngine.Solvers.MaxSat;

/// <summary>
/// Core-guided weighted partial MaxSAT (Fu-Malik / WPM1 lineage). Finds an
/// assignment satisfying every hard clause and minimizing the total weight of
/// unsatisfied soft clauses, by repeatedly extracting an unsatisfiable core of
/// soft clauses, paying its minimum weight, and relaxing it (allowing one more
/// of its members to be given up). Builds on the incremental CDCL solver's
/// assumption-core extraction and the cardinality encoders.
/// </summary>
public sealed class MaxSatSolver
{
    private sealed class Soft
    {
        public required IReadOnlyList<Literal> Literals { get; init; }
        public required long Weight { get; set; }
        public List<string> Relaxations { get; init; } = new();
        public string SelectorName { get; set; } = string.Empty; // set each round
    }

    private readonly IncrementalCdclSolver _solver;
    private readonly List<Soft> _softs;
    private readonly HashSet<string> _originalVariables;
    private int _freshCounter;

    /// <summary>Create a solver over the given hard clauses (must hold) and weighted soft clauses (penalized when violated).</summary>
    public MaxSatSolver(
        IEnumerable<IReadOnlyList<Literal>> hardClauses,
        IEnumerable<SoftClause> softClauses,
        SolverOptions? options = null)
    {
        var hard = hardClauses.Select(c => new Clause(c)).ToList();
        _softs = softClauses.Select(s => new Soft { Literals = s.Literals, Weight = s.Weight }).ToList();

        _originalVariables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clause in hard)
        {
            foreach (var lit in clause.Literals)
            {
                _originalVariables.Add(lit.Variable);
            }
        }
        foreach (var soft in _softs)
        {
            foreach (var lit in soft.Literals)
            {
                _originalVariables.Add(lit.Variable);
            }
        }

        _solver = new IncrementalCdclSolver(new CnfFormula(hard), _originalVariables, options);
    }

    /// <summary>Find an assignment satisfying every hard clause and minimizing the total weight of unsatisfied soft clauses.</summary>
    public MaxSatResult Solve()
    {
        var lowerBound = 0L;

        while (true)
        {
            var selectors = RefreshSoftClauses();
            var result = _solver.SolveUnderWithCore(selectors);
            if (result.IsSatisfiable)
            {
                return new MaxSatResult(ProjectModel(result.Model!), lowerBound);
            }

            var core = result.FailedAssumptions!;
            if (core.Count == 0)
            {
                throw new InvalidOperationException("The hard clauses are unsatisfiable; MaxSAT has no feasible solution.");
            }

            lowerBound += RelaxCore(core);
        }
    }

    /// <summary>
    /// Re-add each soft clause this round as <c>(orig ∨ relaxations ∨ ¬selector)</c>
    /// with a fresh selector, and return the selector literals to assume. Old
    /// rounds' clauses become inert (their selectors are never assumed again).
    /// </summary>
    private List<Literal> RefreshSoftClauses()
    {
        var selectors = new List<Literal>(_softs.Count);
        foreach (var soft in _softs)
        {
            var selector = Fresh("sel");
            soft.SelectorName = selector;

            var clause = new List<Literal>(soft.Literals.Count + soft.Relaxations.Count + 1);
            clause.AddRange(soft.Literals);
            foreach (var relax in soft.Relaxations)
            {
                clause.Add(Literal.Positive(relax));
            }
            clause.Add(Literal.Negative(selector));
            _solver.AddClause(clause);

            selectors.Add(Literal.Positive(selector));
        }
        return selectors;
    }

    /// <summary>Pay the core's minimum weight, weight-split heavier members, and relax it.</summary>
    private long RelaxCore(IReadOnlyList<Literal> core)
    {
        var coreSelectors = core.Select(l => l.Variable).ToHashSet(StringComparer.Ordinal);
        var coreSofts = _softs.Where(s => coreSelectors.Contains(s.SelectorName)).ToList();
        var wmin = coreSofts.Min(s => s.Weight);

        var blockers = new List<string>(coreSofts.Count);
        foreach (var soft in coreSofts)
        {
            if (soft.Weight > wmin)
            {
                // Split off the residual weight as an independent clone.
                _softs.Add(new Soft
                {
                    Literals = soft.Literals,
                    Weight = soft.Weight - wmin,
                    Relaxations = new List<string>(soft.Relaxations),
                });
                soft.Weight = wmin;
            }

            var blocker = Fresh("blk");
            _solver.NewVariable(blocker);
            soft.Relaxations.Add(blocker);
            blockers.Add(blocker);
        }

        // Exactly one of this core's new blockers may fire (relax exactly one member).
        var inputs = blockers.Select(b => (Formula)Formula.Var(b)).ToList();
        var cnf = CnfTransformer.ToCnf(Cardinality.ExactlyOne(inputs));
        foreach (var clause in cnf.Clauses)
        {
            _solver.AddClause(clause.Literals);
        }

        return wmin;
    }

    private IReadOnlyDictionary<string, bool> ProjectModel(Model model)
    {
        var projected = new Dictionary<string, bool>(_originalVariables.Count, StringComparer.Ordinal);
        foreach (var name in _originalVariables)
        {
            projected[name] = model.TryGetValue(name, out var v) && v;
        }
        return projected;
    }

    private string Fresh(string kind)
    {
        var name = $"__maxsat_{kind}_{_freshCounter++}";
        _solver.NewVariable(name);
        return name;
    }
}
