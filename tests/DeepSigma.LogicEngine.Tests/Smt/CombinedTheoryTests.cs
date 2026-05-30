using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class CombinedTheoryTests
{
    private static SmtFormula Mix(string lra, string euf) => new SmtAnd(LraParser.Parse(lra), SmtParser.Parse(euf));

    [Fact]
    public void EufLraInteraction_IsUnsatisfiable()
    {
        // x ≤ y ∧ y ≤ x ⇒ x = y (LRA), then congruence ⇒ f(x) = f(y), contradicting f(x) ≠ f(y).
        var formula = Mix("x <= y & y <= x", "f(x) != f(y)");
        Assert.False(CombinedSolver.IsSatisfiable(formula));

        // Neither theory alone detects it: the LRA part and the EUF part are each satisfiable.
        Assert.True(LraSolver.IsSatisfiable(LraParser.Parse("x <= y & y <= x")));
        Assert.True(EufSolver.IsSatisfiable(SmtParser.Parse("f(x) != f(y)")));
    }

    [Fact]
    public void WithoutForcedEquality_IsSatisfiable()
    {
        // x ≤ y alone does not force x = y, so f(x) ≠ f(y) is fine.
        Assert.True(CombinedSolver.IsSatisfiable(Mix("x <= y", "f(x) != f(y)")));
    }

    [Fact]
    public void ConsistentInteraction_IsSatisfiable()
    {
        // x = y forces f(x) = f(y), which is consistent with asserting it.
        Assert.True(CombinedSolver.IsSatisfiable(Mix("x <= y & y <= x", "f(x) = f(y)")));
    }

    [Fact]
    public void EqualityChainThroughArithmetic_IsUnsatisfiable()
    {
        // x = y and y = z via arithmetic, then f(x) ≠ f(z) contradicts congruence.
        var formula = new SmtAnd(
            LraParser.Parse("x <= y & y <= x & y <= z & z <= y"),
            SmtParser.Parse("f(x) != f(z)"));
        Assert.False(CombinedSolver.IsSatisfiable(formula));
    }

    [Fact]
    public void Entailment_ArithmeticEqualityImpliesCongruence()
    {
        // {x ≤ y, y ≤ x} ⊨ f(x) = f(y).
        Assert.True(CombinedSolver.Entails(
            new[] { LraParser.Parse("x <= y & y <= x") },
            SmtParser.Parse("f(x) = f(y)")));
    }

    [Theory]
    [InlineData("x >= 1 & y >= 1 & x + y <= 1", false)]
    [InlineData("x <= 5 & x >= 3", true)]
    [InlineData("2*x = 4 & x <= 1", false)]
    public void AgreesWithLra_OnPureArithmetic(string input, bool expected)
    {
        var formula = LraParser.Parse(input);
        Assert.Equal(expected, CombinedSolver.IsSatisfiable(formula));
        Assert.Equal(LraSolver.IsSatisfiable(formula), CombinedSolver.IsSatisfiable(formula));
    }

    [Theory]
    [InlineData("a = b & b = c & f(a) != f(c)", false)]
    [InlineData("a = b & f(a) = f(b)", true)]
    [InlineData("(a = b | c = d) & f(a) != f(b)", true)]
    public void AgreesWithEuf_OnPureEuf(string input, bool expected)
    {
        var formula = SmtParser.Parse(input);
        Assert.Equal(expected, CombinedSolver.IsSatisfiable(formula));
        Assert.Equal(EufSolver.IsSatisfiable(formula), CombinedSolver.IsSatisfiable(formula));
    }
}
