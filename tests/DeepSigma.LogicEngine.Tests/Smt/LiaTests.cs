using System.Numerics;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class LiaTests
{
    private static readonly string[] XY = { "x", "y" };

    [Fact]
    public void IntegerInfeasibility_DiffersFromReal()
    {
        var f = LraParser.Parse("2*x = 1");
        Assert.True(LraSolver.IsSatisfiable(f));                 // x = 1/2 over the reals
        Assert.False(LiaSolver.IsSatisfiable(f, new[] { "x" })); // no integer x
    }

    [Fact]
    public void LinearDiophantine_HasIntegerSolution()
    {
        var f = LraParser.Parse("3*x + 5*y = 7");
        var model = LiaSolver.FindModel(f, XY, bound: 20);
        Assert.NotNull(model);
        var x = model!.Values["x"];
        var y = model.Values["y"];
        Assert.Equal(new BigInteger(7), 3 * x + 5 * y);
    }

    [Fact]
    public void MixedIntegerReal_IsSatisfiableButIntegerOnlyIsNot()
    {
        var f = LraParser.Parse("x = 2*y & x = 1");
        Assert.True(LiaSolver.IsSatisfiable(f, new[] { "x" }));        // y = 1/2 is a real
        Assert.False(LiaSolver.IsSatisfiable(f, new[] { "x", "y" }));  // y would have to be 1/2
    }

    [Theory]
    [InlineData("x >= 1 & x <= 3", true)]
    [InlineData("x >= 1 & x <= 0", false)]
    [InlineData("x > 0 & x < 1", false)]   // strict boundary: no integer strictly between 0 and 1
    [InlineData("x > 0 & x < 2", true)]    // x = 1
    public void Bounds_DecideIntegerFeasibility(string input, bool expected)
    {
        Assert.Equal(expected, LiaSolver.IsSatisfiable(LraParser.Parse(input), new[] { "x" }));
    }

    [Fact]
    public void Validity_AndEntailment()
    {
        Assert.True(LiaSolver.IsValid(LraParser.Parse("x <= 5 -> x <= 6"), new[] { "x" }));
        Assert.True(LiaSolver.Entails(new[] { LraParser.Parse("x >= 2") }, LraParser.Parse("x >= 1"), new[] { "x" }));
        Assert.False(LiaSolver.Entails(new[] { LraParser.Parse("x >= 1") }, LraParser.Parse("x >= 2"), new[] { "x" }));
    }

    [Fact]
    public void FindModel_NegativeAndZero()
    {
        var model = LiaSolver.FindModel(LraParser.Parse("x + y = 0 & x >= 1"), XY, bound: 10);
        Assert.NotNull(model);
        Assert.Equal(BigInteger.Zero, model!.Values["x"] + model.Values["y"]);
        Assert.True(model.Values["x"] >= 1);
    }

    [Fact]
    public void Differential_AgreesWithIntegerBoxEnumeration()
    {
        const int bound = 4;
        var rng = new Random(0x71A);

        for (var trial = 0; trial < 250; trial++)
        {
            var formula = RandomFormula(rng, depth: 2);
            var byLia = LiaSolver.IsSatisfiable(formula, XY, bound);
            var byEnumeration = HasIntegerPoint(formula, bound);
            Assert.Equal(byEnumeration, byLia);
        }
    }

    // ---- brute-force oracle ----

    private static bool HasIntegerPoint(SmtFormula formula, int bound)
    {
        for (var x = -bound; x <= bound; x++)
        {
            for (var y = -bound; y <= bound; y++)
            {
                var assignment = new Dictionary<string, Rational>(StringComparer.Ordinal)
                {
                    ["x"] = Rational.Of(x),
                    ["y"] = Rational.Of(y),
                };
                if (Evaluate(formula, assignment))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool Evaluate(SmtFormula f, IReadOnlyDictionary<string, Rational> a) => f switch
    {
        SmtBool b => b.Value,
        SmtNot n => !Evaluate(n.Operand, a),
        SmtAnd x => Evaluate(x.Left, a) && Evaluate(x.Right, a),
        SmtOr x => Evaluate(x.Left, a) || Evaluate(x.Right, a),
        SmtImplies x => !Evaluate(x.Antecedent, a) || Evaluate(x.Consequent, a),
        SmtIff x => Evaluate(x.Left, a) == Evaluate(x.Right, a),
        LinearConstraintAtom atom => EvaluateAtom(atom, a),
        _ => throw new InvalidOperationException($"Unexpected node: {f.GetType().Name}"),
    };

    private static bool EvaluateAtom(LinearConstraintAtom atom, IReadOnlyDictionary<string, Rational> a)
    {
        var sum = Rational.Zero;
        foreach (var term in atom.Terms)
        {
            sum += term.Coefficient * a[term.Variable];
        }
        var c = sum.CompareTo(atom.Constant);
        return atom.Relation switch
        {
            LinearRelation.LessOrEqual => c <= 0,
            LinearRelation.Less => c < 0,
            LinearRelation.GreaterOrEqual => c >= 0,
            LinearRelation.Greater => c > 0,
            LinearRelation.Equal => c == 0,
            _ => throw new InvalidOperationException(),
        };
    }

    // ---- random linear formulas over x, y ----

    private static SmtFormula RandomFormula(Random rng, int depth)
    {
        if (depth <= 0 || rng.NextDouble() < 0.5)
        {
            return RandomAtom(rng);
        }
        return rng.Next(4) switch
        {
            0 => new SmtAnd(RandomFormula(rng, depth - 1), RandomFormula(rng, depth - 1)),
            1 => new SmtOr(RandomFormula(rng, depth - 1), RandomFormula(rng, depth - 1)),
            2 => new SmtNot(RandomFormula(rng, depth - 1)),
            _ => new SmtImplies(RandomFormula(rng, depth - 1), RandomFormula(rng, depth - 1)),
        };
    }

    private static SmtFormula RandomAtom(Random rng)
    {
        var terms = new List<LinearAtomTerm>
        {
            new("x", Rational.Of(rng.Next(-3, 4))),
            new("y", Rational.Of(rng.Next(-3, 4))),
        };
        // Equality is excluded: the parser splits '=' into ≤ ∧ ≥ so the theory never
        // sees a (negatable) equality atom; raw equality atoms are covered elsewhere.
        var relations = new[]
        {
            LinearRelation.LessOrEqual, LinearRelation.Less,
            LinearRelation.GreaterOrEqual, LinearRelation.Greater,
        };
        return new LinearConstraintAtom(terms, relations[rng.Next(relations.Length)], Rational.Of(rng.Next(-5, 6)));
    }
}
