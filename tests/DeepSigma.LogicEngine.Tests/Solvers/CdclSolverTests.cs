using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

public class CdclSolverTests
{
    [Theory]
    [InlineData("p", true)]
    [InlineData("!p", true)]
    [InlineData("p & !p", false)]
    [InlineData("p | !p", true)]
    [InlineData("(p | q) & (!p | !q)", true)]
    [InlineData("(p | q) & (!p | !q) & (p <-> q)", false)]
    [InlineData("(p -> q) & (q -> r) -> (p -> r)", true)]
    public void Sat_AgreesWithExpected(string input, bool expected)
    {
        var formula = Formula.Parse(input);
        var result = new CdclSolver().Solve(formula);
        Assert.Equal(expected, result.IsSatisfiable);
        if (expected)
        {
            Assert.NotNull(result.Model);
            Assert.True(Evaluator.Evaluate(formula, result.Model!));
        }
    }

    [Fact]
    public void PigeonholePrinciple_IsUnsatisfiable()
    {
        Assert.False(new CdclSolver().Solve(Pigeonhole(pigeons: 3, holes: 2)).IsSatisfiable);
    }

    [Fact]
    public void LargerPigeonhole_IsUnsatisfiable()
    {
        // 5-into-4 is well beyond what the DPLL solver handles comfortably.
        Assert.False(new CdclSolver().Solve(Pigeonhole(pigeons: 5, holes: 4)).IsSatisfiable);
    }

    public static IEnumerable<object[]> RefinementConfigs()
    {
        foreach (var restart in new[] { RestartStrategy.Luby, RestartStrategy.Glucose })
        foreach (var minimize in new[] { false, true })
        foreach (var lbd in new[] { false, true })
        {
            yield return new object[] { restart, minimize, lbd };
        }
    }

    [Theory]
    [MemberData(nameof(RefinementConfigs))]
    public void Refinements_AgreeWithTruthTableOracle(RestartStrategy restart, bool minimize, bool lbd)
    {
        var options = new SolverOptions
        {
            RestartStrategy = restart,
            MinimizeLearnedClauses = minimize,
            UseLbdClauseDeletion = lbd,
            // Force frequent reductions so the LBD-deletion path is actually exercised.
            InitialLearnedClauseLimit = 5,
        };
        var rng = new Random(0x1B2);
        for (var trial = 0; trial < 150; trial++)
        {
            var formula = RandomCnf(rng, variables: 6, clauses: 18);
            var result = new CdclSolver(options).Solve(formula);
            var expected = new TruthTableSolver().Solve(formula).IsSatisfiable;
            Assert.Equal(expected, result.IsSatisfiable);
            if (result.IsSatisfiable)
            {
                Assert.True(Evaluator.Evaluate(formula, result.Model!));
            }
        }
    }

    private static Formula RandomCnf(Random rng, int variables, int clauses)
    {
        var all = new List<Formula>();
        for (var c = 0; c < clauses; c++)
        {
            var literals = new List<Formula>();
            for (var k = 0; k < 3; k++)
            {
                var v = Formula.Var($"v{rng.Next(variables)}");
                literals.Add(rng.Next(2) == 0 ? v : new Negation(v));
            }
            all.Add(Formula.Any(literals));
        }
        return Formula.All(all);
    }

    [Fact]
    public void Statistics_AreRecorded()
    {
        var solver = new CdclSolver();
        solver.Solve(Pigeonhole(pigeons: 4, holes: 3));
        Assert.True(solver.Statistics.Conflicts > 0);
        Assert.True(solver.Statistics.Decisions > 0);
        Assert.True(solver.Statistics.Propagations > 0);
    }

    private static Formula Pigeonhole(int pigeons, int holes)
    {
        Formula Slot(int p, int h) => Formula.Var($"x_{p}_{h}");

        var atLeast = Formula.All(Enumerable.Range(1, pigeons)
            .Select(p => Formula.Any(Enumerable.Range(1, holes).Select(h => Slot(p, h)))));

        var atMostOne = new List<Formula>();
        for (var h = 1; h <= holes; h++)
        {
            for (var p = 1; p <= pigeons; p++)
            {
                for (var q = p + 1; q <= pigeons; q++)
                {
                    atMostOne.Add(new Negation(Slot(p, h) & Slot(q, h)));
                }
            }
        }
        return atLeast & Formula.All(atMostOne);
    }
}
