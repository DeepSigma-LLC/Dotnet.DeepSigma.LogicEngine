using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Reasoning;

public class WeightedModelCounterTests
{
    [Fact]
    public void UnitWeights_EqualModelCount()
    {
        var f = Formula.Parse("p | q");
        var weights = new Dictionary<string, (double, double)>();
        // 3 models, each weight 1 → 3.0
        Assert.Equal(3.0, WeightedModelCounter.Count(f, weights), 9);
    }

    [Fact]
    public void SatisfactionProbability_Disjunction()
    {
        // P(p ∨ q) = 1 - (1-P(p))(1-P(q)) for independent p,q.
        var f = Formula.Parse("p | q");
        var probs = new Dictionary<string, double> { ["p"] = 0.5, ["q"] = 0.25 };
        var expected = 1.0 - (1.0 - 0.5) * (1.0 - 0.25); // 0.625
        Assert.Equal(expected, WeightedModelCounter.SatisfactionProbability(f, probs), 9);
    }

    [Fact]
    public void SatisfactionProbability_Conjunction_IsProduct()
    {
        var f = Formula.Parse("p & q");
        var probs = new Dictionary<string, double> { ["p"] = 0.3, ["q"] = 0.6 };
        Assert.Equal(0.3 * 0.6, WeightedModelCounter.SatisfactionProbability(f, probs), 9);
    }

    [Fact]
    public void SatisfactionProbability_Negation()
    {
        var f = Formula.Parse("!p");
        var probs = new Dictionary<string, double> { ["p"] = 0.7 };
        Assert.Equal(0.3, WeightedModelCounter.SatisfactionProbability(f, probs), 9);
    }

    [Fact]
    public void Tautology_IsProbabilityOne_Contradiction_Zero()
    {
        Assert.Equal(1.0, WeightedModelCounter.SatisfactionProbability(Formula.Parse("p | !p"),
            new Dictionary<string, double> { ["p"] = 0.4 }), 9);
        Assert.Equal(0.0, WeightedModelCounter.SatisfactionProbability(Formula.Parse("p & !p"),
            new Dictionary<string, double> { ["p"] = 0.4 }), 9);
    }

    [Fact]
    public void Differential_AgreesWithBruteForce()
    {
        var rng = new Random(0x1234);
        for (var trial = 0; trial < 40; trial++)
        {
            var formula = RandomFormula(rng, 3, 4);
            var vars = Evaluation.Evaluator.Variables(formula).ToArray();
            var probs = vars.ToDictionary(v => v, _ => Math.Round(rng.NextDouble(), 3));

            var wmc = WeightedModelCounter.SatisfactionProbability(formula, probs);
            var brute = BruteForceProbability(formula, vars, probs);
            Assert.Equal(brute, wmc, 9);
        }
    }

    private static double BruteForceProbability(Formula f, string[] vars, Dictionary<string, double> probs)
    {
        var total = 0.0;
        for (var mask = 0; mask < (1 << vars.Length); mask++)
        {
            var assignment = new Dictionary<string, bool>();
            var weight = 1.0;
            for (var i = 0; i < vars.Length; i++)
            {
                var on = (mask & (1 << i)) != 0;
                assignment[vars[i]] = on;
                weight *= on ? probs[vars[i]] : 1.0 - probs[vars[i]];
            }
            if (Evaluation.Evaluator.Evaluate(f, assignment))
            {
                total += weight;
            }
        }
        return total;
    }

    private static Formula RandomFormula(Random rng, int depth, int varPool)
    {
        if (depth == 0 || rng.NextDouble() < 0.3)
        {
            return Formula.Var("v" + rng.Next(varPool));
        }
        return rng.Next(4) switch
        {
            0 => new Negation(RandomFormula(rng, depth - 1, varPool)),
            1 => new Conjunction(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
            2 => new Disjunction(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
            _ => new Implication(RandomFormula(rng, depth - 1, varPool), RandomFormula(rng, depth - 1, varPool)),
        };
    }
}
