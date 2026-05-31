using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Shared conversions from abstracted <see cref="SmtFormula"/> theory atoms to the
/// representations the concrete theory solvers consume. The mapping is identical
/// wherever it appears, so it lives here once: <see cref="EufTheory"/> and
/// <see cref="CombinedTheory"/> share <see cref="ToEufLiteral"/>; the linear theories
/// (<see cref="LraTheory"/>, <see cref="LiaTheory"/>, <see cref="CombinedTheory"/>)
/// share <see cref="LinearTerms"/>/<see cref="Polarized"/>. Each theory keeps its own
/// "not my kind of atom" guard text and its own solver-specific add call.
/// </summary>
internal static class TheoryAtoms
{
    /// <summary>Maps an EUF atom (equality or predicate) and its polarity to an <see cref="EufLiteral"/>.</summary>
    public static EufLiteral ToEufLiteral(SmtFormula atom, bool positive, int atomId) => atom switch
    {
        EqualityAtom e => new EufLiteral(atomId, positive, EufAtomKind.Equality, e.Left, e.Right),
        PredicateAtom p => new EufLiteral(atomId, positive, EufAtomKind.Predicate,
            new Term(p.Symbol, p.Arguments), new Term(p.Symbol, p.Arguments)),
        _ => throw new ArgumentException($"Not an EUF theory atom: {atom}"),
    };

    /// <summary>The atom's linear terms as the <c>(variable, coefficient)</c> tuples the feasibility solver expects.</summary>
    public static (string Variable, Rational Coefficient)[] LinearTerms(LinearConstraintAtom atom)
        => atom.Terms.Select(t => (t.Variable, t.Coefficient)).ToArray();

    /// <summary>The atom's relation under the asserted polarity (negated when the atom is asserted false).</summary>
    public static LinearRelation Polarized(LinearConstraintAtom atom, bool value)
        => value ? atom.Relation : atom.Relation.Negate();
}
