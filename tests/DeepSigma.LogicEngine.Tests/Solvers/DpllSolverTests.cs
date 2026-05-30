using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

public class DpllSolverTests
{
    [Theory]
    [InlineData("p", true)]
    [InlineData("!p", true)]
    [InlineData("p & !p", false)]
    [InlineData("p | !p", true)]
    [InlineData("(p | q) & (!p | !q)", true)]
    [InlineData("(p | q) & (!p | !q) & (p <-> q)", false)]
    [InlineData("(p -> q) & (q -> r) -> (p -> r)", true)]
    public void Sat_AgreesWithTruthTable(string input, bool expected)
    {
        var formula = Formula.Parse(input);
        var dpll = new DpllSolver().Solve(formula);
        Assert.Equal(expected, dpll.IsSatisfiable);
        if (expected)
        {
            Assert.NotNull(dpll.Model);
            Assert.True(Evaluator.Evaluate(formula, dpll.Model!));
        }
    }

    [Fact]
    public void RandomFormulas_AgreeWithTruthTableOracle()
    {
        var rng = new Random(20260529);
        var oracle = new TruthTableSolver();
        var dpll = new DpllSolver();
        for (var trial = 0; trial < 80; trial++)
        {
            var formula = RandomFormula(rng, 4, 4);
            var oracleResult = oracle.Solve(formula);
            var dpllResult = dpll.Solve(formula);
            Assert.Equal(oracleResult.IsSatisfiable, dpllResult.IsSatisfiable);
            if (dpllResult.IsSatisfiable)
            {
                Assert.True(Evaluator.Evaluate(formula, dpllResult.Model!));
            }
        }
    }

    [Fact]
    public void PigeonholePrinciple_IsUnsatisfiable()
    {
        // 3 pigeons, 2 holes — classic UNSAT.
        var pigeons = 3;
        var holes = 2;
        Formula PH(int i, int j) => Formula.Var($"x_{i}_{j}");

        // Every pigeon is in at least one hole.
        var atLeast = Formula.All(Enumerable.Range(1, pigeons)
            .Select(i => Formula.Any(Enumerable.Range(1, holes).Select(j => PH(i, j)))));

        // No hole has two pigeons.
        var clauses = new List<Formula>();
        for (var j = 1; j <= holes; j++)
        {
            for (var i = 1; i <= pigeons; i++)
            {
                for (var k = i + 1; k <= pigeons; k++)
                {
                    clauses.Add(new Negation(PH(i, j) & PH(k, j)));
                }
            }
        }
        var atMostOne = Formula.All(clauses);

        var problem = atLeast & atMostOne;
        Assert.False(new DpllSolver().Solve(problem).IsSatisfiable);
    }

    private static Formula RandomFormula(Random rng, int maxDepth, int varPool)
    {
        if (maxDepth == 0 || rng.NextDouble() < 0.25)
        {
            return Formula.Var("v" + rng.Next(varPool));
        }
        var pick = rng.Next(5);
        return pick switch
        {
            0 => new Negation(RandomFormula(rng, maxDepth - 1, varPool)),
            1 => new Conjunction(RandomFormula(rng, maxDepth - 1, varPool), RandomFormula(rng, maxDepth - 1, varPool)),
            2 => new Disjunction(RandomFormula(rng, maxDepth - 1, varPool), RandomFormula(rng, maxDepth - 1, varPool)),
            3 => new Implication(RandomFormula(rng, maxDepth - 1, varPool), RandomFormula(rng, maxDepth - 1, varPool)),
            _ => new Biconditional(RandomFormula(rng, maxDepth - 1, varPool), RandomFormula(rng, maxDepth - 1, varPool)),
        };
    }
}
