using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Encoding;

/// <summary>
/// Helpers that produce propositional formulas encoding cardinality constraints
/// over a list of inputs.
///
/// <para>
/// The encoding used is the <em>pairwise</em> form for <c>AtMostOne</c> and the
/// <em>binomial</em> form for general <c>AtMostK</c>/<c>AtLeastK</c>:
/// every (k+1)-subset has at least one false literal (resp. every (n-k+1)-subset
/// has at least one true literal). These are correct and need no auxiliary
/// variables, but they are exponential in <c>min(k, n-k)</c>. For small k —
/// which covers most logic-puzzle uses (k = 1 in sudoku, n-queens, graph
/// coloring) — they are fine. For large k, prefer a sequential-counter
/// encoding (not yet implemented).
/// </para>
/// </summary>
public static class Cardinality
{
    public static Formula AtLeastOne(IReadOnlyList<Formula> inputs)
    {
        if (inputs.Count == 0)
        {
            return Formula.False;
        }
        return Formula.Any(inputs);
    }

    public static Formula AtMostOne(IReadOnlyList<Formula> inputs)
    {
        var clauses = new List<Formula>();
        for (var i = 0; i < inputs.Count; i++)
        {
            for (var j = i + 1; j < inputs.Count; j++)
            {
                clauses.Add(new Negation(inputs[i]) | new Negation(inputs[j]));
            }
        }
        return Formula.All(clauses);
    }

    public static Formula ExactlyOne(IReadOnlyList<Formula> inputs)
        => new Conjunction(AtLeastOne(inputs), AtMostOne(inputs));

    public static Formula AtMostK(IReadOnlyList<Formula> inputs, int k)
    {
        if (k < 0)
        {
            return Formula.False;
        }
        if (k >= inputs.Count)
        {
            return Formula.True;
        }
        if (k == 0)
        {
            return Formula.All(inputs.Select(v => (Formula)new Negation(v)));
        }
        if (k == 1)
        {
            return AtMostOne(inputs);
        }
        var clauses = Combinations(inputs.Count, k + 1)
            .Select(subset => Formula.Any(subset.Select(i => (Formula)new Negation(inputs[i]))));
        return Formula.All(clauses);
    }

    public static Formula AtLeastK(IReadOnlyList<Formula> inputs, int k)
    {
        if (k <= 0)
        {
            return Formula.True;
        }
        if (k > inputs.Count)
        {
            return Formula.False;
        }
        if (k == 1)
        {
            return AtLeastOne(inputs);
        }
        if (k == inputs.Count)
        {
            return Formula.All(inputs);
        }
        var clauses = Combinations(inputs.Count, inputs.Count - k + 1)
            .Select(subset => Formula.Any(subset.Select(i => inputs[i])));
        return Formula.All(clauses);
    }

    public static Formula ExactlyK(IReadOnlyList<Formula> inputs, int k)
        => new Conjunction(AtMostK(inputs, k), AtLeastK(inputs, k));

    /// <summary>
    /// Yield each k-subset of the indices {0..n-1} as a sorted int[] in
    /// lexicographic order. Returns no items if k &gt; n.
    /// </summary>
    private static IEnumerable<int[]> Combinations(int n, int k)
    {
        if (k < 0 || k > n)
        {
            yield break;
        }
        if (k == 0)
        {
            yield return Array.Empty<int>();
            yield break;
        }
        var indices = new int[k];
        for (var i = 0; i < k; i++)
        {
            indices[i] = i;
        }
        while (true)
        {
            yield return (int[])indices.Clone();
            var j = k - 1;
            while (j >= 0 && indices[j] == n - k + j)
            {
                j--;
            }
            if (j < 0)
            {
                yield break;
            }
            indices[j]++;
            for (var p = j + 1; p < k; p++)
            {
                indices[p] = indices[p - 1] + 1;
            }
        }
    }
}
