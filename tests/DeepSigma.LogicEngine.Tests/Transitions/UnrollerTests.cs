using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Transitions;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Transitions;

public class UnrollerTests
{
    [Fact]
    public void ToggleSystem_AlternatesAcrossSteps()
    {
        // State x; init: !x; transition: x' <-> !x  (x toggles each step).
        var system = new TransitionSystem(
            new[] { "x" },
            Initial: new Negation(Formula.Var("x")),
            Transition: new Biconditional(Formula.Var("x'"), new Negation(Formula.Var("x"))));

        var unrolled = Unroller.Unroll(system, steps: 3);
        var model = Reasoner.FindModel(unrolled);
        Assert.NotNull(model);

        // x@0=F, x@1=T, x@2=F, x@3=T
        Assert.False(model![Unroller.At("x", 0)]);
        Assert.True(model[Unroller.At("x", 1)]);
        Assert.False(model[Unroller.At("x", 2)]);
        Assert.True(model[Unroller.At("x", 3)]);
    }

    [Fact]
    public void Unrolling_IsDeterministic_ForDeterministicSystem()
    {
        var system = new TransitionSystem(
            new[] { "x" },
            Initial: new Negation(Formula.Var("x")),
            Transition: new Biconditional(Formula.Var("x'"), new Negation(Formula.Var("x"))));

        var unrolled = Unroller.Unroll(system, steps: 4);
        // Only one trace exists → exactly one model.
        Assert.Equal(1, Reasoner.CountModels(unrolled));
    }

    [Fact]
    public void TwoBitCounter_ReachesState()
    {
        // Bits lo, hi forming a 2-bit counter. init: !lo & !hi.
        // lo' <-> !lo ; hi' <-> hi XOR lo  (carry).
        var system = new TransitionSystem(
            new[] { "lo", "hi" },
            Initial: new Conjunction(new Negation(Formula.Var("lo")), new Negation(Formula.Var("hi"))),
            Transition: new Conjunction(
                new Biconditional(Formula.Var("lo'"), new Negation(Formula.Var("lo"))),
                new Biconditional(Formula.Var("hi'"), Xor(Formula.Var("hi"), Formula.Var("lo")))));

        var unrolled = Unroller.Unroll(system, steps: 3);
        var model = Reasoner.FindModel(unrolled)!;

        // counts 00,01,10,11 over steps 0..3
        Assert.False(model[Unroller.At("lo", 0)]); Assert.False(model[Unroller.At("hi", 0)]); // 0
        Assert.True(model[Unroller.At("lo", 1)]);  Assert.False(model[Unroller.At("hi", 1)]); // 1
        Assert.False(model[Unroller.At("lo", 2)]); Assert.True(model[Unroller.At("hi", 2)]);  // 2
        Assert.True(model[Unroller.At("lo", 3)]);  Assert.True(model[Unroller.At("hi", 3)]);  // 3
    }

    [Fact]
    public void Reachability_PropertyHolds()
    {
        // Toggle reaches x=true at an odd step: "x@1" must be entailed.
        var system = new TransitionSystem(
            new[] { "x" },
            Initial: new Negation(Formula.Var("x")),
            Transition: new Biconditional(Formula.Var("x'"), new Negation(Formula.Var("x"))));
        var unrolled = Unroller.Unroll(system, 2);
        // Is "x is false at step 2" entailed? (toggle: F,T,F)
        Assert.True(Reasoner.Entails(unrolled, new Negation(Formula.Var(Unroller.At("x", 2)))));
    }

    private static Formula Xor(Formula a, Formula b) => new Negation(new Biconditional(a, b));
}
