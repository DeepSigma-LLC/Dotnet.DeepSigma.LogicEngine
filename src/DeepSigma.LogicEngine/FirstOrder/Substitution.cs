namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>An immutable variable → term substitution, with application to terms, atoms, and literals.</summary>
public sealed class Substitution
{
    private readonly IReadOnlyDictionary<string, FolTerm> _bindings;

    /// <summary>The identity substitution, which binds no variables.</summary>
    public static Substitution Empty { get; } = new(new Dictionary<string, FolTerm>());

    /// <summary>Create a substitution from a variable-name → term mapping.</summary>
    public Substitution(IReadOnlyDictionary<string, FolTerm> bindings) => _bindings = bindings;

    /// <summary>Apply the substitution to a term, recursively resolving bound variables.</summary>
    public FolTerm Apply(FolTerm term)
    {
        switch (term)
        {
            case FolVar v:
                return _bindings.TryGetValue(v.Name, out var bound) ? Apply(bound) : term;
            case FolFunc f when f.Arguments.Count == 0:
                return f;
            case FolFunc f:
                return new FolFunc(f.Symbol, f.Arguments.Select(Apply).ToArray());
            default:
                return term;
        }
    }

    /// <summary>Apply the substitution throughout a formula.</summary>
    public FolFormula Apply(FolFormula formula) => formula switch
    {
        FolPredicate p => new FolPredicate(p.Symbol, p.Arguments.Select(Apply).ToArray()),
        FolEquals e => new FolEquals(Apply(e.Left), Apply(e.Right)),
        FolNot n => new FolNot(Apply(n.Operand)),
        FolAnd x => new FolAnd(Apply(x.Left), Apply(x.Right)),
        FolOr x => new FolOr(Apply(x.Left), Apply(x.Right)),
        FolImplies x => new FolImplies(Apply(x.Antecedent), Apply(x.Consequent)),
        FolIff x => new FolIff(Apply(x.Left), Apply(x.Right)),
        FolForall q => new FolForall(q.Variable, Apply(q.Body)),
        FolExists q => new FolExists(q.Variable, Apply(q.Body)),
        _ => formula,
    };

    /// <summary>Apply the substitution to a literal's atom, preserving its sign.</summary>
    public FolLiteral Apply(FolLiteral literal) => literal with { Atom = Apply(literal.Atom) };
}
