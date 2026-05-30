using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Reasoning;

public class ResolutionUpgradeTests
{
    [Fact]
    public void Proof_UsesOriginalVariableNames_NoTseitinAux()
    {
        var kb = new[] { Formula.Parse("p"), Formula.Parse("p -> q"), Formula.Parse("q -> r") };
        var result = ResolutionRefuter.Refute(kb, Formula.Parse("r"));
        Assert.True(result.IsRefuted);
        var rendered = result.Proof!.Render();
        // Classical CNF introduces no auxiliary variables.
        Assert.DoesNotContain("__t", rendered, StringComparison.Ordinal);
        Assert.Contains("[]", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitSetOfSupport_RefutesGoal()
    {
        // Usable (satisfiable KB): (!a | b), (!b | c)
        var usable = CnfTransformer.ToCnf(Formula.Parse("(a -> b) & (b -> c)"));
        // Support (goal): a, !c   — together with KB this is unsatisfiable.
        var support = CnfTransformer.ToCnf(Formula.Parse("a & !c"));
        var result = ResolutionRefuter.Refute(usable, support);
        Assert.True(result.IsRefuted);
    }

    [Fact]
    public void Subsumption_DoesNotPreventRefutation()
    {
        // Add a redundant superset clause that should be subsumed by a unit.
        var clauses = new[]
        {
            Clause.Of(Literal.Positive("p")),                                   // p
            Clause.Of(Literal.Positive("p"), Literal.Positive("q")),            // p | q  (subsumed by p)
            Clause.Of(Literal.Negative("p")),                                   // !p
        };
        var result = ResolutionRefuter.Refute(new CnfFormula(clauses));
        Assert.True(result.IsRefuted);
    }

    [Theory]
    [InlineData("p", "p -> q", "q", true)]
    [InlineData("p | q", "!p", "q", true)]
    [InlineData("p | q", "", "p", false)]
    [InlineData("(a -> b) & (b -> c) & a", "", "c", true)]
    public void Refutation_AgreesWithEntailment(string kb1, string kb2, string query, bool entailed)
    {
        var kb = new List<Formula> { Formula.Parse(kb1) };
        if (kb2.Length > 0)
        {
            kb.Add(Formula.Parse(kb2));
        }
        var q = Formula.Parse(query);

        var refuted = ResolutionRefuter.Refute(kb, q).IsRefuted;
        var entails = Reasoner.Entails(kb, q);

        Assert.Equal(entailed, entails);
        Assert.Equal(entails, refuted);
    }

    [Fact]
    public void DoesNotRefute_SatisfiableSupport()
    {
        var result = ResolutionRefuter.Refute(new[] { Formula.Parse("p | q") }, Formula.Parse("p"));
        Assert.False(result.IsRefuted);
        Assert.NotNull(result.Reason);
    }
}
