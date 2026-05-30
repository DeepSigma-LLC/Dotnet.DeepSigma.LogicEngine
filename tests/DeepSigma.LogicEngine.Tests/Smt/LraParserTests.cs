using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class LraParserTests
{
    [Theory]
    [InlineData("x >= 1 & y >= 1 & x + y <= 1", false)]   // infeasible
    [InlineData("x >= 1 & x <= 5", true)]
    [InlineData("x = 3 & x + y = 5 & y >= 3", false)]      // forces y=2
    [InlineData("x = 3 & x + y = 5 & y >= 2", true)]
    [InlineData("(x >= 3 | x <= 1) & x >= 2 & x <= 2", false)]
    [InlineData("x > 0 & x < 0", false)]
    [InlineData("x > 0 & x < 1", true)]
    public void Parse_AndSolve(string input, bool expectedSat)
    {
        var formula = LraParser.Parse(input);
        Assert.Equal(expectedSat, LraSolver.IsSatisfiable(formula));
    }

    [Fact]
    public void Parse_TwoThreeFive_IsSatisfiable()
    {
        // 2x + 3y <= 5, x >= 1, y >= 1 is satisfiable at x=1,y=1 (2+3=5).
        Assert.True(LraSolver.IsSatisfiable(LraParser.Parse("2*x + 3*y <= 5 & x >= 1 & y >= 1")));
    }

    [Fact]
    public void Validity_ViaParser()
    {
        Assert.True(LraSolver.IsValid(LraParser.Parse("x <= 5 -> x <= 6")));
        Assert.False(LraSolver.IsValid(LraParser.Parse("x <= 6 -> x <= 5")));
    }

    [Fact]
    public void Disequality_ExpandsToStrictDisjunction()
    {
        // x != 0 is satisfiable (x can be anything but 0); with x = 0 it is not.
        Assert.True(LraSolver.IsSatisfiable(LraParser.Parse("x != 0")));
        Assert.False(LraSolver.IsSatisfiable(LraParser.Parse("x != 0 & x = 0")));
    }

    [Fact]
    public void MovesTermsAcrossRelation()
    {
        // x + 1 <= y  ⇔  x - y <= -1; with x = y it is infeasible.
        Assert.False(LraSolver.IsSatisfiable(LraParser.Parse("x + 1 <= y & x = y")));
        Assert.True(LraSolver.IsSatisfiable(LraParser.Parse("x + 1 <= y & y >= 5 & x >= 0")));
    }

    [Theory]
    [InlineData("x >=")]
    [InlineData("<= 5")]
    [InlineData("x + <= 3")]
    public void SyntaxErrors(string input)
    {
        Assert.False(LraParser.TryParse(input, out _));
    }
}
