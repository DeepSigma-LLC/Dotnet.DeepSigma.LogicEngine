namespace DeepSigma.LogicEngine.Common;

/// <summary>
/// Incremental bounded search: try successive bounds <c>n = from..to</c> and return the first
/// that succeeds. Captures the "increase the bound until a witness appears or the cap is reached"
/// loop shared by the SAT-encoded bounded procedures (modal Kripke-model construction, LTL
/// bounded model checking), which would otherwise hand-roll the same loop in every overload.
/// </summary>
internal static class BoundedSearch
{
    /// <summary>The first <c>attempt(n)</c> (n = <paramref name="from"/>..<paramref name="to"/>) that returns non-null, or null if none does.</summary>
    public static TResult? FirstNonNull<TResult>(int from, int to, Func<int, TResult?> attempt)
        where TResult : class
    {
        for (var n = from; n <= to; n++)
        {
            if (attempt(n) is { } result)
            {
                return result;
            }
        }
        return null;
    }

    /// <summary>True if <c>predicate(n)</c> holds for some <c>n = <paramref name="from"/>..<paramref name="to"/></c>.</summary>
    public static bool Any(int from, int to, Func<int, bool> predicate)
    {
        for (var n = from; n <= to; n++)
        {
            if (predicate(n))
            {
                return true;
            }
        }
        return false;
    }
}
