using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

/// <summary>
/// The correctness backbone: CDCL must agree with both the DPLL solver and the
/// brute-force truth-table oracle on randomly generated formulas, and any model
/// it returns must actually satisfy the input.
/// </summary>
public class SolverDifferentialTests
{
    [Fact]
    public void Cdcl_AgreesWithTruthTableAndDpll_OnRandomFormulas()
    {
        var rng = new Random(0x5EED);
        var oracle = new TruthTableSolver();
        var dpll = new DpllSolver();
        var cdcl = new CdclSolver();

        for (var trial = 0; trial < 400; trial++)
        {
            var formula = RandomFormula(rng, depth: 4, varPool: 5);

            var oracleSat = oracle.Solve(formula).IsSatisfiable;
            var dpllSat = dpll.Solve(formula).IsSatisfiable;
            var cdclResult = cdcl.Solve(formula);

            Assert.Equal(oracleSat, dpllSat);
            Assert.Equal(oracleSat, cdclResult.IsSatisfiable);

            if (cdclResult.IsSatisfiable)
            {
                Assert.True(
                    Evaluator.Evaluate(formula, cdclResult.Model!),
                    $"CDCL model does not satisfy formula: {formula}");
            }
        }
    }

    [Fact]
    public void Cdcl_AgreesWithDpll_OnRandomCnf()
    {
        var rng = new Random(0xC0FFEE);
        var dpll = new DpllSolver();
        var cdcl = new CdclSolver();

        for (var trial = 0; trial < 300; trial++)
        {
            var cnf = RandomCnf(rng, variables: 8, clauses: 30, clauseWidth: 3);
            var dpllSat = dpll.Solve(cnf).IsSatisfiable;
            var cdclResult = cdcl.Solve(cnf);
            Assert.Equal(dpllSat, cdclResult.IsSatisfiable);

            if (cdclResult.IsSatisfiable)
            {
                AssertModelSatisfiesCnf(cnf, cdclResult.Model!);
            }
        }
    }

    private static void AssertModelSatisfiesCnf(CnfFormula cnf, IReadOnlyDictionary<string, bool> model)
    {
        foreach (var clause in cnf.Clauses)
        {
            var satisfied = clause.Literals.Any(lit =>
                model.TryGetValue(lit.Variable, out var v) && v != lit.Negated);
            Assert.True(satisfied, $"Clause not satisfied: {clause}");
        }
    }

    private static Formula RandomFormula(Random rng, int depth, int varPool)
    {
        if (depth == 0 || rng.NextDouble() < 0.25)
        {
            return Formula.Var("v" + rng.Next(varPool));
        }
        return rng.Next(5) switch
        {
            0 => new Negation(RandomFormula(rng, depth - 1, varPool)),
            1 => new Conjunction(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
            2 => new Disjunction(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
            3 => new Implication(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
            _ => new Biconditional(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
        };
    }

    private static CnfFormula RandomCnf(Random rng, int variables, int clauses, int clauseWidth)
    {
        var result = new List<Clause>(clauses);
        for (var c = 0; c < clauses; c++)
        {
            var literals = new List<Literal>(clauseWidth);
            for (var w = 0; w < clauseWidth; w++)
            {
                var name = "v" + rng.Next(variables);
                literals.Add(new Literal(name, rng.Next(2) == 0));
            }
            result.Add(new Clause(literals));
        }
        return new CnfFormula(result);
    }
}
