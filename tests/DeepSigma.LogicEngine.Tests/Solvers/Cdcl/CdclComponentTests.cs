using DeepSigma.LogicEngine.Solvers.Cdcl;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers.Cdcl;

public class CdclLiteralsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(7, false)]
    [InlineData(7, true)]
    public void Encode_Decode_RoundTrips(int variable, bool negated)
    {
        var lit = CdclLiterals.Make(variable, negated);
        Assert.Equal(variable, CdclLiterals.Variable(lit));
        Assert.Equal(negated, CdclLiterals.IsNegated(lit));
        Assert.Equal(lit, CdclLiterals.Negate(CdclLiterals.Negate(lit)));
        Assert.Equal(variable, CdclLiterals.Variable(CdclLiterals.Negate(lit)));
    }
}

public class LubyRestartScheduleTests
{
    [Fact]
    public void Sequence_MatchesKnownPrefix()
    {
        // 1,1,2,1,1,2,4,1,1,2,1,1,2,4,8
        int[] expected = { 1, 1, 2, 1, 1, 2, 4, 1, 1, 2, 1, 1, 2, 4, 8 };
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], LubyRestartSchedule.Luby(i + 1));
        }
    }

    [Fact]
    public void Budget_TracksSequenceTimesUnit()
    {
        var schedule = new LubyRestartSchedule(unit: 10);
        Assert.Equal(10, schedule.CurrentBudget); // luby(1)=1
        schedule.Advance();
        Assert.Equal(10, schedule.CurrentBudget); // luby(2)=1
        schedule.Advance();
        Assert.Equal(20, schedule.CurrentBudget); // luby(3)=2
    }
}

public class VsidsHeapTests
{
    [Fact]
    public void RemoveMax_ReturnsHighestActivityFirst()
    {
        var heap = new VsidsHeap(variableCount: 5, decay: 0.95);
        heap.Bump(2);
        heap.Bump(2);
        heap.Bump(4);
        // var 2 has the highest activity, var 4 next, the rest zero.
        Assert.Equal(2, heap.RemoveMax());
        Assert.Equal(4, heap.RemoveMax());
    }

    [Fact]
    public void Bump_AfterRemoval_DoesNotResurrect()
    {
        var heap = new VsidsHeap(variableCount: 3, decay: 0.95);
        var first = heap.RemoveMax();
        var second = heap.RemoveMax();
        var third = heap.RemoveMax();
        Assert.True(heap.IsEmpty);
        var all = new[] { first, second, third };
        Assert.Equal(new[] { 0, 1, 2 }, all.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void InsertIfAbsent_RestoresRemovedVariable()
    {
        var heap = new VsidsHeap(variableCount: 3, decay: 0.95);
        var removed = heap.RemoveMax();
        heap.InsertIfAbsent(removed);
        Assert.False(heap.IsEmpty);
        // It can be removed again.
        var again = heap.RemoveMax();
        Assert.Contains(again, new[] { 0, 1, 2 });
    }
}

public class TrailTests
{
    [Fact]
    public void Enqueue_RecordsValueLevelAndReason()
    {
        var trail = new Trail(variableCount: 3);
        var x0 = CdclLiterals.Positive(0);
        Assert.True(trail.Enqueue(x0, reason: null));
        Assert.Equal(LBool.True, trail.Value(0));
        Assert.Equal(0, trail.LevelOf(0));
        Assert.Equal(LBool.True, trail.LiteralValue(x0));
        Assert.Equal(LBool.False, trail.LiteralValue(CdclLiterals.Negate(x0)));
    }

    [Fact]
    public void Enqueue_ConflictingValue_ReturnsFalse()
    {
        var trail = new Trail(variableCount: 2);
        Assert.True(trail.Enqueue(CdclLiterals.Positive(0), null));
        Assert.False(trail.Enqueue(CdclLiterals.Negative(0), null));
    }

    [Fact]
    public void CancelUntil_UnassignsAboveLevelAndInvokesCallback()
    {
        var trail = new Trail(variableCount: 4);
        trail.Enqueue(CdclLiterals.Positive(0), null); // level 0

        trail.Decide(CdclLiterals.Positive(1));         // level 1
        trail.Enqueue(CdclLiterals.Positive(2), null);  // implied at level 1

        trail.Decide(CdclLiterals.Positive(3));         // level 2

        Assert.Equal(2, trail.DecisionLevel);

        var restored = new List<int>();
        trail.CancelUntil(1, restored.Add);

        Assert.Equal(1, trail.DecisionLevel);
        Assert.Equal(LBool.Unassigned, trail.Value(3));   // level 2 undone
        Assert.Equal(LBool.True, trail.Value(2));         // level 1 kept
        Assert.Equal(LBool.True, trail.Value(0));         // level 0 kept
        Assert.Contains(3, restored);
    }

    [Fact]
    public void SavedPhase_SurvivesBacktrack()
    {
        var trail = new Trail(variableCount: 2);
        trail.Decide(CdclLiterals.Positive(0)); // assign var 0 = true
        Assert.True(trail.SavedPhase(0));
        trail.CancelUntil(0, _ => { });
        Assert.Equal(LBool.Unassigned, trail.Value(0));
        Assert.True(trail.SavedPhase(0)); // phase remembered
    }
}
