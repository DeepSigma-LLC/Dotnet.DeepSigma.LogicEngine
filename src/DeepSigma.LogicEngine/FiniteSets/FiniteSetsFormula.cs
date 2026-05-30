namespace DeepSigma.LogicEngine.FiniteSets;

/// <summary>
/// A <b>set expression</b> denoting a subset of the (finite) universe: a set
/// variable, the constants ∅ and U, or a combination via complement, union,
/// intersection, difference, and symmetric difference.
/// </summary>
public abstract record SetExpr
{
    public static SetExpr Var(string name) => new SetVar(name);
    public static SetExpr Empty { get; } = new SetConst(false);
    public static SetExpr Universe { get; } = new SetConst(true);
    public static SetExpr Complement(SetExpr s) => new SetCompl(s);
    public static SetExpr Union(SetExpr a, SetExpr b) => new SetUnion(a, b);
    public static SetExpr Intersect(SetExpr a, SetExpr b) => new SetInter(a, b);
    public static SetExpr Difference(SetExpr a, SetExpr b) => new SetDiff(a, b);
    public static SetExpr SymmetricDifference(SetExpr a, SetExpr b) => new SetSymDiff(a, b);

    public static SetExpr operator |(SetExpr a, SetExpr b) => new SetUnion(a, b);
    public static SetExpr operator &(SetExpr a, SetExpr b) => new SetInter(a, b);
    public static SetExpr operator -(SetExpr a, SetExpr b) => new SetDiff(a, b);
    public static SetExpr operator ^(SetExpr a, SetExpr b) => new SetSymDiff(a, b);
    public static SetExpr operator ~(SetExpr a) => new SetCompl(a);

    // Fluent relation builders.
    public SetFormula SubsetOf(SetExpr other) => new SubsetRel(this, other, Proper: false);
    public SetFormula ProperSubsetOf(SetExpr other) => new SubsetRel(this, other, Proper: true);
    public SetFormula EqualTo(SetExpr other) => new EqualRel(this, other);
    public SetFormula Cardinality(CardOp op, int bound) => new CardRel(this, op, bound);

    public sealed override string ToString() => FiniteSetsPrinter.Print(this);
}

public sealed record SetVar(string Name) : SetExpr;
public sealed record SetConst(bool Full) : SetExpr;            // false → ∅, true → U
public sealed record SetCompl(SetExpr Operand) : SetExpr;
public sealed record SetUnion(SetExpr Left, SetExpr Right) : SetExpr;
public sealed record SetInter(SetExpr Left, SetExpr Right) : SetExpr;
public sealed record SetDiff(SetExpr Left, SetExpr Right) : SetExpr;
public sealed record SetSymDiff(SetExpr Left, SetExpr Right) : SetExpr;

/// <summary>An <b>element expression</b>: a named element variable ranging over the universe.</summary>
public abstract record ElementExpr
{
    public static ElementExpr Var(string name) => new ElementVar(name);
    public SetFormula In(SetExpr set) => new MemberRel(this, set);
    public sealed override string ToString() => FiniteSetsPrinter.Print(this);
}

public sealed record ElementVar(string Name) : ElementExpr;

/// <summary>Comparison operator for a cardinality constraint <c>|S| ⋈ k</c>.</summary>
public enum CardOp
{
    Eq,
    Le,
    Lt,
    Ge,
    Gt,
}

/// <summary>
/// A <b>finite-set formula</b>: a Boolean combination of atomic set relations —
/// membership (∈), subset (⊆/⊂), equality (=), disjointness, and cardinality
/// bounds (|S| ⋈ k).
/// </summary>
public abstract record SetFormula
{
    public static SetFormula Member(ElementExpr element, SetExpr set) => new MemberRel(element, set);
    public static SetFormula Subset(SetExpr a, SetExpr b) => new SubsetRel(a, b, Proper: false);
    public static SetFormula ProperSubset(SetExpr a, SetExpr b) => new SubsetRel(a, b, Proper: true);
    public static SetFormula Equal(SetExpr a, SetExpr b) => new EqualRel(a, b);
    public static SetFormula Disjoint(SetExpr a, SetExpr b) => new DisjointRel(a, b);
    public static SetFormula Card(SetExpr s, CardOp op, int bound) => new CardRel(s, op, bound);

    public static SetFormula Not(SetFormula f) => new SetNot(f);
    public static SetFormula And(SetFormula a, SetFormula b) => new SetAnd(a, b);
    public static SetFormula Or(SetFormula a, SetFormula b) => new SetOr(a, b);
    public static SetFormula Implies(SetFormula a, SetFormula b) => new SetImplies(a, b);
    public static SetFormula Iff(SetFormula a, SetFormula b) => new SetIff(a, b);

    public static SetFormula operator !(SetFormula f) => new SetNot(f);
    public static SetFormula operator &(SetFormula a, SetFormula b) => new SetAnd(a, b);
    public static SetFormula operator |(SetFormula a, SetFormula b) => new SetOr(a, b);

    public static SetFormula Parse(string source) => FiniteSetsParser.Parse(source);

    public sealed override string ToString() => FiniteSetsPrinter.Print(this);
}

public sealed record MemberRel(ElementExpr Element, SetExpr Set) : SetFormula;
public sealed record SubsetRel(SetExpr Left, SetExpr Right, bool Proper) : SetFormula;
public sealed record EqualRel(SetExpr Left, SetExpr Right) : SetFormula;
public sealed record DisjointRel(SetExpr Left, SetExpr Right) : SetFormula;
public sealed record CardRel(SetExpr Set, CardOp Op, int Bound) : SetFormula;
public sealed record SetNot(SetFormula Operand) : SetFormula;
public sealed record SetAnd(SetFormula Left, SetFormula Right) : SetFormula;
public sealed record SetOr(SetFormula Left, SetFormula Right) : SetFormula;
public sealed record SetImplies(SetFormula Left, SetFormula Right) : SetFormula;
public sealed record SetIff(SetFormula Left, SetFormula Right) : SetFormula;
