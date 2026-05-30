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
/// binary resolution, factoring, and (optionally) <b>paramodulation</b> for built-in
/// equality, all modulo unification. Redundant clauses are pruned by tautology
/// deletion and forward/backward <b>θ-subsumption</b>. Bounded by a clause budget.
/// Resolution + factoring is refutation-complete for first-order logic; adding
/// paramodulation with the reflexivity clause <c>x = x</c> extends completeness to
/// logic with equality.
/// </summary>
internal sealed class FirstOrderResolver
{
    private readonly int _maxClauses;
    private readonly bool _paramodulate;
    private int _renameCounter;
    private int _groundCounter;

    public FirstOrderResolver(int maxClauses, bool paramodulate)
    {
        _maxClauses = maxClauses;
        _paramodulate = paramodulate;
    }

    public FolProofStatus Refute(IReadOnlyList<FolClause> clauses)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unprocessed = new List<FolClause>();
        var processed = new List<FolClause>();

        bool Enqueue(FolClause c)
        {
            if (c.IsEmpty)
            {
                return true;
            }
            if (IsTautology(c) || !seen.Add(CanonicalKey(c)))
            {
                return false;
            }
            if (processed.Any(p => Subsumes(p, c)))
            {
                return false; // forward subsumption
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
            if (processed.Any(p => Subsumes(p, given)))
            {
                continue;
            }
            processed.RemoveAll(p => Subsumes(given, p)); // backward subsumption
            processed.Add(given);

            var derived = new List<FolClause>();
            derived.AddRange(Factors(given));
            if (_paramodulate)
            {
                derived.AddRange(ReflexivityResolvents(given)); // close s ≠ t when s and t unify
            }
            foreach (var p in processed)
            {
                derived.AddRange(Resolvents(given, p));
                if (_paramodulate)
                {
                    derived.AddRange(Paramodulants(given, p));
                    if (!ReferenceEquals(p, given))
                    {
                        derived.AddRange(Paramodulants(p, given));
                    }
                }
            }

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

    // ---- inference rules ----

    private IEnumerable<FolClause> Resolvents(FolClause c, FolClause d)
    {
        var left = Rename(c);
        var right = Rename(d);
        var results = new List<FolClause>();
        for (var i = 0; i < left.Literals.Count; i++)
        {
            for (var j = 0; j < right.Literals.Count; j++)
            {
                if (left.Literals[i].Negated == right.Literals[j].Negated)
                {
                    continue;
                }
                var mgu = Unifier.Unify(left.Literals[i].Atom, right.Literals[j].Atom);
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
                var clause = new FolClause(Dedup(literals));
                if (!IsTautology(clause)) { results.Add(clause); }
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
                if (mgu is not null)
                {
                    results.Add(new FolClause(Dedup(clause.Literals.Select(mgu.Apply))));
                }
            }
        }
        return results;
    }

    /// <summary>Reflexivity resolution: drop a negated equality <c>s ≠ t</c> whose sides unify.</summary>
    private IEnumerable<FolClause> ReflexivityResolvents(FolClause c)
    {
        var clause = Rename(c);
        var results = new List<FolClause>();
        for (var i = 0; i < clause.Literals.Count; i++)
        {
            if (clause.Literals[i] is { Negated: true, Atom: FolEquals e })
            {
                var mgu = Unifier.Unify(e.Left, e.Right);
                if (mgu is null)
                {
                    continue;
                }
                var literals = new List<FolLiteral>();
                for (var a = 0; a < clause.Literals.Count; a++)
                {
                    if (a != i) { literals.Add(mgu.Apply(clause.Literals[a])); }
                }
                var resolvent = new FolClause(Dedup(literals));
                if (!IsTautology(resolvent)) { results.Add(resolvent); }
            }
        }
        return results;
    }

    /// <summary>Paramodulate from an equation in <paramref name="from"/> into <paramref name="into"/>.</summary>
    private IEnumerable<FolClause> Paramodulants(FolClause from, FolClause into)
    {
        var src = Rename(from);
        var tgt = Rename(into);
        var results = new List<FolClause>();
        for (var fi = 0; fi < src.Literals.Count; fi++)
        {
            if (src.Literals[fi].Negated || src.Literals[fi].Atom is not FolEquals equation)
            {
                continue;
            }
            foreach (var (lhs, rhs) in new[] { (equation.Left, equation.Right), (equation.Right, equation.Left) })
            {
                for (var di = 0; di < tgt.Literals.Count; di++)
                {
                    foreach (var (sub, rebuild) in AtomPositions(tgt.Literals[di].Atom))
                    {
                        if (sub is FolVar)
                        {
                            continue; // paramodulating into a variable is redundant
                        }
                        var mgu = Unifier.Unify(lhs, sub);
                        if (mgu is null)
                        {
                            continue;
                        }
                        var rewritten = tgt.Literals[di] with { Atom = rebuild(rhs) };
                        var literals = new List<FolLiteral>();
                        for (var a = 0; a < src.Literals.Count; a++)
                        {
                            if (a != fi) { literals.Add(mgu.Apply(src.Literals[a])); }
                        }
                        for (var b = 0; b < tgt.Literals.Count; b++)
                        {
                            if (b != di) { literals.Add(mgu.Apply(tgt.Literals[b])); }
                        }
                        literals.Add(mgu.Apply(rewritten));
                        var clause = new FolClause(Dedup(literals));
                        if (!IsTautology(clause)) { results.Add(clause); }
                    }
                }
            }
        }
        return results;
    }

    // ---- subterm positions (for paramodulation) ----

    private static IEnumerable<(FolTerm Sub, Func<FolTerm, FolFormula> Rebuild)> AtomPositions(FolFormula atom)
    {
        switch (atom)
        {
            case FolPredicate p:
                for (var i = 0; i < p.Args.Count; i++)
                {
                    var index = i;
                    foreach (var (sub, rebuild) in TermPositions(p.Args[i]))
                    {
                        yield return (sub, nt => new FolPredicate(p.Symbol, Replace(p.Args, index, rebuild(nt))));
                    }
                }
                break;
            case FolEquals e:
                foreach (var (sub, rebuild) in TermPositions(e.Left))
                {
                    yield return (sub, nt => new FolEquals(rebuild(nt), e.Right));
                }
                foreach (var (sub, rebuild) in TermPositions(e.Right))
                {
                    yield return (sub, nt => new FolEquals(e.Left, rebuild(nt)));
                }
                break;
        }
    }

    private static IEnumerable<(FolTerm Sub, Func<FolTerm, FolTerm> Rebuild)> TermPositions(FolTerm term)
    {
        yield return (term, x => x);
        if (term is FolFunc f)
        {
            for (var i = 0; i < f.Args.Count; i++)
            {
                var index = i;
                foreach (var (sub, rebuild) in TermPositions(f.Args[i]))
                {
                    yield return (sub, x => new FolFunc(f.Symbol, Replace(f.Args, index, rebuild(x))));
                }
            }
        }
    }

    private static FolTerm[] Replace(IReadOnlyList<FolTerm> args, int index, FolTerm value)
    {
        var copy = args.ToArray();
        copy[index] = value;
        return copy;
    }

    // ---- subsumption (θ-subsumption: a subsumes b if ∃σ. aσ ⊆ b) ----

    private bool Subsumes(FolClause a, FolClause b)
    {
        if (a.Literals.Count > b.Literals.Count)
        {
            return false;
        }
        var ground = GroundVariables(b);
        return MatchLiterals(a.Literals, 0, ground.Literals, new Dictionary<string, FolTerm>(StringComparer.Ordinal));
    }

    private static bool MatchLiterals(IReadOnlyList<FolLiteral> pattern, int index, IReadOnlyList<FolLiteral> target, Dictionary<string, FolTerm> bindings)
    {
        if (index == pattern.Count)
        {
            return true;
        }
        foreach (var t in target)
        {
            if (t.Negated != pattern[index].Negated)
            {
                continue;
            }
            var trial = new Dictionary<string, FolTerm>(bindings, StringComparer.Ordinal);
            if (MatchAtom(pattern[index].Atom, t.Atom, trial) && MatchLiterals(pattern, index + 1, target, trial))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchAtom(FolFormula pattern, FolFormula target, Dictionary<string, FolTerm> bindings)
    {
        switch (pattern, target)
        {
            case (FolPredicate pp, FolPredicate tp):
                if (pp.Symbol != tp.Symbol || pp.Args.Count != tp.Args.Count) { return false; }
                for (var i = 0; i < pp.Args.Count; i++)
                {
                    if (!MatchTerm(pp.Args[i], tp.Args[i], bindings)) { return false; }
                }
                return true;
            case (FolEquals pe, FolEquals te):
                return (MatchTerm(pe.Left, te.Left, bindings) && MatchTerm(pe.Right, te.Right, bindings))
                    || (MatchTerm(pe.Left, te.Right, bindings) && MatchTerm(pe.Right, te.Left, bindings));
            default:
                return false;
        }
    }

    private static bool MatchTerm(FolTerm pattern, FolTerm target, Dictionary<string, FolTerm> bindings)
    {
        switch (pattern)
        {
            case FolVar v:
                if (bindings.TryGetValue(v.Name, out var bound))
                {
                    return bound.Equals(target);
                }
                bindings[v.Name] = target;
                return true;
            case FolFunc pf when target is FolFunc tf:
                if (pf.Symbol != tf.Symbol || pf.Args.Count != tf.Args.Count) { return false; }
                for (var i = 0; i < pf.Args.Count; i++)
                {
                    if (!MatchTerm(pf.Args[i], tf.Args[i], bindings)) { return false; }
                }
                return true;
            default:
                return false;
        }
    }

    private FolClause GroundVariables(FolClause clause)
    {
        var map = new Dictionary<string, FolTerm>(StringComparer.Ordinal);
        foreach (var name in Variables(clause))
        {
            map[name] = FolTerm.Constant("$g" + _groundCounter++);
        }
        var substitution = new Substitution(map);
        return new FolClause(clause.Literals.Select(substitution.Apply).ToList());
    }

    // ---- helpers ----

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
            case FolPredicate p: foreach (var a in p.Args) { CollectVars(a, names); } break;
            case FolEquals e: CollectVars(e.Left, names); CollectVars(e.Right, names); break;
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
            if (seen.Add(l)) { result.Add(l); }
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
            // x = x is a tautology.
            if (!clause.Literals[i].Negated && clause.Literals[i].Atom is FolEquals e && e.Left.Equals(e.Right))
            {
                return true;
            }
        }
        return false;
    }

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
