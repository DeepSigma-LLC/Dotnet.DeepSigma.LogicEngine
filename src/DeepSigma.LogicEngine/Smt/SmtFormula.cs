namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A quantifier-free formula over EUF theory atoms (equalities and uninterpreted
/// predicates) combined with the usual boolean connectives. Kept separate from
/// the propositional <see cref="Formulas.Formula"/>: the SMT layer abstracts an
/// <see cref="SmtFormula"/> into a propositional skeleton for the SAT solver.
/// </summary>
public abstract record SmtFormula
{
    /// <summary>The boolean constant true.</summary>
    public static SmtFormula True { get; } = new SmtBool(true);
    /// <summary>The boolean constant false.</summary>
    public static SmtFormula False { get; } = new SmtBool(false);

    /// <summary>Builds an equality atom <paramref name="left"/> = <paramref name="right"/>.</summary>
    public static SmtFormula Eq(Term left, Term right) => new EqualityAtom(left, right);
    /// <summary>Builds a disequality ¬(<paramref name="left"/> = <paramref name="right"/>).</summary>
    public static SmtFormula Distinct(Term left, Term right) => new SmtNot(new EqualityAtom(left, right));
    /// <summary>Builds an uninterpreted predicate atom applying <paramref name="symbol"/> to <paramref name="arguments"/>.</summary>
    public static SmtFormula Pred(string symbol, params Term[] arguments) => new PredicateAtom(symbol, arguments);
    /// <summary>Builds the negation ¬<paramref name="operand"/>.</summary>
    public static SmtFormula Not(SmtFormula operand) => new SmtNot(operand);
    /// <summary>Builds the conjunction <paramref name="left"/> ∧ <paramref name="right"/>.</summary>
    public static SmtFormula And(SmtFormula left, SmtFormula right) => new SmtAnd(left, right);
    /// <summary>Builds the disjunction <paramref name="left"/> ∨ <paramref name="right"/>.</summary>
    public static SmtFormula Or(SmtFormula left, SmtFormula right) => new SmtOr(left, right);
    /// <summary>Builds the implication <paramref name="antecedent"/> → <paramref name="consequent"/>.</summary>
    public static SmtFormula Implies(SmtFormula antecedent, SmtFormula consequent) => new SmtImplies(antecedent, consequent);
    /// <summary>Builds the biconditional <paramref name="left"/> ↔ <paramref name="right"/>.</summary>
    public static SmtFormula Iff(SmtFormula left, SmtFormula right) => new SmtIff(left, right);

    /// <summary>Big conjunction over a sequence; empty yields True. Built as a balanced tree (depth O(log n)).</summary>
    public static SmtFormula All(IEnumerable<SmtFormula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<SmtFormula> ?? formulas.ToList(), static (a, b) => new SmtAnd(a, b)) ?? True;

    /// <summary>Big disjunction over a sequence; empty yields False. Built as a balanced tree (depth O(log n)).</summary>
    public static SmtFormula Any(IEnumerable<SmtFormula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<SmtFormula> ?? formulas.ToList(), static (a, b) => new SmtOr(a, b)) ?? False;

    /// <summary>Operator form of <see cref="Not(SmtFormula)"/>: ¬<paramref name="f"/>.</summary>
    public static SmtFormula operator !(SmtFormula f) => new SmtNot(f);
    /// <summary>Operator form of <see cref="And(SmtFormula, SmtFormula)"/>: <paramref name="a"/> ∧ <paramref name="b"/>.</summary>
    public static SmtFormula operator &(SmtFormula a, SmtFormula b) => new SmtAnd(a, b);
    /// <summary>Operator form of <see cref="Or(SmtFormula, SmtFormula)"/>: <paramref name="a"/> ∨ <paramref name="b"/>.</summary>
    public static SmtFormula operator |(SmtFormula a, SmtFormula b) => new SmtOr(a, b);

    /// <summary>Parses a formula from its textual syntax; throws <see cref="FormatException"/> on malformed input.</summary>
    public static SmtFormula Parse(string source) => SmtParser.Parse(source);
    /// <summary>Attempts to parse a formula from its textual syntax; returns false on malformed input.</summary>
    public static bool TryParse(string source, out SmtFormula formula) => SmtParser.TryParse(source, out formula);

    /// <summary>Renders the formula in the textual syntax.</summary>
    public sealed override string ToString() => SmtPrinter.Print(this);
}

/// <summary>Boolean constant <paramref name="Value"/>.</summary>
public sealed record SmtBool(bool Value) : SmtFormula;

/// <summary>Equality atom <paramref name="Left"/> = <paramref name="Right"/>.</summary>
public sealed record EqualityAtom(Term Left, Term Right) : SmtFormula;

/// <summary>Uninterpreted predicate atom: <paramref name="Symbol"/> applied to <paramref name="Arguments"/>.</summary>
public sealed record PredicateAtom(string Symbol, IReadOnlyList<Term> Arguments) : SmtFormula
{
    /// <summary>Structural equality over the symbol and argument list.</summary>
    public bool Equals(PredicateAtom? other)
        => other is not null && string.Equals(Symbol, other.Symbol, StringComparison.Ordinal)
            && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    /// <summary>Structural hash over the symbol and argument list.</summary>
    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);
}

/// <summary>Logical negation ¬<paramref name="Operand"/>.</summary>
public sealed record SmtNot(SmtFormula Operand) : SmtFormula;

/// <summary>Logical conjunction <paramref name="Left"/> ∧ <paramref name="Right"/>.</summary>
public sealed record SmtAnd(SmtFormula Left, SmtFormula Right) : SmtFormula;

/// <summary>Logical disjunction <paramref name="Left"/> ∨ <paramref name="Right"/>.</summary>
public sealed record SmtOr(SmtFormula Left, SmtFormula Right) : SmtFormula;

/// <summary>Logical implication <paramref name="Antecedent"/> → <paramref name="Consequent"/>.</summary>
public sealed record SmtImplies(SmtFormula Antecedent, SmtFormula Consequent) : SmtFormula;

/// <summary>Logical biconditional <paramref name="Left"/> ↔ <paramref name="Right"/>.</summary>
public sealed record SmtIff(SmtFormula Left, SmtFormula Right) : SmtFormula;
