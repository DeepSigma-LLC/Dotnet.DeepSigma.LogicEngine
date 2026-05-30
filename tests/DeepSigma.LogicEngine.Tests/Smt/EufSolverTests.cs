using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class EufSolverTests
{
    [Theory]
    [InlineData("a = b & f(a) != f(b)", false)]                       // congruence violation
    [InlineData("a = b & b = c & a != c", false)]                     // transitivity
    [InlineData("a = b & f(f(a)) != f(f(b))", false)]                 // nested congruence
    [InlineData("a = b & P(a) & !P(b)", false)]                       // predicate congruence
    [InlineData("a = b & f(a) = f(b)", true)]                         // consistent
    [InlineData("P(a) & !P(b)", true)]                                // consistent (a, b distinct)
    [InlineData("a = b | a = c", true)]
    public void Solve_MatchesExpectedSatisfiability(string input, bool expectedSat)
    {
        Assert.Equal(expectedSat, EufSolver.IsSatisfiable(SmtFormula.Parse(input)));
    }

    [Fact]
    public void Solve_BooleanStructureForcesTheoryConflict()
    {
        // (a=b ∨ a=c) ∧ f(a)≠f(b) ∧ f(a)≠f(c): both disjuncts violate congruence → UNSAT.
        var f = SmtFormula.Parse("(a = b | a = c) & f(a) != f(b) & f(a) != f(c)");
        Assert.False(EufSolver.IsSatisfiable(f));
    }

    [Fact]
    public void Solve_SatisfiableModel_IsTheoryConsistent()
    {
        var f = SmtFormula.Parse("(a = b | c = d) & f(a) != f(b)");
        var result = EufSolver.Solve(f);
        Assert.True(result.IsSatisfiable);
        // f(a) != f(b) forbids a=b, so the model must take c=d.
        Assert.True(result.Model!.Holds(SmtFormula.Parse("c = d")));
        Assert.False(result.Model!.Holds(SmtFormula.Parse("a = b")));
    }

    [Fact]
    public void IsValid_CongruenceTautology()
    {
        // a = b -> f(a) = f(b) is valid.
        Assert.True(EufSolver.IsValid(SmtFormula.Parse("a = b -> f(a) = f(b)")));
        // transitivity
        Assert.True(EufSolver.IsValid(SmtFormula.Parse("a = b & b = c -> a = c")));
    }

    [Fact]
    public void Entails_Congruence()
    {
        var kb = new[] { SmtFormula.Parse("a = b"), SmtFormula.Parse("b = c") };
        Assert.True(EufSolver.Entails(kb, SmtFormula.Parse("f(a) = f(c)")));
        Assert.False(EufSolver.Entails(kb, SmtFormula.Parse("a = d")));
    }

    [Fact]
    public void TrivialEquality_IsHandled()
    {
        // a = a is valid; a != a is unsatisfiable. These short-circuit in abstraction.
        Assert.True(EufSolver.IsValid(SmtFormula.Parse("a = a")));
        Assert.False(EufSolver.IsSatisfiable(SmtFormula.Parse("a != a")));
    }

    [Fact]
    public void StressCase_ForcesMultipleRefinementRounds()
    {
        // A chain of disjunctions each of which is theory-inconsistent.
        var f = SmtFormula.Parse(
            "(a = b | a = c) & (a = c | a = d) & f(a) != f(b) & f(a) != f(c) & f(a) != f(d)");
        Assert.False(EufSolver.IsSatisfiable(f));
    }

    [Fact]
    public void ConflictCore_ReturnsMinimalInconsistentSubset()
    {
        var literals = new[]
        {
            SmtFormula.Parse("a = b"),
            SmtFormula.Parse("c = d"),       // irrelevant
            SmtFormula.Parse("b = c"),
            SmtFormula.Parse("a != c"),
        };
        var core = EufSolver.ConflictCore(literals);
        Assert.NotNull(core);
        Assert.Contains(SmtFormula.Parse("a = b"), core!);
        Assert.Contains(SmtFormula.Parse("b = c"), core!);
        Assert.Contains(SmtFormula.Parse("a != c"), core!);
        Assert.DoesNotContain(SmtFormula.Parse("c = d"), core!);
    }

    [Fact]
    public void ConflictCore_ConsistentReturnsNull()
    {
        var literals = new[] { SmtFormula.Parse("a = b"), SmtFormula.Parse("f(a) = f(b)") };
        Assert.Null(EufSolver.ConflictCore(literals));
    }
}
