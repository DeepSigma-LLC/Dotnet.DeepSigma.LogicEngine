namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Shrinks a theory conflict to a 1-minimal core by deletion: starting from a raw
/// conflict, drop one asserted literal at a time and re-check; keep the literal only
/// if removing it makes the subset consistent. Sound for any theory whose raw check
/// returns conflict-or-null. A smaller core lets the DPLL(T) loop learn a stronger
/// blocking clause and converge faster.
/// </summary>
internal static class ConflictMinimizer
{
    public static IReadOnlySet<int>? Minimize(
        IReadOnlyList<(SmtFormula Atom, bool Value)> asserted,
        Func<IReadOnlyList<(SmtFormula Atom, bool Value)>, IReadOnlySet<int>?> rawCheck)
    {
        var core = rawCheck(asserted);
        if (core is null)
        {
            return null;
        }

        var kept = core.OrderBy(i => i).ToList(); // original indices
        var index = 0;
        while (index < kept.Count)
        {
            var candidate = new List<int>(kept);
            candidate.RemoveAt(index);
            var subset = candidate.Select(i => asserted[i]).ToList();
            if (rawCheck(subset) is not null)
            {
                kept = candidate; // still inconsistent without this literal — drop it
            }
            else
            {
                index++; // needed — keep it
            }
        }
        return kept.ToHashSet();
    }
}
