using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Result of a satisfiability check. <see cref="IsSatisfiable"/> indicates the
/// answer; if true, <see cref="Model"/> is a satisfying assignment for at least
/// the variables of the original input (auxiliary variables may also be present).
/// </summary>
public sealed record SatResult(bool IsSatisfiable, Model? Model)
{
    public static SatResult Unsatisfiable { get; } = new(false, null);
    public static SatResult Satisfiable(Model model) => new(true, model);
}
