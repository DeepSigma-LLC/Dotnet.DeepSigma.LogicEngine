using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers.Cdcl;
using DeepSigma.LogicEngine.Solvers.MaxSat;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

public class AssumptionCoreTests
{
    [Fact]
    public void Core_SingleForcedLiteral()
    {
        // (a) forces a; assuming !a is unsatisfiable with core {!a}.
        var cnf = new CnfFormula(new[] { Clause.Of(Literal.Positive("a")) });
        var solver = new IncrementalCdclSolver(cnf);
        var r = solver.SolveUnderWithCore(new[] { Literal.Negative("a") });
        Assert.False(r.IsSatisfiable);
        Assert.NotNull(r.FailedAssumptions);
        Assert.Contains(Literal.Negative("a"), r.FailedAssumptions!);
    }

    [Fact]
    public void Core_TwoConflictingAssumptions()
    {
        // (!a | !b): a and b cannot both hold. Core = {a, b}.
        var cnf = new CnfFormula(new[] { Clause.Of(Literal.Negative("a"), Literal.Negative("b")) });
        var solver = new IncrementalCdclSolver(cnf);
        var r = solver.SolveUnderWithCore(new[] { Literal.Positive("a"), Literal.Positive("b") });
        Assert.False(r.IsSatisfiable);
        Assert.Equal(
            new[] { "a", "b" },
            r.FailedAssumptions!.Select(l => l.Variable).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Core_SatisfiableReturnsModel()
    {
        var cnf = new CnfFormula(new[] { Clause.Of(Literal.Positive("a"), Literal.Positive("b")) });
        var solver = new IncrementalCdclSolver(cnf);
        var r = solver.SolveUnderWithCore(new[] { Literal.Positive("a") });
        Assert.True(r.IsSatisfiable);
        Assert.True(r.Model!["a"]);
    }
}

public class MaxSatTests
{
    private static IReadOnlyList<Literal> Clause(params Literal[] lits) => lits;

    [Fact]
    public void ContradictorySoftClauses_DropOne()
    {
        // soft (a) and soft (!a), each weight 1, no hard: one must fail → cost 1.
        var softs = new[]
        {
            new SoftClause(Clause(Literal.Positive("a")), 1),
            new SoftClause(Clause(Literal.Negative("a")), 1),
        };
        var result = new MaxSatSolver(Array.Empty<IReadOnlyList<Literal>>(), softs).Solve();
        Assert.Equal(1, result.Cost);
    }

    [Fact]
    public void Weighted_DropsLighterClause()
    {
        // soft (a) w3, soft (!a) w5 → drop the lighter (a), cost 3, model a=false.
        var softs = new[]
        {
            new SoftClause(Clause(Literal.Positive("a")), 3),
            new SoftClause(Clause(Literal.Negative("a")), 5),
        };
        var result = new MaxSatSolver(Array.Empty<IReadOnlyList<Literal>>(), softs).Solve();
        Assert.Equal(3, result.Cost);
        Assert.False(result.Model["a"]);
    }

    [Fact]
    public void PartialMaxSat_RespectsHardClauses()
    {
        // hard (a | b); soft (!a) w1, soft (!b) w1 → satisfy hard with exactly one true → cost 1.
        var hard = new[] { Clause(Literal.Positive("a"), Literal.Positive("b")) };
        var softs = new[]
        {
            new SoftClause(Clause(Literal.Negative("a")), 1),
            new SoftClause(Clause(Literal.Negative("b")), 1),
        };
        var result = new MaxSatSolver(hard, softs).Solve();
        Assert.Equal(1, result.Cost);
        Assert.True(result.Model["a"] || result.Model["b"]); // hard satisfied
    }

    [Fact]
    public void AllSoftSatisfiable_CostZero()
    {
        var softs = new[]
        {
            new SoftClause(Clause(Literal.Positive("a")), 1),
            new SoftClause(Clause(Literal.Positive("b")), 1),
        };
        var result = new MaxSatSolver(Array.Empty<IReadOnlyList<Literal>>(), softs).Solve();
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void HardUnsatisfiable_Throws()
    {
        var hard = new[]
        {
            Clause(Literal.Positive("a")),
            Clause(Literal.Negative("a")),
        };
        var softs = new[] { new SoftClause(Clause(Literal.Positive("b")), 1) };
        Assert.Throws<InvalidOperationException>(() => new MaxSatSolver(hard, softs).Solve());
    }

    [Fact]
    public void Differential_AgreesWithBruteForceOptimum()
    {
        var rng = new Random(0x6A5A7);
        for (var trial = 0; trial < 60; trial++)
        {
            var (hard, softs, vars) = RandomInstance(rng, varCount: 4, hardCount: 2, softCount: 5);
            var brute = BruteForceOptimum(hard, softs, vars);
            if (brute is null)
            {
                Assert.Throws<InvalidOperationException>(() => new MaxSatSolver(hard, softs).Solve());
                continue;
            }
            var result = new MaxSatSolver(hard, softs).Solve();
            Assert.Equal(brute.Value, result.Cost);
            AssertHardSatisfied(hard, result.Model);
            Assert.Equal(brute.Value, SoftCost(softs, result.Model));
        }
    }

    private static (List<IReadOnlyList<Literal>> Hard, List<SoftClause> Soft, string[] Vars) RandomInstance(
        Random rng, int varCount, int hardCount, int softCount)
    {
        var vars = Enumerable.Range(0, varCount).Select(i => "v" + i).ToArray();
        IReadOnlyList<Literal> RandClause(int width) =>
            Enumerable.Range(0, width).Select(_ => new Literal(vars[rng.Next(varCount)], rng.Next(2) == 0)).ToList();

        var hard = Enumerable.Range(0, hardCount).Select(_ => RandClause(2)).ToList();
        var soft = Enumerable.Range(0, softCount)
            .Select(_ => new SoftClause(RandClause(1), rng.Next(1, 4))).ToList();
        return (hard, soft, vars);
    }

    private static long? BruteForceOptimum(IReadOnlyList<IReadOnlyList<Literal>> hard, IReadOnlyList<SoftClause> softs, string[] vars)
    {
        long? best = null;
        for (var mask = 0; mask < (1 << vars.Length); mask++)
        {
            var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (var i = 0; i < vars.Length; i++)
            {
                assignment[vars[i]] = (mask & (1 << i)) != 0;
            }
            if (!hard.All(c => Satisfied(c, assignment)))
            {
                continue;
            }
            var cost = SoftCost(softs, assignment);
            best = best is null ? cost : Math.Min(best.Value, cost);
        }
        return best;
    }

    private static long SoftCost(IReadOnlyList<SoftClause> softs, IReadOnlyDictionary<string, bool> assignment)
        => softs.Where(s => !Satisfied(s.Literals, assignment)).Sum(s => s.Weight);

    private static bool Satisfied(IReadOnlyList<Literal> clause, IReadOnlyDictionary<string, bool> assignment)
        => clause.Any(l => assignment.TryGetValue(l.Variable, out var v) && v != l.Negated);

    private static void AssertHardSatisfied(IReadOnlyList<IReadOnlyList<Literal>> hard, IReadOnlyDictionary<string, bool> model)
    {
        foreach (var clause in hard)
        {
            Assert.True(Satisfied(clause, model));
        }
    }
}
