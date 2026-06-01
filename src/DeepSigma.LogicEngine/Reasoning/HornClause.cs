using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>
/// A propositional Horn clause. Equivalent to <c>Antecedents → Consequent</c>
/// when the consequent is present, or to the negative constraint
/// <c>¬(a₁ ∧ a₂ ∧ … ∧ aₙ)</c> when it is null.
/// A clause with no antecedents and a non-null consequent is a fact.
/// </summary>
public sealed record HornClause(IReadOnlyList<string> Antecedents, string? Consequent)
{
    /// <summary>True when the clause has a consequent and no antecedents (a fact).</summary>
    public bool IsFact => Antecedents.Count == 0 && Consequent is not null;

    /// <summary>True when the clause has no consequent (a negative/goal clause).</summary>
    public bool IsGoalClause => Consequent is null;

    /// <summary>True when the clause has a (single positive) consequent.</summary>
    public bool IsDefinite => Consequent is not null;

    /// <summary>Renders the Horn clause in its <c>body =&gt; head</c> form.</summary>
    public override string ToString()
    {
        if (IsFact)
        {
            return Consequent!;
        }
        var lhs = Antecedents.Count == 0 ? "true" : string.Join(" & ", Antecedents);
        return Consequent is null ? $"!({lhs})" : $"{lhs} -> {Consequent}";
    }
}

/// <summary>Converts a propositional formula's CNF into Horn clauses when possible.</summary>
public static class HornConverter
{
    /// <summary>
    /// Try to express the formula as a set of propositional Horn clauses.
    /// Returns false when at least one CNF clause has more than one positive
    /// literal; the formula is not Horn in that case.
    /// </summary>
    public static bool TryConvert(Formula formula, out IReadOnlyList<HornClause> clauses)
    {
        var cnf = CnfTransformer.ToCnf(formula);
        var result = new List<HornClause>();
        foreach (var clause in cnf.Clauses)
        {
            if (clause.IsTautology() || clause.IsEmpty)
            {
                if (clause.IsEmpty)
                {
                    result.Add(new HornClause(Array.Empty<string>(), null));
                }
                continue;
            }
            var positives = new List<string>();
            var negatives = new List<string>();
            foreach (var lit in clause.Literals)
            {
                if (lit.Negated)
                {
                    negatives.Add(lit.Variable);
                }
                else
                {
                    positives.Add(lit.Variable);
                }
            }
            if (positives.Count > 1)
            {
                clauses = Array.Empty<HornClause>();
                return false;
            }
            result.Add(new HornClause(negatives, positives.Count == 1 ? positives[0] : null));
        }
        clauses = result;
        return true;
    }
}
