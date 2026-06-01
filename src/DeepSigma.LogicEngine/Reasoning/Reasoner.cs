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
/// Each method has a parameterless overload that uses the default CDCL engine and
/// an overload that takes an explicit <see cref="ISatSolver"/>. Pass the explicit
/// overload to choose a different engine (e.g. a Z3-backed solver) or for
/// differential testing — running the same query through two engines and comparing.
/// </para>
/// </summary>
public static class Reasoner
{
    private static ISatSolver DefaultSolver() => new CdclSolver();

    // --- Satisfiability ---------------------------------------------------

    /// <summary>True if some assignment satisfies the formula.</summary>
    public static bool IsSatisfiable(Formula formula) => IsSatisfiable(formula, DefaultSolver());

    /// <summary>True if some assignment satisfies the formula, decided with the given engine.</summary>
    public static bool IsSatisfiable(Formula formula, ISatSolver solver)
        => SolveFormula(solver, formula).IsSatisfiable;

    /// <summary>True if no assignment satisfies the formula.</summary>
    public static bool IsUnsatisfiable(Formula formula) => !IsSatisfiable(formula);

    /// <summary>True if no assignment satisfies the formula, decided with the given engine.</summary>
    public static bool IsUnsatisfiable(Formula formula, ISatSolver solver) => !IsSatisfiable(formula, solver);

    // --- Validity / equivalence ------------------------------------------

    /// <summary>True if the formula is true under every assignment.</summary>
    public static bool IsValid(Formula formula) => IsValid(formula, DefaultSolver());

    /// <summary>True if the formula is true under every assignment, decided with the given engine.</summary>
    public static bool IsValid(Formula formula, ISatSolver solver)
        => !IsSatisfiable(new Negation(formula), solver);

    /// <summary>True if a ↔ b is a tautology.</summary>
    public static bool AreEquivalent(Formula a, Formula b) => AreEquivalent(a, b, DefaultSolver());

    /// <summary>True if a ↔ b is a tautology, decided with the given engine.</summary>
    public static bool AreEquivalent(Formula a, Formula b, ISatSolver solver)
        => IsValid(new Biconditional(a, b), solver);

    // --- Entailment -------------------------------------------------------

    /// <summary>True if KB ⊨ query, i.e. every model of KB is a model of query.</summary>
    public static bool Entails(IEnumerable<Formula> knowledgeBase, Formula query)
        => Entails(knowledgeBase, query, DefaultSolver());

    /// <summary>True if KB ⊨ query, decided with the given engine.</summary>
    public static bool Entails(IEnumerable<Formula> knowledgeBase, Formula query, ISatSolver solver)
        => Entails(Formula.All(knowledgeBase), query, solver);

    /// <summary>True if KB ⊨ query, i.e. every model of KB is a model of query.</summary>
    public static bool Entails(Formula knowledgeBase, Formula query)
        => Entails(knowledgeBase, query, DefaultSolver());

    /// <summary>True if KB ⊨ query, decided with the given engine.</summary>
    public static bool Entails(Formula knowledgeBase, Formula query, ISatSolver solver)
        => !IsSatisfiable(new Conjunction(knowledgeBase, new Negation(query)), solver);

    // --- Model finding ----------------------------------------------------

    /// <summary>Find one satisfying model, or null if none.</summary>
    public static Model? FindModel(Formula formula) => FindModel(formula, DefaultSolver());

    /// <summary>Find one satisfying model, or null if none, using the given engine.</summary>
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

            var blocking = BlockingClause(model, projected);
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
            clauses.Add(new Clause(BlockingClause(model, originalVars)));
        }
    }

    /// <summary>Count satisfying assignments over the formula's variables.</summary>
    public static long CountModels(Formula formula) => CountModels(formula, DefaultSolver());

    /// <summary>Count satisfying assignments over the formula's variables, using the given engine.</summary>
    public static long CountModels(Formula formula, ISatSolver solver)
        => EnumerateModels(formula, solver).LongCount();

    /// <summary>
    /// The clause that rules out <paramref name="model"/> on the given
    /// <paramref name="variables"/>: the disjunction of each variable's complement
    /// literal, so any later model must differ on at least one of them.
    /// </summary>
    private static List<Literal> BlockingClause(Model model, IReadOnlySet<string> variables)
        => variables.Select(name => model[name] ? Literal.Negative(name) : Literal.Positive(name)).ToList();

    private static SatResult SolveFormula(ISatSolver solver, Formula formula)
    {
        var prepared = CnfPreparer.Prepare(formula);
        var result = solver.Solve(prepared.Cnf);
        return result is { IsSatisfiable: true, Model: not null }
            ? SatResult.Satisfiable(CnfPreparer.Project(result.Model, prepared.OriginalVariables))
            : result;
    }
}
