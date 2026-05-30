using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Encoding;

/// <summary>
/// A cardinality constraint encoded with auxiliary "counter" variables. The
/// constraint is logically correct over the input formulas, but the auxiliary
/// variables it introduces are not uniquely determined by the inputs, so the
/// number of models of <see cref="Constraint"/> is NOT the number of input
/// assignments satisfying the bound. Use this for satisfiability and solving;
/// for model counting use the auxiliary-free <see cref="Cardinality"/> helpers,
/// or project <see cref="AuxiliaryVariables"/> out of the count.
/// </summary>
public readonly record struct CardinalityEncoding(Formula Constraint, IReadOnlySet<string> AuxiliaryVariables);

/// <summary>
/// Sinz's sequential-counter encoding of cardinality constraints. It produces a
/// CNF of size O(n·k) using O(n·k) auxiliary variables — linear in the bound,
/// where the auxiliary-free <see cref="Cardinality"/> encoding is exponential in
/// min(k, n−k). Prefer this when k is large.
/// </summary>
public static class SequentialCounter
{
    /// <summary>Encode "at most <paramref name="k"/> of the inputs are true".</summary>
    public static CardinalityEncoding AtMostK(IReadOnlyList<Formula> inputs, int k)
    {
        var prefix = FreshPrefix(inputs);
        var aux = new HashSet<string>(StringComparer.Ordinal);
        var constraint = EncodeAtMost(inputs, k, prefix, aux);
        return new CardinalityEncoding(constraint, aux);
    }

    /// <summary>Encode "at least <paramref name="k"/> of the inputs are true".</summary>
    public static CardinalityEncoding AtLeastK(IReadOnlyList<Formula> inputs, int k)
    {
        // At least k of n true ⟺ at most (n − k) of them false.
        var negated = inputs.Select(f => (Formula)new Negation(f)).ToList();
        var prefix = FreshPrefix(inputs);
        var aux = new HashSet<string>(StringComparer.Ordinal);
        var constraint = EncodeAtMost(negated, inputs.Count - k, prefix, aux);
        return new CardinalityEncoding(constraint, aux);
    }

    /// <summary>Encode "exactly <paramref name="k"/> of the inputs are true".</summary>
    public static CardinalityEncoding ExactlyK(IReadOnlyList<Formula> inputs, int k)
    {
        var basePrefix = FreshPrefix(inputs);
        var aux = new HashSet<string>(StringComparer.Ordinal);

        var atMost = EncodeAtMost(inputs, k, basePrefix + "u", aux);
        var negated = inputs.Select(f => (Formula)new Negation(f)).ToList();
        var atLeast = EncodeAtMost(negated, inputs.Count - k, basePrefix + "l", aux);

        return new CardinalityEncoding(new Conjunction(atMost, atLeast), aux);
    }

    /// <summary>
    /// The core Sinz "≤ k" sequential counter. Register variable
    /// <c>s(i, j)</c> means "at least j of the first i inputs are true".
    /// </summary>
    private static Formula EncodeAtMost(IReadOnlyList<Formula> inputs, int k, string prefix, HashSet<string> aux)
    {
        var n = inputs.Count;
        if (k >= n)
        {
            return Formula.True;
        }
        if (k <= 0)
        {
            // At most zero: every input must be false.
            return Formula.All(inputs.Select(f => (Formula)new Negation(f)));
        }

        var clauses = new List<Formula>();

        Formula S(int i, int j)
        {
            var name = $"{prefix}_{i}_{j}";
            aux.Add(name);
            return new Variable(name);
        }

        Formula X(int i) => inputs[i - 1]; // 1-based input access

        // x1 → s(1,1)
        clauses.Add(new Disjunction(new Negation(X(1)), S(1, 1)));
        // ¬s(1,j) for j > 1
        for (var j = 2; j <= k; j++)
        {
            clauses.Add(new Negation(S(1, j)));
        }

        for (var i = 2; i < n; i++)
        {
            clauses.Add(new Disjunction(new Negation(X(i)), S(i, 1)));         // xi → s(i,1)
            clauses.Add(new Disjunction(new Negation(S(i - 1, 1)), S(i, 1)));  // s(i-1,1) → s(i,1)
            for (var j = 2; j <= k; j++)
            {
                // (xi ∧ s(i-1,j-1)) → s(i,j)
                clauses.Add(Formula.Any(new[] { new Negation(X(i)), new Negation(S(i - 1, j - 1)), (Formula)S(i, j) }));
                // s(i-1,j) → s(i,j)
                clauses.Add(new Disjunction(new Negation(S(i - 1, j)), S(i, j)));
            }
            // overflow: (xi ∧ s(i-1,k)) is forbidden
            clauses.Add(new Disjunction(new Negation(X(i)), new Negation(S(i - 1, k))));
        }

        // Final overflow guard for the last input.
        clauses.Add(new Disjunction(new Negation(X(n)), new Negation(S(n - 1, k))));

        return Formula.All(clauses);
    }

    private static string FreshPrefix(IReadOnlyList<Formula> inputs)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            foreach (var variable in Evaluator.Variables(input))
            {
                existing.Add(variable);
            }
        }
        var prefix = "__sc";
        while (existing.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            prefix += "_";
        }
        return prefix;
    }
}
