using DeepSigma.LogicEngine.FiniteSets;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.FiniteSets;

public class FiniteSetsParserTests
{
    [Theory]
    [InlineData("A subset B")]
    [InlineData("A <= B & B <= A -> A = B")]
    [InlineData("x in (A union B) <-> (x in A | x in B)")]
    [InlineData("~(A ∪ B) = ~A ∩ ~B")]
    [InlineData("disjoint(A, B) -> |A ∪ B| >= 2")]
    [InlineData("|A| = 1 | |B| = 1")]
    [InlineData("(A \\ B) ⊆ A")]
    public void Parses(string input) => Assert.NotNull(SetFormula.Parse(input));

    [Fact]
    public void RoundTripsThroughPrinter()
    {
        var f = SetFormula.Parse("A subset B & |A| = 2");
        Assert.NotNull(SetFormula.Parse(f.ToString()));
    }
}

public class FiniteSetsSolverTests
{
    [Theory]
    [InlineData("A subset A")]                                  // reflexivity
    [InlineData("A <= B & B <= A -> A = B")]                    // antisymmetry
    [InlineData("A <= B & B <= C -> A <= C")]                   // transitivity
    [InlineData("~(A ∪ B) = ~A ∩ ~B")]                          // De Morgan
    [InlineData("~(A ∩ B) = ~A ∪ ~B")]                          // De Morgan
    [InlineData("A ∩ (B ∪ C) = (A ∩ B) ∪ (A ∩ C)")]             // distributivity
    [InlineData("empty subset A")]                              // ∅ ⊆ A
    [InlineData("A subset U")]                                  // A ⊆ U
    [InlineData("x in (A ∪ B) <-> (x in A | x in B)")]          // membership distributes
    [InlineData("x in (A ∩ B) <-> (x in A & x in B)")]
    public void KnownValidities(string input)
        => Assert.True(FiniteSetsSolver.IsValid(SetFormula.Parse(input)));

    [Theory]
    [InlineData("A ⊂ A")]                                       // proper subset is irreflexive
    [InlineData("A subset B & B subset A & A != B")]            // contradicts antisymmetry
    public void KnownUnsatisfiable(string input)
        => Assert.False(FiniteSetsSolver.IsSatisfiable(SetFormula.Parse(input)));

    [Fact]
    public void Cardinality_SubsetBoundsSize()
    {
        // A ⊆ B forces |A| ≤ |B|, so |A| = 3 with |B| = 2 is impossible.
        Assert.False(FiniteSetsSolver.IsSatisfiable(SetFormula.Parse("A subset B & |A| = 3 & |B| = 2"), universe: 4));
    }

    [Fact]
    public void Cardinality_OverfullIsUnsatisfiable()
    {
        Assert.False(FiniteSetsSolver.IsSatisfiable(SetFormula.Parse("|A| = 5"), universe: 3));
        Assert.True(FiniteSetsSolver.IsSatisfiable(SetFormula.Parse("|A| = 2"), universe: 3));
    }

    [Fact]
    public void Cardinality_DisjointUnionSum()
    {
        Assert.True(FiniteSetsSolver.IsSatisfiable(
            SetFormula.Parse("disjoint(A, B) & |A| = 1 & |B| = 1 & |A ∪ B| = 2"), universe: 3));
        Assert.False(FiniteSetsSolver.IsSatisfiable(
            SetFormula.Parse("disjoint(A, B) & |A| = 1 & |B| = 1 & |A ∪ B| < 2"), universe: 3));
    }

    [Fact]
    public void Cardinality_EmptyAndUniverse()
    {
        Assert.True(FiniteSetsSolver.IsValid(SetFormula.Parse("|empty| = 0"), universe: 4));
        Assert.True(FiniteSetsSolver.IsValid(SetFormula.Parse("|U| = 4"), universe: 4));
    }

