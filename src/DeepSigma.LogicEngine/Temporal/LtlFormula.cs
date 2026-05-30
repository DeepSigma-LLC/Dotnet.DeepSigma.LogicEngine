namespace DeepSigma.LogicEngine.Temporal;

/// <summary>
/// A Linear Temporal Logic (LTL) formula over atomic propositions. Interpreted
/// over infinite traces. Temporal operators: <c>X</c> (next), <c>F</c>
/// (eventually), <c>G</c> (globally), <c>U</c> (until), <c>R</c> (release),
/// <c>W</c> (weak until), combined with the usual boolean connectives.
/// </summary>
public abstract record LtlFormula
{
    public static LtlFormula True { get; } = new LtlBool(true);
    public static LtlFormula False { get; } = new LtlBool(false);

    public static LtlFormula Atom(string name) => new LtlAtom(name);
    public static LtlFormula Not(LtlFormula f) => new LtlNot(f);
    public static LtlFormula And(LtlFormula a, LtlFormula b) => new LtlAnd(a, b);
    public static LtlFormula Or(LtlFormula a, LtlFormula b) => new LtlOr(a, b);
    public static LtlFormula Implies(LtlFormula a, LtlFormula b) => new LtlImplies(a, b);
    public static LtlFormula Iff(LtlFormula a, LtlFormula b) => new LtlIff(a, b);
    public static LtlFormula Next(LtlFormula f) => new LtlNext(f);
    public static LtlFormula Eventually(LtlFormula f) => new LtlEventually(f);
    public static LtlFormula Globally(LtlFormula f) => new LtlGlobally(f);
    public static LtlFormula Until(LtlFormula a, LtlFormula b) => new LtlUntil(a, b);
    public static LtlFormula Release(LtlFormula a, LtlFormula b) => new LtlRelease(a, b);
    public static LtlFormula WeakUntil(LtlFormula a, LtlFormula b) => new LtlWeakUntil(a, b);

    public static LtlFormula operator !(LtlFormula f) => new LtlNot(f);
    public static LtlFormula operator &(LtlFormula a, LtlFormula b) => new LtlAnd(a, b);
    public static LtlFormula operator |(LtlFormula a, LtlFormula b) => new LtlOr(a, b);

    public static LtlFormula Parse(string source) => LtlParser.Parse(source);

    /// <summary>The set of atomic proposition names occurring in this formula.</summary>
    public IReadOnlySet<string> Atoms()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        Collect(this, set);
        return set;
    }

    private static void Collect(LtlFormula f, HashSet<string> acc)
    {
        switch (f)
        {
            case LtlAtom a: acc.Add(a.Name); break;
            case LtlBool: break;
            case LtlUnary u: Collect(u.Operand, acc); break;
            case LtlBinary b: Collect(b.Left, acc); Collect(b.Right, acc); break;
        }
    }

    /// <summary>
    /// Negation normal form: negations pushed to atoms, implications/iff/weak-until
    /// eliminated, leaving only literals, ∧, ∨, X, F, G, U, R.
    /// </summary>
    public LtlFormula ToNnf() => Nnf(this, negate: false);

    private static LtlFormula Nnf(LtlFormula f, bool negate) => f switch
    {
        LtlBool b => new LtlBool(negate ? !b.Value : b.Value),
        LtlAtom => negate ? new LtlNot(f) : f,
        LtlNot n => Nnf(n.Operand, !negate),
        LtlAnd a => negate
            ? new LtlOr(Nnf(a.Left, true), Nnf(a.Right, true))
            : new LtlAnd(Nnf(a.Left, false), Nnf(a.Right, false)),
        LtlOr o => negate
            ? new LtlAnd(Nnf(o.Left, true), Nnf(o.Right, true))
            : new LtlOr(Nnf(o.Left, false), Nnf(o.Right, false)),
        LtlImplies i => Nnf(new LtlOr(new LtlNot(i.Left), i.Right), negate),
        LtlIff bi => Nnf(new LtlOr(
            new LtlAnd(bi.Left, bi.Right),
            new LtlAnd(new LtlNot(bi.Left), new LtlNot(bi.Right))), negate),
        LtlNext x => new LtlNext(Nnf(x.Operand, negate)),
        LtlEventually e => negate ? new LtlGlobally(Nnf(e.Operand, true)) : new LtlEventually(Nnf(e.Operand, false)),
        LtlGlobally g => negate ? new LtlEventually(Nnf(g.Operand, true)) : new LtlGlobally(Nnf(g.Operand, false)),
        LtlUntil u => negate
            ? new LtlRelease(Nnf(u.Left, true), Nnf(u.Right, true))
            : new LtlUntil(Nnf(u.Left, false), Nnf(u.Right, false)),
        LtlRelease r => negate
            ? new LtlUntil(Nnf(r.Left, true), Nnf(r.Right, true))
            : new LtlRelease(Nnf(r.Left, false), Nnf(r.Right, false)),
        // a W b ≡ (a U b) ∨ G a
        LtlWeakUntil w => Nnf(new LtlOr(new LtlUntil(w.Left, w.Right), new LtlGlobally(w.Left)), negate),
        _ => throw new InvalidOperationException($"Unknown LTL node: {f.GetType().Name}"),
    };

    public sealed override string ToString() => LtlPrinter.Print(this);
}

public sealed record LtlBool(bool Value) : LtlFormula;
public sealed record LtlAtom(string Name) : LtlFormula;

public abstract record LtlUnary(LtlFormula Operand) : LtlFormula;
public sealed record LtlNot(LtlFormula Operand) : LtlUnary(Operand);
public sealed record LtlNext(LtlFormula Operand) : LtlUnary(Operand);
public sealed record LtlEventually(LtlFormula Operand) : LtlUnary(Operand);
public sealed record LtlGlobally(LtlFormula Operand) : LtlUnary(Operand);

public abstract record LtlBinary(LtlFormula Left, LtlFormula Right) : LtlFormula;
public sealed record LtlAnd(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
public sealed record LtlOr(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
public sealed record LtlImplies(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
public sealed record LtlIff(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
public sealed record LtlUntil(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
public sealed record LtlRelease(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
public sealed record LtlWeakUntil(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
