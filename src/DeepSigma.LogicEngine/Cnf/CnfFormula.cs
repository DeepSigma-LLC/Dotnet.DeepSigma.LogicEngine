using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// A conjunction of clauses. An empty conjunction is the empty CNF, i.e. true.
/// A CNF containing the empty clause is unsatisfiable.
/// </summary>
public sealed class CnfFormula
{
    public IReadOnlyList<Clause> Clauses { get; }

    public CnfFormula(IEnumerable<Clause> clauses)
    {
        Clauses = clauses.ToArray();
    }

    public static CnfFormula True { get; } = new(Array.Empty<Clause>());
    public static CnfFormula False { get; } = new(new[] { Clause.Empty });

    public bool HasEmptyClause => Clauses.Any(c => c.IsEmpty);

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

    public Formula ToFormula()
    {
        if (Clauses.Count == 0)
        {
            return Formula.True;
        }
        return Formula.All(Clauses.Select(c => c.ToFormula()));
    }

    public override string ToString()
    {
        if (Clauses.Count == 0)
        {
            return "true";
        }
        return string.Join(" & ", Clauses.Select(c => c.ToString()));
    }
}
