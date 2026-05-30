using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>One term <c>coefficient · variable</c> of a linear expression.</summary>
public readonly record struct LinearAtomTerm(string Variable, Rational Coefficient);

/// <summary>
/// A linear-arithmetic atom <c>Σ coefficientᵢ·variableᵢ ⋈ constant</c> over
/// rational-valued variables, for the LRA theory. Equalities are normally
/// expressed as a conjunction of <c>≤</c> and <c>≥</c> so that negation is
/// handled by the boolean structure.
/// </summary>
public sealed record LinearConstraintAtom(
    IReadOnlyList<LinearAtomTerm> Terms,
    LinearRelation Relation,
    Rational Constant) : SmtFormula
{
    public bool Equals(LinearConstraintAtom? other)
    {
        if (other is null || Relation != other.Relation || Constant != other.Constant)
        {
            return false;
        }
        if (Terms.Count != other.Terms.Count)
        {
            return false;
        }
        for (var i = 0; i < Terms.Count; i++)
        {
            if (!Terms[i].Equals(other.Terms[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Relation);
        hash.Add(Constant);
        foreach (var term in Terms)
        {
            hash.Add(term);
        }
        return hash.ToHashCode();
    }
}
