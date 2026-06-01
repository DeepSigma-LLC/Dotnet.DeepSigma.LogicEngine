using DeepSigma.LogicEngine.FiniteGroups;
using DeepSigma.LogicEngine.FiniteSets;
using DeepSigma.LogicEngine.Fuzzy;
using DeepSigma.LogicEngine.Modal;
using DeepSigma.LogicEngine.Temporal;
using DeepSigma.LogicEngine.Z3;
using Xunit;

namespace DeepSigma.LogicEngine.Z3.Tests;

/// <summary>
/// The encoder logics (modal, LTL, finite-sets, finite-groups) run on Z3 by passing a
/// <see cref="Z3SatSolver"/> through the existing <c>ISatSolver</c> overloads — no internals
/// exposed. Fuzzy routes through the public <see cref="FuzzyEncoder"/> solved by <see cref="Z3SmtReasoner"/>.
/// Each must agree with the native engine.
/// </summary>
public class Z3EncoderLogicsTests
{
    private static readonly Z3SatSolver Z3 = new();

    [Theory]
    [InlineData("[]p -> p", ModalSystem.K, false)]   // T axiom: not valid in K
    [InlineData("[]p -> p", ModalSystem.T, true)]    // valid in T (reflexive)
    [InlineData("<>p -> []<>p", ModalSystem.S5, true)]
    public void Modal_Agrees(string text, ModalSystem system, bool expectedValid)
    {
        var f = ModalParser.Parse(text);
        Assert.Equal(expectedValid, ModalSolver.IsValid(f, system));
        Assert.Equal(ModalSolver.IsValid(f, system), ModalSolver.IsValid(f, system, Z3));
    }

    [Fact]
    public void Ltl_Agrees()
    {
        var f = LtlParser.Parse("G F a");
        Assert.Equal(
            BoundedModelChecker.IsSatisfiable(f, maxBound: 4),
            BoundedModelChecker.IsSatisfiable(f, Z3, maxBound: 4));
    }

    [Theory]
    [InlineData("~(A ∪ B) = ~A ∩ ~B", true)]              // De Morgan — valid
    [InlineData("A subset B & B subset A -> A = B", true)] // antisymmetry — valid
    public void FiniteSets_Agrees(string text, bool expectedValid)
    {
        var f = SetFormula.Parse(text);
        Assert.Equal(expectedValid, FiniteSetsSolver.IsValid(f));
        Assert.Equal(FiniteSetsSolver.IsValid(f), FiniteSetsSolver.IsValid(f, Z3));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(6, true)]
    public void FiniteGroups_Agrees(int order, bool expectedExists)
    {
        Assert.Equal(expectedExists, GroupFinder.ExistsGroup(order, Z3));
        Assert.Equal(GroupFinder.ExistsGroup(order), GroupFinder.ExistsGroup(order, Z3));
        Assert.NotNull(GroupFinder.FindGroup(order, Z3));
    }

    [Fact]
    public void Fuzzy_Agrees_ViaEncoderAndZ3()
    {
        var em = FuzzyFormula.Var("p") | !FuzzyFormula.Var("p");   // excluded middle

        // Łukasiewicz: valid; Gödel: not valid. Z3 decides the same LRA reduction.
        foreach (var logic in new[] { FuzzyLogic.Lukasiewicz, FuzzyLogic.Godel })
        {
            var nativeValid = FuzzySolver.IsValid(em, logic);
            var z3Valid = !Z3SmtReasoner.IsSatisfiable(FuzzyEncoder.ValidityCounterexampleQuery(em, logic), Z3SmtTheory.Lra);
            Assert.Equal(nativeValid, z3Valid);
        }
    }
}
