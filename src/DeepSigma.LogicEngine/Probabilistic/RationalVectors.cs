using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.Probabilistic;

/// <summary>Small exact-rational vector builders shared by the PSAT LP construction.</summary>
internal static class RationalVectors
{
    public static Rational[] Zeros(int length) => Enumerable.Repeat(Rational.Zero, length).ToArray();

    public static Rational[] Ones(int length) => Enumerable.Repeat(Rational.One, length).ToArray();
}
