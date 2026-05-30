namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// A first-order formula: predicate / equality atoms over <see cref="FolTerm"/>s,
/// the boolean connectives, and the quantifiers ∀ and ∃. Equality is structural.
/// </summary>
public abstract record FolFormula
{
    public static FolFormula Predicate(string symbol, params FolTerm[] args) => new FolPredicate(symbol, args);
    public static FolFormula Equal(FolTerm left, FolTerm right) => new FolEquals(left, right);
    public static FolFormula Not(FolFormula f) => new FolNot(f);
    public static FolFormula And(FolFormula a, FolFormula b) => new FolAnd(a, b);
    public static FolFormula Or(FolFormula a, FolFormula b) => new FolOr(a, b);
    public static FolFormula Implies(FolFormula a, FolFormula b) => new FolImplies(a, b);
    public static FolFormula Iff(FolFormula a, FolFormula b) => new FolIff(a, b);
    public static FolFormula ForAll(string variable, FolFormula body) => new FolForall(variable, body);
    public static FolFormula Exists(string variable, FolFormula body) => new FolExists(variable, body);

    public static FolFormula operator !(FolFormula f) => new FolNot(f);
    public static FolFormula operator &(FolFormula a, FolFormula b) => new FolAnd(a, b);
    public static FolFormula operator |(FolFormula a, FolFormula b) => new FolOr(a, b);

    public static FolFormula Parse(string source) => FolParser.Parse(source);

    public sealed override string ToString() => FolPrinter.Print(this);
}

public sealed record FolPredicate : FolFormula
{
    public string Symbol { get; }
    public IReadOnlyList<FolTerm> Args { get; }

    public FolPredicate(string symbol, IReadOnlyList<FolTerm> args)
    {
        Symbol = symbol;
        Args = args;
    }

    public bool Equals(FolPredicate? other)
        => other is not null && Symbol == other.Symbol && Common.StructuralEquality.ListEquals(Args, other.Args);

    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Args);
}

public sealed record FolEquals(FolTerm Left, FolTerm Right) : FolFormula;
public sealed record FolBool(bool Value) : FolFormula;
public sealed record FolNot(FolFormula Operand) : FolFormula;
public sealed record FolAnd(FolFormula Left, FolFormula Right) : FolFormula;
public sealed record FolOr(FolFormula Left, FolFormula Right) : FolFormula;
public sealed record FolImplies(FolFormula Antecedent, FolFormula Consequent) : FolFormula;
public sealed record FolIff(FolFormula Left, FolFormula Right) : FolFormula;
public sealed record FolForall(string Variable, FolFormula Body) : FolFormula;
public sealed record FolExists(string Variable, FolFormula Body) : FolFormula;