    [Fact]
    public void FindModel_ProducesASatisfyingInterpretation()
    {
        var f = SetFormula.Parse("x in A & A subset B & |B| = 2");
        var model = FiniteSetsSolver.FindModel(f, universe: 3);
        Assert.NotNull(model);
        var a = model!.Sets["A"];
        var b = model.Sets["B"];
        Assert.Contains(model.Elements["x"], a);
        Assert.True(a.IsSubsetOf(b));
        Assert.Equal(2, b.Count);
        // Re-check the original formula against the oracle's evaluator on the found model.
        Assert.True(FiniteSetOracle.Evaluate(f, model.Universe, model.Sets, model.Elements));
    }

    [Fact]
    public void Differential_EncoderAgreesWithBruteForceOracle()
    {
        var rng = new Random(0x5E7);
        var sets = new[] { "A", "B" };
        var elements = new[] { "x" };
        const int universe = 3;

        for (var trial = 0; trial < 300; trial++)
        {
            var f = RandomFormula(rng, depth: 3, sets, elements, universe);
            var sat = FiniteSetsSolver.IsSatisfiable(f, universe);
            var valid = FiniteSetsSolver.IsValid(f, universe);
            Assert.Equal(FiniteSetOracle.IsSatisfiable(f, universe, sets, elements), sat);
            Assert.Equal(FiniteSetOracle.IsValid(f, universe, sets, elements), valid);
        }
    }

    private static SetFormula RandomFormula(Random rng, int depth, string[] sets, string[] elements, int universe)
    {
        if (depth <= 0 || rng.NextDouble() < 0.5)
        {
            return RandomAtom(rng, sets, elements, universe);
        }
        return rng.Next(5) switch
        {
            0 => SetFormula.Not(RandomFormula(rng, depth - 1, sets, elements, universe)),
            1 => SetFormula.And(RandomFormula(rng, depth - 1, sets, elements, universe), RandomFormula(rng, depth - 1, sets, elements, universe)),
            2 => SetFormula.Or(RandomFormula(rng, depth - 1, sets, elements, universe), RandomFormula(rng, depth - 1, sets, elements, universe)),
            3 => SetFormula.Implies(RandomFormula(rng, depth - 1, sets, elements, universe), RandomFormula(rng, depth - 1, sets, elements, universe)),
            _ => SetFormula.Iff(RandomFormula(rng, depth - 1, sets, elements, universe), RandomFormula(rng, depth - 1, sets, elements, universe)),
        };
    }

    private static SetFormula RandomAtom(Random rng, string[] sets, string[] elements, int universe)
    {
        return rng.Next(5) switch
        {
            0 => SetFormula.Member(ElementExpr.Var(Pick(rng, elements)), RandomSet(rng, sets, 2)),
            1 => SetFormula.Subset(RandomSet(rng, sets, 2), RandomSet(rng, sets, 2)),
            2 => SetFormula.Equal(RandomSet(rng, sets, 2), RandomSet(rng, sets, 2)),
            3 => SetFormula.Disjoint(RandomSet(rng, sets, 2), RandomSet(rng, sets, 2)),
            _ => SetFormula.Card(RandomSet(rng, sets, 1), (CardOp)rng.Next(5), rng.Next(universe + 2)),
        };
    }

    private static SetExpr RandomSet(Random rng, string[] sets, int depth)
    {
        if (depth <= 0 || rng.NextDouble() < 0.5)
        {
            return rng.Next(6) switch
            {
                0 => SetExpr.Empty,
                1 => SetExpr.Universe,
                _ => SetExpr.Var(Pick(rng, sets)),
            };
        }
        return rng.Next(5) switch
        {
            0 => SetExpr.Complement(RandomSet(rng, sets, depth - 1)),
            1 => SetExpr.Union(RandomSet(rng, sets, depth - 1), RandomSet(rng, sets, depth - 1)),
            2 => SetExpr.Intersect(RandomSet(rng, sets, depth - 1), RandomSet(rng, sets, depth - 1)),
            3 => SetExpr.Difference(RandomSet(rng, sets, depth - 1), RandomSet(rng, sets, depth - 1)),
            _ => SetExpr.SymmetricDifference(RandomSet(rng, sets, depth - 1), RandomSet(rng, sets, depth - 1)),
        };
    }

    private static string Pick(Random rng, string[] names) => names[rng.Next(names.Length)];
}
