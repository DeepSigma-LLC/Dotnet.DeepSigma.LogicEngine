using DeepSigma.LogicEngine.Ctl;
using DeepSigma.LogicEngine.FiniteSets;
using DeepSigma.LogicEngine.FirstOrder;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Modal;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Temporal;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Parsing;

/// <summary>
/// Every formula type exposes both <c>Parse</c> and <c>TryParse</c>. <c>TryParse</c> must
/// round-trip a valid string (agreeing with <c>Parse</c>) and return <c>false</c> — never
/// throw — on malformed input.
/// </summary>
public class UniformParseTests
{
    [Fact]
    public void Formula_TryParse()
    {
        Assert.True(Formula.TryParse("p & q", out var ok));
        Assert.Equal(Formula.Parse("p & q"), ok);
        Assert.False(Formula.TryParse("p &", out _));
    }

    [Fact]
    public void SmtFormula_TryParse()
    {
        Assert.True(SmtFormula.TryParse("a = b", out var ok));
        Assert.Equal(SmtFormula.Parse("a = b"), ok);
        Assert.False(SmtFormula.TryParse("a =", out _));
    }

    [Fact]
    public void ModalFormula_TryParse()
    {
        Assert.True(ModalFormula.TryParse("[]p -> p", out var ok));
        Assert.Equal(ModalFormula.Parse("[]p -> p"), ok);
        Assert.False(ModalFormula.TryParse("[]", out _));
    }

    [Fact]
    public void LtlFormula_TryParse()
    {
        Assert.True(LtlFormula.TryParse("G F a", out var ok));
        Assert.Equal(LtlFormula.Parse("G F a"), ok);
        Assert.False(LtlFormula.TryParse("G F", out _));
    }

    [Fact]
    public void CtlFormula_TryParse()
    {
        Assert.True(CtlFormula.TryParse("EF goal", out var ok));
        Assert.Equal(CtlFormula.Parse("EF goal"), ok);
        Assert.False(CtlFormula.TryParse("E[", out _));
    }

    [Fact]
    public void FolFormula_TryParse()
    {
        Assert.True(FolFormula.TryParse("forall x. P(x)", out var ok));
        Assert.Equal(FolFormula.Parse("forall x. P(x)"), ok);
        Assert.False(FolFormula.TryParse("forall", out _));
    }

    [Fact]
    public void SetFormula_TryParse()
    {
        Assert.True(SetFormula.TryParse("A subset B", out var ok));
        Assert.Equal(SetFormula.Parse("A subset B"), ok);
        Assert.False(SetFormula.TryParse("A subset", out _));
    }
}
