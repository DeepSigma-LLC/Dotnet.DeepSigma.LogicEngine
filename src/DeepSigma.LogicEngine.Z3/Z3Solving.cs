using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>Runs the session's solver and maps Z3's check outcome to a <see cref="Z3Result"/>.</summary>
internal static class Z3Solving
{
    public static Z3Result Run(Z3Session session, IReadOnlyDictionary<string, MZ3.Expr> constants)
        => session.Solver.Check() switch
        {
            MZ3.Status.SATISFIABLE => new Z3Result(Z3Status.Satisfiable, Z3Modeling.Build(session.Solver.Model, constants)),
            MZ3.Status.UNSATISFIABLE => new Z3Result(Z3Status.Unsatisfiable, null),
            _ => new Z3Result(Z3Status.Unknown, null),
        };
}
