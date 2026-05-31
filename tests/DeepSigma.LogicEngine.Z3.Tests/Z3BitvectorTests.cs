using System.Numerics;
using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// Bit-vector theory (QF_BV) — a capability with no native equivalent, solved only via Z3.
/// Known-answer checks: wraparound overflow, bitwise identities, signed vs unsigned comparison,
/// and concat/extract.
/// </summary>
public class Z3BitvectorTests
{
    private static readonly SortedExpr X = SortedExpr.BitVecVar("x", 8);
    private static readonly SortedExpr Zero = SortedExpr.BitVec(0, 8);
    private static readonly SortedExpr One = SortedExpr.BitVec(1, 8);

    [Fact]
    public void Overflow_WrapsAround()
    {
        // x + 1 == 0 (mod 2^8) has the unique 8-bit solution x = 255.
        var result = Z3Sorted.Solve(SortedExpr.Eq(X + One, Zero));
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)255, result.Model!["x"]!.Integer);
    }

    [Fact]
    public void BitwiseIdentities_AreValid()
    {
        Assert.True(Z3Sorted.IsValid(SortedExpr.Eq(X & X, X)));                 // x & x = x
        Assert.True(Z3Sorted.IsValid(SortedExpr.Eq(X | Zero, X)));             // x | 0 = x
        Assert.True(Z3Sorted.IsValid(SortedExpr.Eq(X ^ X, Zero)));             // x ^ x = 0
        Assert.True(Z3Sorted.IsValid(SortedExpr.Eq(~(~X), X)));                // ~~x = x
    }

    [Fact]
    public void SignedAndUnsigned_Differ()
    {
        var max = SortedExpr.BitVec(255, 8);   // 255 unsigned, -1 signed (8-bit)
        Assert.True(Z3Sorted.IsValid(SortedExpr.Slt(max, Zero)));        // -1 < 0 (signed)
        Assert.False(Z3Sorted.IsSatisfiable(SortedExpr.Ult(max, Zero))); // 255 < 0 (unsigned) is never true
    }

    [Fact]
    public void ConcatAndExtract_RoundTrip()
    {
        var hi = SortedExpr.BitVec(0xAB, 8);
        var lo = SortedExpr.BitVec(0xCD, 8);
        var word = SortedExpr.Concat(hi, lo);   // 16-bit 0xABCD
        Assert.True(Z3Sorted.IsValid(SortedExpr.Eq(SortedExpr.Extract(15, 8, word), hi)));
        Assert.True(Z3Sorted.IsValid(SortedExpr.Eq(SortedExpr.Extract(7, 0, word), lo)));
    }

    [Fact]
    public void Contradiction_IsUnsatisfiable()
        => Assert.False(Z3Sorted.IsSatisfiable(SortedExpr.And(SortedExpr.Eq(X, Zero), SortedExpr.Distinct(X, Zero))));

    [Fact]
    public void Multiply_FindsModel()
    {
        // 2*x = 6 (mod 256): solutions x = 3 and x = 131.
        var result = Z3Sorted.Solve(SortedExpr.Eq(SortedExpr.BitVec(2, 8) * X, SortedExpr.BitVec(6, 8)));
        Assert.True(result.IsSatisfiable);
        var x = result.Model!["x"]!.Integer;
        Assert.True(x == 3 || x == 131);
    }
}
