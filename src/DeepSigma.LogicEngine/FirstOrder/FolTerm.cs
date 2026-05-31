namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// A first-order term: a logic <see cref="FolVar">variable</see> (subject to
/// unification) or a <see cref="FolFunc">function application</see> (a constant
/// when it has no arguments). Equality is structural.
/// </summary>
public abstract record FolTerm
{
    public static FolTerm Var(string name) => new FolVar(name);
    public static FolTerm Constant(string symbol) => new FolFunc(symbol, Array.Empty<FolTerm>());
    public static FolTerm Func(string symbol, params FolTerm[] arguments) => new FolFunc(symbol, arguments);

    public sealed override string ToString() => FolPrinter.Print(this);
}

public sealed record FolVar(string Name) : FolTerm;

public sealed record FolFunc : FolTerm
{
    public string Symbol { get; }
    public IReadOnlyList<FolTerm> Arguments { get; }

    public FolFunc(string symbol, IReadOnlyList<FolTerm> arguments)
    {
        Symbol = symbol;
        Arguments = arguments;
    }

    public bool IsConstant => Arguments.Count == 0;

    public bool Equals(FolFunc? other)
        => other is not null && Symbol == other.Symbol && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);
}
