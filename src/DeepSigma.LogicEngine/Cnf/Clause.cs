using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// A disjunction of literals. An empty clause is the empty disjunction, i.e. false.
/// </summary>
public sealed class Clause : IEquatable<Clause>
{
    private readonly Literal[] _literals;

    /// <summary>The literals of the clause, deduplicated and in canonical order.</summary>
    public IReadOnlyList<Literal> Literals => _literals;
    /// <summary>True if the clause has no literals (the empty clause, i.e. false).</summary>
    public bool IsEmpty => _literals.Length == 0;
    /// <summary>True if the clause has exactly one literal.</summary>
    public bool IsUnit => _literals.Length == 1;

    /// <summary>Creates a clause from the given literals, deduplicating and canonically ordering them.</summary>
    public Clause(IEnumerable<Literal> literals)
    {
        _literals = literals.Distinct().OrderBy(l => l.Variable, StringComparer.Ordinal)
            .ThenBy(l => l.Negated).ToArray();
    }

    /// <summary>The empty clause (the empty disjunction, i.e. false).</summary>
    public static Clause Empty { get; } = new(Array.Empty<Literal>());

    /// <summary>Creates a clause from the given literals.</summary>
    public static Clause Of(params Literal[] literals) => new(literals);

    /// <summary>True if the clause contains both x and !x for some variable.</summary>
    public bool IsTautology()
    {
        var seenPos = new HashSet<string>(StringComparer.Ordinal);
        var seenNeg = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lit in _literals)
        {
            if (lit.Negated ? seenPos.Contains(lit.Variable) : seenNeg.Contains(lit.Variable))
            {
                return true;
            }
            (lit.Negated ? seenNeg : seenPos).Add(lit.Variable);
        }
        return false;
    }

    /// <summary>Converts the clause to a propositional formula (a disjunction of its literals, or false if empty).</summary>
    public Formula ToFormula()
    {
        if (_literals.Length == 0)
        {
            return Formula.False;
        }
        return Formula.Any(_literals.Select(LiteralToFormula));
    }

    private static Formula LiteralToFormula(Literal lit)
        => lit.Negated ? new Negation(new Variable(lit.Variable)) : new Variable(lit.Variable);

    /// <summary>Determines whether another clause has the same literals.</summary>
    public bool Equals(Clause? other)
    {
        if (other is null || other._literals.Length != _literals.Length)
        {
            return false;
        }
        for (var i = 0; i < _literals.Length; i++)
        {
            if (!_literals[i].Equals(other._literals[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Determines whether the given object is an equal clause.</summary>
    public override bool Equals(object? obj) => obj is Clause c && Equals(c);

    /// <summary>Returns a hash code over the clause's literals.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var lit in _literals)
        {
            hash.Add(lit);
        }
        return hash.ToHashCode();
    }

    /// <summary>Renders the clause as <c>[]</c> if empty or <c>(l1 | l2 | ...)</c> otherwise.</summary>
    public override string ToString()
    {
        if (_literals.Length == 0)
        {
            return "[]";
        }
        return "(" + string.Join(" | ", _literals.Select(l => l.ToString())) + ")";
    }
}
