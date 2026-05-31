using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Solvers;
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

    /// <summary>As <see cref="ExistsGroup(int, GroupSpec?)"/>, but solving the SAT encoding with the supplied engine (e.g. a Z3-backed <see cref="ISatSolver"/>).</summary>
    /// <param name="order">The group order to search at.</param>
    /// <param name="solver">The SAT engine to solve the group-axiom encoding with.</param>
    /// <param name="spec">Optional structural requirements.</param>
    public static bool ExistsGroup(int order, ISatSolver solver, GroupSpec? spec = null)
    {
        if (spec?.HasPostFilter == true)
        {
            return FindGroup(order, solver, spec) is not null;
        }
        return Reasoner.IsSatisfiable(GroupSatEncoder.Encode(order, spec), solver);
    }

    /// <summary>As <see cref="FindGroup(int, GroupSpec?)"/>, but solving with the supplied engine. (An order-based post-filter miss falls back to native enumeration.)</summary>
    /// <param name="order">The group order to search at.</param>
    /// <param name="solver">The SAT engine to solve with.</param>
    /// <param name="spec">Optional structural requirements.</param>
    public static GroupTable? FindGroup(int order, ISatSolver solver, GroupSpec? spec = null)
    {
        var model = Reasoner.FindModel(GroupSatEncoder.Encode(order, spec), solver);
        if (model is null)
        {
            return null;
        }
        var table = GroupModelDecoder.Decode(model, order);
        if (spec is null || PostFilterMatches(table, spec))
        {
            return table;
        }
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
        // Lex-leader pruning reduces the labeled models explored; canonical-key dedup
        // remains the exact-count guarantee. Enumerate over the product variables only
        // so the lex-leader auxiliary variables don't multiply the enumeration.
        var lexSpec = (spec ?? new GroupSpec()) with { UseLexLeader = true };
        var formula = GroupSatEncoder.Encode(order, lexSpec);
        var projection = ProductVariables(order);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var representatives = new List<GroupTable>();
        foreach (var model in Reasoner.EnumerateModels(formula, projection))
        {
            var table = GroupModelDecoder.Decode(model, order);
            if (spec is not null && !PostFilterMatches(table, spec))
            {
                continue;
            }
            if (seen.Add(table.CanonicalKey()))
            {
                representatives.Add(table);
            }
        }
        return representatives;
    }

    private static HashSet<string> ProductVariables(int order)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < order; i++)
        {
            for (var j = 0; j < order; j++)
            {
                for (var k = 0; k < order; k++)
                {
                    names.Add(GroupSatEncoder.VarName(i, j, k));
                }
            }
        }
        return names;
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
