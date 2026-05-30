namespace DeepSigma.LogicEngine.Common;

/// <summary>
/// Structural equality/hashing for symbol-plus-argument-list nodes (terms, predicate
/// atoms). Records compare reference-typed list members by reference, so the
/// argument-bearing AST nodes (Smt <c>Term</c>/<c>PredicateAtom</c>, FOL
/// <c>FolFunc</c>/<c>FolPredicate</c>) share these helpers instead of each hand-rolling
/// the same element-wise loop.
/// </summary>
internal static class StructuralEquality
{
    /// <summary>Element-wise equality of two lists (using each element's <see cref="object.Equals(object)"/>).</summary>
    public static bool ListEquals<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) where T : notnull
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>A hash combining an ordinal symbol with its argument list.</summary>
    public static int Hash<T>(string symbol, IReadOnlyList<T> arguments)
    {
        var hash = new HashCode();
        hash.Add(symbol, StringComparer.Ordinal);
        hash.Add(arguments.Count);
        foreach (var argument in arguments)
        {
            hash.Add(argument);
        }
        return hash.ToHashCode();
    }
}
