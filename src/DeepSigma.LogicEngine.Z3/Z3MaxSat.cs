using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers.MaxSat;
using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>The outcome of a Z3 MaxSAT optimization: a status, the optimal model, and its cost.</summary>
public sealed record Z3MaxSatResult(Z3Status Status, Z3Model? Model, long Cost);

/// <summary>
/// Weighted partial MaxSAT via Z3's native optimizer (νZ): find an assignment satisfying every
/// hard clause that minimizes the total weight of unsatisfied soft clauses. Mirrors the native
/// <see cref="MaxSatSolver"/>'s inputs.
/// </summary>
public static class Z3MaxSat
{
    public static Z3MaxSatResult Solve(
        IEnumerable<IReadOnlyList<Literal>> hardClauses,
        IEnumerable<SoftClause> softClauses,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hard = hardClauses.ToList();
        var soft = softClauses.ToList();

        using var ctx = new MZ3.Context();
        var optimize = ctx.MkOptimize();
        if (timeout is { } t)
        {
            optimize.Set("timeout", (uint)Math.Clamp(t.TotalMilliseconds, 0, uint.MaxValue));
        }
        using var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(static state => ((MZ3.Context)state!).Interrupt(), ctx)
            : default;

        var constants = new Dictionary<string, MZ3.BoolExpr>(StringComparer.Ordinal);
        MZ3.BoolExpr Var(string name)
        {
            if (!constants.TryGetValue(name, out var c))
            {
                c = ctx.MkBoolConst(name);
                constants[name] = c;
            }
            return c;
        }
        MZ3.BoolExpr Lit(Literal literal) => literal.Negated ? ctx.MkNot(Var(literal.Variable)) : Var(literal.Variable);
        MZ3.BoolExpr ClauseExpr(IReadOnlyList<Literal> literals)
            => literals.Count == 0 ? ctx.MkFalse() : ctx.MkOr(literals.Select(Lit).ToArray());

        foreach (var clause in hard)
        {
            optimize.Assert(ClauseExpr(clause));
        }
        foreach (var clause in soft)
        {
            optimize.AssertSoft(ClauseExpr(clause.Literals), (uint)Math.Clamp(clause.Weight, 0, uint.MaxValue), "soft");
        }

        var status = optimize.Check();
        if (status != MZ3.Status.SATISFIABLE)
        {
            return new Z3MaxSatResult(status == MZ3.Status.UNSATISFIABLE ? Z3Status.Unsatisfiable : Z3Status.Unknown, null, 0);
        }

        var model = optimize.Model;
        long cost = 0;
        foreach (var clause in soft)
        {
            if (!model.Eval(ClauseExpr(clause.Literals), completion: true).IsTrue)
            {
                cost += clause.Weight;
            }
        }
        var z3Model = Z3Modeling.Build(model, constants.ToDictionary(kv => kv.Key, kv => (MZ3.Expr)kv.Value, StringComparer.Ordinal));
        return new Z3MaxSatResult(Z3Status.Satisfiable, z3Model, cost);
    }
}
