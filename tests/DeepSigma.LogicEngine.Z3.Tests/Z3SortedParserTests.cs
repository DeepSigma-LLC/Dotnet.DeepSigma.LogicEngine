using System.Numerics;
using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// The textual front end for the typed sorted layer (<see cref="Z3SortedParser"/> /
/// <see cref="Z3SortedPrinter"/>). Covers parse-then-solve across all four Z3-only theories,
/// structural round-tripping (parse∘print = identity), and rejection of malformed / ill-sorted input.
/// </summary>
public class Z3SortedParserTests
{
    // --- parse then solve: bit-vectors ---

    [Fact]
    public void Parse_BitVectorOverflow_SolvesToMax()
    {
        var result = Z3SortedSolver.Solve(SortedExpr.Parse("bv8 x; x + 1 == 0"));
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)255, result.Model!["x"]!.Integer);
    }

    [Fact]
    public void Parse_BitVectorLiteralHex_Solves()
    {
        var result = Z3SortedSolver.Solve(SortedExpr.Parse("bv8 x; x == #xFF"));
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)255, result.Model!["x"]!.Integer);
    }

    [Fact]
    public void Parse_BitVectorFunctions_AreValid()
    {
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("bv8 x; bvand(x, x) == x")));
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("bv8 x; ~(~x) == x")));
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("bv8 x; extract(7, 0, concat(x, x)) == x")));
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("bv8 x; slt(#xFF, #x00)")));   // -1 < 0 signed
    }

    // --- parse then solve: arithmetic, quantifiers, strings ---

    [Fact]
    public void Parse_NonlinearArithmetic_FindsRoot()
    {
        var result = Z3SortedSolver.Solve(SortedExpr.Parse("int n; n * n == 49 & n > 0"));
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)7, result.Model!["n"]!.Integer);
    }

    [Fact]
    public void Parse_RealArithmetic_ExactModel()
    {
        var result = Z3SortedSolver.Solve(SortedExpr.Parse("real x; 2*x == 1"));
        Assert.True(result.IsSatisfiable);
        Assert.Equal(Rational.Of(1, 2), result.Model!["x"]!.Rational);
    }

    [Fact]
    public void Parse_Quantifiers_DecideValidity()
    {
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("forall int n . n + 1 > n")));
        Assert.False(Z3SortedSolver.IsValid(SortedExpr.Parse("forall int n . n > 0")));
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("forall int x, int y . x + y == y + x")));
        Assert.True(Z3SortedSolver.IsSatisfiable(SortedExpr.Parse("exists int x . 2*x == 10")));
    }

    [Fact]
    public void Parse_Strings_Solve()
    {
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("\"foo\" ++ \"bar\" == \"foobar\"")));
        Assert.True(Z3SortedSolver.IsValid(SortedExpr.Parse("contains(\"foobar\", \"oob\")")));
        Assert.True(Z3SortedSolver.IsSatisfiable(SortedExpr.Parse("string s; s ++ \"bar\" == \"foobar\" & |s| == 3")));
    }

    [Fact]
    public void Parse_LiteralFirst_InfersSort()
    {
        // The literal appears before the variable, so its sort must be inferred from the sibling.
        var result = Z3SortedSolver.Solve(SortedExpr.Parse("bv8 x; 1 + x == 0"));
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)255, result.Model!["x"]!.Integer);
    }

    // --- round-trip: parse(print(e)) == e ---

    [Theory]
    [InlineData("bv8 x; x + 1 == 0")]
    [InlineData("bv8 x, y; x + 1 == 0 & y == ~x")]
    [InlineData("bv8 x; bvand(x, x) == x")]
    [InlineData("bv16 x; ult(x, #b0000000000000001)")]
    [InlineData("int n; n * n == 49 & n > 0")]
    [InlineData("real x; 2/1 * x == 1/1")]
    [InlineData("bool p, q; p -> (q -> p)")]
    [InlineData("forall int n . n + 1 > n")]
    [InlineData("forall int x, int y . x + y == y + x")]
    [InlineData("exists int x . 2 * x == 10")]
    [InlineData("string s; s ++ \"bar\" == \"foobar\" & |s| == 3")]
    public void RoundTrips(string source)
    {
        // parse∘print = identity: printing an expression and reparsing the text yields a structurally
        // equal AST (SortedExpr nodes — including quantifiers — have value equality).
        var parsed = SortedExpr.Parse(source);
        var reparsed = SortedExpr.Parse(parsed.ToString());
        Assert.Equal(parsed, reparsed);
    }

    [Fact]
    public void Quantifiers_HaveValueEquality()
    {
        // Independently parsed identical quantifiers compare equal (and differing ones do not).
        Assert.Equal(SortedExpr.Parse("forall int x, int y . x + y == y + x"),
                     SortedExpr.Parse("forall int x, int y . x + y == y + x"));
        Assert.NotEqual(SortedExpr.Parse("forall int n . n + 1 > n"),
                        SortedExpr.Parse("exists int n . n + 1 > n"));
    }

    [Fact]
    public void Print_ProducesDeclarationPrefix()
    {
        var x = SortedExpr.BitVecVar("x", 8);
        var formula = SortedExpr.Eq(x + SortedExpr.BitVec(1, 8), SortedExpr.BitVec(0, 8));
        Assert.Equal("bv8 x; x + #b00000001 == #b00000000", formula.ToString());
    }

    // --- malformed / ill-sorted input is rejected ---

    [Theory]
    [InlineData("int n; n +")]                 // truncated
    [InlineData("x + 1 == 0")]                 // undeclared variable
    [InlineData("int n; n + 1")]               // top-level is not boolean
    [InlineData("int n; bv8 x; n == x")]       // equality across sorts
    [InlineData("bv8 x; x < 1")]               // arithmetic compare on a bit-vector
    [InlineData("int forall; forall == 0")]    // reserved word as a variable
    [InlineData("int n n; n == 0")]            // missing ';'
    [InlineData("bv8 x; bvand(x) == x")]       // wrong arity
    public void Rejects(string source)
        => Assert.False(Z3SortedParser.TryParse(source, out _));
}
