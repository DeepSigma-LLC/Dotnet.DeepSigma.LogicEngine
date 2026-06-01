using DeepSigma.LogicEngine.Cnf;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// A satisfiability decision procedure over CNF. This is the shared, swappable
/// contract: any two implementations must agree on satisfiability and, when
/// satisfiable, return a model that satisfies the input. Solving an arbitrary
/// <see cref="Formulas.Formula"/> is a per-solver convenience built on top of
/// this via <see cref="CnfPreparer"/>; it is intentionally not part of the
/// interface.
/// </summary>
public interface ISatSolver
{
    /// <summary>
    /// Decide the CNF formula, returning satisfiability and (if satisfiable) a satisfying model.
    /// Pass a <paramref name="cancellationToken"/> to abort a long search cooperatively; on
    /// cancellation the call throws <see cref="OperationCanceledException"/>. (For a wall-clock
    /// limit, pass <c>new CancellationTokenSource(duration).Token</c>.)
    /// </summary>
    SatResult Solve(CnfFormula formula, CancellationToken cancellationToken = default);
}
