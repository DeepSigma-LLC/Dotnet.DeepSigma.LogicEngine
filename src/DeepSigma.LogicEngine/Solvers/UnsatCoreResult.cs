using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Result of an assumption-based solve that also reports, on failure, the subset
/// of assumptions responsible. When unsatisfiable purely because of the formula
/// (independent of the assumptions), <see cref="FailedAssumptions"/> is empty.
/// </summary>
public sealed record UnsatCoreResult(bool IsSatisfiable, Model? Model, IReadOnlyList<Literal>? FailedAssumptions)
{
    /// <summary>Creates a satisfiable result carrying the given model and no failed assumptions.</summary>
    public static UnsatCoreResult Satisfiable(Model model) => new(true, model, null);

    /// <summary>Creates an unsatisfiable result carrying the subset of assumptions responsible.</summary>
    public static UnsatCoreResult Unsatisfiable(IReadOnlyList<Literal> failedAssumptions)
        => new(false, null, failedAssumptions);
}
