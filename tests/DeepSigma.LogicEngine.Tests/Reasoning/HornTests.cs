using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Reasoning;

public class HornTests
{
    private static HornClause Fact(string atom) => new(Array.Empty<string>(), atom);
    private static HornClause Rule(string consequent, params string[] antecedents) => new(antecedents, consequent);

    [Fact]
    public void ForwardChaining_ChainsThroughRules()
    {
        var kb = new[]
        {
            Fact("a"),
            Fact("b"),
            Rule("c", "a", "b"),
            Rule("d", "c"),
            Rule("e", "d", "x"), // x is never derived
        };
        var inferred = ForwardChainer.Infer(kb);
        Assert.Contains("a", inferred);
        Assert.Contains("b", inferred);
        Assert.Contains("c", inferred);
        Assert.Contains("d", inferred);
        Assert.DoesNotContain("e", inferred);
    }

    [Fact]
    public void Conversion_DetectsNonHornClauses()
    {
        // p | q has two positive literals — not Horn.
        Assert.False(HornConverter.TryConvert(Formula.Parse("p | q"), out _));
        // p & q & (a | !b) is Horn.
        Assert.True(HornConverter.TryConvert(Formula.Parse("p & q & (a | !b)"), out _));
    }

    [Fact]
    public void Conversion_PreservesEntailmentOverFacts()
    {
        var formula = Formula.Parse("(a -> b) & (b -> c) & a");
        Assert.True(HornConverter.TryConvert(formula, out var clauses));
        var inferred = ForwardChainer.Infer(clauses);
        Assert.Contains("c", inferred);
    }

    [Fact]
    public void BackwardChaining_ProducesProofTree()
    {
        var kb = new[]
        {
            Fact("a"),
            Fact("b"),
            Rule("c", "a", "b"),
            Rule("d", "c"),
        };
        var proof = BackwardChainer.Prove(kb, "d");
        Assert.NotNull(proof);
        Assert.Equal("d", proof!.Atom);
        Assert.Single(proof.Premises);
        Assert.Equal("c", proof.Premises[0].Atom);
        Assert.Equal(2, proof.Premises[0].Premises.Count);
    }

    [Fact]
    public void BackwardChaining_FailsWhenUnderivable()
    {
        var kb = new[] { Fact("a"), Rule("b", "a", "c") };
        Assert.Null(BackwardChainer.Prove(kb, "b"));
    }

    [Fact]
    public void BackwardChaining_HandlesCycles()
    {
        var kb = new[] { Rule("a", "b"), Rule("b", "a") };
        Assert.Null(BackwardChainer.Prove(kb, "a"));
    }
}
