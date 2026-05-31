namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// The EUF theory (equality with uninterpreted functions and predicates) as an
/// <see cref="ITheory"/>: each asserted equality/predicate atom is fed to a
/// fresh <see cref="CongruenceClosure"/>, which decides consistency and returns
/// a conflict core. The raw congruence-closure core is run through
/// <see cref="ConflictMinimizer"/> so the reported core is 1-minimal (matching
/// the LIA and combined theories), which lets the DPLL(T) loop learn a stronger
/// blocking clause.
/// </summary>
internal sealed class EufTheory : ITheory
{
    public IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
        => ConflictMinimizer.Minimize(asserted, RawCheck);

    private static IReadOnlySet<int>? RawCheck(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var closure = new CongruenceClosure();
        for (var i = 0; i < asserted.Count; i++)
        {
            closure.Assert(TheoryAtoms.ToEufLiteral(asserted[i].Atom, asserted[i].Value, i));
        }
        return closure.FindConflict();
    }
}
