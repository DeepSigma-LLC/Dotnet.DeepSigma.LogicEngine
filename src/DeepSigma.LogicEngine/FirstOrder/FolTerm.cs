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
    public static FolTerm Func(string symbol, params FolTerm[] args) => new FolFunc(symbol, args);

    public sealed override string ToString() => FolPrinter.Print(this);
}

public sealed record FolVar(string Name) : FolTerm;

public sealed record FolFunc : FolTerm
{
    public string Symbol { get; }
    public IReadOnlyList<FolTerm> Args { get; }

    public FolFunc(string symbol, IReadOnlyList<FolTerm> args)
    {
        Symbol = symbol;
        Args = args;
    }

    public bool IsConstant => Args.Count == 0;

    public bool Equals(FolFunc? other)
        => other is not null && Symbol == other.Symbol && Args.Count == other.Args.Count && Args.SequenceEqual(other.Args);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Symbol);
        hash.Add(Args.Count);
        foreach (var a in Args)
        {
            hash.Add(a);
        }
        return hash.ToHashCode();
    }
}
