namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// A first-order term: a logic <see cref="FolVar">variable</see> (subject to
/// unification) or a <see cref="FolFunc">function application</see> (a constant
/// when it has no arguments). Equality is structural.
/// </summary>
public abstract record FolTerm
{
    /// <summary>Builds a logic variable named <paramref name="name"/>.</summary>
    public static FolTerm Var(string name) => new FolVar(name);
    /// <summary>Builds a constant: a nullary function application of <paramref name="symbol"/>.</summary>
    public static FolTerm Constant(string symbol) => new FolFunc(symbol, Array.Empty<FolTerm>());
    /// <summary>Builds a function application of <paramref name="symbol"/> to <paramref name="arguments"/>.</summary>
    public static FolTerm Func(string symbol, params FolTerm[] arguments) => new FolFunc(symbol, arguments);

    /// <summary>Renders the term in the textual syntax.</summary>
    public sealed override string ToString() => FolPrinter.Print(this);
}

/// <summary>A logic variable named <paramref name="Name"/>, subject to unification.</summary>
public sealed record FolVar(string Name) : FolTerm;

/// <summary>Function application: <see cref="Symbol"/> applied to <see cref="Arguments"/> (a constant when nullary).</summary>
public sealed record FolFunc : FolTerm
{
    /// <summary>The function symbol.</summary>
    public string Symbol { get; }
    /// <summary>The argument terms the function is applied to.</summary>
    public IReadOnlyList<FolTerm> Arguments { get; }

    /// <summary>Creates a function application from a symbol and its argument terms.</summary>
    public FolFunc(string symbol, IReadOnlyList<FolTerm> arguments)
    {
        Symbol = symbol;
        Arguments = arguments;
    }

    /// <summary>True when the application is nullary, i.e. a constant.</summary>
    public bool IsConstant => Arguments.Count == 0;

    /// <summary>Structural equality over the symbol and argument list.</summary>
    public bool Equals(FolFunc? other)
        => other is not null && Symbol == other.Symbol && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    /// <summary>Structural hash over the symbol and argument list.</summary>
    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);
}
