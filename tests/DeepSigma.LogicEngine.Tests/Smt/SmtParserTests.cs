using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class SmtParserTests
{
    [Theory]
    [InlineData("a = b", "a = b")]
    [InlineData("f(a) = b", "f(a) = b")]
    [InlineData("f(a, g(b)) = c", "f(a, g(b)) = c")]
    [InlineData("a != b", "!(a = b)")]
    [InlineData("P(x)", "P(x)")]
    [InlineData("P", "P")]
    [InlineData("a = b & c = d", "a = b & c = d")]
    [InlineData("a = b -> c = d", "a = b -> c = d")]
    [InlineData("f(a) = b & a = c -> f(c) = b", "f(a) = b & a = c -> f(c) = b")]
    [InlineData("P(a) <-> P(b)", "P(a) <-> P(b)")]
    public void Print_RoundTripsThroughParser(string input, string expected)
    {
        Assert.Equal(expected, SmtFormula.Parse(input).ToString());
    }

    [Theory]
    [InlineData("a = b && c = d", "a = b & c = d")]
    [InlineData("a = b || c = d", "a = b | c = d")]
    [InlineData("a = b => c = d", "a = b -> c = d")]
    [InlineData("not P(x)", "!P(x)")]
    [InlineData("a = b /\\ c = d", "a = b & c = d")]
    public void Accepts_OperatorAliases(string input, string canonical)
    {
        Assert.Equal(canonical, SmtFormula.Parse(input).ToString());
    }

    [Fact]
    public void Parses_NestedFunctionsAndStructure()
    {
        var f = SmtFormula.Parse("f(g(a), b) = c & (P(a) | a != b)");
        Assert.IsType<SmtAnd>(f);
        var and = (SmtAnd)f;
        Assert.IsType<EqualityAtom>(and.Left);
        var eq = (EqualityAtom)and.Left;
        Assert.Equal("f", eq.Left.Symbol);
        Assert.Equal(2, eq.Left.Arguments.Count);
    }

    [Theory]
    [InlineData("a =")]
    [InlineData("= b")]
    [InlineData("f(a")]
    [InlineData("a = b &")]
    [InlineData("f(,) = a")]
    public void Reports_SyntaxErrors(string input)
    {
        Assert.False(SmtFormula.TryParse(input, out _));
        Assert.Throws<FormatException>(() => SmtFormula.Parse(input));
    }

    [Fact]
    public void Term_StructuralEquality()
    {
        Assert.Equal(Term.Func("f", Term.Constant("a")), Term.Func("f", Term.Constant("a")));
        Assert.NotEqual(Term.Func("f", Term.Constant("a")), Term.Func("f", Term.Constant("b")));
    }
}
