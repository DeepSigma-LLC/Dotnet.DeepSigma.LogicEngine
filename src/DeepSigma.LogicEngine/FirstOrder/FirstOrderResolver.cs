namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>Outcome of a saturation run.</summary>
public enum FolProofStatus
{
    /// <summary>The empty clause was derived — the clause set is unsatisfiable (the conjecture is proved).</summary>
    Proved,

    /// <summary>The clause set saturated with no empty clause — it is satisfiable (the conjecture is not valid).</summary>
    Saturated,

    /// <summary>The clause/step budget was exhausted without a verdict.</summary>
    Unknown,
}

/// <summary>
/// A first-order resolution refutation engine: a given-clause saturation loop with
/// binary resolution and factoring (both modulo unification), tautology deletion,
/// and variant/duplicate elimination, bounded by a clause budget. Binary resolution
/// plus factoring is refutation-complete for first-order logic, so a derivable
/// empty clause is found given enough budget; the bound keeps it terminating.
/// </summary>
internal sealed class FirstOrderResolver
{
    private readonly int _maxClauses;
    private int _renameCounter;

    public FirstOrderResolver(int maxClauses) => _maxClauses = maxClauses;

    public FolProofStatus Refute(IReadOnlyList<FolClause> clauses)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unprocessed = new List<FolClause>();
        var processed = new List<FolClause>();

        bool Enqueue(FolClause c)
        {
            if (c.IsEmpty)
            {
                return true; // empty clause: refutation found
            }
            if (IsTautology(c) || !seen.Add(CanonicalKey(c)))
            {
                return false;
            }
            unprocessed.Add(c);
            return false;
        }

        foreach (var c in clauses)
        {
            if (Enqueue(c))
            {
                return FolProofStatus.Proved;
            }
        }

        while (unprocessed.Count > 0)
        {
            if (seen.Count > _maxClauses)
            {
                return FolProofStatus.Unknown;
            }

            var given = TakeShortest(unprocessed);

            var derived = new List<FolClause>();
            derived.AddRange(Factors(given));
            derived.AddRange(Resolvents(given, given));
            foreach (var p in processed)
            {
                derived.AddRange(Resolvents(given, p));
            }
            processed.Add(given);

            foreach (var clause in derived)
            {
                if (Enqueue(clause))
                {
                    return FolProofStatus.Proved;
                }
            }
        }

