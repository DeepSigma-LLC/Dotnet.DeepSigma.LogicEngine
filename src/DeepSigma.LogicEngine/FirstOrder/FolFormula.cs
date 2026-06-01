namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// A first-order formula: predicate / equality atoms over <see cref="FolTerm"/>s,
/// the boolean connectives, and the quantifiers ∀ and ∃. Equality is structural.
/// </summary>
public abstract record FolFormula
{
    /// <summary>Builds a predicate atom applying <paramref name="symbol"/> to <paramref name="arguments"/>.</summary>
    public static FolFormula Predicate(string symbol, params FolTerm[] arguments) => new FolPredicate(symbol, arguments);
    /// <summary>Builds an equality atom <paramref name="left"/> = <paramref name="right"/>.</summary>
    public static FolFormula Equal(FolTerm left, FolTerm right) => new FolEquals(left, right);
    /// <summary>Builds the negation ¬<paramref name="f"/>.</summary>
    public static FolFormula Not(FolFormula f) => new FolNot(f);
    /// <summary>Builds the conjunction <paramref name="a"/> ∧ <paramref name="b"/>.</summary>
    public static FolFormula And(FolFormula a, FolFormula b) => new FolAnd(a, b);
    /// <summary>Builds the disjunction <paramref name="a"/> ∨ <paramref name="b"/>.</summary>
    public static FolFormula Or(FolFormula a, FolFormula b) => new FolOr(a, b);
    /// <summary>Builds the implication <paramref name="a"/> → <paramref name="b"/>.</summary>
    public static FolFormula Implies(FolFormula a, FolFormula b) => new FolImplies(a, b);
    /// <summary>Builds the biconditional <paramref name="a"/> ↔ <paramref name="b"/>.</summary>
    public static FolFormula Iff(FolFormula a, FolFormula b) => new FolIff(a, b);
    /// <summary>Builds the universally quantified formula ∀<paramref name="variable"/>. <paramref name="body"/>.</summary>
    public static FolFormula ForAll(string variable, FolFormula body) => new FolForall(variable, body);
    /// <summary>Builds the existentially quantified formula ∃<paramref name="variable"/>. <paramref name="body"/>.</summary>
    public static FolFormula Exists(string variable, FolFormula body) => new FolExists(variable, body);

    /// <summary>Operator form of <see cref="Not(FolFormula)"/>: ¬<paramref name="f"/>.</summary>
    public static FolFormula operator !(FolFormula f) => new FolNot(f);
    /// <summary>Operator form of <see cref="And(FolFormula, FolFormula)"/>: <paramref name="a"/> ∧ <paramref name="b"/>.</summary>
    public static FolFormula operator &(FolFormula a, FolFormula b) => new FolAnd(a, b);
    /// <summary>Operator form of <see cref="Or(FolFormula, FolFormula)"/>: <paramref name="a"/> ∨ <paramref name="b"/>.</summary>
    public static FolFormula operator |(FolFormula a, FolFormula b) => new FolOr(a, b);

    /// <summary>Parses a formula from its textual syntax; throws <see cref="FormatException"/> on malformed input.</summary>
    public static FolFormula Parse(string source) => FolParser.Parse(source);
    /// <summary>Attempts to parse a formula from its textual syntax; returns false on malformed input.</summary>
    public static bool TryParse(string source, out FolFormula formula) => FolParser.TryParse(source, out formula);

    /// <summary>Renders the formula in the textual syntax.</summary>
    public sealed override string ToString() => FolPrinter.Print(this);
}

/// <summary>Predicate atom: <see cref="Symbol"/> applied to <see cref="Arguments"/>.</summary>
public sealed record FolPredicate : FolFormula
{
    /// <summary>The predicate symbol.</summary>
    public string Symbol { get; }
    /// <summary>The argument terms the predicate is applied to.</summary>
    public IReadOnlyList<FolTerm> Arguments { get; }

    /// <summary>Creates a predicate atom from a symbol and its argument terms.</summary>
    public FolPredicate(string symbol, IReadOnlyList<FolTerm> arguments)
    {
        Symbol = symbol;
        Arguments = arguments;
    }

    /// <summary>Structural equality over the symbol and argument list.</summary>
    public bool Equals(FolPredicate? other)
        => other is not null && Symbol == other.Symbol && Common.StructuralEquality.ListEquals(Arguments, other.Arguments);

    /// <summary>Structural hash over the symbol and argument list.</summary>
    public override int GetHashCode() => Common.StructuralEquality.Hash(Symbol, Arguments);
}

/// <summary>Equality atom <paramref name="Left"/> = <paramref name="Right"/>.</summary>
public sealed record FolEquals(FolTerm Left, FolTerm Right) : FolFormula;
/// <summary>Boolean constant <paramref name="Value"/>.</summary>
public sealed record FolBool(bool Value) : FolFormula;
/// <summary>Logical negation ¬<paramref name="Operand"/>.</summary>
public sealed record FolNot(FolFormula Operand) : FolFormula;
/// <summary>Logical conjunction <paramref name="Left"/> ∧ <paramref name="Right"/>.</summary>
public sealed record FolAnd(FolFormula Left, FolFormula Right) : FolFormula;
/// <summary>Logical disjunction <paramref name="Left"/> ∨ <paramref name="Right"/>.</summary>
public sealed record FolOr(FolFormula Left, FolFormula Right) : FolFormula;
/// <summary>Logical implication <paramref name="Antecedent"/> → <paramref name="Consequent"/>.</summary>
public sealed record FolImplies(FolFormula Antecedent, FolFormula Consequent) : FolFormula;
/// <summary>Logical biconditional <paramref name="Left"/> ↔ <paramref name="Right"/>.</summary>
public sealed record FolIff(FolFormula Left, FolFormula Right) : FolFormula;
/// <summary>Universal quantification ∀<paramref name="Variable"/>. <paramref name="Body"/>.</summary>
public sealed record FolForall(string Variable, FolFormula Body) : FolFormula;
/// <summary>Existential quantification ∃<paramref name="Variable"/>. <paramref name="Body"/>.</summary>
public sealed record FolExists(string Variable, FolFormula Body) : FolFormula;
