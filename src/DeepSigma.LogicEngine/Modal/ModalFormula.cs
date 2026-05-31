namespace DeepSigma.LogicEngine.Modal;

/// <summary>
/// A propositional modal logic formula with the unary operators <c>□</c>
/// (necessity / "box") and <c>◇</c> (possibility / "diamond") over Kripke
/// frames, combined with the usual boolean connectives. Interpreted relative to
/// a chosen modal system (see <see cref="ModalSystem"/>).
/// </summary>
public abstract record ModalFormula
{
    public static ModalFormula True { get; } = new ModalBool(true);
    public static ModalFormula False { get; } = new ModalBool(false);

    public static ModalFormula Atom(string name) => new ModalAtom(name);
    public static ModalFormula Not(ModalFormula f) => new ModalNot(f);
    public static ModalFormula And(ModalFormula a, ModalFormula b) => new ModalAnd(a, b);
    public static ModalFormula Or(ModalFormula a, ModalFormula b) => new ModalOr(a, b);
    public static ModalFormula Implies(ModalFormula a, ModalFormula b) => new ModalImplies(a, b);
    public static ModalFormula Iff(ModalFormula a, ModalFormula b) => new ModalIff(a, b);
    public static ModalFormula Box(ModalFormula f) => new ModalBox(f);
    public static ModalFormula Diamond(ModalFormula f) => new ModalDiamond(f);

    public static ModalFormula operator !(ModalFormula f) => new ModalNot(f);
    public static ModalFormula operator &(ModalFormula a, ModalFormula b) => new ModalAnd(a, b);
    public static ModalFormula operator |(ModalFormula a, ModalFormula b) => new ModalOr(a, b);

    public static ModalFormula Parse(string source) => ModalParser.Parse(source);
    public static bool TryParse(string source, out ModalFormula formula) => ModalParser.TryParse(source, out formula);

    /// <summary>The atomic proposition names occurring in this formula.</summary>
    public IReadOnlySet<string> Atoms()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sub in Subformulas())
        {
            if (sub is ModalAtom a)
            {
                set.Add(a.Name);
            }
        }
        return set;
    }

    /// <summary>All distinct subformulas (including this one), in a bottom-up order.</summary>
    public IReadOnlyList<ModalFormula> Subformulas()
    {
        var ordered = new List<ModalFormula>();
        var seen = new HashSet<ModalFormula>();
        Collect(this, ordered, seen);
        return ordered;
    }

    private static void Collect(ModalFormula f, List<ModalFormula> ordered, HashSet<ModalFormula> seen)
    {
        switch (f)
        {
            case ModalUnary u: Collect(u.Operand, ordered, seen); break;
            case ModalBinary b: Collect(b.Left, ordered, seen); Collect(b.Right, ordered, seen); break;
        }
        if (seen.Add(f))
        {
            ordered.Add(f);
        }
    }

    public sealed override string ToString() => ModalPrinter.Print(this);
}

public sealed record ModalBool(bool Value) : ModalFormula;
public sealed record ModalAtom(string Name) : ModalFormula;

public abstract record ModalUnary(ModalFormula Operand) : ModalFormula;
public sealed record ModalNot(ModalFormula Operand) : ModalUnary(Operand);
public sealed record ModalBox(ModalFormula Operand) : ModalUnary(Operand);
public sealed record ModalDiamond(ModalFormula Operand) : ModalUnary(Operand);

public abstract record ModalBinary(ModalFormula Left, ModalFormula Right) : ModalFormula;
public sealed record ModalAnd(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
public sealed record ModalOr(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
public sealed record ModalImplies(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
public sealed record ModalIff(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
