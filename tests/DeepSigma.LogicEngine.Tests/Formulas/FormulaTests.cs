using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Formulas;

public class FormulaTests
{
    [Fact]
    public void OperatorOverloads_BuildExpectedTree()
    {
        var p = Formula.Var("p");
        var q = Formula.Var("q");
        var f = !p & q | Formula.True;

        Assert.IsType<Disjunction>(f);
        var top = (Disjunction)f;
        Assert.IsType<Conjunction>(top.Left);
    }

    [Fact]
    public void Evaluate_RespectsAssignment()
    {
        var p = Formula.Var("p");
        var q = Formula.Var("q");
        var formula = Formula.Implies(p, q);

        Assert.False(Evaluator.Evaluate(formula, new Dictionary<string, bool> { ["p"] = true, ["q"] = false }));
        Assert.True(Evaluator.Evaluate(formula, new Dictionary<string, bool> { ["p"] = false, ["q"] = false }));
        Assert.True(Evaluator.Evaluate(formula, new Dictionary<string, bool> { ["p"] = true, ["q"] = true }));
    }

    [Fact]
    public void Variables_CollectsAllAtoms()
    {
        var f = Formula.Parse("(p & q) | (!r <-> s)");
        Assert.Equal(new[] { "p", "q", "r", "s" }, Evaluator.Variables(f).OrderBy(v => v).ToArray());
    }

    [Fact]
    public void Simplify_FoldsConstants()
    {
        var p = Formula.Var("p");
        Assert.Equal(p, Simplifier.Simplify(p & Formula.True));
        Assert.Equal(Formula.False, Simplifier.Simplify(p & !p));
        Assert.Equal(Formula.True, Simplifier.Simplify(p | !p));
        Assert.Equal(p, Simplifier.Simplify(!!p));
    }

    [Fact]
    public void All_And_Any_FoldOverSequence()
    {
        var ps = new[] { Formula.Var("a"), Formula.Var("b"), Formula.Var("c") };
        Assert.Equal(Formula.True, Formula.All(Array.Empty<Formula>()));
        Assert.Equal(Formula.False, Formula.Any(Array.Empty<Formula>()));
        var conj = Formula.All(ps);
        Assert.True(Evaluator.Evaluate(conj, new Dictionary<string, bool> { ["a"] = true, ["b"] = true, ["c"] = true }));
        Assert.False(Evaluator.Evaluate(conj, new Dictionary<string, bool> { ["a"] = true, ["b"] = false, ["c"] = true }));
    }
}
