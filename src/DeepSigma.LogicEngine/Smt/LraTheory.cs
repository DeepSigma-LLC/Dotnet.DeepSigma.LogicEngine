using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// The theory of linear real arithmetic (LRA) as an <see cref="ITheory"/>. Maps
/// each asserted linear-constraint atom (folding polarity) to a tagged
/// constraint in an exact-rational <see cref="ExactLinearFeasibilitySolver"/>,
/// and returns the infeasible core (by atom index) when the conjunction has no
/// solution over the rationals. The raw simplex core is run through
/// <see cref="ConflictMinimizer"/> so the reported core is 1-minimal (matching
/// the LIA and combined theories).
/// </summary>
internal sealed class LraTheory : ITheory
{
    public IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
        => ConflictMinimizer.Minimize(asserted, RawCheck);

    private static IReadOnlySet<int>? RawCheck(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var solver = new ExactLinearFeasibilitySolver();
        for (var i = 0; i < asserted.Count; i++)
        {
            if (asserted[i].Atom is not LinearConstraintAtom atom)
            {
                throw new ArgumentException($"Not an LRA theory atom: {asserted[i].Atom}");
            }
            solver.AddConstraint(i, TheoryAtoms.LinearTerms(atom), TheoryAtoms.Polarized(atom, asserted[i].Value), atom.Constant);
        }
        return solver.FindConflict();
    }
}
