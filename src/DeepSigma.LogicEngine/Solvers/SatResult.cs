using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Result of a satisfiability check. <see cref="IsSatisfiable"/> indicates the
/// answer; if true, <see cref="Model"/> is a satisfying assignment for at least
/// the variables of the original input (auxiliary variables may also be present).
/// </summary>
public sealed record SatResult(bool IsSatisfiable, Model? Model)
{
    /// <summary>The shared unsatisfiable result (no model).</summary>
    public static SatResult Unsatisfiable { get; } = new(false, null);
    /// <summary>Creates a satisfiable result carrying the given model.</summary>
    public static SatResult Satisfiable(Model model) => new(true, model);
}
