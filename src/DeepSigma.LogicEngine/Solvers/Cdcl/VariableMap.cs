using DeepSigma.LogicEngine.Cnf;

namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// The sole boundary between the string-named world of <see cref="Cnf.Literal"/>
/// and the dense-integer world of the CDCL core. Interns variable names to
/// 0-based ids during ingestion and reverses the mapping when extracting a model.
/// </summary>
internal sealed class VariableMap
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly List<string> _names = new();

    public int Count => _names.Count;

    public int GetOrAdd(string name)
    {
        if (_ids.TryGetValue(name, out var id))
        {
            return id;
        }
        id = _names.Count;
        _ids[name] = id;
        _names.Add(name);
        return id;
    }

    public string NameOf(int variable) => _names[variable];

    public bool TryGetId(string name, out int id) => _ids.TryGetValue(name, out id);

    /// <summary>Encode a CNF literal to an integer literal, interning its variable.</summary>
    public int Encode(Literal literal) => CdclLiterals.Make(GetOrAdd(literal.Variable), literal.Negated);

    /// <summary>
    /// Build a variable map and the integer-encoded, tautology-free clause list
    /// for a CNF formula.
    /// </summary>
    public static (VariableMap Map, List<int[]> Clauses) Encode(CnfFormula formula)
    {
        var map = new VariableMap();
        var clauses = new List<int[]>(formula.Clauses.Count);
        foreach (var clause in formula.Clauses)
        {
            if (clause.IsTautology())
            {
                continue;
            }
            var literals = new int[clause.Literals.Count];
            for (var i = 0; i < literals.Length; i++)
            {
                literals[i] = map.Encode(clause.Literals[i]);
            }
            clauses.Add(literals);
        }
        return (map, clauses);
    }

    /// <summary>
    /// Decode a per-variable truth array (indexed by id) into a name→bool model.
    /// </summary>
    public Dictionary<string, bool> Decode(Func<int, bool> valueOf)
    {
        var model = new Dictionary<string, bool>(_names.Count, StringComparer.Ordinal);
        for (var v = 0; v < _names.Count; v++)
        {
            model[_names[v]] = valueOf(v);
        }
        return model;
    }
}
