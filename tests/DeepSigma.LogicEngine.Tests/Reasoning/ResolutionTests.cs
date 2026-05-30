using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Reasoning;

public class ResolutionTests
{
    [Fact]
    public void Refutes_ModusPonens()
    {
        var kb = new[] { Formula.Parse("p"), Formula.Parse("p -> q") };
        var result = ResolutionRefuter.Refute(kb, Formula.Parse("q"));
        Assert.True(result.IsRefuted);
        Assert.NotNull(result.Proof);
        Assert.Contains("premise", result.Proof!.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void Refutes_DisjunctiveSyllogism()
    {
        var kb = new[] { Formula.Parse("p | q"), Formula.Parse("!p") };
        var result = ResolutionRefuter.Refute(kb, Formula.Parse("q"));
        Assert.True(result.IsRefuted);
    }

    [Fact]
    public void DoesNotRefute_WhenNotEntailed()
    {
        var kb = new[] { Formula.Parse("p | q") };
        var result = ResolutionRefuter.Refute(kb, Formula.Parse("p"));
        Assert.False(result.IsRefuted);
    }

    [Fact]
    public void Render_TracesBackToEmptyClause()
    {
        var kb = new[] { Formula.Parse("p"), Formula.Parse("!p | q"), Formula.Parse("!q") };
        var result = ResolutionRefuter.Refute(kb, Formula.True); // KB itself is unsat under ¬True == False
        // Easier: refute KB ∧ ¬False == KB. The combined CNF must be unsat.
        Assert.True(result.IsRefuted);
        var rendered = result.Proof!.Render();
        Assert.Contains("[]", rendered, StringComparison.Ordinal);
    }
}
