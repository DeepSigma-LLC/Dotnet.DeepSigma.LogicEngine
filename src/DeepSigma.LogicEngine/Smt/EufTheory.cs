namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// The EUF theory (equality with uninterpreted functions and predicates) as an
/// <see cref="ITheory"/>: each asserted equality/predicate atom is fed to a
/// fresh <see cref="CongruenceClosure"/>, which decides consistency and returns
/// a minimal conflict core.
/// </summary>
internal sealed class EufTheory : ITheory
{
    public IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var closure = new CongruenceClosure();
        for (var i = 0; i < asserted.Count; i++)
        {
            closure.Assert(ToLiteral(asserted[i].Atom, asserted[i].Value, i));
        }
        return closure.FindConflict();
    }

    private static EufLiteral ToLiteral(SmtFormula atom, bool positive, int atomId) => atom switch
    {
        EqualityAtom e => new EufLiteral(atomId, positive, EufAtomKind.Equality, e.Left, e.Right),
        PredicateAtom p => new EufLiteral(atomId, positive, EufAtomKind.Predicate,
            new Term(p.Symbol, p.Arguments), new Term(p.Symbol, p.Arguments)),
        _ => throw new ArgumentException($"Not an EUF theory atom: {atom}"),
    };
}
