using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// The theory of linear real arithmetic (LRA) as an <see cref="ITheory"/>. Maps
/// each asserted linear-constraint atom (folding polarity) to a tagged
/// constraint in an exact-rational <see cref="ExactLinearFeasibilitySolver"/>,
/// and returns the infeasible core (by atom index) when the conjunction has no
/// solution over the rationals.
/// </summary>
internal sealed class LraTheory : ITheory
{
    public IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var solver = new ExactLinearFeasibilitySolver();
        for (var i = 0; i < asserted.Count; i++)
        {
            if (asserted[i].Atom is not LinearConstraintAtom atom)
            {
                throw new ArgumentException($"Not an LRA theory atom: {asserted[i].Atom}");
            }
            var relation = asserted[i].Value ? atom.Relation : Negate(atom.Relation);
            var terms = atom.Terms.Select(t => (t.Variable, t.Coefficient)).ToArray();
            solver.AddConstraint(i, terms, relation, atom.Constant);
        }
        return solver.FindConflict();
    }

    private static LinearRelation Negate(LinearRelation relation) => relation switch
    {
        LinearRelation.LessOrEqual => LinearRelation.Greater,
        LinearRelation.Less => LinearRelation.GreaterOrEqual,
        LinearRelation.GreaterOrEqual => LinearRelation.Less,
        LinearRelation.Greater => LinearRelation.LessOrEqual,
        _ => throw new NotSupportedException(
            "Negated equality is not supported; express equalities as a conjunction of <= and >=."),
    };
}
