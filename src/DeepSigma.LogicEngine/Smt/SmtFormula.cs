namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A quantifier-free formula over EUF theory atoms (equalities and uninterpreted
/// predicates) combined with the usual boolean connectives. Kept separate from
/// the propositional <see cref="Formulas.Formula"/>: the SMT layer abstracts an
/// <see cref="SmtFormula"/> into a propositional skeleton for the SAT solver.
/// </summary>
public abstract record SmtFormula
{
    public static SmtFormula True { get; } = new SmtBool(true);
    public static SmtFormula False { get; } = new SmtBool(false);

    public static SmtFormula Eq(Term left, Term right) => new EqualityAtom(left, right);
    public static SmtFormula Distinct(Term left, Term right) => new SmtNot(new EqualityAtom(left, right));
    public static SmtFormula Pred(string symbol, params Term[] arguments) => new PredicateAtom(symbol, arguments);
    public static SmtFormula Not(SmtFormula operand) => new SmtNot(operand);
    public static SmtFormula And(SmtFormula left, SmtFormula right) => new SmtAnd(left, right);
    public static SmtFormula Or(SmtFormula left, SmtFormula right) => new SmtOr(left, right);
    public static SmtFormula Implies(SmtFormula antecedent, SmtFormula consequent) => new SmtImplies(antecedent, consequent);
    public static SmtFormula Iff(SmtFormula left, SmtFormula right) => new SmtIff(left, right);

    /// <summary>Big conjunction over a sequence; empty yields True. Built as a balanced tree (depth O(log n)).</summary>
    public static SmtFormula All(IEnumerable<SmtFormula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<SmtFormula> ?? formulas.ToList(), static (a, b) => new SmtAnd(a, b)) ?? True;

    /// <summary>Big disjunction over a sequence; empty yields False. Built as a balanced tree (depth O(log n)).</summary>
    public static SmtFormula Any(IEnumerable<SmtFormula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<SmtFormula> ?? formulas.ToList(), static (a, b) => new SmtOr(a, b)) ?? False;

    public static SmtFormula operator !(SmtFormula f) => new SmtNot(f);
    public static SmtFormula operator &(SmtFormula a, SmtFormula b) => new SmtAnd(a, b);
    public static SmtFormula operator |(SmtFormula a, SmtFormula b) => new SmtOr(a, b);

    public static SmtFormula Parse(string source) => SmtParser.Parse(source);
    public static bool TryParse(string source, out SmtFormula formula) => SmtParser.TryParse(source, out formula);

    public sealed override string ToString() => SmtPrinter.Print(this);
}

public sealed record SmtBool(bool Value) : SmtFormula;

public sealed record EqualityAtom(Term Left, Term Right) : SmtFormula;

public sealed record PredicateAtom(string Symbol, IReadOnlyList<Term> Arguments) : SmtFormula
{
    public bool Equals(PredicateAtom? other)
        => other is not null && string.Equals(Symbol, other.Symbol, StringComparison.Ordinal)
            && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);
}

public sealed record SmtNot(SmtFormula Operand) : SmtFormula;

public sealed record SmtAnd(SmtFormula Left, SmtFormula Right) : SmtFormula;

public sealed record SmtOr(SmtFormula Left, SmtFormula Right) : SmtFormula;

public sealed record SmtImplies(SmtFormula Antecedent, SmtFormula Consequent) : SmtFormula;

public sealed record SmtIff(SmtFormula Left, SmtFormula Right) : SmtFormula;
