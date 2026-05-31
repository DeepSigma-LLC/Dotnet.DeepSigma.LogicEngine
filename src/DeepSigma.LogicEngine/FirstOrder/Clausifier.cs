namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>A first-order literal: an atom (predicate or equality) with a sign.</summary>
public sealed record FolLiteral(FolFormula Atom, bool Negated)
{
    public FolLiteral Negate() => this with { Negated = !Negated };
    public override string ToString() => (Negated ? "!" : string.Empty) + Atom;
}

/// <summary>A disjunctive clause (implicitly universally quantified over its variables).</summary>
public sealed class FolClause
{
    public IReadOnlyList<FolLiteral> Literals { get; }
    public bool IsEmpty => Literals.Count == 0;

    public FolClause(IReadOnlyList<FolLiteral> literals) => Literals = literals;

    public override string ToString() => Literals.Count == 0 ? "□" : string.Join(" | ", Literals);
}

/// <summary>
/// Converts a first-order formula to clause form: eliminate →/↔, push negations
/// to atoms (NNF), standardize bound variables apart, Skolemize the existentials,
/// drop the (now outermost) universals, and distribute to CNF.
/// </summary>
internal sealed class Clausifier
{
    private int _skolemCounter;
    private int _freshCounter;

    public static IReadOnlyList<FolClause> Clausify(FolFormula formula) => new Clausifier().Run(formula);

    public static IReadOnlyList<FolClause> ClausifyAll(IEnumerable<FolFormula> formulas)
    {
        var clausifier = new Clausifier();
        return formulas.SelectMany(clausifier.Run).ToList();
    }

    private IReadOnlyList<FolClause> Run(FolFormula formula)
    {
        var nnf = ToNnf(EliminateConnectives(formula));
        var standardized = StandardizeApart(nnf, new Dictionary<string, FolTerm>(StringComparer.Ordinal));
        var skolemized = Skolemize(standardized, new List<FolTerm>());
        return Distribute(skolemized)
            .Select(literals => new FolClause(Dedup(literals)))
            .Where(c => !IsTautology(c))
            .ToList();
    }

    private static FolFormula EliminateConnectives(FolFormula f) => f switch
    {
        FolImplies x => new FolOr(new FolNot(EliminateConnectives(x.Antecedent)), EliminateConnectives(x.Consequent)),
        FolIff x => new FolAnd(
            new FolOr(new FolNot(EliminateConnectives(x.Left)), EliminateConnectives(x.Right)),
            new FolOr(new FolNot(EliminateConnectives(x.Right)), EliminateConnectives(x.Left))),
        FolNot n => new FolNot(EliminateConnectives(n.Operand)),
        FolAnd x => new FolAnd(EliminateConnectives(x.Left), EliminateConnectives(x.Right)),
        FolOr x => new FolOr(EliminateConnectives(x.Left), EliminateConnectives(x.Right)),
        FolForall q => new FolForall(q.Variable, EliminateConnectives(q.Body)),
        FolExists q => new FolExists(q.Variable, EliminateConnectives(q.Body)),
        _ => f,
    };

    private static FolFormula ToNnf(FolFormula f) => f switch
    {
        FolNot n => PushNot(n.Operand),
        FolAnd x => new FolAnd(ToNnf(x.Left), ToNnf(x.Right)),
        FolOr x => new FolOr(ToNnf(x.Left), ToNnf(x.Right)),
        FolForall q => new FolForall(q.Variable, ToNnf(q.Body)),
        FolExists q => new FolExists(q.Variable, ToNnf(q.Body)),
        _ => f,
    };

    private static FolFormula PushNot(FolFormula f) => f switch
    {
        FolNot n => ToNnf(n.Operand),
        FolAnd x => new FolOr(PushNot(x.Left), PushNot(x.Right)),
        FolOr x => new FolAnd(PushNot(x.Left), PushNot(x.Right)),
        FolForall q => new FolExists(q.Variable, PushNot(q.Body)),
        FolExists q => new FolForall(q.Variable, PushNot(q.Body)),
        FolBool b => new FolBool(!b.Value),
        _ => new FolNot(f), // atom
    };

