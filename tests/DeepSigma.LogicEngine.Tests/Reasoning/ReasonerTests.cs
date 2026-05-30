using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Reasoning;

public class ReasonerTests
{
    [Theory]
    [InlineData("p | !p")]
    [InlineData("(p -> q) <-> (!q -> !p)")] // contrapositive
    [InlineData("(p -> q) & (q -> r) -> (p -> r)")] // hypothetical syllogism
    [InlineData("!(p & q) <-> (!p | !q)")] // De Morgan
    public void IsValid_Tautologies(string input)
    {
        Assert.True(Reasoner.IsValid(Formula.Parse(input)));
    }

    [Theory]
    [InlineData("p & !p")]
    [InlineData("(p | q) & !p & !q")]
    public void IsSatisfiable_Contradictions_ReturnFalse(string input)
    {
        Assert.False(Reasoner.IsSatisfiable(Formula.Parse(input)));
    }

    [Fact]
    public void Equivalence_DeMorgan()
    {
        var a = Formula.Parse("!(p & q)");
        var b = Formula.Parse("!p | !q");
        Assert.True(Reasoner.AreEquivalent(a, b));
    }

    [Fact]
    public void Entails_ModusPonens()
    {
        var kb = new[] { Formula.Parse("p"), Formula.Parse("p -> q") };
        Assert.True(Reasoner.Entails(kb, Formula.Parse("q")));
        Assert.False(Reasoner.Entails(kb, Formula.Parse("!q")));
    }

    [Fact]
    public void Entails_DisjunctiveSyllogism()
    {
        var kb = new[] { Formula.Parse("p | q"), Formula.Parse("!p") };
        Assert.True(Reasoner.Entails(kb, Formula.Parse("q")));
    }

    [Fact]
    public void FindModel_ReturnsSatisfyingAssignment()
    {
        var formula = Formula.Parse("(p | q) & (!p | r)");
        var model = Reasoner.FindModel(formula);
        Assert.NotNull(model);
        Assert.True(Evaluator.Evaluate(formula, model!));
    }

    [Fact]
    public void CountModels_AgreesWithEnumeration()
    {
        // (p ∨ q) has 3 models over {p, q}: (T,T), (T,F), (F,T).
        Assert.Equal(3, Reasoner.CountModels(Formula.Parse("p | q")));
        // Tautology over 2 variables has 4 models.
        Assert.Equal(4, Reasoner.CountModels(Formula.Parse("(p | !p) & (q | !q)")));
        // Contradiction has 0 models.
        Assert.Equal(0, Reasoner.CountModels(Formula.Parse("p & !p")));
    }

    [Fact]
    public void EnumerateModels_YieldsAllSatisfyingAssignments()
    {
        var formula = Formula.Parse("p -> q");
        var models = Reasoner.EnumerateModels(formula).ToArray();
        Assert.Equal(3, models.Length);
        foreach (var m in models)
        {
            Assert.True(Evaluator.Evaluate(formula, m));
        }
    }
}
