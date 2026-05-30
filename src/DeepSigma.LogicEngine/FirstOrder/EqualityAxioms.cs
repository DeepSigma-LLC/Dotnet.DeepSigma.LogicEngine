namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// Generates the equality axioms (reflexivity, symmetry, transitivity, and
/// congruence for every function and predicate symbol that occurs) so that the
/// resolution prover treats <c>=</c> as genuine equality. Added only when an
/// equality atom appears in the clause set.
/// </summary>
internal static class EqualityAxioms
{
    public static IReadOnlyList<FolClause> For(IReadOnlyList<FolClause> clauses)
    {
        if (!UsesEquality(clauses))
        {
            return Array.Empty<FolClause>();
        }

        var functions = new Dictionary<string, int>(StringComparer.Ordinal);
        var predicates = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var clause in clauses)
        {
            foreach (var literal in clause.Literals)
            {
                CollectFromAtom(literal.Atom, functions, predicates);
            }
        }

        var axioms = new List<FolClause>
        {
            // reflexivity, symmetry, transitivity
            Clause(Pos(Eq(V("x"), V("x")))),
            Clause(Neg(Eq(V("x"), V("y"))), Pos(Eq(V("y"), V("x")))),
            Clause(Neg(Eq(V("x"), V("y"))), Neg(Eq(V("y"), V("z"))), Pos(Eq(V("x"), V("z")))),
        };

        foreach (var (symbol, arity) in functions)
        {
            axioms.Add(FunctionCongruence(symbol, arity));
        }
        foreach (var (symbol, arity) in predicates)
        {
            axioms.Add(PredicateCongruence(symbol, arity));
        }
        return axioms;
    }

    private static bool UsesEquality(IReadOnlyList<FolClause> clauses)
        => clauses.Any(c => c.Literals.Any(l => l.Atom is FolEquals));

    private static void CollectFromAtom(FolFormula atom, Dictionary<string, int> functions, Dictionary<string, int> predicates)
    {
        switch (atom)
        {
            case FolPredicate p:
                if (p.Args.Count > 0)
                {
                    predicates[p.Symbol] = p.Args.Count;
                }
                foreach (var a in p.Args)
                {
                    CollectFromTerm(a, functions);
                }
                break;
            case FolEquals e:
                CollectFromTerm(e.Left, functions);
                CollectFromTerm(e.Right, functions);
                break;
        }
    }

    private static void CollectFromTerm(FolTerm term, Dictionary<string, int> functions)
    {
        if (term is FolFunc f)
        {
            if (f.Args.Count > 0)
            {
                functions[f.Symbol] = f.Args.Count;
                foreach (var a in f.Args)
                {
                    CollectFromTerm(a, functions);
                }
            }
        }
    }

    private static FolClause FunctionCongruence(string symbol, int arity)
    {
        var xs = Vars("x", arity);
        var ys = Vars("y", arity);
        var literals = new List<FolLiteral>();
        for (var i = 0; i < arity; i++)
        {
            literals.Add(Neg(Eq(xs[i], ys[i])));
        }
        literals.Add(Pos(Eq(new FolFunc(symbol, xs), new FolFunc(symbol, ys))));
        return new FolClause(literals);
    }

    private static FolClause PredicateCongruence(string symbol, int arity)
    {
        var xs = Vars("x", arity);
        var ys = Vars("y", arity);
        var literals = new List<FolLiteral>();
        for (var i = 0; i < arity; i++)
        {
            literals.Add(Neg(Eq(xs[i], ys[i])));
        }
        literals.Add(Neg(new FolPredicate(symbol, xs)));
        literals.Add(Pos(new FolPredicate(symbol, ys)));
        return new FolClause(literals);
    }

    private static FolTerm[] Vars(string prefix, int n)
        => Enumerable.Range(0, n).Select(i => (FolTerm)new FolVar($"{prefix}{i}")).ToArray();

    private static FolTerm V(string name) => new FolVar(name);
    private static FolFormula Eq(FolTerm a, FolTerm b) => new FolEquals(a, b);
    private static FolLiteral Pos(FolFormula atom) => new(atom, false);
    private static FolLiteral Neg(FolFormula atom) => new(atom, true);
    private static FolClause Clause(params FolLiteral[] literals) => new(literals);
}
