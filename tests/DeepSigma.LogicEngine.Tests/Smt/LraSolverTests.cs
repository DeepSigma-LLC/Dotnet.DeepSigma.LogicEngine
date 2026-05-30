using DeepSigma.LogicEngine.Smt;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class LraSolverTests
{
    private static SmtFormula Constraint(LinearRelation rel, Rational c, params (string Var, int Coeff)[] terms)
        => new LinearConstraintAtom(
            terms.Select(t => new LinearAtomTerm(t.Var, t.Coeff)).ToList(), rel, c);

    private static SmtFormula Ge(string v, int c) => Constraint(LinearRelation.GreaterOrEqual, c, (v, 1));
    private static SmtFormula Le(string v, int c) => Constraint(LinearRelation.LessOrEqual, c, (v, 1));
    private static SmtFormula SumLe(int c, params string[] vars) => Constraint(LinearRelation.LessOrEqual, c, vars.Select(v => (v, 1)).ToArray());

    [Fact]
    public void Infeasible_Conjunction()
    {
        // x >= 1 ∧ y >= 1 ∧ x + y <= 1
        var f = new SmtAnd(new SmtAnd(Ge("x", 1), Ge("y", 1)), SumLe(1, "x", "y"));
        Assert.False(LraSolver.IsSatisfiable(f));
    }

    [Fact]
    public void Feasible_Conjunction()
    {
        var f = new SmtAnd(Ge("x", 1), Le("x", 5));
        Assert.True(LraSolver.IsSatisfiable(f));
    }

    [Fact]
    public void BooleanStructure_ForcesArithmeticConflict()
    {
        // (x >= 3 ∨ x <= 1) ∧ x >= 2 ∧ x <= 2  →  x must be 2, neither disjunct holds → UNSAT.
        var f = new SmtAnd(
            new SmtAnd(new SmtOr(Ge("x", 3), Le("x", 1)), Ge("x", 2)),
            Le("x", 2));
        Assert.False(LraSolver.IsSatisfiable(f));
    }

    [Fact]
    public void BooleanStructure_Satisfiable()
    {
        // (x >= 3 ∨ x <= 1) ∧ x >= 0 ∧ x <= 5  → satisfiable (e.g. x=0 or x=4).
        var f = new SmtAnd(
            new SmtAnd(new SmtOr(Ge("x", 3), Le("x", 1)), Ge("x", 0)),
            Le("x", 5));
        Assert.True(LraSolver.IsSatisfiable(f));
    }

    [Fact]
    public void Validity_Monotone()
    {
        // x <= 5 -> x <= 6 is valid.
        Assert.True(LraSolver.IsValid(new SmtImplies(Le("x", 5), Le("x", 6))));
    }

    [Fact]
    public void Entailment()
    {
        // x >= 5 entails x >= 3.
        Assert.True(LraSolver.Entails(new[] { Ge("x", 5) }, Ge("x", 3)));
        Assert.False(LraSolver.Entails(new[] { Ge("x", 5) }, Le("x", 3)));
    }

    [Fact]
    public void ConflictCore_IsMinimal()
    {
        var literals = new[]
        {
            Ge("x", 2),
            Le("z", 9),       // irrelevant
            Ge("y", 2),
            SumLe(2, "x", "y"),
        };
        var core = LraSolver.ConflictCore(literals);
        Assert.NotNull(core);
        Assert.DoesNotContain(Le("z", 9), core!);
    }
}
