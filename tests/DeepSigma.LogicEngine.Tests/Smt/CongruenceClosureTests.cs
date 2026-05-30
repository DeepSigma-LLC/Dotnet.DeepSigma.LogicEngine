using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class CongruenceClosureTests
{
    private static Term C(string s) => Term.Constant(s);
    private static Term F(string s, params Term[] args) => Term.Func(s, args);

    private static EufLiteral Eq(int id, Term l, Term r, bool positive = true)
        => new(id, positive, EufAtomKind.Equality, l, r);

    private static EufLiteral Pred(int id, Term app, bool positive)
        => new(id, positive, EufAtomKind.Predicate, app, app);

    [Fact]
    public void Congruence_EqualArgsImplyEqualApplications()
    {
        // a = b ∧ f(a) != f(b) is unsatisfiable.
        var cc = new CongruenceClosure();
        cc.Assert(Eq(0, C("a"), C("b")));
        cc.Assert(Eq(1, F("f", C("a")), F("f", C("b")), positive: false));

        var conflict = cc.FindConflict();
        Assert.NotNull(conflict);
        Assert.Contains(0, conflict!);
        Assert.Contains(1, conflict!);
    }

    [Fact]
    public void Transitivity_Conflict()
    {
        // a = b ∧ b = c ∧ a != c is unsatisfiable.
        var cc = new CongruenceClosure();
        cc.Assert(Eq(0, C("a"), C("b")));
        cc.Assert(Eq(1, C("b"), C("c")));
        cc.Assert(Eq(2, C("a"), C("c"), positive: false));

        var conflict = cc.FindConflict();
        Assert.NotNull(conflict);
        Assert.Equal(new[] { 0, 1, 2 }, conflict!.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void NestedCongruence_Conflict()
    {
        // a = b ∧ f(f(a)) != f(f(b)) is unsatisfiable.
        var cc = new CongruenceClosure();
        cc.Assert(Eq(0, C("a"), C("b")));
        cc.Assert(Eq(1, F("f", F("f", C("a"))), F("f", F("f", C("b"))), positive: false));
        Assert.NotNull(cc.FindConflict());
    }

    [Fact]
    public void Predicate_CongruenceConflict()
    {
        // a = b ∧ P(a) ∧ ¬P(b) is unsatisfiable.
        var cc = new CongruenceClosure();
        cc.Assert(Eq(0, C("a"), C("b")));
        cc.Assert(Pred(1, F("P", C("a")), positive: true));
        cc.Assert(Pred(2, F("P", C("b")), positive: false));

        var conflict = cc.FindConflict();
        Assert.NotNull(conflict);
        Assert.Equal(new[] { 0, 1, 2 }, conflict!.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Consistent_NoConflict()
    {
        // a = b ∧ f(a) = f(b) ∧ c != d is satisfiable.
        var cc = new CongruenceClosure();
        cc.Assert(Eq(0, C("a"), C("b")));
        cc.Assert(Eq(1, F("f", C("a")), F("f", C("b"))));
        cc.Assert(Eq(2, C("c"), C("d"), positive: false));
        Assert.Null(cc.FindConflict());
    }

    [Fact]
    public void Predicate_ConsistentWhenArgsDiffer()
    {
        // P(a) ∧ ¬P(b) with no a=b is satisfiable.
        var cc = new CongruenceClosure();
        cc.Assert(Pred(0, F("P", C("a")), positive: true));
        cc.Assert(Pred(1, F("P", C("b")), positive: false));
        Assert.Null(cc.FindConflict());
    }

    [Fact]
    public void Conflict_Core_IsMinimal_ExcludesIrrelevantEqualities()
    {
        // a=b, b=c, plus unrelated d=e; a!=c. Core must be {0,1,3}, not include d=e (2).
        var cc = new CongruenceClosure();
        cc.Assert(Eq(0, C("a"), C("b")));
        cc.Assert(Eq(1, C("b"), C("c")));
        cc.Assert(Eq(2, C("d"), C("e")));            // irrelevant
        cc.Assert(Eq(3, C("a"), C("c"), positive: false));

        var conflict = cc.FindConflict();
        Assert.NotNull(conflict);
        Assert.Equal(new[] { 0, 1, 3 }, conflict!.OrderBy(x => x).ToArray());
        Assert.DoesNotContain(2, conflict!);
    }
}
