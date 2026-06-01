using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>
/// Weighted model counting (WMC): the sum, over all satisfying assignments, of
/// the product of per-variable weights. Generalizes <see cref="Reasoner.CountModels(Formula, CancellationToken)"/>
/// (which is WMC with all weights 1) and is the bridge to probabilistic
/// reasoning — with weights interpreted as probabilities it yields the
/// probability that a formula holds under independent variable assignments.
///
/// <para>
/// This v1 enumerates models (correct, but exponential); a knowledge-compilation
/// (d-DNNF) backend would scale it. General numeric machinery (log-space sums,
/// graphical-model inference) lives in <c>DeepSigma.Mathematics</c>.
/// </para>
/// </summary>
public static class WeightedModelCounter
{
    /// <summary>
    /// Sum over satisfying assignments of ∏ over variables of the weight chosen
    /// by that variable's truth value. Variables absent from
    /// <paramref name="weights"/> default to weight 1 for both polarities.
    /// </summary>
    public static double Count(Formula formula, IReadOnlyDictionary<string, (double WhenTrue, double WhenFalse)> weights)
    {
        var variables = Evaluator.Variables(formula);
        var total = 0.0;
        foreach (var model in Reasoner.EnumerateModels(formula))
        {
            var weight = 1.0;
            foreach (var variable in variables)
            {
                var (whenTrue, whenFalse) = weights.TryGetValue(variable, out var w) ? w : (1.0, 1.0);
                weight *= model[variable] ? whenTrue : whenFalse;
            }
            total += weight;
        }
        return total;
    }

    /// <summary>
    /// Probability that <paramref name="formula"/> is true when each variable is
    /// independently true with the given probability. Variables not listed
    /// default to probability 0.5.
    /// </summary>
    public static double SatisfactionProbability(Formula formula, IReadOnlyDictionary<string, double> probabilityTrue)
    {
        var weights = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
        foreach (var variable in Evaluator.Variables(formula))
        {
            var p = probabilityTrue.TryGetValue(variable, out var value) ? value : 0.5;
            weights[variable] = (p, 1.0 - p);
        }
        return Count(formula, weights);
    }
}
