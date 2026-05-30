using DeepSigma.LogicEngine.FiniteGroups;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.FiniteGroups;

public class GroupFinderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void FindGroup_ReturnsAGroupOfTheRequestedOrder(int order)
    {
        var group = GroupFinder.FindGroup(order);
        Assert.NotNull(group);
        Assert.Equal(order, group!.Value.Order);
        Assert.True(group.Value.IsGroup);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    public void CountGroupsUpToIsomorphism_MatchesKnownSequence(int order, int expected)
    {
        Assert.Equal(expected, GroupFinder.CountGroupsUpToIsomorphism(order));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void PrimeOrderGroups_AreCyclic(int prime)
    {
        var group = GroupFinder.FindGroup(prime);
        Assert.NotNull(group);
        Assert.True(group!.Value.IsCyclic);
        Assert.True(group.Value.IsAbelian);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NoNonAbelianGroup_BelowOrderSix(int order)
    {
        Assert.False(GroupFinder.ExistsGroup(order, new GroupSpec { Abelian = false }));
    }

    [Fact]
    public void SmallestNonAbelianGroup_IsSymmetricGroupThree()
    {
        var group = GroupFinder.FindGroup(6, new GroupSpec { Abelian = false });
        Assert.NotNull(group);
        Assert.True(group!.Value.IsGroup);
        Assert.False(group.Value.IsAbelian);
        Assert.True(group.Value.IsIsomorphicTo(GroupTables.SymmetricGroup(3)));
    }

    [Fact]
    public void Spec_RequiresCyclicGroup()
    {
        // Order 4 has a cyclic group (ℤ₄) and a non-cyclic one (Klein four).
        Assert.True(GroupFinder.ExistsGroup(4, new GroupSpec { Cyclic = true }));
        Assert.True(GroupFinder.ExistsGroup(4, new GroupSpec { Cyclic = false }));
        var cyclic = GroupFinder.FindGroup(4, new GroupSpec { Cyclic = true });
        Assert.NotNull(cyclic);
        Assert.True(cyclic!.Value.IsCyclic);
    }

    [Fact]
    public void GroupsUpToIsomorphism_OrderFour_AreZ4AndKleinFour()
    {
        var groups = GroupFinder.GroupsUpToIsomorphism(4);
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.IsIsomorphicTo(GroupTables.CyclicGroup(4)));
        Assert.Contains(groups, g => g.IsIsomorphicTo(GroupTables.KleinFour()));
    }
}
