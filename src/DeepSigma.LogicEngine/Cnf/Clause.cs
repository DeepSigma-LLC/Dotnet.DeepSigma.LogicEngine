using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// A disjunction of literals. An empty clause is the empty disjunction, i.e. false.
/// </summary>
public sealed class Clause : IEquatable<Clause>
{
    private readonly Literal[] _literals;

    public IReadOnlyList<Literal> Literals => _literals;
    public bool IsEmpty => _literals.Length == 0;
    public bool IsUnit => _literals.Length == 1;

    public Clause(IEnumerable<Literal> literals)
    {
        _literals = literals.Distinct().OrderBy(l => l.Variable, StringComparer.Ordinal)
            .ThenBy(l => l.Negated).ToArray();
    }

    public static Clause Empty { get; } = new(Array.Empty<Literal>());

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

    public override bool Equals(object? obj) => obj is Clause c && Equals(c);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var lit in _literals)
        {
            hash.Add(lit);
        }
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        if (_literals.Length == 0)
        {
            return "[]";
        }
        return "(" + string.Join(" | ", _literals.Select(l => l.ToString())) + ")";
    }
}
