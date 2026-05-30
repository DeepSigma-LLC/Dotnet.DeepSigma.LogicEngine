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
    public static UnsatCoreResult Satisfiable(Model model) => new(true, model, null);

    public static UnsatCoreResult Unsatisfiable(IReadOnlyList<Literal> failedAssumptions)
        => new(false, null, failedAssumptions);
}
