namespace DeepSigma.LogicEngine.FiniteSets;

/// <summary>
/// A <b>set expression</b> denoting a subset of the (finite) universe: a set
/// variable, the constants ∅ and U, or a combination via complement, union,
/// intersection, difference, and symmetric difference.
/// </summary>
public abstract record SetExpr
{
    /// <summary>A set variable with the given name.</summary>
    public static SetExpr Var(string name) => new SetVar(name);

    /// <summary>The empty set ∅.</summary>
    public static SetExpr Empty { get; } = new SetConst(false);

    /// <summary>The universe U (every element).</summary>
    public static SetExpr Universe { get; } = new SetConst(true);

    /// <summary>The complement of <paramref name="s"/> (U \ s).</summary>
    public static SetExpr Complement(SetExpr s) => new SetCompl(s);

    /// <summary>The union a ∪ b.</summary>
    public static SetExpr Union(SetExpr a, SetExpr b) => new SetUnion(a, b);

    /// <summary>The intersection a ∩ b.</summary>
    public static SetExpr Intersect(SetExpr a, SetExpr b) => new SetInter(a, b);

    /// <summary>The difference a \ b.</summary>
    public static SetExpr Difference(SetExpr a, SetExpr b) => new SetDiff(a, b);

    /// <summary>The symmetric difference a Δ b.</summary>
    public static SetExpr SymmetricDifference(SetExpr a, SetExpr b) => new SetSymDiff(a, b);

    /// <summary>Union operator: a ∪ b.</summary>
    public static SetExpr operator |(SetExpr a, SetExpr b) => new SetUnion(a, b);

    /// <summary>Intersection operator: a ∩ b.</summary>
    public static SetExpr operator &(SetExpr a, SetExpr b) => new SetInter(a, b);

    /// <summary>Difference operator: a \ b.</summary>
    public static SetExpr operator -(SetExpr a, SetExpr b) => new SetDiff(a, b);

    /// <summary>Symmetric-difference operator: a Δ b.</summary>
    public static SetExpr operator ^(SetExpr a, SetExpr b) => new SetSymDiff(a, b);

    /// <summary>Complement operator: U \ a.</summary>
    public static SetExpr operator ~(SetExpr a) => new SetCompl(a);

    /// <summary>The relation <c>this ⊆ <paramref name="other"/></c>.</summary>
    public SetFormula SubsetOf(SetExpr other) => new SubsetRel(this, other, Proper: false);

    /// <summary>The relation <c>this ⊂ <paramref name="other"/></c> (proper subset).</summary>
    public SetFormula ProperSubsetOf(SetExpr other) => new SubsetRel(this, other, Proper: true);

    /// <summary>The relation <c>this = <paramref name="other"/></c>.</summary>
    public SetFormula EqualTo(SetExpr other) => new EqualRel(this, other);

    /// <summary>The cardinality constraint <c>|this| <paramref name="op"/> <paramref name="bound"/></c>.</summary>
    public SetFormula Cardinality(CardOp op, int bound) => new CardRel(this, op, bound);

    /// <summary>Renders the set expression in the textual syntax.</summary>
    public sealed override string ToString() => FiniteSetsPrinter.Print(this);
}

/// <summary>A set variable referenced by name.</summary>
public sealed record SetVar(string Name) : SetExpr;

/// <summary>A constant set: ∅ when <paramref name="Full"/> is false, the universe U when true.</summary>
public sealed record SetConst(bool Full) : SetExpr;

/// <summary>The complement U \ <paramref name="Operand"/>.</summary>
public sealed record SetCompl(SetExpr Operand) : SetExpr;

/// <summary>The union <paramref name="Left"/> ∪ <paramref name="Right"/>.</summary>
public sealed record SetUnion(SetExpr Left, SetExpr Right) : SetExpr;

/// <summary>The intersection <paramref name="Left"/> ∩ <paramref name="Right"/>.</summary>
public sealed record SetInter(SetExpr Left, SetExpr Right) : SetExpr;

/// <summary>The difference <paramref name="Left"/> \ <paramref name="Right"/>.</summary>
public sealed record SetDiff(SetExpr Left, SetExpr Right) : SetExpr;

/// <summary>The symmetric difference <paramref name="Left"/> Δ <paramref name="Right"/>.</summary>
public sealed record SetSymDiff(SetExpr Left, SetExpr Right) : SetExpr;

/// <summary>An <b>element expression</b>: a named element variable ranging over the universe.</summary>
public abstract record ElementExpr
{
    /// <summary>An element variable with the given name.</summary>
    public static ElementExpr Var(string name) => new ElementVar(name);

    /// <summary>The membership relation <c>this ∈ <paramref name="set"/></c>.</summary>
    public SetFormula In(SetExpr set) => new MemberRel(this, set);

    /// <summary>Renders the element expression in the textual syntax.</summary>
    public sealed override string ToString() => FiniteSetsPrinter.Print(this);
}

/// <summary>An element variable referenced by name.</summary>
public sealed record ElementVar(string Name) : ElementExpr;

/// <summary>Comparison operator for a cardinality constraint <c>|S| ⋈ k</c>.</summary>
public enum CardOp
{
    /// <summary><c>|S| = k</c>.</summary>
    Equal,

    /// <summary><c>|S| &lt;= k</c>.</summary>
    LessOrEqual,

    /// <summary><c>|S| &lt; k</c>.</summary>
    Less,

    /// <summary><c>|S| &gt;= k</c>.</summary>
    GreaterOrEqual,

