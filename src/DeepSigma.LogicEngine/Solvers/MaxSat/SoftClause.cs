using DeepSigma.LogicEngine.Cnf;

namespace DeepSigma.LogicEngine.Solvers.MaxSat;

/// <summary>
/// A soft clause for MaxSAT: a clause that should be satisfied if possible,
/// carrying a positive penalty <see cref="Weight"/> incurred when it is not.
/// </summary>
public readonly record struct SoftClause(IReadOnlyList<Literal> Literals, long Weight)
{
    /// <summary>A soft clause over the given literals with the given penalty weight.</summary>
    public static SoftClause Of(long weight, params Literal[] literals) => new(literals, weight);
}

/// <summary>
/// Result of a MaxSAT solve. When <see cref="IsSatisfiable"/> is true, <see cref="Model"/> is an
/// assignment satisfying every hard clause (restricted to the original problem variables) and
/// <see cref="Cost"/> is the optimum — the minimum total weight of unsatisfied soft clauses. When
/// false, the hard clauses are unsatisfiable, so there is no feasible assignment: <see cref="Model"/>
/// is null and <see cref="Cost"/> is 0.
/// </summary>
public sealed record MaxSatResult(bool IsSatisfiable, IReadOnlyDictionary<string, bool>? Model, long Cost)
{
    /// <summary>The hard clauses are unsatisfiable — there is no feasible assignment.</summary>
    public static MaxSatResult Unsatisfiable { get; } = new(false, null, 0);

    /// <summary>A feasible optimum: a model satisfying every hard clause, with the minimum soft-clause cost.</summary>
    public static MaxSatResult Satisfiable(IReadOnlyDictionary<string, bool> model, long cost) => new(true, model, cost);
}