        return FolProofStatus.Saturated;
    }

    private static FolClause TakeShortest(List<FolClause> clauses)
    {
        var best = 0;
        for (var i = 1; i < clauses.Count; i++)
        {
            if (clauses[i].Literals.Count < clauses[best].Literals.Count)
            {
                best = i;
            }
        }
        var clause = clauses[best];
        clauses.RemoveAt(best);
        return clause;
    }

    private IEnumerable<FolClause> Resolvents(FolClause c, FolClause d)
    {
        var left = Rename(c);
        var right = Rename(d);
        var results = new List<FolClause>();
        for (var i = 0; i < left.Literals.Count; i++)
        {
            for (var j = 0; j < right.Literals.Count; j++)
            {
                var li = left.Literals[i];
                var lj = right.Literals[j];
                if (li.Negated == lj.Negated)
                {
                    continue;
                }
                var mgu = Unifier.Unify(li.Atom, lj.Atom);
                if (mgu is null)
                {
                    continue;
                }
                var literals = new List<FolLiteral>();
                for (var a = 0; a < left.Literals.Count; a++)
                {
                    if (a != i) { literals.Add(mgu.Apply(left.Literals[a])); }
                }
                for (var b = 0; b < right.Literals.Count; b++)
                {
                    if (b != j) { literals.Add(mgu.Apply(right.Literals[b])); }
                }
                results.Add(new FolClause(Dedup(literals)));
            }
        }
        return results;
    }

    private IEnumerable<FolClause> Factors(FolClause c)
    {
        var clause = Rename(c);
        var results = new List<FolClause>();
        for (var i = 0; i < clause.Literals.Count; i++)
        {
            for (var j = i + 1; j < clause.Literals.Count; j++)
            {
                if (clause.Literals[i].Negated != clause.Literals[j].Negated)
                {
                    continue;
                }
                var mgu = Unifier.Unify(clause.Literals[i].Atom, clause.Literals[j].Atom);
                if (mgu is null)
                {
                    continue;
                }
                results.Add(new FolClause(Dedup(clause.Literals.Select(mgu.Apply))));
            }
        }
        return results;
    }

    private FolClause Rename(FolClause clause)
    {
        var map = new Dictionary<string, FolTerm>(StringComparer.Ordinal);
        foreach (var name in Variables(clause))
        {
            map[name] = new FolVar("r" + _renameCounter++);
        }
        var substitution = new Substitution(map);
        return new FolClause(clause.Literals.Select(substitution.Apply).ToList());
    }

    private static IEnumerable<string> Variables(FolClause clause)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var literal in clause.Literals)
        {
            CollectVars(literal.Atom, names);
        }
        return names;
    }

    private static void CollectVars(FolFormula atom, HashSet<string> names)
    {
        switch (atom)
        {
            case FolPredicate p:
                foreach (var a in p.Args) { CollectVars(a, names); }
                break;
            case FolEquals e:
                CollectVars(e.Left, names);
                CollectVars(e.Right, names);
                break;
        }
    }

    private static void CollectVars(FolTerm term, HashSet<string> names)
    {
        switch (term)
        {
            case FolVar v: names.Add(v.Name); break;
            case FolFunc f: foreach (var a in f.Args) { CollectVars(a, names); } break;
        }
    }

    private static IReadOnlyList<FolLiteral> Dedup(IEnumerable<FolLiteral> literals)
    {
        var seen = new HashSet<FolLiteral>();
        var result = new List<FolLiteral>();
        foreach (var l in literals)
        {
            if (seen.Add(l))
            {
                result.Add(l);
            }
        }
        return result;
    }

    private static bool IsTautology(FolClause clause)
    {
        for (var i = 0; i < clause.Literals.Count; i++)
        {
            for (var j = i + 1; j < clause.Literals.Count; j++)
            {
                if (clause.Literals[i].Negated != clause.Literals[j].Negated &&
                    clause.Literals[i].Atom.Equals(clause.Literals[j].Atom))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>A canonical string (variables renamed by first appearance) for variant/duplicate elimination.</summary>
    private static string CanonicalKey(FolClause clause)
    {
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var literalKeys = clause.Literals.Select(l => (l.Negated ? "!" : string.Empty) + CanonicalAtom(l.Atom, canonical)).ToList();
        literalKeys.Sort(StringComparer.Ordinal);
        return string.Join(" | ", literalKeys);
    }

    private static string CanonicalAtom(FolFormula atom, Dictionary<string, string> canonical) => atom switch
    {
        FolPredicate p => p.Symbol + "(" + string.Join(",", p.Args.Select(a => CanonicalTerm(a, canonical))) + ")",
        FolEquals e => "=(" + CanonicalTerm(e.Left, canonical) + "," + CanonicalTerm(e.Right, canonical) + ")",
        _ => atom.ToString()!,
    };

    private static string CanonicalTerm(FolTerm term, Dictionary<string, string> canonical)
    {
        switch (term)
        {
            case FolVar v:
                if (!canonical.TryGetValue(v.Name, out var name))
                {
                    name = "V" + canonical.Count;
                    canonical[v.Name] = name;
                }
                return name;
            case FolFunc f when f.Args.Count == 0:
                return f.Symbol;
            case FolFunc f:
                return f.Symbol + "(" + string.Join(",", f.Args.Select(a => CanonicalTerm(a, canonical))) + ")";
            default:
                return term.ToString()!;
        }
    }
}
