using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Encoding;

public class SequentialCounterTests
{
    private static Formula[] Vars(int n)
        => Enumerable.Range(0, n).Select(i => Formula.Var("x" + i)).ToArray();

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    [InlineData(5, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 2)]
    public void AtMostK_IsSatEquivalentToBound(int n, int k)
    {
        AssertMatchesBound(Vars(n), SequentialCounter.AtMostK(Vars(n), k), count => count <= k);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 4)]
    public void AtLeastK_IsSatEquivalentToBound(int n, int k)
    {
        AssertMatchesBound(Vars(n), SequentialCounter.AtLeastK(Vars(n), k), count => count >= k);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 2)]
    [InlineData(5, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 3)]
    public void ExactlyK_IsSatEquivalentToBound(int n, int k)
    {
        AssertMatchesBound(Vars(n), SequentialCounter.ExactlyK(Vars(n), k), count => count == k);
    }

    [Fact]
    public void AuxiliaryVariables_AreDisjointFromInputs()
    {
        var inputs = Vars(5);
        var encoding = SequentialCounter.ExactlyK(inputs, 2);
        var inputNames = inputs.Select(f => ((Variable)f).Name).ToHashSet();
        Assert.NotEmpty(encoding.AuxiliaryVariables);
        Assert.Empty(encoding.AuxiliaryVariables.Intersect(inputNames));
    }

    [Fact]
    public void AgreesWithBinomialEncoding_OnSolvability()
    {
        var inputs = Vars(7);
        for (var k = 0; k <= 7; k++)
        {
            var sequential = SequentialCounter.AtMostK(inputs, k).Constraint;
            var binomial = Cardinality.AtMostK(inputs, k);
            // Both are satisfiable (each has at least the all-false assignment when k>=0).
            Assert.Equal(Reasoner.IsSatisfiable(binomial), Reasoner.IsSatisfiable(sequential));
        }
    }

    /// <summary>
    /// For every assignment of the inputs, fixing them and asking whether the
    /// auxiliary variables can be completed must agree with the numeric bound.
    /// </summary>
    private static void AssertMatchesBound(Formula[] inputs, CardinalityEncoding encoding, Func<int, bool> bound)
    {
        var n = inputs.Length;
        for (var mask = 0; mask < (1 << n); mask++)
        {
            var trueCount = System.Numerics.BitOperations.PopCount((uint)mask);
            var fixers = new List<Formula>(n);
            for (var i = 0; i < n; i++)
            {
                var on = (mask & (1 << i)) != 0;
                fixers.Add(on ? inputs[i] : new Negation(inputs[i]));
            }
            var fixedInputs = new Conjunction(encoding.Constraint, Formula.All(fixers));
            var satisfiable = Reasoner.IsSatisfiable(fixedInputs);
            Assert.Equal(bound(trueCount), satisfiable);
        }
    }
}
