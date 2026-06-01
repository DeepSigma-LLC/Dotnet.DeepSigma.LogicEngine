using System.Numerics;
using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// Integer/real arithmetic in the sorted layer, including <b>nonlinear</b> (var·var) constraints —
/// which the native engine's linear-only atoms cannot express.
/// </summary>
public class Z3NonlinearTests
{
    [Fact]
    public void Nonlinear_Integer_FindsModel()
    {
        // x * x == 49, x > 0  ⇒  x = 7.
        var x = SortedExpr.IntVar("x");
        var constraint = SortedExpr.And(
            SortedExpr.Eq(x * x, SortedExpr.Int(49)),
            SortedExpr.Gt(x, SortedExpr.Int(0)));
        var result = Z3SortedSolver.Solve(constraint);
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)7, result.Model!["x"]!.Integer);
    }

    [Fact]
    public void Nonlinear_Integer_Unsatisfiable()
    {
        // x*x = 2 has no integer (or rational) solution.
        var x = SortedExpr.IntVar("x");
        Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.Eq(x * x, SortedExpr.Int(2))));
    }

    [Fact]
    public void Real_LinearModel_IsExact()
    {
        // 2*x = 1 over the reals ⇒ x = 1/2.
        var x = SortedExpr.RealVar("x");
        var result = Z3SortedSolver.Solve(SortedExpr.Eq(SortedExpr.Real(2) * x, SortedExpr.Real(1)));
        Assert.True(result.IsSatisfiable);
        Assert.Equal(Rational.Of(1, 2), result.Model!["x"]!.Rational);
    }

    [Fact]
    public void Integer_IsUnbounded()
    {
        // Pure sorted integers are unbounded (no native LIA box). x > 1000000 is satisfiable.
        var x = SortedExpr.IntVar("x");
        Assert.True(Z3SortedSolver.IsSatisfiable(SortedExpr.Gt(x, SortedExpr.Int(1_000_000))));
    }

    [Fact]
    public void Arithmetic_Validity()
    {
        // ∀ integer x (here free): x + 1 > x is valid.
        var x = SortedExpr.IntVar("x");
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Gt(x + SortedExpr.Int(1), x)));
    }
}
