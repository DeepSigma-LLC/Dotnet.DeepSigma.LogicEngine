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

    public static SmtFormula All(IEnumerable<SmtFormula> formulas)
    {
        SmtFormula? acc = null;
        foreach (var f in formulas)
        {
            acc = acc is null ? f : new SmtAnd(acc, f);
        }
        return acc ?? True;
    }

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
    {
        if (other is null || !string.Equals(Symbol, other.Symbol, StringComparison.Ordinal))
        {
            return false;
        }
        if (Arguments.Count != other.Arguments.Count)
        {
            return false;
        }
        for (var i = 0; i < Arguments.Count; i++)
        {
            if (!Arguments[i].Equals(other.Arguments[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Symbol, StringComparer.Ordinal);
        foreach (var argument in Arguments)
        {
            hash.Add(argument);
        }
        return hash.ToHashCode();
    }
}

public sealed record SmtNot(SmtFormula Operand) : SmtFormula;

public sealed record SmtAnd(SmtFormula Left, SmtFormula Right) : SmtFormula;

public sealed record SmtOr(SmtFormula Left, SmtFormula Right) : SmtFormula;

public sealed record SmtImplies(SmtFormula Antecedent, SmtFormula Consequent) : SmtFormula;

public sealed record SmtIff(SmtFormula Left, SmtFormula Right) : SmtFormula;
