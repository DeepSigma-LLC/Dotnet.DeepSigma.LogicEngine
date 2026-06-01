using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Z3;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// The Z3 facades must agree with the native engine on the shared (decidable) fragment. Any
/// disagreement is a real bug in one of the two engines — that is the value of having both.
/// </summary>
public class Z3DifferentialTests
{
    [Theory]
    [InlineData("(p -> q) <-> (!q -> !p)", true)]   // contrapositive — valid
    [InlineData("p | !p", true)]
    [InlineData("p & !p", false)]
    [InlineData("(p | q) & (!p | r)", false)]       // not valid
    public void Propositional_ValidityAgrees(string text, bool expectedValid)
    {
        var f = Formula.Parse(text);
        Assert.Equal(expectedValid, Reasoner.IsValid(f));
        Assert.Equal(Reasoner.IsValid(f), Z3Reasoner.IsValid(f));
    }

    [Theory]
    [InlineData("a = b & b = c -> f(a) = f(c)", true)]
    [InlineData("a = b & P(a) & !P(b)", false)]      // unsat → negation valid? no: this is a sat query
    public void Euf_Agrees(string text, bool _)
    {
        var f = SmtFormula.Parse(text);
        Assert.Equal(EufSolver.IsSatisfiable(f), Z3SmtReasoner.IsSatisfiable(f, Z3SmtTheory.Euf));
        Assert.Equal(EufSolver.IsValid(f), Z3SmtReasoner.IsValid(f, Z3SmtTheory.Euf));
    }

    [Theory]
    [InlineData("x >= 1 & y >= 1 & x + y <= 1")]     // unsat
    [InlineData("x > 0 & x < 1")]                    // sat over reals
    [InlineData("2*x + 3*y <= 5 & x >= 0 & y >= 0")] // sat
    public void Lra_Agrees(string text)
    {
        var f = LraParser.Parse(text);
        Assert.Equal(LraSolver.IsSatisfiable(f), Z3SmtReasoner.IsSatisfiable(f, Z3SmtTheory.Lra));
    }

    [Fact]
    public void Combined_Agrees()
    {
        var mixed = new SmtAnd(LraParser.Parse("x <= y & y <= x"), SmtParser.Parse("f(x) != f(y)"));
        Assert.Equal(CombinedSolver.IsSatisfiable(mixed), Z3SmtReasoner.IsSatisfiable(mixed, Z3SmtTheory.Combined));
        Assert.False(Z3SmtReasoner.IsSatisfiable(mixed, Z3SmtTheory.Combined));   // x = y ⇒ f(x) = f(y), contradicting f(x) ≠ f(y)
    }

    [Theory]
    [InlineData("select(store(a, i, v), i) = v", true)]                         // valid
    [InlineData("i != j -> select(store(a, i, v), j) = select(a, j)", true)]    // valid
    [InlineData("select(store(a, i, v), i) != v", false)]                       // unsat
    public void Arrays_Agrees(string text, bool expectedValid)
    {
        var f = SmtParser.Parse(text);
        Assert.Equal(ArraySolver.IsSatisfiable(f), Z3SmtReasoner.IsSatisfiable(f, Z3SmtTheory.Arrays));
        Assert.Equal(expectedValid, Z3SmtReasoner.IsValid(f, Z3SmtTheory.Arrays));
    }
}
