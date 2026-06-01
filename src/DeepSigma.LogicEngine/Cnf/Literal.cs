namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// A propositional literal: a variable name with a polarity.
/// <see cref="Negated"/> = false means the positive literal, true means the negated literal.
/// </summary>
public readonly record struct Literal(string Variable, bool Negated)
{
    /// <summary>Creates the positive literal for the given variable.</summary>
    public static Literal Positive(string variable) => new(variable, false);
    /// <summary>Creates the negated literal for the given variable.</summary>
    public static Literal Negative(string variable) => new(variable, true);

    /// <summary>Returns the literal with the opposite polarity.</summary>
    public Literal Negate() => new(Variable, !Negated);

    /// <summary>Renders the literal as the variable name, prefixed with <c>!</c> when negated.</summary>
    public override string ToString() => Negated ? "!" + Variable : Variable;
}
