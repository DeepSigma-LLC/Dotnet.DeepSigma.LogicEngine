using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// String / sequence theory — another capability with no native equivalent, solved only via Z3.
/// Known-answer checks over length, concatenation, and the containment predicates, plus a
/// constraint-solving query that recovers an unknown string.
/// </summary>
public class Z3StringTests
{
    [Fact]
    public void Length_OfLiteral_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.Eq(SortedExpr.Length(SortedExpr.Str("hello")), SortedExpr.Int(5))));

    [Fact]
    public void Length_Mismatch_IsUnsatisfiable()
        => Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.Eq(SortedExpr.Length(SortedExpr.Str("hi")), SortedExpr.Int(5))));

    [Fact]
    public void Concat_OfLiterals_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(
            SortedExpr.Eq(SortedExpr.StringConcat(SortedExpr.Str("foo"), SortedExpr.Str("bar")), SortedExpr.Str("foobar"))));

    [Fact]
    public void Contains_True_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.Contains(SortedExpr.Str("foobar"), SortedExpr.Str("oob"))));

    [Fact]
    public void Contains_False_IsUnsatisfiable()
        => Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.Contains(SortedExpr.Str("foobar"), SortedExpr.Str("xyz"))));

    [Fact]
    public void PrefixOf_True_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.PrefixOf(SortedExpr.Str("foo"), SortedExpr.Str("foobar"))));

    [Fact]
    public void SuffixOf_True_IsValid()
        => Assert.True(Z3SortedSolver.IsValid(SortedExpr.SuffixOf(SortedExpr.Str("bar"), SortedExpr.Str("foobar"))));

    [Fact]
    public void SuffixOf_False_IsUnsatisfiable()
        => Assert.False(Z3SortedSolver.IsSatisfiable(SortedExpr.SuffixOf(SortedExpr.Str("foo"), SortedExpr.Str("foobar"))));

    [Fact]
    public void SolveForUnknownString_IsSatisfiable()
    {
        // Find s such that s ++ "bar" = "foobar" and |s| = 3 — the unique answer is "foo".
        var s = SortedExpr.StringVar("s");
        var query = SortedExpr.And(
            SortedExpr.Eq(SortedExpr.StringConcat(s, SortedExpr.Str("bar")), SortedExpr.Str("foobar")),
            SortedExpr.Eq(SortedExpr.Length(s), SortedExpr.Int(3)));
        Assert.True(Z3SortedSolver.IsSatisfiable(query));
    }

    [Fact]
    public void ContradictoryLengthAndConcat_IsUnsatisfiable()
    {
        // s ++ "bar" = "foobar" forces |s| = 3, contradicting |s| = 2.
        var s = SortedExpr.StringVar("s");
        var query = SortedExpr.And(
            SortedExpr.Eq(SortedExpr.StringConcat(s, SortedExpr.Str("bar")), SortedExpr.Str("foobar")),
            SortedExpr.Eq(SortedExpr.Length(s), SortedExpr.Int(2)));
        Assert.False(Z3SortedSolver.IsSatisfiable(query));
    }
}
