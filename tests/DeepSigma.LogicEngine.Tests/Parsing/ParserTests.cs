using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Parsing;

public class ParserTests
{
    [Theory]
    [InlineData("p", "p")]
    [InlineData("!p", "!p")]
    [InlineData("p & q", "p & q")]
    [InlineData("p | q & r", "p | q & r")]
    [InlineData("(p | q) & r", "(p | q) & r")]
    [InlineData("p -> q -> r", "p -> q -> r")]
    [InlineData("p <-> q | r", "p <-> q | r")]
    [InlineData("!p & q | r", "!p & q | r")]
    [InlineData("true & p", "true & p")]
    public void Print_RoundTripsThroughParser(string input, string expected)
    {
        var formula = Formula.Parse(input);
        Assert.Equal(expected, formula.ToString());
    }

    [Theory]
    [InlineData("p && q", "p & q")]
    [InlineData("p /\\ q", "p & q")]
    [InlineData("p \\/ q", "p | q")]
    [InlineData("p || q", "p | q")]
    [InlineData("~p", "!p")]
    [InlineData("p => q", "p -> q")]
    [InlineData("p <=> q", "p <-> q")]
    [InlineData("NOT p AND q OR r", "!p & q | r")]
    public void Accepts_OperatorAliases(string input, string canonical)
    {
        Assert.Equal(canonical, Formula.Parse(input).ToString());
    }

    [Theory]
    [InlineData("p ->")]
    [InlineData("& p")]
    [InlineData("(p & q")]
    [InlineData("p $ q")]
    public void Reports_SyntaxErrors(string input)
    {
        Assert.False(Formula.TryParse(input, out _));
        Assert.Throws<FormatException>(() => Formula.Parse(input));
    }

    [Fact]
    public void ImpliesIsRightAssociative()
    {
        var f = Formula.Parse("p -> q -> r");
        Assert.IsType<Implication>(f);
        var imp = (Implication)f;
        Assert.IsType<Variable>(imp.Antecedent);
        Assert.IsType<Implication>(imp.Consequent);
    }

    [Fact]
    public void Printer_AvoidsRedundantParens()
    {
        var f = Formula.Parse("a & b & c");
        Assert.Equal("a & b & c", f.ToString());
    }

    [Fact]
    public void Parsed_FormulasEvaluateCorrectly()
    {
        var f = Formula.Parse("(p -> q) & (q -> r) -> (p -> r)");
        // Hypothetical syllogism — a tautology.
        var vars = Evaluator.Variables(f);
        foreach (var assignment in AllAssignments(vars))
        {
            Assert.True(Evaluator.Evaluate(f, assignment));
        }
    }

    private static IEnumerable<Dictionary<string, bool>> AllAssignments(IReadOnlySet<string> vars)
    {
        var names = vars.ToArray();
        for (var mask = 0; mask < (1 << names.Length); mask++)
        {
            var dict = new Dictionary<string, bool>();
            for (var i = 0; i < names.Length; i++)
            {
                dict[names[i]] = (mask & (1 << i)) != 0;
            }
            yield return dict;
        }
    }
}
