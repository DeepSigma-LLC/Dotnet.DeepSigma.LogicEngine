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

    public override string ToString()
        => IsConstant ? Symbol : $"{Symbol}({string.Join(", ", Arguments)})";
}
