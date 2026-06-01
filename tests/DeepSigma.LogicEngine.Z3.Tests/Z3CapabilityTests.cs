using System.Numerics;
using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Solvers.MaxSat;
using DeepSigma.LogicEngine.Z3;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>Things Z3 does that the native engine cannot (or does only within a bound), plus model/cancel behavior.</summary>
public class Z3CapabilityTests
{
    [Fact]
    public void Lia_IsUnbounded_WhereNativeBoxIsNot()
    {
        var f = LraParser.Parse("x = 100000");
        var intVars = new[] { "x" };

        // Native LIA is complete only inside [-bound, bound]; 100000 is outside the default-ish small box.
        Assert.False(LiaSolver.IsSatisfiable(f, intVars, bound: 1000));

        // Z3's integer arithmetic is unbounded: it finds the solution.
        var result = Z3SmtSolver.Solve(f, Z3SmtTheory.Lia, intVars);
        Assert.True(result.IsSatisfiable);
        Assert.Equal((BigInteger)100000, result.Model!["x"]!.Integer);
    }

    [Theory]
    [InlineData("3*x + 5*y = 7", true)]   // integer solution exists, e.g. x=4, y=-1
    [InlineData("2*x = 1", false)]        // no integer x
    public void Lia_KnownAnswers(string text, bool expectedSat)
    {
        var f = LraParser.Parse(text);
        Assert.Equal(expectedSat, Z3SmtSolver.IsSatisfiable(f, Z3SmtTheory.Lia, new[] { "x", "y" }));
    }

    [Fact]
    public void Lra_Model_ExposesRationalValue()
    {
        var result = Z3SmtSolver.Solve(LraParser.Parse("2*x = 1"), Z3SmtTheory.Lra);
        Assert.True(result.IsSatisfiable);
        Assert.Equal(Rational.Of(1, 2), result.Model!["x"]!.Rational);
    }

    [Fact]
    public void MaxSat_AgreesWithNativeOptimum()
    {
        // Hard: (a ∨ b). Soft: prefer ¬a (1) and ¬b (1). Best gives up exactly one ⇒ cost 1.
        var hard = new[] { new[] { Literal.Positive("a"), Literal.Positive("b") } };
        var soft = new[]
        {
            new SoftClause(new[] { Literal.Negative("a") }, 1),
            new SoftClause(new[] { Literal.Negative("b") }, 1),
        };

        var native = new MaxSatSolver(hard, soft).Solve();
        var z3 = Z3MaxSatSolver.Solve(hard, soft);

        Assert.Equal(Z3Status.Satisfiable, z3.Status);
        Assert.Equal(1, native.Cost);
        Assert.Equal(native.Cost, z3.Cost);
    }

    [Fact]
    public void PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Z3Reasoner.Solve(Formula.Parse("p | q"), cts.Token));
    }

    [Fact]
    public void Lia_RequiresIntegerVariables()
        => Assert.Throws<ArgumentNullException>(() => Z3SmtSolver.Solve(LraParser.Parse("x = 1"), Z3SmtTheory.Lia));
}