    /// <summary><c>|S| &gt; k</c>.</summary>
    Greater,
}

/// <summary>
/// A <b>finite-set formula</b>: a Boolean combination of atomic set relations —
/// membership (∈), subset (⊆/⊂), equality (=), disjointness, and cardinality
/// bounds (|S| ⋈ k).
/// </summary>
public abstract record SetFormula
{
    /// <summary>The membership relation <paramref name="element"/> ∈ <paramref name="set"/>.</summary>
    public static SetFormula Member(ElementExpr element, SetExpr set) => new MemberRel(element, set);

    /// <summary>The subset relation a ⊆ b.</summary>
    public static SetFormula Subset(SetExpr a, SetExpr b) => new SubsetRel(a, b, Proper: false);

    /// <summary>The proper-subset relation a ⊂ b.</summary>
    public static SetFormula ProperSubset(SetExpr a, SetExpr b) => new SubsetRel(a, b, Proper: true);

    /// <summary>The equality relation a = b.</summary>
    public static SetFormula Equal(SetExpr a, SetExpr b) => new EqualRel(a, b);

    /// <summary>The disjointness relation a ∩ b = ∅.</summary>
    public static SetFormula Disjoint(SetExpr a, SetExpr b) => new DisjointRel(a, b);

    /// <summary>The cardinality constraint <c>|s| <paramref name="op"/> <paramref name="bound"/></c>.</summary>
    public static SetFormula Card(SetExpr s, CardOp op, int bound) => new CardRel(s, op, bound);

    /// <summary>Logical negation.</summary>
    public static SetFormula Not(SetFormula f) => new SetNot(f);

    /// <summary>Logical conjunction a ∧ b.</summary>
    public static SetFormula And(SetFormula a, SetFormula b) => new SetAnd(a, b);

    /// <summary>Logical disjunction a ∨ b.</summary>
    public static SetFormula Or(SetFormula a, SetFormula b) => new SetOr(a, b);

    /// <summary>Material implication a → b.</summary>
    public static SetFormula Implies(SetFormula a, SetFormula b) => new SetImplies(a, b);

    /// <summary>Biconditional a ↔ b.</summary>
    public static SetFormula Iff(SetFormula a, SetFormula b) => new SetIff(a, b);

    /// <summary>Negation operator.</summary>
    public static SetFormula operator !(SetFormula f) => new SetNot(f);

    /// <summary>Conjunction operator.</summary>
    public static SetFormula operator &(SetFormula a, SetFormula b) => new SetAnd(a, b);

    /// <summary>Disjunction operator.</summary>
    public static SetFormula operator |(SetFormula a, SetFormula b) => new SetOr(a, b);

    /// <summary>Parses a finite-set formula from text. Throws <see cref="FormatException"/> on malformed input.</summary>
    public static SetFormula Parse(string source) => FiniteSetsParser.Parse(source);

    /// <summary>Attempts to parse a finite-set formula, returning false on malformed input.</summary>
    public static bool TryParse(string source, out SetFormula formula) => FiniteSetsParser.TryParse(source, out formula);

    /// <summary>Renders the formula in the textual syntax.</summary>
    public sealed override string ToString() => FiniteSetsPrinter.Print(this);
}

/// <summary>The membership relation <paramref name="Element"/> ∈ <paramref name="Set"/>.</summary>
public sealed record MemberRel(ElementExpr Element, SetExpr Set) : SetFormula;

/// <summary>The subset relation <paramref name="Left"/> ⊆ <paramref name="Right"/> (⊂ when <paramref name="Proper"/> is true).</summary>
public sealed record SubsetRel(SetExpr Left, SetExpr Right, bool Proper) : SetFormula;

/// <summary>The equality relation <paramref name="Left"/> = <paramref name="Right"/>.</summary>
public sealed record EqualRel(SetExpr Left, SetExpr Right) : SetFormula;

/// <summary>The disjointness relation <paramref name="Left"/> ∩ <paramref name="Right"/> = ∅.</summary>
public sealed record DisjointRel(SetExpr Left, SetExpr Right) : SetFormula;

/// <summary>A cardinality constraint <c>|Set| Op Bound</c> (e.g. <c>|A| &lt;= 3</c>).</summary>
/// <param name="Set">The set whose cardinality is constrained.</param>
/// <param name="Op">The comparison operator applied to the cardinality.</param>
/// <param name="Bound">The integer the cardinality is compared against (its role depends on <paramref name="Op"/>).</param>
public sealed record CardRel(SetExpr Set, CardOp Op, int Bound) : SetFormula;

/// <summary>Logical negation of a set formula.</summary>
public sealed record SetNot(SetFormula Operand) : SetFormula;

/// <summary>Logical conjunction <paramref name="Left"/> ∧ <paramref name="Right"/>.</summary>
public sealed record SetAnd(SetFormula Left, SetFormula Right) : SetFormula;

/// <summary>Logical disjunction <paramref name="Left"/> ∨ <paramref name="Right"/>.</summary>
public sealed record SetOr(SetFormula Left, SetFormula Right) : SetFormula;

/// <summary>Material implication <paramref name="Left"/> → <paramref name="Right"/>.</summary>
public sealed record SetImplies(SetFormula Left, SetFormula Right) : SetFormula;

/// <summary>Biconditional <paramref name="Left"/> ↔ <paramref name="Right"/>.</summary>
public sealed record SetIff(SetFormula Left, SetFormula Right) : SetFormula;
