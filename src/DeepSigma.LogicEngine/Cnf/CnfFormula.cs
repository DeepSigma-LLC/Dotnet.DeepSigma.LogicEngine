using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// A conjunction of clauses. An empty conjunction is the empty CNF, i.e. true.
/// A CNF containing the empty clause is unsatisfiable.
/// </summary>
public sealed class CnfFormula
{
    /// <summary>The clauses whose conjunction forms this CNF.</summary>
    public IReadOnlyList<Clause> Clauses { get; }

    /// <summary>Creates a CNF formula from the given clauses.</summary>
    public CnfFormula(IEnumerable<Clause> clauses)
    {
        Clauses = clauses.ToArray();
    }

    /// <summary>The trivially true CNF (the empty conjunction).</summary>
    public static CnfFormula True { get; } = new(Array.Empty<Clause>());
    /// <summary>The trivially false CNF (containing the empty clause).</summary>
    public static CnfFormula False { get; } = new(new[] { Clause.Empty });

    /// <summary>True if any clause is the empty clause, making the CNF unsatisfiable.</summary>
    public bool HasEmptyClause => Clauses.Any(c => c.IsEmpty);

    /// <summary>Returns the set of variable names occurring in the CNF.</summary>
    public IReadOnlySet<string> Variables()
    {
        var vars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clause in Clauses)
        {
            foreach (var lit in clause.Literals)
            {
                vars.Add(lit.Variable);
            }
        }
        return vars;
    }

    /// <summary>Converts the CNF to a propositional formula (a conjunction of its clauses, or true if empty).</summary>
    public Formula ToFormula()
    {
        if (Clauses.Count == 0)
        {
            return Formula.True;
        }
        return Formula.All(Clauses.Select(c => c.ToFormula()));
    }

    /// <summary>Renders the CNF as <c>true</c> if empty or its clauses joined by <c>&amp;</c> otherwise.</summary>
    public override string ToString()
    {
        if (Clauses.Count == 0)
        {
            return "true";
        }
        return string.Join(" & ", Clauses.Select(c => c.ToString()));
    }
}
