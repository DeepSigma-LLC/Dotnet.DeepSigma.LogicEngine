namespace DeepSigma.LogicEngine.Common;

/// <summary>
/// Folds a list into a <b>balanced</b> (depth O(log n)) binary tree with an associative
/// combiner. Balancing matters because the recursive normal-form transforms and
/// evaluators that consume these trees would overflow the stack on a left-nested
/// (depth O(n)) chain built from a large clause set. Returns the single element for a
/// one-element list, or <c>null</c> for an empty one.
/// </summary>
internal static class BalancedFold
{
    public static T? Combine<T>(IReadOnlyList<T> items, Func<T, T, T> combine) where T : class
    {
        if (items.Count == 0)
        {
            return null;
        }
        T Fold(int lo, int hi)
        {
            if (hi - lo == 1)
            {
                return items[lo];
            }
            var mid = lo + (hi - lo) / 2;
            return combine(Fold(lo, mid), Fold(mid, hi));
        }
        return Fold(0, items.Count);
    }
}
