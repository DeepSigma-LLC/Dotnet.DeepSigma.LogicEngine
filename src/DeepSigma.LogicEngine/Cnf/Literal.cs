namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// A propositional literal: a variable name with a polarity.
/// <see cref="Negated"/> = false means the positive literal, true means the negated literal.
/// </summary>
public readonly record struct Literal(string Variable, bool Negated)
{
    public static Literal Positive(string variable) => new(variable, false);
    public static Literal Negative(string variable) => new(variable, true);

    public Literal Negate() => new(Variable, !Negated);

    public override string ToString() => Negated ? "!" + Variable : Variable;
}
