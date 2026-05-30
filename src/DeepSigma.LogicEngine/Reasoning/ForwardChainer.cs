namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>
/// Linear-time forward-chaining inference for definite-Horn knowledge bases.
/// Repeatedly applies clauses whose antecedents are already inferred to derive
/// their consequents.
/// </summary>
public static class ForwardChainer
{
    /// <summary>Return every atom that the KB derives by forward chaining.</summary>
    public static IReadOnlySet<string> Infer(IEnumerable<HornClause> knowledgeBase)
    {
        var clauses = knowledgeBase.Where(c => c.IsDefinite).ToArray();
        var inferred = new HashSet<string>(StringComparer.Ordinal);
        var counts = new int[clauses.Length];
        var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        for (var i = 0; i < clauses.Length; i++)
        {
            counts[i] = clauses[i].Antecedents.Count;
            foreach (var a in clauses[i].Antecedents)
            {
                if (!index.TryGetValue(a, out var bucket))
                {
                    bucket = new List<int>();
                    index[a] = bucket;
                }
                bucket.Add(i);
            }
            if (counts[i] == 0)
            {
                queue.Enqueue(clauses[i].Consequent!);
            }
        }

        while (queue.Count > 0)
        {
            var atom = queue.Dequeue();
            if (!inferred.Add(atom))
            {
                continue;
            }
            if (!index.TryGetValue(atom, out var bucket))
            {
                continue;
            }
            foreach (var i in bucket)
            {
                counts[i]--;
                if (counts[i] == 0)
                {
                    queue.Enqueue(clauses[i].Consequent!);
                }
            }
        }
        return inferred;
    }

    /// <summary>True if the KB derives the query atom by forward chaining.</summary>
    public static bool Entails(IEnumerable<HornClause> knowledgeBase, string queryAtom)
        => Infer(knowledgeBase).Contains(queryAtom);
}
