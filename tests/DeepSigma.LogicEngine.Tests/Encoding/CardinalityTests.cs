using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Encoding;

public class CardinalityTests
{
    private static Formula[] Vars(int n)
        => Enumerable.Range(0, n).Select(i => Formula.Var("x" + i)).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void AtLeastOne_HasTwoPowerNMinusOneModels(int n)
    {
        Assert.Equal((1L << n) - 1, Reasoner.CountModels(Cardinality.AtLeastOne(Vars(n))));
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    public void AtMostOne_HasNPlusOneModels(int n, int expected)
    {
        Assert.Equal(expected, Reasoner.CountModels(Cardinality.AtMostOne(Vars(n))));
    }

    [Fact]
    public void AtMostOne_SingleInput_IsTriviallyValid()
    {
        Assert.True(Reasoner.IsValid(Cardinality.AtMostOne(Vars(1))));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(6, 6)]
    public void ExactlyOne_HasNModels(int n, int expected)
    {
        Assert.Equal(expected, Reasoner.CountModels(Cardinality.ExactlyOne(Vars(n))));
    }

    [Theory]
    [InlineData(5, 0, 1)]   // C(5,0)
    [InlineData(5, 1, 6)]   // C(5,0) + C(5,1)
    [InlineData(5, 2, 16)]  // C(5,0) + C(5,1) + C(5,2)
    [InlineData(5, 4, 31)]  // all but the one where every var is true
    public void AtMostK_CountsMatchBinomialSum(int n, int k, int expected)
    {
        Assert.Equal(expected, Reasoner.CountModels(Cardinality.AtMostK(Vars(n), k)));
    }

    [Theory]
    [InlineData(5, 1, 31)]
    [InlineData(5, 3, 16)]
    [InlineData(5, 5, 1)]
    public void AtLeastK_CountsMatchSumFromKToN(int n, int k, int expected)
    {
        Assert.Equal(expected, Reasoner.CountModels(Cardinality.AtLeastK(Vars(n), k)));
    }

    [Theory]
    [InlineData(5, 0, 1)]   // C(5,0)
    [InlineData(5, 1, 5)]   // C(5,1)
    [InlineData(5, 2, 10)]  // C(5,2)
    [InlineData(5, 3, 10)]  // C(5,3)
    [InlineData(5, 5, 1)]   // C(5,5)
    public void ExactlyK_CountsEqualBinomialCoefficient(int n, int k, int expected)
    {
        Assert.Equal(expected, Reasoner.CountModels(Cardinality.ExactlyK(Vars(n), k)));
    }

    [Fact]
    public void AtMostK_WithKGreaterThanN_IsTriviallyTrue()
    {
        Assert.True(Reasoner.IsValid(Cardinality.AtMostK(Vars(3), 10)));
    }

    [Fact]
    public void AtLeastK_WithKGreaterThanN_IsTriviallyFalse()
    {
        Assert.True(Reasoner.IsUnsatisfiable(Cardinality.AtLeastK(Vars(3), 10)));
    }

    [Fact]
    public void Empty_Inputs_AreSensible()
    {
        Assert.True(Reasoner.IsUnsatisfiable(Cardinality.AtLeastOne(Array.Empty<Formula>())));
        Assert.True(Reasoner.IsValid(Cardinality.AtMostOne(Array.Empty<Formula>())));
    }
}
