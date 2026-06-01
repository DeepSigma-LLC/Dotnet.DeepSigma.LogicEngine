namespace DeepSigma.LogicEngine.Temporal;

/// <summary>
/// A Linear Temporal Logic (LTL) formula over atomic propositions. Interpreted
/// over infinite traces. Temporal operators: <c>X</c> (next), <c>F</c>
/// (eventually), <c>G</c> (globally), <c>U</c> (until), <c>R</c> (release),
/// <c>W</c> (weak until), combined with the usual boolean connectives.
/// </summary>
public abstract record LtlFormula
{
    /// <summary>The constant true.</summary>
    public static LtlFormula True { get; } = new LtlBool(true);

    /// <summary>The constant false.</summary>
    public static LtlFormula False { get; } = new LtlBool(false);

    /// <summary>An atomic proposition with the given name.</summary>
    public static LtlFormula Atom(string name) => new LtlAtom(name);

    /// <summary>Logical negation.</summary>
    public static LtlFormula Not(LtlFormula f) => new LtlNot(f);

    /// <summary>Logical conjunction a ∧ b.</summary>
    public static LtlFormula And(LtlFormula a, LtlFormula b) => new LtlAnd(a, b);

    /// <summary>Logical disjunction a ∨ b.</summary>
    public static LtlFormula Or(LtlFormula a, LtlFormula b) => new LtlOr(a, b);

    /// <summary>Material implication a → b.</summary>
    public static LtlFormula Implies(LtlFormula a, LtlFormula b) => new LtlImplies(a, b);

    /// <summary>Biconditional a ↔ b.</summary>
    public static LtlFormula Iff(LtlFormula a, LtlFormula b) => new LtlIff(a, b);

    /// <summary>X f — f holds in the next state.</summary>
    public static LtlFormula Next(LtlFormula f) => new LtlNext(f);

    /// <summary>F f — f eventually holds.</summary>
    public static LtlFormula Eventually(LtlFormula f) => new LtlEventually(f);

    /// <summary>G f — f holds in every state.</summary>
    public static LtlFormula Globally(LtlFormula f) => new LtlGlobally(f);

    /// <summary>a U b — a holds until b becomes true (and b does eventually hold).</summary>
    public static LtlFormula Until(LtlFormula a, LtlFormula b) => new LtlUntil(a, b);

    /// <summary>a R b — b holds up to and including when a releases it (dual of until).</summary>
    public static LtlFormula Release(LtlFormula a, LtlFormula b) => new LtlRelease(a, b);

    /// <summary>a W b — weak until: a holds until b, or a holds forever.</summary>
    public static LtlFormula WeakUntil(LtlFormula a, LtlFormula b) => new LtlWeakUntil(a, b);

    /// <summary>Negation operator.</summary>
    public static LtlFormula operator !(LtlFormula f) => new LtlNot(f);

    /// <summary>Conjunction operator.</summary>
    public static LtlFormula operator &(LtlFormula a, LtlFormula b) => new LtlAnd(a, b);

    /// <summary>Disjunction operator.</summary>
    public static LtlFormula operator |(LtlFormula a, LtlFormula b) => new LtlOr(a, b);

    /// <summary>Parses an LTL formula from text. Throws <see cref="FormatException"/> on malformed input.</summary>
    public static LtlFormula Parse(string source) => LtlParser.Parse(source);

    /// <summary>Attempts to parse an LTL formula, returning false on malformed input.</summary>
    public static bool TryParse(string source, out LtlFormula formula) => LtlParser.TryParse(source, out formula);

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

    /// <summary>Renders the formula in the textual syntax.</summary>
    public sealed override string ToString() => LtlPrinter.Print(this);
}

/// <summary>A boolean constant.</summary>
public sealed record LtlBool(bool Value) : LtlFormula;

/// <summary>An atomic proposition referenced by name.</summary>
public sealed record LtlAtom(string Name) : LtlFormula;

/// <summary>Base record for the single-operand LTL operators.</summary>
public abstract record LtlUnary(LtlFormula Operand) : LtlFormula;

/// <summary>Logical negation.</summary>
public sealed record LtlNot(LtlFormula Operand) : LtlUnary(Operand);

/// <summary>X — the operand holds in the next state.</summary>
public sealed record LtlNext(LtlFormula Operand) : LtlUnary(Operand);

/// <summary>F — the operand eventually holds.</summary>
public sealed record LtlEventually(LtlFormula Operand) : LtlUnary(Operand);

/// <summary>G — the operand holds in every state.</summary>
public sealed record LtlGlobally(LtlFormula Operand) : LtlUnary(Operand);

/// <summary>Base record for the two-operand LTL operators.</summary>
public abstract record LtlBinary(LtlFormula Left, LtlFormula Right) : LtlFormula;

/// <summary>Logical conjunction <paramref name="Left"/> ∧ <paramref name="Right"/>.</summary>
public sealed record LtlAnd(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);

/// <summary>Logical disjunction <paramref name="Left"/> ∨ <paramref name="Right"/>.</summary>
public sealed record LtlOr(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);

/// <summary>Material implication <paramref name="Left"/> → <paramref name="Right"/>.</summary>
public sealed record LtlImplies(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);

/// <summary>Biconditional <paramref name="Left"/> ↔ <paramref name="Right"/>.</summary>
public sealed record LtlIff(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);

/// <summary>Until: <paramref name="Left"/> holds until <paramref name="Right"/> (which does eventually hold).</summary>
public sealed record LtlUntil(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);

/// <summary>Release: <paramref name="Right"/> holds up to and including when <paramref name="Left"/> releases it.</summary>
public sealed record LtlRelease(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);

/// <summary>Weak until: <paramref name="Left"/> holds until <paramref name="Right"/>, or forever.</summary>
public sealed record LtlWeakUntil(LtlFormula Left, LtlFormula Right) : LtlBinary(Left, Right);
