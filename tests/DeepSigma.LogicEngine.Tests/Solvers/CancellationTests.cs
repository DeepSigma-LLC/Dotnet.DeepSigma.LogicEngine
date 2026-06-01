using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.FiniteSets;
using DeepSigma.LogicEngine.FirstOrder;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Modal;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;
using DeepSigma.LogicEngine.Solvers.MaxSat;
using DeepSigma.LogicEngine.Temporal;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

/// <summary>
/// A pre-cancelled <see cref="CancellationToken"/> aborts each native solve cooperatively with
/// <see cref="OperationCanceledException"/> — the parity feature matching the optional Z3 engine.
/// (For a wall-clock limit, callers pass <c>new CancellationTokenSource(duration).Token</c>.)
/// </summary>
public class CancellationTests
{
    private static CancellationToken Cancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    [Fact]
    public void Cdcl_Cancels()
    {
        var f = Formula.Parse("(p | q) & (!p | r)");
        Assert.Throws<OperationCanceledException>(() => new CdclSolver().Solve(f, Cancelled()));
    }

    [Fact]
    public void Dpll_Cancels()
    {
        var f = Formula.Parse("(p | q) & (!p | r)");
        Assert.Throws<OperationCanceledException>(() => new DpllSolver().Solve(f, Cancelled()));
    }

    [Fact]
    public void Reasoner_Cancels()
    {
        var f = Formula.Parse("(p | q) & (!p | r)");
        Assert.Throws<OperationCanceledException>(() => Reasoner.IsSatisfiable(f, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => Reasoner.IsValid(f, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => Reasoner.EnumerateModels(f, Cancelled()).ToList());
    }

    [Fact]
    public void Smt_Cancels()
    {
        var euf = SmtFormula.Parse("a = b & b = c -> f(a) = f(c)");
        Assert.Throws<OperationCanceledException>(() => EufSolver.IsValid(euf, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => SmtReasoner.IsSatisfiable(euf, SmtTheory.Euf, Cancelled()));
        Assert.Throws<OperationCanceledException>(() => LiaSolver.IsSatisfiable(LraParser.Parse("x >= 1"), new[] { "x" }, cancellationToken: Cancelled()));
    }

    [Fact]
    public void Modal_Cancels()
        => Assert.Throws<OperationCanceledException>(
            () => ModalSolver.IsValid(ModalParser.Parse("[]p -> p"), ModalSystem.T, cancellationToken: Cancelled()));

    [Fact]
    public void Ltl_Cancels()
        => Assert.Throws<OperationCanceledException>(
            () => BoundedModelChecker.IsSatisfiable(LtlParser.Parse("G F a"), maxBound: 6, cancellationToken: Cancelled()));

    [Fact]
    public void FiniteSets_Cancels()
        => Assert.Throws<OperationCanceledException>(
            () => FiniteSetsSolver.IsSatisfiable(SetFormula.Parse("A subset B"), cancellationToken: Cancelled()));

    [Fact]
    public void FirstOrder_Cancels()
        => Assert.Throws<OperationCanceledException>(
            () => FirstOrderProver.IsValid(FolFormula.Parse("forall x. P(x)"), cancellationToken: Cancelled()));

    [Fact]
    public void MaxSat_Cancels()
    {
        var hard = new[] { (IReadOnlyList<Literal>)new[] { Literal.Positive("a") } };
        var soft = new[] { new SoftClause(new[] { Literal.Negative("a") }, 1) };
        Assert.Throws<OperationCanceledException>(() => new MaxSatSolver(hard, soft).Solve(Cancelled()));
    }
}
