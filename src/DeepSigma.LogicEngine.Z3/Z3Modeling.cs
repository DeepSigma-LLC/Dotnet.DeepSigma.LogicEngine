using DeepSigma.Mathematics.Algebra;
using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>Builds a <see cref="Z3Model"/> from a Z3 model by evaluating each declared constant.</summary>
internal static class Z3Modeling
{
    public static Z3Model Build(MZ3.Model model, IReadOnlyDictionary<string, MZ3.Expr> constants)
    {
        var values = new Dictionary<string, Z3Value>(StringComparer.Ordinal);
        foreach (var (name, constant) in constants)
        {
            var evaluated = model.Eval(constant, completion: true);
            values[name] = Classify(evaluated);
        }
        return new Z3Model(values);
    }

    private static Z3Value Classify(MZ3.Expr value)
    {
        var text = value.ToString() ?? string.Empty;
        if (value.IsTrue)
        {
            return new Z3Value(Z3ValueKind.Boolean, text, boolean: true);
        }
        if (value.IsFalse)
        {
            return new Z3Value(Z3ValueKind.Boolean, text, boolean: false);
        }
        if (value is MZ3.IntNum integer)
        {
            return new Z3Value(Z3ValueKind.Integer, text, integer: integer.BigInteger);
        }
        if (value is MZ3.BitVecNum bitVector)
        {
            // A bit-vector's value as an unsigned integer.
            return new Z3Value(Z3ValueKind.Integer, text, integer: bitVector.BigInteger);
        }
        if (value is MZ3.RatNum rational)
        {
            return new Z3Value(Z3ValueKind.Rational, text, rational: Rational.Of(rational.BigIntNumerator, rational.BigIntDenominator));
        }
        return new Z3Value(Z3ValueKind.Other, text);
    }
}
