using System.Text;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A satisfying assignment for an <see cref="SmtFormula"/>: the truth value of
/// each theory atom in a model that is both propositionally and theory
/// consistent.
/// </summary>
public sealed class SmtModel
{
    private readonly IReadOnlyDictionary<SmtFormula, bool> _atoms;

    internal SmtModel(IReadOnlyDictionary<SmtFormula, bool> atoms) => _atoms = atoms;

    /// <summary>The atoms that hold (are true) in this model.</summary>
    public IEnumerable<SmtFormula> TrueAtoms => _atoms.Where(kv => kv.Value).Select(kv => kv.Key);

    /// <summary>The truth value assigned to an atom (false if the atom is unconstrained).</summary>
    public bool Holds(SmtFormula atom) => _atoms.TryGetValue(atom, out var value) && value;

    /// <summary>Renders the model as a sorted brace-delimited list of atom=T/F assignments.</summary>
    public override string ToString()
    {
        if (_atoms.Count == 0)
        {
            return "{}";
        }
        var sb = new StringBuilder("{ ");
        sb.AppendJoin(", ", _atoms.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={(kv.Value ? "T" : "F")}"));
        sb.Append(" }");
        return sb.ToString();
    }
}

/// <summary>Result of an EUF satisfiability query.</summary>
public sealed record SmtResult(bool IsSatisfiable, SmtModel? Model)
{
    /// <summary>The shared unsatisfiable result (no model).</summary>
    public static SmtResult Unsatisfiable { get; } = new(false, null);
    /// <summary>Creates a satisfiable result carrying the given model.</summary>
    public static SmtResult Satisfiable(SmtModel model) => new(true, model);
}
