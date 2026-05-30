using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Cnf;

public class TransformerTests
{
    private static readonly string[] SampleFormulas =
    {
        "p",
        "!p",
        "p & q",
        "p | q",
        "p -> q",
        "p <-> q",
        "(p | q) & (!p | r)",
        "!(p & (q | !r))",
        "(p -> q) & (q -> r) -> (p -> r)",
        "(a <-> b) <-> (c <-> d)",
    };

    [Theory]
    [MemberData(nameof(SampleData))]
    public void Nnf_IsLogicallyEquivalentToInput(string input)
    {
        var original = Formula.Parse(input);
        var nnf = NnfTransformer.ToNnf(original);
        AssertEquivalent(original, nnf);
    }

    [Theory]
    [MemberData(nameof(SampleData))]
    public void ClassicalCnf_IsLogicallyEquivalentToInput(string input)
    {
        var original = Formula.Parse(input);
        var cnf = CnfTransformer.ToCnf(original);
        AssertEquivalent(original, cnf.ToFormula());
    }

    [Theory]
    [MemberData(nameof(SampleData))]
    public void Dnf_IsLogicallyEquivalentToInput(string input)
    {
        var original = Formula.Parse(input);
        var dnf = DnfTransformer.ToDnf(original);
        AssertEquivalent(original, dnf);
    }

    [Theory]
    [MemberData(nameof(SampleData))]
    public void Tseitin_PreservesSatisfiability(string input)
    {
        var original = Formula.Parse(input);
        var oracle = new TruthTableSolver().Solve(original).IsSatisfiable;
        var tseitin = TseitinTransformer.ToCnf(original);
        var dpll = new DpllSolver().Solve(tseitin).IsSatisfiable;
        Assert.Equal(oracle, dpll);
    }

    public static IEnumerable<object[]> SampleData() => SampleFormulas.Select(s => new object[] { s });

    private static void AssertEquivalent(Formula a, Formula b)
    {
        var vars = Evaluator.Variables(a).Union(Evaluator.Variables(b)).OrderBy(v => v).ToArray();
        for (var mask = 0; mask < (1 << vars.Length); mask++)
        {
            var assignment = new Dictionary<string, bool>();
            for (var i = 0; i < vars.Length; i++)
            {
                assignment[vars[i]] = (mask & (1 << i)) != 0;
            }
            Assert.Equal(Evaluator.Evaluate(a, assignment), Evaluator.Evaluate(b, assignment));
        }
    }
}
