using DeepSigma.LogicEngine.FirstOrder;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.FirstOrder;

public class UnificationTests
{
    [Fact]
    public void UnifiesVariableWithTerm()
    {
        var sub = Unifier.Unify(FolTerm.Var("x"), FolTerm.Func("f", FolTerm.Constant("a")));
        Assert.NotNull(sub);
        Assert.Equal(FolTerm.Func("f", FolTerm.Constant("a")), sub!.Apply(FolTerm.Var("x")));
    }

    [Fact]
    public void UnifiesCompatibleFunctions()
    {
        var sub = Unifier.Unify(
            FolTerm.Func("f", FolTerm.Var("x"), FolTerm.Constant("b")),
            FolTerm.Func("f", FolTerm.Constant("a"), FolTerm.Var("y")));
        Assert.NotNull(sub);
        Assert.Equal(FolTerm.Constant("a"), sub!.Apply(FolTerm.Var("x")));
        Assert.Equal(FolTerm.Constant("b"), sub.Apply(FolTerm.Var("y")));
    }

    [Fact]
    public void FailsOccursCheck()
    {
        Assert.Null(Unifier.Unify(FolTerm.Var("x"), FolTerm.Func("f", FolTerm.Var("x"))));
    }

    [Fact]
    public void FailsOnSymbolClash()
    {
        Assert.Null(Unifier.Unify(FolTerm.Constant("a"), FolTerm.Constant("b")));
    }
}

public class FolParserTests
{
    [Theory]
    [InlineData("forall x. (P(x) -> Q(x))")]
    [InlineData("exists x. P(x) & Q(x)")]
    [InlineData("forall x. forall y. R(x, y) -> R(y, x)")]
    [InlineData("a = b -> f(a) = f(b)")]
    [InlineData("P & (Q -> R)")]
    public void Parses(string input) => Assert.NotNull(FolFormula.Parse(input));
}

public class FirstOrderProverTests
{
    [Fact]
    public void ModusPonens_IsProved()
    {
        var status = FirstOrderProver.Entails(
            new[] { FolFormula.Parse("forall x. (P(x) -> Q(x))"), FolFormula.Parse("P(a)") },
            FolFormula.Parse("Q(a)"));
        Assert.Equal(FolProofStatus.Proved, status);
    }

    [Fact]
    public void Syllogism_IsProved()
    {
        var status = FirstOrderProver.Entails(
            new[] { FolFormula.Parse("forall x. (Man(x) -> Mortal(x))"), FolFormula.Parse("Man(socrates)") },
            FolFormula.Parse("Mortal(socrates)"));
        Assert.Equal(FolProofStatus.Proved, status);
    }

    [Fact]
    public void UniversalImpliesExistential_IsValid()
    {
        // Over a non-empty domain, (∀x P(x)) → (∃x P(x)) is valid.
        Assert.Equal(FolProofStatus.Proved, FirstOrderProver.IsValid(FolFormula.Parse("(forall x. P(x)) -> (exists x. P(x))")));
    }

    [Fact]
    public void UniversalQuantifierSwap_IsValid()
    {
        Assert.Equal(FolProofStatus.Proved,
            FirstOrderProver.IsValid(FolFormula.Parse("(forall x. forall y. R(x, y)) -> (forall y. forall x. R(x, y))")));
    }

    [Fact]
    public void EqualityTransitivity_IsProved()
    {
        var status = FirstOrderProver.Entails(
            new[] { FolFormula.Parse("a = b"), FolFormula.Parse("b = c") },
            FolFormula.Parse("a = c"));
        Assert.Equal(FolProofStatus.Proved, status);
    }

    [Fact]
    public void EqualityCongruence_IsProved()
    {
        var status = FirstOrderProver.Entails(
            new[] { FolFormula.Parse("a = b") },
            FolFormula.Parse("f(a) = f(b)"));
        Assert.Equal(FolProofStatus.Proved, status);
    }

    [Fact]
    public void EqualitySubstitutionIntoPredicate_IsProved()
    {
        // a = b, P(a) ⊢ P(b) — paramodulation rewrites the predicate argument.
        Assert.Equal(FolProofStatus.Proved, FirstOrderProver.Entails(
            new[] { FolFormula.Parse("a = b"), FolFormula.Parse("P(a)") },
            FolFormula.Parse("P(b)")));
    }

    [Fact]
    public void NestedFunctionCongruence_IsProved()
    {
        // a = b ⊢ g(f(a), c) = g(f(b), c) — paramodulation into a deep subterm.
        Assert.Equal(FolProofStatus.Proved, FirstOrderProver.Entails(
            new[] { FolFormula.Parse("a = b") },
            FolFormula.Parse("g(f(a), c) = g(f(b), c)")));
    }

    [Fact]
    public void EqualityWithQuantifiedCongruence_IsProved()
    {
        // ∀x (P(x) -> Q(x)), a = b, P(a) ⊢ Q(b).
        Assert.Equal(FolProofStatus.Proved, FirstOrderProver.Entails(
            new[]
            {
                FolFormula.Parse("forall x. (P(x) -> Q(x))"),
                FolFormula.Parse("a = b"),
                FolFormula.Parse("P(a)"),
            },
            FolFormula.Parse("Q(b)")));
    }

    [Fact]
    public void ContradictoryKnowledgeBase_IsRefuted()
    {
        var status = FirstOrderProver.Refute(new[]
        {
            FolFormula.Parse("forall x. P(x)"),
            FolFormula.Parse("!P(a)"),
        });
        Assert.Equal(FolProofStatus.Proved, status);
    }

    [Fact]
    public void NonTheorem_IsNotProved()
    {
        // (∃x P(x)) → (∀x P(x)) is not valid; the prover must not prove it.
        Assert.NotEqual(FolProofStatus.Proved,
            FirstOrderProver.IsValid(FolFormula.Parse("(exists x. P(x)) -> (forall x. P(x))")));
    }

    [Fact]
    public void SatisfiableSet_IsNotRefuted()
    {
        Assert.NotEqual(FolProofStatus.Proved, FirstOrderProver.Refute(new[]
        {
            FolFormula.Parse("forall x. (P(x) -> Q(x))"),
            FolFormula.Parse("P(a)"),
            FolFormula.Parse("Q(a)"),
        }));
    }
}
