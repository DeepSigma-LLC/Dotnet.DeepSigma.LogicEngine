using DeepSigma.LogicEngine.Cnf;

namespace DeepSigma.LogicEngine.Solvers.MaxSat;

/// <summary>
/// A soft clause for MaxSAT: a clause that should be satisfied if possible,
/// carrying a positive penalty <see cref="Weight"/> incurred when it is not.
/// </summary>
public readonly record struct SoftClause(IReadOnlyList<Literal> Literals, long Weight)
{
    public static SoftClause Of(long weight, params Literal[] literals) => new(literals, weight);
}

/// <summary>
/// Result of a MaxSAT solve: an assignment satisfying all hard clauses, the
/// total <see cref="Cost"/> (sum of weights of unsatisfied soft clauses, i.e.
/// the optimum), restricted to the original problem variables.
/// </summary>
public sealed record MaxSatResult(IReadOnlyDictionary<string, bool> Model, long Cost);
