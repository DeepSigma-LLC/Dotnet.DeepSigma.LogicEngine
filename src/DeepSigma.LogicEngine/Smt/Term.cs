namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A first-order term over uninterpreted function symbols: a symbol applied to
/// zero or more argument terms. A constant is simply a symbol with no arguments.
/// Terms have structural equality, so equal terms compare equal and hash alike.
/// </summary>
public sealed record Term(string Symbol, IReadOnlyList<Term> Arguments)
{
    public bool IsConstant => Arguments.Count == 0;

    public static Term Constant(string symbol) => new(symbol, Array.Empty<Term>());

    public static Term Func(string symbol, params Term[] arguments) => new(symbol, arguments);

    public bool Equals(Term? other)
        => other is not null && string.Equals(Symbol, other.Symbol, StringComparison.Ordinal)
            && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);

    public override string ToString()
        => IsConstant ? Symbol : $"{Symbol}({string.Join(", ", Arguments)})";
}