    private FolFormula StandardizeApart(FolFormula f, Dictionary<string, FolTerm> renaming) => f switch
    {
        FolForall q => RenameQuantified(q.Variable, q.Body, renaming, isForall: true),
        FolExists q => RenameQuantified(q.Variable, q.Body, renaming, isForall: false),
        FolAnd x => new FolAnd(StandardizeApart(x.Left, renaming), StandardizeApart(x.Right, renaming)),
        FolOr x => new FolOr(StandardizeApart(x.Left, renaming), StandardizeApart(x.Right, renaming)),
        FolNot n => new FolNot(StandardizeApart(n.Operand, renaming)),
        FolPredicate p => new FolPredicate(p.Symbol, p.Arguments.Select(a => RenameTerm(a, renaming)).ToArray()),
        FolEquals e => new FolEquals(RenameTerm(e.Left, renaming), RenameTerm(e.Right, renaming)),
        _ => f,
    };

    private FolFormula RenameQuantified(string variable, FolFormula body, Dictionary<string, FolTerm> renaming, bool isForall)
    {
        var fresh = "v" + _freshCounter++;
        var inner = new Dictionary<string, FolTerm>(renaming, StringComparer.Ordinal) { [variable] = new FolVar(fresh) };
        var renamedBody = StandardizeApart(body, inner);
        return isForall ? new FolForall(fresh, renamedBody) : new FolExists(fresh, renamedBody);
    }

    private static FolTerm RenameTerm(FolTerm t, Dictionary<string, FolTerm> renaming) => t switch
    {
        FolVar v => renaming.TryGetValue(v.Name, out var renamed) ? renamed : t,
        FolFunc f => f.Arguments.Count == 0 ? f : new FolFunc(f.Symbol, f.Arguments.Select(a => RenameTerm(a, renaming)).ToArray()),
        _ => t,
    };

    private FolFormula Skolemize(FolFormula f, List<FolTerm> universals) => f switch
    {
        FolForall q => SkolemizeForall(q.Variable, q.Body, universals),
        FolExists q => SkolemizeExists(q.Variable, q.Body, universals),
        FolAnd x => new FolAnd(Skolemize(x.Left, universals), Skolemize(x.Right, universals)),
        FolOr x => new FolOr(Skolemize(x.Left, universals), Skolemize(x.Right, universals)),
        _ => f, // Not(atom) / atom / bool
    };

    private FolFormula SkolemizeForall(string variable, FolFormula body, List<FolTerm> universals)
    {
        universals.Add(new FolVar(variable));
        var result = Skolemize(body, universals);
        universals.RemoveAt(universals.Count - 1);
        return result;
    }

    private FolFormula SkolemizeExists(string variable, FolFormula body, List<FolTerm> universals)
    {
        FolTerm skolem = universals.Count == 0
            ? FolTerm.Constant("sk" + _skolemCounter++)
            : new FolFunc("sk" + _skolemCounter++, universals.ToArray());
        var substituted = new Substitution(new Dictionary<string, FolTerm> { [variable] = skolem }).Apply(body);
        return Skolemize(substituted, universals);
    }

    private static List<List<FolLiteral>> Distribute(FolFormula f)
    {
        switch (f)
        {
            case FolBool b:
                return b.Value ? new List<List<FolLiteral>>() : new List<List<FolLiteral>> { new() };
            case FolNot n:
                return new List<List<FolLiteral>> { new() { new FolLiteral(n.Operand, true) } };
            case FolAnd x:
                var both = Distribute(x.Left);
                both.AddRange(Distribute(x.Right));
                return both;
            case FolOr x:
                var result = new List<List<FolLiteral>>();
                foreach (var left in Distribute(x.Left))
                {
                    foreach (var right in Distribute(x.Right))
                    {
                        result.Add(left.Concat(right).ToList());
                    }
                }
                return result;
            default: // atom
                return new List<List<FolLiteral>> { new() { new FolLiteral(f, false) } };
        }
    }

    private static IReadOnlyList<FolLiteral> Dedup(IEnumerable<FolLiteral> literals)
    {
        var seen = new HashSet<FolLiteral>();
        var result = new List<FolLiteral>();
        foreach (var literal in literals)
        {
            if (seen.Add(literal))
            {
                result.Add(literal);
            }
        }
        return result;
    }

    private static bool IsTautology(FolClause clause)
    {
        for (var i = 0; i < clause.Literals.Count; i++)
        {
            for (var j = i + 1; j < clause.Literals.Count; j++)
            {
                if (clause.Literals[i].Negated != clause.Literals[j].Negated &&
                    clause.Literals[i].Atom.Equals(clause.Literals[j].Atom))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
