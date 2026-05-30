using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class ConflictCoreTests
{
    [Fact]
    public void Lia_CoreExcludesIrrelevantLiterals()
    {
        // x ≥ 1 ∧ x ≤ 0 is the conflict; y ≥ 5 is irrelevant.
        var literals = new[]
        {
            LraParser.Parse("x >= 1"),
            LraParser.Parse("x <= 0"),
            LraParser.Parse("y >= 5"),
        };
        var core = LiaSolver.ConflictCore(literals, new[] { "x", "y" });
        Assert.NotNull(core);
        Assert.Equal(2, core!.Count);
        Assert.DoesNotContain(LraParser.Parse("y >= 5"), core);
    }

    [Fact]
    public void Lia_IntegerInfeasibilityCoreIsMinimal()
    {
        // 2x ≤ 1 ∧ 2x ≥ 1 forces x = 1/2, which has no integer solution; y ≤ 3 is irrelevant.
        var literals = new[]
        {
            LraParser.Parse("2*x <= 1"),
            LraParser.Parse("2*x >= 1"),
            LraParser.Parse("y <= 3"),
        };
        var core = LiaSolver.ConflictCore(literals, new[] { "x", "y" });
        Assert.NotNull(core);
        Assert.Equal(2, core!.Count);
        Assert.DoesNotContain(LraParser.Parse("y <= 3"), core);
    }

    [Fact]
    public void Combined_CoreExcludesIrrelevantLiterals()
    {
        // The conflict is {x ≤ y, y ≤ x, f(x) ≠ f(y)}; P(z) is irrelevant.
        var literals = new SmtFormula[]
        {
            LraParser.Parse("x <= y"),
            LraParser.Parse("y <= x"),
            SmtParser.Parse("f(x) != f(y)"),
            SmtParser.Parse("P(z)"),
        };
        var core = CombinedSolver.ConflictCore(literals);
        Assert.NotNull(core);
        Assert.DoesNotContain(SmtParser.Parse("P(z)"), core!);
    }

    [Fact]
    public void ConsistentSet_HasNoCore()
    {
        Assert.Null(LiaSolver.ConflictCore(new[] { LraParser.Parse("x >= 1"), LraParser.Parse("x <= 5") }, new[] { "x" }));
    }
}
