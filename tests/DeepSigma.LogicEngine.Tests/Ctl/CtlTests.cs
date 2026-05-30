using DeepSigma.LogicEngine.Ctl;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Ctl;

public class CtlParserTests
{
    [Theory]
    [InlineData("EX p")]
    [InlineData("AG (p -> EF q)")]
    [InlineData("E[p U q]")]
    [InlineData("A[p U (q | r)]")]
    [InlineData("!EG !p")]
    public void Parses(string input) => Assert.NotNull(CtlFormula.Parse(input));
}

public class CtlModelCheckerTests
{
    // 0 → 1 → 2 → 2 (self-loop); p holds only at state 2.
    private static KripkeStructure Chain()
    {
        var labels = new Dictionary<int, IEnumerable<string>> { [2] = new[] { "p" } };
        return new KripkeStructure(3, new[] { (0, 1), (1, 2), (2, 2) }, labels);
    }

    [Theory]
    [InlineData("p", new[] { 2 })]
    [InlineData("EX p", new[] { 1, 2 })]
    [InlineData("EF p", new[] { 0, 1, 2 })]
    [InlineData("EG p", new[] { 2 })]
    [InlineData("AG p", new[] { 2 })]
    [InlineData("AF p", new[] { 0, 1, 2 })]
    [InlineData("AG (EF p)", new[] { 0, 1, 2 })]
    public void SatisfyingStates_MatchExpected(string input, int[] expected)
    {
        var states = CtlModelChecker.SatisfyingStates(Chain(), CtlFormula.Parse(input));
        Assert.Equal(expected.OrderBy(x => x), states.OrderBy(x => x));
    }

    [Fact]
    public void DerivedOperators_MatchTheirDefinitions()
    {
        var k = Chain();
        // AG φ ≡ ¬EF¬φ ; AF φ ≡ ¬EG¬φ ; AX φ ≡ ¬EX¬φ ; EF φ ≡ E[true U φ].
        AssertSameStates(k, "AG p", "!EF !p");
        AssertSameStates(k, "AF p", "!EG !p");
        AssertSameStates(k, "AX p", "!EX !p");
        AssertSameStates(k, "EF p", "E[true U p]");
    }

    [Fact]
    public void Differential_FixpointMatchesPathSemantics()
    {
        var rng = new Random(0xC71);
        var atoms = new[] { "p", "q" };
        for (var trial = 0; trial < 200; trial++)
        {
            var k = RandomKripke(rng, states: 4, atoms);
            var f = RandomFormula(rng, depth: 3, atoms);
            var byFixpoint = CtlModelChecker.SatisfyingStates(k, f);
            var byPaths = CtlOracle.SatisfyingStates(k, f, atoms);
            Assert.Equal(byPaths.OrderBy(x => x), byFixpoint.OrderBy(x => x));
        }
    }

    private static void AssertSameStates(KripkeStructure k, string a, string b)
    {
        Assert.Equal(
            CtlModelChecker.SatisfyingStates(k, CtlFormula.Parse(a)).OrderBy(x => x),
            CtlModelChecker.SatisfyingStates(k, CtlFormula.Parse(b)).OrderBy(x => x));
    }

    private static KripkeStructure RandomKripke(Random rng, int states, string[] atoms)
    {
        var edges = new List<(int, int)>();
        for (var s = 0; s < states; s++)
        {
            var hasSuccessor = false;
            for (var t = 0; t < states; t++)
            {
                if (rng.NextDouble() < 0.35) { edges.Add((s, t)); hasSuccessor = true; }
            }
            if (!hasSuccessor) { edges.Add((s, s)); } // keep the relation total
        }
        var labels = new Dictionary<int, IEnumerable<string>>();
        for (var s = 0; s < states; s++)
        {
            labels[s] = atoms.Where(_ => rng.NextDouble() < 0.5).ToList();
        }
        return new KripkeStructure(states, edges, labels);
    }

    private static CtlFormula RandomFormula(Random rng, int depth, string[] atoms)
    {
        if (depth <= 0 || rng.NextDouble() < 0.4)
        {
            return CtlFormula.Atom(atoms[rng.Next(atoms.Length)]);
        }
        return rng.Next(9) switch
        {
            0 => CtlFormula.Not(RandomFormula(rng, depth - 1, atoms)),
            1 => CtlFormula.And(RandomFormula(rng, depth - 1, atoms), RandomFormula(rng, depth - 1, atoms)),
            2 => CtlFormula.Or(RandomFormula(rng, depth - 1, atoms), RandomFormula(rng, depth - 1, atoms)),
            3 => CtlFormula.EX(RandomFormula(rng, depth - 1, atoms)),
            4 => CtlFormula.EG(RandomFormula(rng, depth - 1, atoms)),
            5 => CtlFormula.EF(RandomFormula(rng, depth - 1, atoms)),
            6 => CtlFormula.EU(RandomFormula(rng, depth - 1, atoms), RandomFormula(rng, depth - 1, atoms)),
            7 => CtlFormula.AX(RandomFormula(rng, depth - 1, atoms)),
            _ => CtlFormula.AG(RandomFormula(rng, depth - 1, atoms)),
        };
    }
}
