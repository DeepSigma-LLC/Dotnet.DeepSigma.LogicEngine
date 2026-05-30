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

    /// <summary>Big conjunction over a sequence; empty sequence yields True. Built as a balanced tree.</summary>
    public static Formula All(IEnumerable<Formula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<Formula> ?? formulas.ToList(), static (a, b) => new Conjunction(a, b)) ?? True;

    /// <summary>Big disjunction over a sequence; empty sequence yields False. Built as a balanced tree.</summary>
    public static Formula Any(IEnumerable<Formula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<Formula> ?? formulas.ToList(), static (a, b) => new Disjunction(a, b)) ?? False;

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
