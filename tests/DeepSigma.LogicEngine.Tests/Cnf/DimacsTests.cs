using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Cnf;

public class DimacsTests
{
    [Fact]
    public void Reads_BasicExample()
    {
        const string source =
            "c sample\n" +
            "p cnf 3 2\n" +
            "1 -2 3 0\n" +
            "-1 2 0\n";
        var cnf = Dimacs.ReadString(source);
        Assert.Equal(2, cnf.Clauses.Count);
        Assert.Equal(new[] { "x1", "x2", "x3" }, cnf.Variables().OrderBy(v => v).ToArray());
    }

    [Fact]
    public void Reads_MultiLineClause()
    {
        const string source = "p cnf 3 1\n1 2\n3\n0\n";
        var cnf = Dimacs.ReadString(source);
        Assert.Single(cnf.Clauses);
        Assert.Equal(3, cnf.Clauses[0].Literals.Count);
    }

    [Fact]
    public void Reads_EmptyClause_AsUnsat()
    {
        var cnf = Dimacs.ReadString("p cnf 0 1\n0\n");
        Assert.Single(cnf.Clauses);
        Assert.True(cnf.Clauses[0].IsEmpty);
        Assert.False(new DpllSolver().Solve(cnf).IsSatisfiable);
    }

    [Fact]
    public void Reads_NoHeader_StillWorks()
    {
        // We accept missing 'p' header; it's not strictly required for content.
        var cnf = Dimacs.ReadString("1 0\n-1 2 0\n");
        Assert.Equal(2, cnf.Clauses.Count);
    }

    [Fact]
    public void Reads_PercentTerminator_StopsParsing()
    {
        var cnf = Dimacs.ReadString("p cnf 1 1\n1 0\n%\n0\n");
        Assert.Single(cnf.Clauses);
    }

    [Fact]
    public void Reads_RejectsGarbage()
    {
        Assert.Throws<FormatException>(() => Dimacs.ReadString("p cnf 1 1\nhello 0\n"));
    }

    [Fact]
    public void Reads_RejectsTrailingUnterminatedClause()
    {
        Assert.Throws<FormatException>(() => Dimacs.ReadString("p cnf 2 1\n1 2\n"));
    }

    [Fact]
    public void WriteRead_RoundTrips()
    {
        var original = new CnfFormula(new[]
        {
            new Clause(new[] { Literal.Positive("p"), Literal.Negative("q") }),
            new Clause(new[] { Literal.Positive("q"), Literal.Positive("r") }),
        });
        var text = Dimacs.WriteToString(original);
        var read = Dimacs.ReadString(text, id => id switch { 1 => "p", 2 => "q", 3 => "r", _ => "x" + id });

        var oracleA = new DpllSolver().Solve(original).IsSatisfiable;
        var oracleB = new DpllSolver().Solve(read).IsSatisfiable;
        Assert.Equal(oracleA, oracleB);
        Assert.Equal(original.Clauses.Count, read.Clauses.Count);
    }

    [Fact]
    public void CustomNameMapper_IsRespected()
    {
        var cnf = Dimacs.ReadString("p cnf 2 1\n1 -2 0\n", id => "name" + id);
        Assert.Contains("name1", cnf.Variables());
        Assert.Contains("name2", cnf.Variables());
    }
}
