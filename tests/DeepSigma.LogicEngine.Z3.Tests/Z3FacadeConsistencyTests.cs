using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// Verifies the verb set is consistent across the Z3 facades: IsUnsatisfiable is available on the
/// propositional and sorted facades (it already was on Z3SmtReasoner), and the sorted facade exposes
/// Entails like the others.
/// </summary>
public class Z3FacadeConsistencyTests
{
    [Fact]
    public void Z3Reasoner_IsUnsatisfiable()
    {
        Assert.True(Z3Reasoner.IsUnsatisfiable(Formula.Parse("p & !p")));
        Assert.False(Z3Reasoner.IsUnsatisfiable(Formula.Parse("p | q")));
    }

    [Fact]
    public void Z3SortedSolver_IsUnsatisfiable()
    {
        Assert.True(Z3SortedSolver.IsUnsatisfiable(SortedExpr.Parse("bv8 x; x != x")));
        Assert.False(Z3SortedSolver.IsUnsatisfiable(SortedExpr.Parse("bv8 x; x == #x00")));
    }

    [Fact]
    public void Z3SortedSolver_Entails()
    {
        var kb = new[] { SortedExpr.Parse("int n; n > 5") };
        Assert.True(Z3SortedSolver.Entails(kb, SortedExpr.Parse("int n; n > 0")));   // n>5 ⊨ n>0
        Assert.False(Z3SortedSolver.Entails(kb, SortedExpr.Parse("int n; n > 10"))); // n>5 ⊭ n>10
    }

    [Fact]
    public void SortedExpr_AllAny_FoldCorrectly()
    {
        var x = SortedExpr.BitVecVar("x", 8);
        var eq0 = SortedExpr.Eq(x, SortedExpr.BitVec(0, 8));
        var eq1 = SortedExpr.Eq(x, SortedExpr.BitVec(1, 8));

        Assert.True(Z3SortedSolver.IsSatisfiable(SortedExpr.Any(new[] { eq0, eq1 })));   // x==0 ∨ x==1
        Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.All(new[] { eq0, eq1 })));  // x==0 ∧ x==1

        // Empty-sequence identities: All -> true, Any -> false.
        Assert.True(Z3SortedSolver.IsSatisfiable(SortedExpr.All(Array.Empty<SortedExpr>())));
        Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.Any(Array.Empty<SortedExpr>())));
    }
}
