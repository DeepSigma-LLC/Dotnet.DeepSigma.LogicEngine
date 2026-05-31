namespace DeepSigma.LogicEngine.Ctl;

/// <summary>
/// Explicit-state CTL model checking by bottom-up labelling: it computes the exact
/// set of states satisfying a formula, using least/greatest fixpoints for the
/// temporal operators. <c>EU</c> is a least fixpoint, <c>EG</c> a greatest fixpoint,
/// <c>EX</c> a pre-image; the universal/derived operators reduce to these. Unlike
/// the bounded LTL model checker, this is a <b>complete, exact</b> decision over the
/// finite structure.
///
/// <para>
/// How it works: for each subformula it computes the precise set of states where
/// that subformula is true, starting from the atomic propositions and working
/// outward. The path operators are computed by repeating a one-step rule until the
/// set stops changing — a <em>fixed point</em>. <c>EX φ</c> is one step backward:
/// the states with a successor in φ. <c>E[φ U ψ]</c> starts from the ψ-states and
/// repeatedly adds any φ-state that can step into the set — it grows until it
/// stabilises (a <b>least</b> fixpoint: "can reach ψ"). <c>EG φ</c> starts from all
/// φ-states and repeatedly removes any with no successor still in the set — it
/// shrinks until it stabilises (a <b>greatest</b> fixpoint: "can stay in φ
/// forever", i.e. on a cycle). Because the structure is finite, both iterations are
/// guaranteed to terminate.
/// </para>
/// </summary>
public static class CtlModelChecker
{
    /// <summary>The exact set of states satisfying <paramref name="formula"/>.</summary>
    public static IReadOnlySet<int> SatisfyingStates(KripkeStructure kripke, CtlFormula formula)
    {
        switch (formula)
        {
            case CtlBool b:
                return b.Value ? AllStates(kripke) : new HashSet<int>();
            case CtlAtom a:
                return States(kripke, s => kripke.Holds(s, a.Name));
            case CtlNot n:
                return Complement(kripke, SatisfyingStates(kripke, n.Operand));
            case CtlAnd x:
                return Intersect(SatisfyingStates(kripke, x.Left), SatisfyingStates(kripke, x.Right));
            case CtlOr x:
                return Union(SatisfyingStates(kripke, x.Left), SatisfyingStates(kripke, x.Right));
            case CtlImplies x:
                return Union(Complement(kripke, SatisfyingStates(kripke, x.Antecedent)), SatisfyingStates(kripke, x.Consequent));
            case CtlIff x:
                var l = SatisfyingStates(kripke, x.Left);
                var r = SatisfyingStates(kripke, x.Right);
                return Union(Intersect(l, r), Intersect(Complement(kripke, l), Complement(kripke, r)));

            case CtlEX x:
                return ExistsNext(kripke, SatisfyingStates(kripke, x.Operand));
            case CtlEU u:
                return ExistsUntil(kripke, SatisfyingStates(kripke, u.Left), SatisfyingStates(kripke, u.Right));
            case CtlEG x:
                return ExistsGlobally(kripke, SatisfyingStates(kripke, x.Operand));

            // Derived operators, reduced to EX / EU / EG.
            case CtlEF x:
                return ExistsUntil(kripke, AllStates(kripke), SatisfyingStates(kripke, x.Operand));
            case CtlAX x:
                return Complement(kripke, ExistsNext(kripke, Complement(kripke, SatisfyingStates(kripke, x.Operand))));
            case CtlAG x:
                return Complement(kripke, ExistsUntil(kripke, AllStates(kripke), Complement(kripke, SatisfyingStates(kripke, x.Operand))));
            case CtlAF x:
                return Complement(kripke, ExistsGlobally(kripke, Complement(kripke, SatisfyingStates(kripke, x.Operand))));
            case CtlAU u:
                return AllUntil(kripke, SatisfyingStates(kripke, u.Left), SatisfyingStates(kripke, u.Right));

            default:
                throw new InvalidOperationException($"Unknown CTL node: {formula.GetType().Name}");
        }
    }

    /// <summary>True if <paramref name="formula"/> holds in <paramref name="state"/>.</summary>
    public static bool Holds(KripkeStructure kripke, CtlFormula formula, int state)
        => SatisfyingStates(kripke, formula).Contains(state);

    // EX φ: states with some successor in the set.
    private static IReadOnlySet<int> ExistsNext(KripkeStructure kripke, IReadOnlySet<int> target)
        => States(kripke, s => kripke.Successors(s).Any(target.Contains));

    // E[φ U ψ]: least fixpoint — ψ-states, plus φ-states with a successor already in the set.
    private static IReadOnlySet<int> ExistsUntil(KripkeStructure kripke, IReadOnlySet<int> phi, IReadOnlySet<int> psi)
    {
        var result = new HashSet<int>(psi);
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var s = 0; s < kripke.StateCount; s++)
            {
                if (!result.Contains(s) && phi.Contains(s) && kripke.Successors(s).Any(result.Contains))
                {
                    result.Add(s);
                    changed = true;
                }
            }
        }
        return result;
    }

    // EG φ: greatest fixpoint — φ-states that keep a successor inside the set.
    private static IReadOnlySet<int> ExistsGlobally(KripkeStructure kripke, IReadOnlySet<int> phi)
    {
        var result = new HashSet<int>(phi);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var s in result.ToList())
            {
                if (!kripke.Successors(s).Any(result.Contains))
                {
                    result.Remove(s);
                    changed = true;
                }
            }
        }
        return result;
    }

    // A[φ U ψ] ≡ ¬( E[¬ψ U (¬φ ∧ ¬ψ)] ∨ EG ¬ψ ).
    private static IReadOnlySet<int> AllUntil(KripkeStructure kripke, IReadOnlySet<int> phi, IReadOnlySet<int> psi)
    {
        var notPhi = Complement(kripke, phi);
        var notPsi = Complement(kripke, psi);
        var escape = ExistsUntil(kripke, notPsi, Intersect(notPhi, notPsi));
        var stall = ExistsGlobally(kripke, notPsi);
        return Complement(kripke, Union(escape, stall));
    }

    private static IReadOnlySet<int> AllStates(KripkeStructure kripke)
        => new HashSet<int>(Enumerable.Range(0, kripke.StateCount));

    private static HashSet<int> States(KripkeStructure kripke, Func<int, bool> predicate)
    {
        var set = new HashSet<int>();
        for (var s = 0; s < kripke.StateCount; s++)
        {
            if (predicate(s)) { set.Add(s); }
        }
        return set;
    }

    private static IReadOnlySet<int> Complement(KripkeStructure kripke, IReadOnlySet<int> set)
        => States(kripke, s => !set.Contains(s));

    private static IReadOnlySet<int> Intersect(IReadOnlySet<int> a, IReadOnlySet<int> b)
    {
        var set = new HashSet<int>(a);
        set.IntersectWith(b);
        return set;
    }

    private static IReadOnlySet<int> Union(IReadOnlySet<int> a, IReadOnlySet<int> b)
    {
        var set = new HashSet<int>(a);
        set.UnionWith(b);
        return set;
    }
}
