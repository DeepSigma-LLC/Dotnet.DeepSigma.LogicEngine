namespace DeepSigma.LogicEngine.Modal;

/// <summary>
/// A propositional modal logic formula with the unary operators <c>□</c>
/// (necessity / "box") and <c>◇</c> (possibility / "diamond") over Kripke
/// frames, combined with the usual boolean connectives. Interpreted relative to
/// a chosen modal system (see <see cref="ModalSystem"/>).
/// </summary>
public abstract record ModalFormula
{
    /// <summary>The boolean constant true.</summary>
    public static ModalFormula True { get; } = new ModalBool(true);
    /// <summary>The boolean constant false.</summary>
    public static ModalFormula False { get; } = new ModalBool(false);

    /// <summary>Builds an atomic proposition named <paramref name="name"/>.</summary>
    public static ModalFormula Atom(string name) => new ModalAtom(name);
    /// <summary>Builds the negation ¬<paramref name="f"/>.</summary>
    public static ModalFormula Not(ModalFormula f) => new ModalNot(f);
    /// <summary>Builds the conjunction <paramref name="a"/> ∧ <paramref name="b"/>.</summary>
    public static ModalFormula And(ModalFormula a, ModalFormula b) => new ModalAnd(a, b);
    /// <summary>Builds the disjunction <paramref name="a"/> ∨ <paramref name="b"/>.</summary>
    public static ModalFormula Or(ModalFormula a, ModalFormula b) => new ModalOr(a, b);
    /// <summary>Builds the implication <paramref name="a"/> → <paramref name="b"/>.</summary>
    public static ModalFormula Implies(ModalFormula a, ModalFormula b) => new ModalImplies(a, b);
    /// <summary>Builds the biconditional <paramref name="a"/> ↔ <paramref name="b"/>.</summary>
    public static ModalFormula Iff(ModalFormula a, ModalFormula b) => new ModalIff(a, b);
    /// <summary>Builds the necessity □<paramref name="f"/> ("in every accessible world, <paramref name="f"/>").</summary>
    public static ModalFormula Box(ModalFormula f) => new ModalBox(f);
    /// <summary>Builds the possibility ◇<paramref name="f"/> ("in some accessible world, <paramref name="f"/>").</summary>
    public static ModalFormula Diamond(ModalFormula f) => new ModalDiamond(f);

    /// <summary>Operator form of <see cref="Not(ModalFormula)"/>: ¬<paramref name="f"/>.</summary>
    public static ModalFormula operator !(ModalFormula f) => new ModalNot(f);
    /// <summary>Operator form of <see cref="And(ModalFormula, ModalFormula)"/>: <paramref name="a"/> ∧ <paramref name="b"/>.</summary>
    public static ModalFormula operator &(ModalFormula a, ModalFormula b) => new ModalAnd(a, b);
    /// <summary>Operator form of <see cref="Or(ModalFormula, ModalFormula)"/>: <paramref name="a"/> ∨ <paramref name="b"/>.</summary>
    public static ModalFormula operator |(ModalFormula a, ModalFormula b) => new ModalOr(a, b);

    /// <summary>Parses a formula from its textual syntax; throws <see cref="FormatException"/> on malformed input.</summary>
    public static ModalFormula Parse(string source) => ModalParser.Parse(source);
    /// <summary>Attempts to parse a formula from its textual syntax; returns false on malformed input.</summary>
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

    /// <summary>Renders the formula in the textual syntax.</summary>
    public sealed override string ToString() => ModalPrinter.Print(this);
}

/// <summary>Boolean constant <paramref name="Value"/>.</summary>
public sealed record ModalBool(bool Value) : ModalFormula;
/// <summary>Atomic proposition named <paramref name="Name"/>.</summary>
public sealed record ModalAtom(string Name) : ModalFormula;

/// <summary>Base type for unary modal formulas applying to a single <paramref name="Operand"/>.</summary>
public abstract record ModalUnary(ModalFormula Operand) : ModalFormula;
/// <summary>Logical negation ¬<paramref name="Operand"/>.</summary>
public sealed record ModalNot(ModalFormula Operand) : ModalUnary(Operand);
/// <summary>Necessity □<paramref name="Operand"/>: <paramref name="Operand"/> holds in every accessible world.</summary>
public sealed record ModalBox(ModalFormula Operand) : ModalUnary(Operand);
/// <summary>Possibility ◇<paramref name="Operand"/>: <paramref name="Operand"/> holds in some accessible world.</summary>
public sealed record ModalDiamond(ModalFormula Operand) : ModalUnary(Operand);

/// <summary>Base type for binary modal formulas combining <paramref name="Left"/> and <paramref name="Right"/>.</summary>
public abstract record ModalBinary(ModalFormula Left, ModalFormula Right) : ModalFormula;
/// <summary>Logical conjunction <paramref name="Left"/> ∧ <paramref name="Right"/>.</summary>
public sealed record ModalAnd(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
/// <summary>Logical disjunction <paramref name="Left"/> ∨ <paramref name="Right"/>.</summary>
public sealed record ModalOr(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
/// <summary>Logical implication <paramref name="Left"/> → <paramref name="Right"/>.</summary>
public sealed record ModalImplies(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
/// <summary>Logical biconditional <paramref name="Left"/> ↔ <paramref name="Right"/>.</summary>
public sealed record ModalIff(ModalFormula Left, ModalFormula Right) : ModalBinary(Left, Right);
