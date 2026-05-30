namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A theory decision procedure for the lazy DPLL(T) loop. Given the theory atoms
/// asserted by a propositional model (each atom paired with its truth value),
/// it decides whether that conjunction is consistent in the theory, and on
/// inconsistency returns a small subset (by index into <c>asserted</c>) that is
/// already inconsistent — the conflict core that becomes a blocking clause.
/// </summary>
internal interface ITheory
{
    IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted);
}
