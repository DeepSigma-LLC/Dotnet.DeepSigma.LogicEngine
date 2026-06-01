using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// Quantifiers (∀/∃) over the sorted layer — full SMT, which the native quantifier-free engine
/// cannot express. Examples are linear-integer (Presburger), which Z3 decides; quantified
/// nonlinear queries can legitimately return <see cref="Z3Status.Unknown"/>.
/// </summary>
public class Z3QuantifierTests
{
    private static readonly SortedExpr X = SortedExpr.IntVar("x");
    private static readonly SortedExpr Y = SortedExpr.IntVar("y");

    [Fact]
    public void Forall_Successor_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.ForAll(X, SortedExpr.Gt(X + SortedExpr.Int(1), X))));

    [Fact]
    public void Forall_AllPositive_IsNotValid()
        => Assert.False(Z3SortedSolver.IsValid(SortedExpr.ForAll(X, SortedExpr.Gt(X, SortedExpr.Int(0)))));

    [Fact]
    public void Exists_EvenTarget_IsSatisfiable()
        => Assert.True(Z3SortedSolver.IsSatisfiable(SortedExpr.Exists(X, SortedExpr.Eq(SortedExpr.Int(2) * X, SortedExpr.Int(10)))));

    [Fact]
    public void Exists_OddTarget_IsUnsatisfiable()
        => Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.Exists(X, SortedExpr.Eq(SortedExpr.Int(2) * X, SortedExpr.Int(7)))));

    [Fact]
    public void NestedQuantifiers_NoGreatestInteger_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.ForAll(X, SortedExpr.Exists(Y, SortedExpr.Gt(Y, X)))));

    [Fact]
    public void MultiVariable_Commutativity_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.ForAll(new[] { X, Y }, SortedExpr.Eq(X + Y, Y + X))));
}
