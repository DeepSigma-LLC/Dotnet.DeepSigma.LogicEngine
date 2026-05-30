namespace DeepSigma.LogicEngine.Formulas;

/// <summary>
/// Base of the propositional-logic formula AST. Concrete node types are
/// <see cref="BoolConst"/>, <see cref="Variable"/>, <see cref="Negation"/>,
/// <see cref="Conjunction"/>, <see cref="Disjunction"/>, <see cref="Implication"/>,
/// and <see cref="Biconditional"/>.
/// </summary>
public abstract record Formula
{
    public static Formula True { get; } = new BoolConst(true);
    public static Formula False { get; } = new BoolConst(false);

    public static Formula Const(bool value) => value ? True : False;
    public static Formula Var(string name) => new Variable(name);
    public static Formula Not(Formula operand) => new Negation(operand);
    public static Formula And(Formula left, Formula right) => new Conjunction(left, right);
    public static Formula Or(Formula left, Formula right) => new Disjunction(left, right);
    public static Formula Implies(Formula antecedent, Formula consequent) => new Implication(antecedent, consequent);
    public static Formula Iff(Formula left, Formula right) => new Biconditional(left, right);

    /// <summary>Big conjunction over a non-empty sequence; empty sequence yields True.</summary>
    public static Formula All(IEnumerable<Formula> formulas)
        => Balanced(formulas as IReadOnlyList<Formula> ?? formulas.ToList(), isConjunction: true) ?? True;

    /// <summary>Big disjunction over a non-empty sequence; empty sequence yields False.</summary>
    public static Formula Any(IEnumerable<Formula> formulas)
        => Balanced(formulas as IReadOnlyList<Formula> ?? formulas.ToList(), isConjunction: false) ?? False;

    /// <summary>
    /// Fold the list into a balanced (depth O(log n)) binary tree of conjunctions or
    /// disjunctions. Balancing keeps the recursive normal-form transforms from
    /// overflowing the stack on very large clause sets. Returns null if empty.
    /// </summary>
    private static Formula? Balanced(IReadOnlyList<Formula> formulas, bool isConjunction)
    {
        if (formulas.Count == 0)
        {
            return null;
        }
        Formula Fold(int lo, int hi)
        {
            if (hi - lo == 1)
            {
                return formulas[lo];
            }
            var mid = lo + (hi - lo) / 2;
            var left = Fold(lo, mid);
            var right = Fold(mid, hi);
            return isConjunction ? new Conjunction(left, right) : new Disjunction(left, right);
        }
        return Fold(0, formulas.Count);
    }

    public static Formula Parse(string source) => Parsing.Parser.Parse(source);
    public static bool TryParse(string source, out Formula formula) => Parsing.Parser.TryParse(source, out formula);

    public static Formula operator !(Formula f) => new Negation(f);
    public static Formula operator &(Formula a, Formula b) => new Conjunction(a, b);
    public static Formula operator |(Formula a, Formula b) => new Disjunction(a, b);

    public sealed override string ToString() => Printing.Printer.Print(this);
}

public sealed record BoolConst(bool Value) : Formula;

public sealed record Variable(string Name) : Formula;

public sealed record Negation(Formula Operand) : Formula;

public sealed record Conjunction(Formula Left, Formula Right) : Formula;

public sealed record Disjunction(Formula Left, Formula Right) : Formula;

public sealed record Implication(Formula Antecedent, Formula Consequent) : Formula;

public sealed record Biconditional(Formula Left, Formula Right) : Formula;
