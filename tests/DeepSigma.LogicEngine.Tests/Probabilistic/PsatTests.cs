using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Probabilistic;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Probabilistic;

public class PsatTests
{
    private static Rational R(long n, long d) => Rational.Of(n, d);

    private static ProbabilityConstraint Exactly(string formula, long n, long d)
        => ProbabilityConstraint.Exactly(Formula.Parse(formula), R(n, d));

    [Fact]
    public void Complementary_ProbabilitiesMustSumToOne()
    {
        var coherent = new[] { Exactly("p", 3, 10), Exactly("!p", 7, 10) };
        Assert.True(PsatSolver.IsConsistent(coherent));
        Assert.True(PsatSolver.IsConsistentScalable(coherent));

        var incoherent = new[] { Exactly("p", 3, 10), Exactly("!p", 1, 2) };
        Assert.False(PsatSolver.IsConsistent(incoherent));
        Assert.False(PsatSolver.IsConsistentScalable(incoherent));
    }

    [Fact]
    public void ProbabilityOutOfRange_IsIncoherent()
    {
        var tooBig = new[] { Exactly("p", 3, 2) };
        Assert.False(PsatSolver.IsConsistent(tooBig));
        Assert.False(PsatSolver.IsConsistentScalable(tooBig));
    }

    [Fact]
    public void SingleConstraint_IsCoherent()
    {
        var c = new[] { Exactly("p & q", 1, 2) };
        Assert.True(PsatSolver.IsConsistent(c));
        Assert.True(PsatSolver.IsConsistentScalable(c));
    }

    [Fact]
    public void Monotonicity_SubsetCannotExceedSuperset()
    {
        // P(p & q) = 1/2 but P(p) = 1/4 is impossible since (p & q) implies p.
        var c = new[] { Exactly("p & q", 1, 2), Exactly("p", 1, 4) };
        Assert.False(PsatSolver.IsConsistent(c));
        Assert.False(PsatSolver.IsConsistentScalable(c));
    }

    [Fact]
    public void Bounds_ProbabilisticModusPonens()
    {
        // P(p) = 1/2, P(p -> q) = 1  ⇒  P(q) ∈ [1/2, 1].
        var kb = new[] { Exactly("p", 1, 2), Exactly("p -> q", 1, 1) };
        var bounds = PsatSolver.Bounds(kb, Formula.Parse("q"));
        Assert.NotNull(bounds);
        Assert.Equal(R(1, 2), bounds!.Value.Low);
        Assert.Equal(R(1, 1), bounds.Value.High);
    }

    [Fact]
    public void Bounds_FrechetInequalitiesForConjunction()
    {
        // P(p) = 3/5, P(q) = 7/10  ⇒  P(p & q) ∈ [max(0, 3/10), min(3/5, 7/10)] = [3/10, 3/5].
        var kb = new[] { Exactly("p", 3, 5), Exactly("q", 7, 10) };
        var bounds = PsatSolver.Bounds(kb, Formula.Parse("p & q"));
        Assert.NotNull(bounds);
        Assert.Equal(R(3, 10), bounds!.Value.Low);
        Assert.Equal(R(3, 5), bounds.Value.High);
    }

    [Fact]
    public void Bounds_DisjunctionFrechet()
    {
        // P(p) = 1/2, P(q) = 1/2  ⇒  P(p | q) ∈ [1/2, 1].
        var kb = new[] { Exactly("p", 1, 2), Exactly("q", 1, 2) };
        var bounds = PsatSolver.Bounds(kb, Formula.Parse("p | q"));
        Assert.NotNull(bounds);
        Assert.Equal(R(1, 2), bounds!.Value.Low);
        Assert.Equal(R(1, 1), bounds.Value.High);
    }

    [Fact]
    public void Bounds_ReturnsNullWhenIncoherent()
    {
        var kb = new[] { Exactly("p", 3, 10), Exactly("!p", 1, 2) };
        Assert.Null(PsatSolver.Bounds(kb, Formula.Parse("p")));
    }

