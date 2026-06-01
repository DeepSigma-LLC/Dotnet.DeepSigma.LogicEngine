using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

/// <summary>
/// The <see cref="SmtReasoner"/> umbrella must agree with the dedicated facade it dispatches
/// to for each <see cref="SmtTheory"/>. These are differential checks: the umbrella adds no
/// new semantics, it only routes.
/// </summary>
public class SmtSolverTests
{
    [Fact]
    public void Euf_AgreesWithEufSolver()
    {
        var valid = SmtFormula.Parse("a = b & b = c -> f(a) = f(c)");
        var unsat = SmtFormula.Parse("a = b & f(a) != f(b)");

        Assert.Equal(EufSolver.IsValid(valid), SmtReasoner.IsValid(valid, SmtTheory.Euf));
        Assert.Equal(EufSolver.IsSatisfiable(unsat), SmtReasoner.IsSatisfiable(unsat, SmtTheory.Euf));
        Assert.Equal(EufSolver.IsUnsatisfiable(unsat), SmtReasoner.IsUnsatisfiable(unsat, SmtTheory.Euf));
        Assert.Equal(EufSolver.Solve(valid).IsSatisfiable, SmtReasoner.Solve(valid, SmtTheory.Euf).IsSatisfiable);

        var kb = new[] { SmtFormula.Parse("a = b"), SmtFormula.Parse("b = c") };
        var query = SmtFormula.Parse("f(a) = f(c)");
        Assert.Equal(EufSolver.Entails(kb, query), SmtReasoner.Entails(kb, query, SmtTheory.Euf));
    }

    [Fact]
    public void Lra_AgreesWithLraSolver()
    {
        var unsat = LraParser.Parse("x >= 1 & y >= 1 & x + y <= 1");
        var valid = LraParser.Parse("x <= 5 -> x <= 6");

        Assert.Equal(LraSolver.IsSatisfiable(unsat), SmtReasoner.IsSatisfiable(unsat, SmtTheory.Lra));
        Assert.Equal(LraSolver.IsValid(valid), SmtReasoner.IsValid(valid, SmtTheory.Lra));
        Assert.Equal(LraSolver.Solve(unsat).IsSatisfiable, SmtReasoner.Solve(unsat, SmtTheory.Lra).IsSatisfiable);
    }

    [Fact]
    public void Combined_AgreesWithCombinedSolver()
    {
        // x ≤ y ∧ y ≤ x forces x = y (LRA); congruence then contradicts f(x) ≠ f(y).
        var mixed = new SmtAnd(LraParser.Parse("x <= y & y <= x"), SmtParser.Parse("f(x) != f(y)"));

        Assert.Equal(CombinedSolver.IsSatisfiable(mixed), SmtReasoner.IsSatisfiable(mixed, SmtTheory.Combined));
        Assert.Equal(CombinedSolver.Solve(mixed).IsSatisfiable, SmtReasoner.Solve(mixed, SmtTheory.Combined).IsSatisfiable);
    }

    [Fact]
    public void Arrays_AgreesWithArraySolver()
    {
        var valid = SmtParser.Parse("select(store(a, i, v), i) = v");
        var unsat = SmtParser.Parse("select(store(a, i, v), i) != v");

        Assert.Equal(ArraySolver.IsValid(valid), SmtReasoner.IsValid(valid, SmtTheory.Arrays));
        Assert.Equal(ArraySolver.IsSatisfiable(unsat), SmtReasoner.IsSatisfiable(unsat, SmtTheory.Arrays));
        // Arrays.Solve reduces to EUF over the read-over-write axioms.
        Assert.Equal(ArraySolver.IsSatisfiable(valid), SmtReasoner.Solve(valid, SmtTheory.Arrays).IsSatisfiable);
    }
}
