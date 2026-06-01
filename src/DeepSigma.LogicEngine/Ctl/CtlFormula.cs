namespace DeepSigma.LogicEngine.Ctl;

/// <summary>
/// A Computation Tree Logic (CTL) formula: propositional atoms and connectives plus
/// the path-quantified temporal operators. <c>E</c> means "along some path", <c>A</c>
/// "along every path"; <c>X</c> next, <c>F</c> eventually, <c>G</c> globally, <c>U</c>
/// until. <c>EX</c>, <c>EG</c>, and <c>EU</c> are primitive; the rest are derived.
/// </summary>
public abstract record CtlFormula
{
    /// <summary>The constant true.</summary>
    public static CtlFormula True { get; } = new CtlBool(true);

    /// <summary>The constant false.</summary>
    public static CtlFormula False { get; } = new CtlBool(false);

    /// <summary>An atomic proposition with the given name.</summary>
    public static CtlFormula Atom(string name) => new CtlAtom(name);

    /// <summary>Logical negation.</summary>
    public static CtlFormula Not(CtlFormula f) => new CtlNot(f);

    /// <summary>Logical conjunction a ∧ b.</summary>
    public static CtlFormula And(CtlFormula a, CtlFormula b) => new CtlAnd(a, b);

    /// <summary>Logical disjunction a ∨ b.</summary>
    public static CtlFormula Or(CtlFormula a, CtlFormula b) => new CtlOr(a, b);

    /// <summary>Material implication a → b.</summary>
    public static CtlFormula Implies(CtlFormula a, CtlFormula b) => new CtlImplies(a, b);

    /// <summary>Biconditional a ↔ b.</summary>
    public static CtlFormula Iff(CtlFormula a, CtlFormula b) => new CtlIff(a, b);

    /// <summary>EX f — f holds in some successor state.</summary>
    public static CtlFormula EX(CtlFormula f) => new CtlEX(f);

    /// <summary>EG f — there is a path along which f holds globally.</summary>
    public static CtlFormula EG(CtlFormula f) => new CtlEG(f);

    /// <summary>EF f — there is a path along which f eventually holds.</summary>
    public static CtlFormula EF(CtlFormula f) => new CtlEF(f);

    /// <summary>E[a U b] — along some path, a holds until b becomes true.</summary>
    public static CtlFormula EU(CtlFormula a, CtlFormula b) => new CtlEU(a, b);

    /// <summary>AX f — f holds in every successor state.</summary>
    public static CtlFormula AX(CtlFormula f) => new CtlAX(f);

    /// <summary>AG f — f holds globally on every path.</summary>
    public static CtlFormula AG(CtlFormula f) => new CtlAG(f);

    /// <summary>AF f — f eventually holds on every path.</summary>
    public static CtlFormula AF(CtlFormula f) => new CtlAF(f);

    /// <summary>A[a U b] — along every path, a holds until b becomes true.</summary>
    public static CtlFormula AU(CtlFormula a, CtlFormula b) => new CtlAU(a, b);

    /// <summary>Negation operator.</summary>
    public static CtlFormula operator !(CtlFormula f) => new CtlNot(f);

    /// <summary>Conjunction operator.</summary>
    public static CtlFormula operator &(CtlFormula a, CtlFormula b) => new CtlAnd(a, b);

    /// <summary>Disjunction operator.</summary>
    public static CtlFormula operator |(CtlFormula a, CtlFormula b) => new CtlOr(a, b);

    /// <summary>Parses a CTL formula from text. Throws <see cref="FormatException"/> on malformed input.</summary>
    public static CtlFormula Parse(string source) => CtlParser.Parse(source);

    /// <summary>Attempts to parse a CTL formula, returning false on malformed input.</summary>
    public static bool TryParse(string source, out CtlFormula formula) => CtlParser.TryParse(source, out formula);

    /// <summary>Renders the formula in the textual syntax.</summary>
    public sealed override string ToString() => CtlPrinter.Print(this);
}

/// <summary>A boolean constant.</summary>
public sealed record CtlBool(bool Value) : CtlFormula;

/// <summary>An atomic proposition referenced by name.</summary>
public sealed record CtlAtom(string Name) : CtlFormula;

/// <summary>Logical negation of a CTL formula.</summary>
public sealed record CtlNot(CtlFormula Operand) : CtlFormula;

/// <summary>Logical conjunction <paramref name="Left"/> ∧ <paramref name="Right"/>.</summary>
public sealed record CtlAnd(CtlFormula Left, CtlFormula Right) : CtlFormula;

/// <summary>Logical disjunction <paramref name="Left"/> ∨ <paramref name="Right"/>.</summary>
public sealed record CtlOr(CtlFormula Left, CtlFormula Right) : CtlFormula;

/// <summary>Material implication <paramref name="Antecedent"/> → <paramref name="Consequent"/>.</summary>
public sealed record CtlImplies(CtlFormula Antecedent, CtlFormula Consequent) : CtlFormula;

/// <summary>Biconditional <paramref name="Left"/> ↔ <paramref name="Right"/>.</summary>
public sealed record CtlIff(CtlFormula Left, CtlFormula Right) : CtlFormula;

/// <summary>EX — the operand holds in some successor state.</summary>
public sealed record CtlEX(CtlFormula Operand) : CtlFormula;

/// <summary>EG — there is a path along which the operand holds globally.</summary>
public sealed record CtlEG(CtlFormula Operand) : CtlFormula;

/// <summary>EF — there is a path along which the operand eventually holds.</summary>
public sealed record CtlEF(CtlFormula Operand) : CtlFormula;

/// <summary>E[Left U Right] — along some path, <paramref name="Left"/> holds until <paramref name="Right"/>.</summary>
public sealed record CtlEU(CtlFormula Left, CtlFormula Right) : CtlFormula;

/// <summary>AX — the operand holds in every successor state.</summary>
public sealed record CtlAX(CtlFormula Operand) : CtlFormula;

/// <summary>AG — the operand holds globally on every path.</summary>
public sealed record CtlAG(CtlFormula Operand) : CtlFormula;

/// <summary>AF — the operand eventually holds on every path.</summary>
public sealed record CtlAF(CtlFormula Operand) : CtlFormula;

/// <summary>A[Left U Right] — along every path, <paramref name="Left"/> holds until <paramref name="Right"/>.</summary>
public sealed record CtlAU(CtlFormula Left, CtlFormula Right) : CtlFormula;