    [Fact]
    public void BoundsScalable_MatchesKnownResults()
    {
        var modusPonens = new[] { Exactly("p", 1, 2), Exactly("p -> q", 1, 1) };
        var mp = PsatSolver.BoundsScalable(modusPonens, Formula.Parse("q"));
        Assert.NotNull(mp);
        Assert.Equal(R(1, 2), mp!.Value.Low);
        Assert.Equal(R(1, 1), mp.Value.High);

        var frechet = new[] { Exactly("p", 3, 5), Exactly("q", 7, 10) };
        var conj = PsatSolver.BoundsScalable(frechet, Formula.Parse("p & q"));
        Assert.NotNull(conj);
        Assert.Equal(R(3, 10), conj!.Value.Low);
        Assert.Equal(R(3, 5), conj.Value.High);
    }

    [Fact]
    public void BoundsScalable_ReturnsNullWhenIncoherent()
    {
        var kb = new[] { Exactly("p", 3, 10), Exactly("!p", 1, 2) };
        Assert.Null(PsatSolver.BoundsScalable(kb, Formula.Parse("p")));
    }

    [Fact]
    public void BoundsScalable_FreeQueryAtomIsUnconstrained()
    {
        // q never appears in the constraints, so P(q) ranges over the whole [0, 1].
        var kb = new[] { Exactly("p", 1, 3) };
        var bounds = PsatSolver.BoundsScalable(kb, Formula.Parse("q"));
        Assert.NotNull(bounds);
        Assert.Equal(R(0, 1), bounds!.Value.Low);
        Assert.Equal(R(1, 1), bounds.Value.High);
    }

    [Fact]
    public void Differential_ScalableBoundsAgreeWithEnumeration()
    {
        var pool = new[] { "p", "q", "r", "p & q", "p | r", "p -> q", "!q | r" };
        var grid = new[] { R(0, 1), R(1, 4), R(1, 2), R(3, 4), R(1, 1) };
        var rng = new Random(0x5E2);

        for (var trial = 0; trial < 120; trial++)
        {
            var count = 1 + rng.Next(3);
            var constraints = new ProbabilityConstraint[count];
            for (var i = 0; i < count; i++)
            {
                constraints[i] = ProbabilityConstraint.Exactly(Formula.Parse(pool[rng.Next(pool.Length)]), grid[rng.Next(grid.Length)]);
            }
            var query = Formula.Parse(pool[rng.Next(pool.Length)]);

            var enumeration = PsatSolver.Bounds(constraints, query);
            var columnGen = PsatSolver.BoundsScalable(constraints, query);
            if (enumeration is null)
            {
                Assert.Null(columnGen);
                continue;
            }
            Assert.NotNull(columnGen);
            Assert.Equal(enumeration.Value.Low, columnGen!.Value.Low);
            Assert.Equal(enumeration.Value.High, columnGen.Value.High);
        }
    }

    [Fact]
    public void Differential_ColumnGenerationAgreesWithEnumeration()
    {
        var pool = new[] { "p", "q", "r", "p & q", "p | r", "p -> q", "!q | r", "q <-> r" };
        var grid = new[] { R(0, 1), R(1, 4), R(1, 2), R(3, 4), R(1, 1) };
        var rng = new Random(0x9A7);

        for (var trial = 0; trial < 200; trial++)
        {
            var count = 1 + rng.Next(3);
            var constraints = new ProbabilityConstraint[count];
            for (var i = 0; i < count; i++)
            {
                var formula = Formula.Parse(pool[rng.Next(pool.Length)]);
                constraints[i] = ProbabilityConstraint.Exactly(formula, grid[rng.Next(grid.Length)]);
            }

            var enumeration = PsatSolver.IsConsistent(constraints);
            var columnGen = PsatSolver.IsConsistentScalable(constraints);
            Assert.Equal(enumeration, columnGen);
        }
    }
}
