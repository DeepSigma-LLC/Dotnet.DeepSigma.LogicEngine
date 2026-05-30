using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>One theory atom and the propositional variable that stands for it.</summary>
internal sealed record AtomEntry(string Name, SmtFormula Atom);

/// <summary>
/// Interns the distinct theory atoms of a formula and assigns each a fresh
/// propositional variable name. Equalities are canonicalized (sides ordered) so
/// <c>s=t</c> and <c>t=s</c> share one variable; a syntactic <c>t=t</c> is
/// folded to true and gets no variable.
/// </summary>
internal sealed class AtomTable
{
    private readonly Dictionary<string, int> _byKey = new(StringComparer.Ordinal);
    private readonly List<AtomEntry> _entries = new();

    public IReadOnlyList<AtomEntry> Entries => _entries;

    public IEnumerable<string> Names => _entries.Select(e => e.Name);

    public AtomEntry this[int atomId] => _entries[atomId];

    public Formula EqualityVar(Term left, Term right)
    {
        var leftKey = left.ToString();
        var rightKey = right.ToString();
        var (a, b) = string.CompareOrdinal(leftKey, rightKey) <= 0 ? (left, right) : (right, left);
        var key = $"eq|{a}|{b}";
        return Formula.Var(GetOrAdd(key, name => new AtomEntry(name, new EqualityAtom(a, b))));
    }

    public Formula PredicateVar(PredicateAtom predicate)
    {
        var key = $"pred|{new Term(predicate.Symbol, predicate.Arguments)}";
        return Formula.Var(GetOrAdd(key, name => new AtomEntry(name, predicate)));
    }

    public Formula ArithmeticVar(LinearConstraintAtom atom)
    {
        var key = $"lin|{atom}";
        return Formula.Var(GetOrAdd(key, name => new AtomEntry(name, atom)));
    }

    private string GetOrAdd(string key, Func<string, AtomEntry> factory)
    {
        if (_byKey.TryGetValue(key, out var index))
        {
            return _entries[index].Name;
        }
        index = _entries.Count;
        var name = "@a" + index;
        _entries.Add(factory(name));
        _byKey[key] = index;
        return name;
    }
}

/// <summary>
/// Builds the propositional skeleton of an <see cref="SmtFormula"/>: each theory
/// atom is replaced by its propositional variable, leaving the boolean structure
/// as an ordinary <see cref="Formula"/>.
/// </summary>
internal static class Abstraction
{
    public static (Formula Skeleton, AtomTable Atoms) Abstract(SmtFormula formula)
    {
        var atoms = new AtomTable();
        return (Build(formula, atoms), atoms);
    }

    private static Formula Build(SmtFormula formula, AtomTable atoms) => formula switch
    {
        SmtBool b => Formula.Const(b.Value),
        EqualityAtom e => e.Left.Equals(e.Right) ? Formula.True : atoms.EqualityVar(e.Left, e.Right),
        PredicateAtom p => atoms.PredicateVar(p),
        LinearConstraintAtom lc => atoms.ArithmeticVar(lc),
        SmtNot n => new Negation(Build(n.Operand, atoms)),
        SmtAnd a => new Conjunction(Build(a.Left, atoms), Build(a.Right, atoms)),
        SmtOr o => new Disjunction(Build(o.Left, atoms), Build(o.Right, atoms)),
        SmtImplies i => new Implication(Build(i.Antecedent, atoms), Build(i.Consequent, atoms)),
        SmtIff bi => new Biconditional(Build(bi.Left, atoms), Build(bi.Right, atoms)),
        _ => throw new InvalidOperationException($"Unknown SMT node: {formula.GetType().Name}"),
    };
}
