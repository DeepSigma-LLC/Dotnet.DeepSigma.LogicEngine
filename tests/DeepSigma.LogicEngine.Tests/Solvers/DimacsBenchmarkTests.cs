using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Solvers;

/// <summary>
/// Loads checked-in DIMACS instances with known verdicts and confirms CDCL
/// produces the right answer (and a satisfying model when SAT).
/// </summary>
[Trait("Category", "Benchmark")]
public class DimacsBenchmarkTests
{
    [Theory]
    [InlineData("sat_simple.cnf", true)]
    [InlineData("unsat_trivial.cnf", false)]
    [InlineData("php_3_2.cnf", false)]
    [InlineData("php_4_3.cnf", false)]
    public void Cdcl_ProducesKnownVerdict(string file, bool expectedSat)
    {
        var cnf = Dimacs.ReadFile(BenchmarkPath(file));
        var result = new CdclSolver().Solve(cnf);
        Assert.Equal(expectedSat, result.IsSatisfiable);

        if (expectedSat)
        {
            AssertModelSatisfies(cnf, result.Model!);
        }
    }

    private static void AssertModelSatisfies(CnfFormula cnf, IReadOnlyDictionary<string, bool> model)
    {
        foreach (var clause in cnf.Clauses)
        {
            var satisfied = clause.Literals.Any(lit =>
                model.TryGetValue(lit.Variable, out var v) && v != lit.Negated);
            Assert.True(satisfied, $"Clause not satisfied: {clause}");
        }
    }

    private static string BenchmarkPath(string file)
        => Path.Combine(AppContext.BaseDirectory, "Benchmarks", file);
}
