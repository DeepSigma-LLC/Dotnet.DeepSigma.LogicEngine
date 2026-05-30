using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.FiniteGroups;

/// <summary>
/// Finds finite groups by SAT: it encodes the group axioms over a one-hot Cayley
/// table, solves with the propositional engine, and decodes solutions into
/// <see cref="GroupTable"/>s. It can decide existence, exhibit a group, enumerate
/// the labeled groups, and count groups up to isomorphism for small orders.
///
/// <para>
/// Cost scales with the O(n⁶) associativity encoding: existence/find are practical
/// to roughly order 10; counting up to isomorphism enumerates all labeled groups
/// (then deduplicates by canonical form), which is practical to about order 8.
/// </para>
/// </summary>
public static class GroupFinder
{
    /// <summary>True if a group of the given order (meeting <paramref name="spec"/>) exists.</summary>
    public static bool ExistsGroup(int order, GroupSpec? spec = null)
    {
        if (spec?.HasPostFilter == true)
        {
            return FindGroup(order, spec) is not null;
        }
        return Reasoner.IsSatisfiable(GroupSatEncoder.Encode(order, spec));
    }

    /// <summary>A group of the given order meeting <paramref name="spec"/>, or null if none exists.</summary>
    public static GroupTable? FindGroup(int order, GroupSpec? spec = null)
    {
        var model = Reasoner.FindModel(GroupSatEncoder.Encode(order, spec));
        if (model is null)
        {
            return null;
        }
        var table = GroupModelDecoder.Decode(model, order);
        if (spec is null || PostFilterMatches(table, spec))
        {
            return table;
        }
        // The first model failed an order-based requirement; search the rest.
        foreach (var candidate in EnumerateGroups(order, spec))
        {
            return candidate;
        }
        return null;
    }

    /// <summary>Every <b>labeled</b> group of the given order meeting <paramref name="spec"/> (identity fixed at 0).</summary>
    public static IEnumerable<GroupTable> EnumerateGroups(int order, GroupSpec? spec = null)
    {
        foreach (var model in Reasoner.EnumerateModels(GroupSatEncoder.Encode(order, spec)))
        {
            var table = GroupModelDecoder.Decode(model, order);
            if (spec is null || PostFilterMatches(table, spec))
            {
                yield return table;
            }
        }
    }

    /// <summary>One representative group per isomorphism class of the given order.</summary>
    public static IReadOnlyList<GroupTable> GroupsUpToIsomorphism(int order, GroupSpec? spec = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var representatives = new List<GroupTable>();
        foreach (var table in EnumerateGroups(order, spec))
        {
            if (seen.Add(table.CanonicalKey()))
            {
                representatives.Add(table);
            }
        }
        return representatives;
    }

    /// <summary>The number of groups of the given order up to isomorphism.</summary>
    public static int CountGroupsUpToIsomorphism(int order, GroupSpec? spec = null)
        => GroupsUpToIsomorphism(order, spec).Count;

    private static bool PostFilterMatches(GroupTable table, GroupSpec spec)
    {
        if (spec.Abelian is bool abelian && table.IsAbelian != abelian)
        {
            return false;
        }
        if (spec.Cyclic is bool cyclic && table.IsCyclic != cyclic)
        {
            return false;
        }
        if (spec.Exponent is int exponent && table.Exponent != exponent)
        {
            return false;
        }
        if (spec.RequiredElementOrders is { } required)
        {
            var orders = table.ElementOrders();
            if (required.Any(d => !orders.Contains(d)))
            {
                return false;
            }
        }
        return true;
    }
}
