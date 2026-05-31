namespace DeepSigma.LogicEngine.Ctl;

/// <summary>
/// A Computation Tree Logic (CTL) formula: propositional atoms and connectives plus
/// the path-quantified temporal operators. <c>E</c> means "along some path", <c>A</c>
/// "along every path"; <c>X</c> next, <c>F</c> eventually, <c>G</c> globally, <c>U</c>
/// until. <c>EX</c>, <c>EG</c>, and <c>EU</c> are primitive; the rest are derived.
/// </summary>
public abstract record CtlFormula
{
    public static CtlFormula True { get; } = new CtlBool(true);
    public static CtlFormula False { get; } = new CtlBool(false);

    public static CtlFormula Atom(string name) => new CtlAtom(name);
    public static CtlFormula Not(CtlFormula f) => new CtlNot(f);
    public static CtlFormula And(CtlFormula a, CtlFormula b) => new CtlAnd(a, b);
    public static CtlFormula Or(CtlFormula a, CtlFormula b) => new CtlOr(a, b);
    public static CtlFormula Implies(CtlFormula a, CtlFormula b) => new CtlImplies(a, b);
    public static CtlFormula Iff(CtlFormula a, CtlFormula b) => new CtlIff(a, b);

    public static CtlFormula EX(CtlFormula f) => new CtlEX(f);
    public static CtlFormula EG(CtlFormula f) => new CtlEG(f);
    public static CtlFormula EF(CtlFormula f) => new CtlEF(f);
    public static CtlFormula EU(CtlFormula a, CtlFormula b) => new CtlEU(a, b);
    public static CtlFormula AX(CtlFormula f) => new CtlAX(f);
    public static CtlFormula AG(CtlFormula f) => new CtlAG(f);
    public static CtlFormula AF(CtlFormula f) => new CtlAF(f);
    public static CtlFormula AU(CtlFormula a, CtlFormula b) => new CtlAU(a, b);

    public static CtlFormula operator !(CtlFormula f) => new CtlNot(f);
    public static CtlFormula operator &(CtlFormula a, CtlFormula b) => new CtlAnd(a, b);
    public static CtlFormula operator |(CtlFormula a, CtlFormula b) => new CtlOr(a, b);

    public static CtlFormula Parse(string source) => CtlParser.Parse(source);
    public static bool TryParse(string source, out CtlFormula formula) => CtlParser.TryParse(source, out formula);

    public sealed override string ToString() => CtlPrinter.Print(this);
}

public sealed record CtlBool(bool Value) : CtlFormula;
public sealed record CtlAtom(string Name) : CtlFormula;
public sealed record CtlNot(CtlFormula Operand) : CtlFormula;
public sealed record CtlAnd(CtlFormula Left, CtlFormula Right) : CtlFormula;
public sealed record CtlOr(CtlFormula Left, CtlFormula Right) : CtlFormula;
public sealed record CtlImplies(CtlFormula Antecedent, CtlFormula Consequent) : CtlFormula;
public sealed record CtlIff(CtlFormula Left, CtlFormula Right) : CtlFormula;
public sealed record CtlEX(CtlFormula Operand) : CtlFormula;
public sealed record CtlEG(CtlFormula Operand) : CtlFormula;
public sealed record CtlEF(CtlFormula Operand) : CtlFormula;
public sealed record CtlEU(CtlFormula Left, CtlFormula Right) : CtlFormula;
public sealed record CtlAX(CtlFormula Operand) : CtlFormula;
public sealed record CtlAG(CtlFormula Operand) : CtlFormula;
public sealed record CtlAF(CtlFormula Operand) : CtlFormula;
public sealed record CtlAU(CtlFormula Left, CtlFormula Right) : CtlFormula;
