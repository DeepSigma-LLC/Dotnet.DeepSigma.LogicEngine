using DeepSigma.LogicEngine.Ctl;

namespace DeepSigma.LogicEngine.Tests.Ctl;

/// <summary>
/// Independent CTL semantics by explicit path search (DFS / reachability / bounded
/// walks), distinct from the model checker's least/greatest fixpoints. Used to
/// differentially validate the fixpoint implementation on a total Kripke structure.
/// </summary>
internal static class CtlOracle
{
    public static IReadOnlySet<int> SatisfyingStates(KripkeStructure k, CtlFormula f, string[] atoms)
    {
        var all = Enumerable.Range(0, k.StateCount).ToHashSet();
        switch (f)
        {
            case CtlBool b: return b.Value ? all : new HashSet<int>();
            case CtlAtom a: return Where(k, s => k.Holds(s, a.Name));
            case CtlNot n: return Minus(all, SatisfyingStates(k, n.Operand, atoms));
            case CtlAnd x: return And(SatisfyingStates(k, x.Left, atoms), SatisfyingStates(k, x.Right, atoms));
            case CtlOr x: return Or(SatisfyingStates(k, x.Left, atoms), SatisfyingStates(k, x.Right, atoms));
            case CtlImplies x: return Or(Minus(all, SatisfyingStates(k, x.Antecedent, atoms)), SatisfyingStates(k, x.Consequent, atoms));
            case CtlIff x:
                var l = SatisfyingStates(k, x.Left, atoms);
                var r = SatisfyingStates(k, x.Right, atoms);
                return Or(And(l, r), And(Minus(all, l), Minus(all, r)));

            case CtlEX x:
                var ex = SatisfyingStates(k, x.Operand, atoms);
                return Where(k, s => k.Successors(s).Any(ex.Contains));
            case CtlAX x:
                var ax = SatisfyingStates(k, x.Operand, atoms);
                return Where(k, s => k.Successors(s).All(ax.Contains));
            case CtlEU u:
                return ExistsUntil(k, SatisfyingStates(k, u.Left, atoms), SatisfyingStates(k, u.Right, atoms));
            case CtlEF x:
                return ExistsUntil(k, all, SatisfyingStates(k, x.Operand, atoms));
            case CtlEG x:
                return ExistsGlobally(k, SatisfyingStates(k, x.Operand, atoms));
            case CtlAG x:
                return AllGlobally(k, SatisfyingStates(k, x.Operand, atoms));
            case CtlAF x:
                return Minus(all, ExistsGlobally(k, Minus(all, SatisfyingStates(k, x.Operand, atoms))));
            case CtlAU u:
                var phi = SatisfyingStates(k, u.Left, atoms);
                var psi = SatisfyingStates(k, u.Right, atoms);
                var escape = ExistsUntil(k, Minus(all, psi), And(Minus(all, phi), Minus(all, psi)));
                return Minus(all, Or(escape, ExistsGlobally(k, Minus(all, psi))));
            default:
                throw new InvalidOperationException();
        }
    }

    // s ⊨ E[φ U ψ]: a finite path through φ-states reaching a ψ-state.
    private static IReadOnlySet<int> ExistsUntil(KripkeStructure k, IReadOnlySet<int> phi, IReadOnlySet<int> psi)
        => Where(k, s => EuFrom(k, s, phi, psi, new HashSet<int>()));

    private static bool EuFrom(KripkeStructure k, int s, IReadOnlySet<int> phi, IReadOnlySet<int> psi, HashSet<int> visited)
    {
        if (psi.Contains(s)) { return true; }
        if (!phi.Contains(s) || !visited.Add(s)) { return false; }
        return k.Successors(s).Any(t => EuFrom(k, t, phi, psi, visited));
    }

    // s ⊨ EG φ: an infinite φ-path exists — by pigeonhole, a φ-walk of length StateCount.
    private static IReadOnlySet<int> ExistsGlobally(KripkeStructure k, IReadOnlySet<int> phi)
        => Where(k, s => EgWalk(k, s, phi, k.StateCount));

    private static bool EgWalk(KripkeStructure k, int s, IReadOnlySet<int> phi, int depth)
    {
        if (!phi.Contains(s)) { return false; }
        if (depth == 0) { return true; }
        return k.Successors(s).Any(t => EgWalk(k, t, phi, depth - 1));
    }

    // s ⊨ AG φ: every reachable state (including s) is in φ.
    private static IReadOnlySet<int> AllGlobally(KripkeStructure k, IReadOnlySet<int> phi)
        => Where(k, s => Reachable(k, s).All(phi.Contains));

    private static IEnumerable<int> Reachable(KripkeStructure k, int start)
    {
        var seen = new HashSet<int> { start };
        var stack = new Stack<int>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var s = stack.Pop();
            foreach (var t in k.Successors(s))
            {
                if (seen.Add(t)) { stack.Push(t); }
            }
        }
        return seen;
    }

    private static HashSet<int> Where(KripkeStructure k, Func<int, bool> predicate)
    {
        var set = new HashSet<int>();
        for (var s = 0; s < k.StateCount; s++)
        {
            if (predicate(s)) { set.Add(s); }
        }
        return set;
    }

    private static IReadOnlySet<int> And(IReadOnlySet<int> a, IReadOnlySet<int> b) { var x = new HashSet<int>(a); x.IntersectWith(b); return x; }
    private static IReadOnlySet<int> Or(IReadOnlySet<int> a, IReadOnlySet<int> b) { var x = new HashSet<int>(a); x.UnionWith(b); return x; }
    private static IReadOnlySet<int> Minus(IReadOnlySet<int> a, IReadOnlySet<int> b) { var x = new HashSet<int>(a); x.ExceptWith(b); return x; }
}
