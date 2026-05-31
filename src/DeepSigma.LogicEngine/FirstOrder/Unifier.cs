namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// Robinson unification: the most general unifier of two terms, with an occurs check.
///
/// <para>
/// Unifying two terms means finding a substitution for their variables that makes
/// them syntactically identical — e.g. <c>f(x, b)</c> and <c>f(a, y)</c> unify via
/// <c>x ↦ a, y ↦ b</c>. "Most general" means it commits to nothing beyond what is
/// forced, so any other unifier is an instance of it; this is what lets resolution
/// stay as general as possible. The <em>occurs check</em> refuses to bind a variable
/// to a term that contains it (such as <c>x = f(x)</c>), which has no finite
/// solution — without it the prover could build infinite terms.
/// </para>
/// </summary>
public static class Unifier
{
    /// <summary>The MGU of <paramref name="a"/> and <paramref name="b"/>, or null if they do not unify.</summary>
    public static Substitution? Unify(FolTerm a, FolTerm b)
    {
        var bindings = new Dictionary<string, FolTerm>(StringComparer.Ordinal);
        return UnifyInto(a, b, bindings) ? new Substitution(bindings) : null;
    }

    /// <summary>The MGU of two atoms (same predicate/equality shape), or null.</summary>
    public static Substitution? Unify(FolFormula a, FolFormula b)
    {
        var bindings = new Dictionary<string, FolTerm>(StringComparer.Ordinal);
        return UnifyAtoms(a, b, bindings) ? new Substitution(bindings) : null;
    }

    private static bool UnifyAtoms(FolFormula a, FolFormula b, Dictionary<string, FolTerm> bindings)
    {
        switch (a, b)
        {
            case (FolPredicate pa, FolPredicate pb):
                if (pa.Symbol != pb.Symbol || pa.Arguments.Count != pb.Arguments.Count)
                {
                    return false;
                }
                for (var i = 0; i < pa.Arguments.Count; i++)
                {
                    if (!UnifyInto(pa.Arguments[i], pb.Arguments[i], bindings))
                    {
                        return false;
                    }
                }
                return true;
            case (FolEquals ea, FolEquals eb):
                // Equality is symmetric; try both orientations.
                return (UnifyInto(ea.Left, eb.Left, bindings) && UnifyInto(ea.Right, eb.Right, bindings))
                    || TryFresh(ea, eb, bindings);
            default:
                return false;
        }
    }

    private static bool TryFresh(FolEquals a, FolEquals b, Dictionary<string, FolTerm> bindings)
    {
        var local = new Dictionary<string, FolTerm>(StringComparer.Ordinal);
        if (UnifyInto(a.Left, b.Right, local) && UnifyInto(a.Right, b.Left, local))
        {
            foreach (var (k, v) in local)
            {
                bindings[k] = v;
            }
            return true;
        }
        return false;
    }

    private static bool UnifyInto(FolTerm a, FolTerm b, Dictionary<string, FolTerm> bindings)
    {
        a = Walk(a, bindings);
        b = Walk(b, bindings);

        if (a is FolVar va)
        {
            if (b is FolVar vb && vb.Name == va.Name)
            {
                return true;
            }
            if (Occurs(va.Name, b, bindings))
            {
                return false;
            }
            bindings[va.Name] = b;
            return true;
        }
        if (b is FolVar)
        {
            return UnifyInto(b, a, bindings);
        }

        var fa = (FolFunc)a;
        var fb = (FolFunc)b;
        if (fa.Symbol != fb.Symbol || fa.Arguments.Count != fb.Arguments.Count)
        {
            return false;
        }
        for (var i = 0; i < fa.Arguments.Count; i++)
        {
            if (!UnifyInto(fa.Arguments[i], fb.Arguments[i], bindings))
            {
                return false;
            }
        }
        return true;
    }

    private static FolTerm Walk(FolTerm t, Dictionary<string, FolTerm> bindings)
    {
        while (t is FolVar v && bindings.TryGetValue(v.Name, out var next))
        {
            t = next;
        }
        return t;
    }

    private static bool Occurs(string name, FolTerm t, Dictionary<string, FolTerm> bindings)
    {
        t = Walk(t, bindings);
        return t switch
        {
            FolVar v => v.Name == name,
            FolFunc f => f.Arguments.Any(arg => Occurs(name, arg, bindings)),
            _ => false,
        };
    }
}
