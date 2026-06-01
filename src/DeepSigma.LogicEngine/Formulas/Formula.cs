namespace DeepSigma.LogicEngine.Formulas;

/// <summary>
/// Base of the propositional-logic formula AST. Concrete node types are
/// <see cref="BoolConst"/>, <see cref="Variable"/>, <see cref="Negation"/>,
/// <see cref="Conjunction"/>, <see cref="Disjunction"/>, <see cref="Implication"/>,
/// and <see cref="Biconditional"/>.
/// </summary>
public abstract record Formula
{
    /// <summary>The boolean constant true.</summary>
    public static Formula True { get; } = new BoolConst(true);
    /// <summary>The boolean constant false.</summary>
    public static Formula False { get; } = new BoolConst(false);

    /// <summary>Returns the boolean constant for the given value.</summary>
    public static Formula Const(bool value) => value ? True : False;
    /// <summary>Creates a propositional variable with the given name.</summary>
    public static Formula Var(string name) => new Variable(name);
    /// <summary>Creates the negation of the given formula.</summary>
    public static Formula Not(Formula operand) => new Negation(operand);
    /// <summary>Creates the conjunction (AND) of two formulas.</summary>
    public static Formula And(Formula left, Formula right) => new Conjunction(left, right);
    /// <summary>Creates the disjunction (OR) of two formulas.</summary>
    public static Formula Or(Formula left, Formula right) => new Disjunction(left, right);
    /// <summary>Creates the implication (antecedent → consequent).</summary>
    public static Formula Implies(Formula antecedent, Formula consequent) => new Implication(antecedent, consequent);
    /// <summary>Creates the biconditional (left ↔ right).</summary>
    public static Formula Iff(Formula left, Formula right) => new Biconditional(left, right);

    /// <summary>Big conjunction over a sequence; empty sequence yields True. Built as a balanced tree.</summary>
    public static Formula All(IEnumerable<Formula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<Formula> ?? formulas.ToList(), static (a, b) => new Conjunction(a, b)) ?? True;

    /// <summary>Big disjunction over a sequence; empty sequence yields False. Built as a balanced tree.</summary>
    public static Formula Any(IEnumerable<Formula> formulas)
        => Common.BalancedFold.Combine(formulas as IReadOnlyList<Formula> ?? formulas.ToList(), static (a, b) => new Disjunction(a, b)) ?? False;

    /// <summary>Parses a formula from its textual form; throws <see cref="FormatException"/> on invalid input.</summary>
    public static Formula Parse(string source) => Parsing.Parser.Parse(source);
    /// <summary>Attempts to parse a formula from its textual form; returns false on invalid input.</summary>
    public static bool TryParse(string source, out Formula formula) => Parsing.Parser.TryParse(source, out formula);

    /// <summary>Creates the negation of the given formula.</summary>
    public static Formula operator !(Formula f) => new Negation(f);
    /// <summary>Creates the conjunction (AND) of two formulas.</summary>
    public static Formula operator &(Formula a, Formula b) => new Conjunction(a, b);
    /// <summary>Creates the disjunction (OR) of two formulas.</summary>
    public static Formula operator |(Formula a, Formula b) => new Disjunction(a, b);

    /// <summary>Renders the formula in its standard textual form.</summary>
    public sealed override string ToString() => Printing.Printer.Print(this);
}

/// <summary>A boolean constant (true or false).</summary>
public sealed record BoolConst(bool Value) : Formula;

/// <summary>A propositional variable referenced by name.</summary>
public sealed record Variable(string Name) : Formula;

/// <summary>The negation (NOT) of a sub-formula.</summary>
public sealed record Negation(Formula Operand) : Formula;

/// <summary>The conjunction (AND) of two sub-formulas.</summary>
public sealed record Conjunction(Formula Left, Formula Right) : Formula;

/// <summary>The disjunction (OR) of two sub-formulas.</summary>
public sealed record Disjunction(Formula Left, Formula Right) : Formula;

/// <summary>The implication (antecedent → consequent).</summary>
public sealed record Implication(Formula Antecedent, Formula Consequent) : Formula;

/// <summary>The biconditional (left ↔ right).</summary>
public sealed record Biconditional(Formula Left, Formula Right) : Formula;
