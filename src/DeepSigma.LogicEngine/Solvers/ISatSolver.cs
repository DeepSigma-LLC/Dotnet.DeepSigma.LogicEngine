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
    SatResult Solve(CnfFormula formula);
}
