using System.Text;

namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>
/// A single node of a backward-chaining proof tree. Leaves (with no premises)
/// are facts; internal nodes correspond to a clause whose consequent matches
/// <see cref="Atom"/>.
/// </summary>
public sealed class ProofNode
{
    public string Atom { get; }
    public IReadOnlyList<ProofNode> Premises { get; }

    public ProofNode(string atom, IReadOnlyList<ProofNode> premises)
    {
        Atom = atom;
        Premises = premises;
    }

    public string Render()
    {
        var sb = new StringBuilder();
        Render(this, sb, 0);
        return sb.ToString();
    }

    private static void Render(ProofNode node, StringBuilder sb, int depth)
    {
        sb.Append(new string(' ', depth * 2));
        if (node.Premises.Count == 0)
        {
            sb.Append("- ").Append(node.Atom).Append(" (fact)").AppendLine();
            return;
        }
        sb.Append("- ").Append(node.Atom).AppendLine();
        foreach (var p in node.Premises)
        {
            Render(p, sb, depth + 1);
        }
    }
}

/// <summary>
/// Goal-directed inference over a definite-Horn knowledge base. For a query
/// atom q, tries to find a clause whose consequent is q and recursively
/// prove each antecedent.
/// </summary>
public static class BackwardChainer
{
    public static ProofNode? Prove(IEnumerable<HornClause> knowledgeBase, string queryAtom)
    {
        var clauses = knowledgeBase.Where(c => c.IsDefinite).ToArray();
        var index = new Dictionary<string, List<HornClause>>(StringComparer.Ordinal);
        foreach (var clause in clauses)
        {
            if (!index.TryGetValue(clause.Consequent!, out var bucket))
            {
                bucket = new List<HornClause>();
                index[clause.Consequent!] = bucket;
            }
            bucket.Add(clause);
        }
        return ProveRecursive(queryAtom, index, new HashSet<string>(StringComparer.Ordinal));
    }

    private static ProofNode? ProveRecursive(
        string goal,
        Dictionary<string, List<HornClause>> index,
        HashSet<string> inProgress)
    {
        if (!index.TryGetValue(goal, out var candidates))
        {
            return null;
        }
        if (!inProgress.Add(goal))
        {
            return null;
        }
        try
        {
            foreach (var clause in candidates)
            {
                var children = new List<ProofNode>(clause.Antecedents.Count);
                var ok = true;
                foreach (var antecedent in clause.Antecedents)
                {
                    var sub = ProveRecursive(antecedent, index, inProgress);
                    if (sub is null)
                    {
                        ok = false;
                        break;
                    }
                    children.Add(sub);
                }
                if (ok)
                {
                    return new ProofNode(goal, children);
                }
            }
            return null;
        }
        finally
        {
            inProgress.Remove(goal);
        }
    }
}
