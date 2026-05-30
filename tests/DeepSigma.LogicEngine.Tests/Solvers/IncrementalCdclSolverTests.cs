using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers.Cdcl;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

public class IncrementalCdclSolverTests
{
    private static CnfFormula Cnf(params Clause[] clauses) => new(clauses);

    [Fact]
    public void Solve_FindsSatisfyingModel()
    {
        // (p | q) & (!p | r)
        var cnf = Cnf(
            Clause.Of(Literal.Positive("p"), Literal.Positive("q")),
            Clause.Of(Literal.Negative("p"), Literal.Positive("r")));
        var result = new IncrementalCdclSolver(cnf).Solve();
        Assert.True(result.IsSatisfiable);
    }

    [Fact]
    public void SolveUnder_AssumptionForcesValue()
    {
        // (p | q): satisfiable. Under {!p, !q} it is not.
        var cnf = Cnf(Clause.Of(Literal.Positive("p"), Literal.Positive("q")));
        var solver = new IncrementalCdclSolver(cnf);

        var underNotP = solver.SolveUnder(new[] { Literal.Negative("p") });
        Assert.True(underNotP.IsSatisfiable);
        Assert.True(underNotP.Model!["q"]); // forced

        var underBoth = solver.SolveUnder(new[] { Literal.Negative("p"), Literal.Negative("q") });
        Assert.False(underBoth.IsSatisfiable);
    }

    [Fact]
    public void SolveUnder_DifferentAssumptions_AreIndependentAcrossCalls()
    {
        var cnf = Cnf(Clause.Of(Literal.Positive("a"), Literal.Positive("b")));
        var solver = new IncrementalCdclSolver(cnf);

        Assert.False(solver.SolveUnder(new[] { Literal.Negative("a"), Literal.Negative("b") }).IsSatisfiable);
        // A failed assumption set must not poison later solves.
        Assert.True(solver.Solve().IsSatisfiable);
        Assert.True(solver.SolveUnder(new[] { Literal.Positive("a") }).IsSatisfiable);
    }

    [Fact]
    public void AddClause_AccumulatesConstraints()
    {
        var cnf = Cnf(Clause.Of(Literal.Positive("p"), Literal.Positive("q")));
        var solver = new IncrementalCdclSolver(cnf);

        Assert.True(solver.Solve().IsSatisfiable);
        Assert.True(solver.AddClause(new[] { Literal.Negative("p") })); // now q must hold
        var result = solver.Solve();
        Assert.True(result.IsSatisfiable);
        Assert.True(result.Model!["q"]);

        Assert.False(solver.AddClause(new[] { Literal.Negative("q") })); // unsatisfiable now
        Assert.False(solver.Solve().IsSatisfiable);
    }

    [Fact]
    public void IncrementalEnumeration_MatchesModelCount()
    {
        // Enumerate all models of (p | q) by adding blocking clauses: expect 3.
        var cnf = Cnf(Clause.Of(Literal.Positive("p"), Literal.Positive("q")));
        var solver = new IncrementalCdclSolver(cnf);
        var vars = new[] { "p", "q" };

        var count = 0;
        while (true)
        {
            var result = solver.Solve();
            if (!result.IsSatisfiable)
            {
                break;
            }
            count++;
            var blocking = vars
                .Select(v => result.Model![v] ? Literal.Negative(v) : Literal.Positive(v))
                .ToList();
            if (!solver.AddClause(blocking))
            {
                break;
            }
        }
        Assert.Equal(3, count);
    }

    [Fact]
    public void Encode_UnknownAssumptionVariable_Throws()
    {
        var cnf = Cnf(Clause.Of(Literal.Positive("p")));
        var solver = new IncrementalCdclSolver(cnf);
        Assert.Throws<InvalidOperationException>(() => solver.SolveUnder(new[] { Literal.Positive("unknown") }));
    }
}
