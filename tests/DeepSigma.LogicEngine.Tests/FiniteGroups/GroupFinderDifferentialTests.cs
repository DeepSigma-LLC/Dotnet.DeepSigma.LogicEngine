using DeepSigma.LogicEngine.FiniteGroups;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.FiniteGroups;

public class GroupFinderDifferentialTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SatFinder_AgreesWithBruteForceEnumeration(int order)
    {
        var satClasses = GroupFinder.GroupsUpToIsomorphism(order)
            .Select(g => g.CanonicalKey())
            .ToHashSet(StringComparer.Ordinal);
        var bruteForceClasses = GroupBruteForce.IsomorphismClasses(order);

        Assert.Equal(bruteForceClasses, satClasses);
    }

    [Fact]
    public void SatFinder_OnlyReturnsGroups()
    {
        foreach (var order in new[] { 1, 2, 3, 4, 5, 6 })
        {
            foreach (var group in GroupFinder.GroupsUpToIsomorphism(order))
            {
                Assert.True(group.IsGroup);
            }
        }
    }
}

/// <summary>
/// Independent brute-force enumerator: fill every Cayley table with the identity
/// fixed at element 0, keep the ones that are groups, and deduplicate by canonical
/// form. Feasible only for very small orders (n^((n−1)²) candidate tables); used
/// to differentially validate the SAT finder's isomorphism counts.
/// </summary>
internal static class GroupBruteForce
{
    public static HashSet<string> IsomorphismClasses(int n)
    {
        var classes = new HashSet<string>(StringComparer.Ordinal);
        var freeCells = new List<(int Row, int Col)>();
        for (var i = 1; i < n; i++)
        {
            for (var j = 1; j < n; j++)
            {
                freeCells.Add((i, j));
            }
        }

        var total = (long)Math.Pow(n, freeCells.Count);
        for (long code = 0; code < total; code++)
        {
            var table = new int[n, n];
            for (var i = 0; i < n; i++)
            {
                table[0, i] = i; // identity row
                table[i, 0] = i; // identity column
            }
            var rest = code;
            foreach (var (row, col) in freeCells)
            {
                table[row, col] = (int)(rest % n);
                rest /= n;
            }

            var candidate = GroupTable.FromTable(table);
            if (candidate.IsGroup)
            {
                classes.Add(candidate.CanonicalKey());
            }
        }
        return classes;
    }
}
