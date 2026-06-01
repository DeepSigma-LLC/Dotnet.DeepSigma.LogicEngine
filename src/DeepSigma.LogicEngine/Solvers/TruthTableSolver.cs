using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Brute-force SAT by enumerating every assignment over the variables of the
/// formula. Used as a reference oracle. Refuses to run on more than
/// <see cref="MaxVariables"/> variables.
/// </summary>
public sealed class TruthTableSolver
{
    /// <summary>The largest number of variables this solver will enumerate.</summary>
    public const int MaxVariables = 20;

    /// <summary>Solve a formula by exhaustive enumeration, returning the first satisfying model or unsatisfiable.</summary>
    public SatResult Solve(Formula formula)
    {
        var vars = Evaluator.Variables(formula).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        if (vars.Length > MaxVariables)
        {
            throw new InvalidOperationException(
                $"TruthTableSolver refuses to enumerate {vars.Length} variables (max {MaxVariables}). Use DpllSolver instead.");
        }

        if (vars.Length == 0)
        {
            return Evaluator.Evaluate(formula, new Dictionary<string, bool>())
                ? SatResult.Satisfiable(Model.Empty)
                : SatResult.Unsatisfiable;
        }

        var rows = 1L << vars.Length;
        var assignment = new Dictionary<string, bool>(vars.Length);
        for (var row = 0L; row < rows; row++)
        {
            for (var i = 0; i < vars.Length; i++)
            {
                assignment[vars[i]] = (row & (1L << i)) != 0;
            }
            if (Evaluator.Evaluate(formula, assignment))
            {
                return SatResult.Satisfiable(Model.From(assignment));
            }
        }
        return SatResult.Unsatisfiable;
    }
}
