namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Decision procedures for the quantifier-free theory of arrays (non-extensional),
/// over the functions <c>select(a, i)</c> and <c>store(a, i, v)</c>. It reduces to
/// EUF by <b>eager read-over-write instantiation</b>: for every <c>store(a, i, v)</c>
/// and every index term <c>j</c> occurring in the formula it adds the axiom
/// <c>i = j → select(store(a,i,v), j) = v</c> and
/// <c>i ≠ j → select(store(a,i,v), j) = select(a, j)</c>, then hands the result to
/// <see cref="EufSolver"/>. For ground formulas this instantiation is complete.
/// </summary>
public static class ArraySolver
{
    private const string SelectSymbol = "select";
    private const string StoreSymbol = "store";

    /// <summary>True if some array model satisfies the formula.</summary>
    public static bool IsSatisfiable(SmtFormula formula, CancellationToken cancellationToken = default) => EufSolver.IsSatisfiable(WithArrayAxioms(formula), cancellationToken);

    /// <summary>True if no array model satisfies the formula.</summary>
    public static bool IsUnsatisfiable(SmtFormula formula, CancellationToken cancellationToken = default) => !IsSatisfiable(formula, cancellationToken);

    /// <summary>True if the formula holds in every array model.</summary>
    public static bool IsValid(SmtFormula formula, CancellationToken cancellationToken = default) => !IsSatisfiable(new SmtNot(formula), cancellationToken);

    /// <summary>True if the knowledge base entails the query in the array theory.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query, CancellationToken cancellationToken = default)
        => !IsSatisfiable(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query)), cancellationToken);

    /// <summary>The formula conjoined with the read-over-write axiom instances it requires.</summary>
    public static SmtFormula WithArrayAxioms(SmtFormula formula)
    {
        var axioms = AxiomInstances(formula);
        return axioms.Count == 0 ? formula : new SmtAnd(formula, SmtFormula.All(axioms));
    }

    private static List<SmtFormula> AxiomInstances(SmtFormula formula)
    {
        var terms = new Dictionary<string, Term>(StringComparer.Ordinal);
        foreach (var atom in Atoms(formula))
        {
            foreach (var term in Subterms(atom))
            {
                terms[term.ToString()!] = term;
            }
        }

        var stores = terms.Values.Where(t => t.Symbol == StoreSymbol && t.Arguments.Count == 3).ToList();
        var indices = terms.Values
            .Where(t => (t.Symbol == SelectSymbol || t.Symbol == StoreSymbol) && t.Arguments.Count >= 2)
            .Select(t => t.Arguments[1])
            .GroupBy(t => t.ToString(), StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var axioms = new List<SmtFormula>();
        foreach (var store in stores)
        {
            var array = store.Arguments[0];
            var i = store.Arguments[1];
            var v = store.Arguments[2];
            foreach (var j in indices)
            {
                var selectFromStore = Term.Func(SelectSymbol, store, j);
                var selectFromArray = Term.Func(SelectSymbol, array, j);
                var sameIndex = SmtFormula.Eq(i, j);
                // i = j → select(store(a,i,v), j) = v
                axioms.Add(new SmtOr(new SmtNot(sameIndex), SmtFormula.Eq(selectFromStore, v)));
                // i ≠ j → select(store(a,i,v), j) = select(a, j)
                axioms.Add(new SmtOr(sameIndex, SmtFormula.Eq(selectFromStore, selectFromArray)));
            }
        }
        return axioms;
    }

    private static IEnumerable<SmtFormula> Atoms(SmtFormula f)
    {
        switch (f)
        {
            case EqualityAtom or PredicateAtom: yield return f; break;
            case SmtNot n: foreach (var a in Atoms(n.Operand)) { yield return a; } break;
            case SmtAnd x: foreach (var a in Atoms(x.Left)) { yield return a; } foreach (var a in Atoms(x.Right)) { yield return a; } break;
            case SmtOr x: foreach (var a in Atoms(x.Left)) { yield return a; } foreach (var a in Atoms(x.Right)) { yield return a; } break;
            case SmtImplies x: foreach (var a in Atoms(x.Antecedent)) { yield return a; } foreach (var a in Atoms(x.Consequent)) { yield return a; } break;
            case SmtIff x: foreach (var a in Atoms(x.Left)) { yield return a; } foreach (var a in Atoms(x.Right)) { yield return a; } break;
        }
    }

    private static IEnumerable<Term> Subterms(SmtFormula atom)
    {
        switch (atom)
        {
            case EqualityAtom e:
                foreach (var t in Subterms(e.Left)) { yield return t; }
                foreach (var t in Subterms(e.Right)) { yield return t; }
                break;
            case PredicateAtom p:
                foreach (var arg in p.Arguments)
                {
                    foreach (var t in Subterms(arg)) { yield return t; }
                }
                break;
        }
    }

    private static IEnumerable<Term> Subterms(Term term)
    {
        yield return term;
        foreach (var arg in term.Arguments)
        {
            foreach (var t in Subterms(arg)) { yield return t; }
        }
    }
}
