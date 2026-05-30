using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;

namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>
/// High-level entry points: satisfiability, validity, equivalence, entailment,
/// model finding, model enumeration, and model counting.
///
/// <para>
/// Each method has a parameterless overload that uses the configured default
/// engine (see <see cref="UseSolver"/>) and an overload that takes an explicit
/// <see cref="ISatSolver"/>. The explicit overloads are useful for differential
/// testing — running the same query through two engines and comparing.
/// </para>
/// </summary>
public static class Reasoner
{
    private static Func<ISatSolver> _defaultSolverFactory = () => new CdclSolver();

    /// <summary>
    /// Replace the engine used by the parameterless overloads. Intended for
    /// configuration at start-up and for tests; not safe to change concurrently
    /// with in-flight reasoning calls.
    /// </summary>
    public static void UseSolver(Func<ISatSolver> factory)
        => _defaultSolverFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    private static ISatSolver DefaultSolver() => _defaultSolverFactory();

    // --- Satisfiability ---------------------------------------------------

    public static bool IsSatisfiable(Formula formula) => IsSatisfiable(formula, DefaultSolver());

    public static bool IsSatisfiable(Formula formula, ISatSolver solver)
        => SolveFormula(solver, formula).IsSatisfiable;

    public static bool IsUnsatisfiable(Formula formula) => !IsSatisfiable(formula);

    public static bool IsUnsatisfiable(Formula formula, ISatSolver solver) => !IsSatisfiable(formula, solver);

    // --- Validity / equivalence ------------------------------------------

    /// <summary>True if the formula is true under every assignment.</summary>
    public static bool IsValid(Formula formula) => IsValid(formula, DefaultSolver());

    public static bool IsValid(Formula formula, ISatSolver solver)
        => !IsSatisfiable(new Negation(formula), solver);

    /// <summary>True if a ↔ b is a tautology.</summary>
    public static bool AreEquivalent(Formula a, Formula b) => AreEquivalent(a, b, DefaultSolver());

    public static bool AreEquivalent(Formula a, Formula b, ISatSolver solver)
        => IsValid(new Biconditional(a, b), solver);

    // --- Entailment -------------------------------------------------------

    /// <summary>True if KB ⊨ query, i.e. every model of KB is a model of query.</summary>
    public static bool Entails(IEnumerable<Formula> knowledgeBase, Formula query)
        => Entails(knowledgeBase, query, DefaultSolver());

    public static bool Entails(IEnumerable<Formula> knowledgeBase, Formula query, ISatSolver solver)
        => Entails(Formula.All(knowledgeBase), query, solver);

    public static bool Entails(Formula knowledgeBase, Formula query)
        => Entails(knowledgeBase, query, DefaultSolver());

    public static bool Entails(Formula knowledgeBase, Formula query, ISatSolver solver)
        => !IsSatisfiable(new Conjunction(knowledgeBase, new Negation(query)), solver);

    // --- Model finding ----------------------------------------------------

    /// <summary>Find one satisfying model, or null if none.</summary>
    public static Model? FindModel(Formula formula) => FindModel(formula, DefaultSolver());

    public static Model? FindModel(Formula formula, ISatSolver solver)
    {
        var result = SolveFormula(solver, formula);
        return result.IsSatisfiable ? result.Model : null;
    }

    /// <summary>
    /// Enumerate every satisfying model. Each yielded model assigns every
    /// variable of <paramref name="formula"/>. Uses one incremental CDCL solver
    /// that accumulates a blocking clause per solution, reusing learned clauses
    /// across iterations.
    /// </summary>
    public static IEnumerable<Model> EnumerateModels(Formula formula)
        => EnumerateModels(formula, Evaluator.Variables(formula));

    /// <summary>
    /// Enumerate the distinct assignments of a <paramref name="projection"/> of the
    /// variables that extend to a model — blocking clauses are added over the
    /// projection only, so two full models that agree on the projection are
    /// reported once. Useful when auxiliary variables should not multiply the
    /// enumeration (e.g. counting structures up to a symmetry). The parameterless
    /// overload projects on every variable of the formula.
    /// </summary>
    public static IEnumerable<Model> EnumerateModels(Formula formula, IReadOnlySet<string> projection)
    {
        var originalVars = Evaluator.Variables(formula);
        var projected = projection.Where(originalVars.Contains).ToHashSet(StringComparer.Ordinal);
        if (projected.Count == 0)
        {
            if (IsSatisfiable(formula))
            {
                yield return Model.Empty;
            }
            yield break;
        }

        var prepared = CnfPreparer.Prepare(formula);
        var solver = new IncrementalCdclSolver(prepared.Cnf, originalVars);

        while (true)
        {
            var result = solver.Solve();
            if (!result.IsSatisfiable || result.Model is null)
            {
                yield break;
            }
            var model = CnfPreparer.Project(result.Model, projected);
            yield return model;

            var blocking = projected
                .Select(name => model[name] ? Literal.Negative(name) : Literal.Positive(name))
                .ToList();
            if (!solver.AddClause(blocking))
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Enumerate models using a caller-supplied engine. Rebuilds the working
    /// clause set each iteration, so it works with any <see cref="ISatSolver"/>.
    /// </summary>
    public static IEnumerable<Model> EnumerateModels(Formula formula, ISatSolver solver)
    {
        var originalVars = Evaluator.Variables(formula);
        if (originalVars.Count == 0)
        {
            if (Evaluator.Evaluate(formula, new Dictionary<string, bool>()))
            {
                yield return Model.Empty;
            }
            yield break;
        }

        // Convert once, then keep adding blocking clauses over the original
        // variables only.
        var prepared = CnfPreparer.Prepare(formula);
        var clauses = prepared.Cnf.Clauses.Where(c => !c.IsTautology()).ToList();

        while (true)
        {
            var result = solver.Solve(new CnfFormula(clauses));
            if (!result.IsSatisfiable || result.Model is null)
            {
                yield break;
            }
            var model = CnfPreparer.Project(result.Model, originalVars);
            yield return model;

            // Block the discovered model on the original variables.
            var blocking = originalVars.Select(name =>
                model[name] ? Literal.Negative(name) : Literal.Positive(name)).ToList();
            clauses.Add(new Clause(blocking));
        }
    }

    /// <summary>Count satisfying assignments over the formula's variables.</summary>
    public static long CountModels(Formula formula) => CountModels(formula, DefaultSolver());

    public static long CountModels(Formula formula, ISatSolver solver)
        => EnumerateModels(formula, solver).LongCount();

    private static SatResult SolveFormula(ISatSolver solver, Formula formula)
    {
        var prepared = CnfPreparer.Prepare(formula);
        var result = solver.Solve(prepared.Cnf);
        return result is { IsSatisfiable: true, Model: not null }
            ? SatResult.Satisfiable(CnfPreparer.Project(result.Model, prepared.OriginalVariables))
            : result;
    }
}
