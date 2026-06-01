namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A first-order term over uninterpreted function symbols: a symbol applied to
/// zero or more argument terms. A constant is simply a symbol with no arguments.
/// Terms have structural equality, so equal terms compare equal and hash alike.
/// </summary>
public sealed record Term(string Symbol, IReadOnlyList<Term> Arguments)
{
    /// <summary>True if the term is a constant, i.e. a symbol applied to no arguments.</summary>
    public bool IsConstant => Arguments.Count == 0;

    /// <summary>Creates a constant term (a symbol with no arguments).</summary>
    public static Term Constant(string symbol) => new(symbol, Array.Empty<Term>());

    /// <summary>Creates a function application term from a symbol and its argument terms.</summary>
    public static Term Func(string symbol, params Term[] arguments) => new(symbol, arguments);

    /// <summary>Determines structural equality with another term (same symbol and equal arguments).</summary>
    public bool Equals(Term? other)
        => other is not null && string.Equals(Symbol, other.Symbol, StringComparison.Ordinal)
            && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    /// <summary>Returns a structural hash code over the symbol and arguments.</summary>
    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);

    /// <summary>Renders the term as <c>symbol</c> for constants or <c>symbol(arg, ...)</c> for applications.</summary>
    public override string ToString()
        => IsConstant ? Symbol : $"{Symbol}({string.Join(", ", Arguments)})";
}
