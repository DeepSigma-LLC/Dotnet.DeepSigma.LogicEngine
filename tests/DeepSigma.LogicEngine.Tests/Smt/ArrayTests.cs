using DeepSigma.LogicEngine.Smt;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Smt;

public class ArrayTests
{
    [Theory]
    [InlineData("select(store(a, i, v), i) = v")]                              // read-over-write, same index
    [InlineData("i != j -> select(store(a, i, v), j) = select(a, j)")]         // read-over-write, other index
    [InlineData("i = j -> select(store(a, i, v), j) = v")]
    [InlineData("i != j -> select(store(store(a, i, u), j, w), i) = u")]       // nested stores
    public void KnownArrayValidities(string input)
        => Assert.True(ArraySolver.IsValid(SmtParser.Parse(input)));

    [Theory]
    [InlineData("select(store(a, i, v), i) != v")]                            // contradicts read-over-write
    public void KnownArrayContradictions(string input)
        => Assert.False(ArraySolver.IsSatisfiable(SmtParser.Parse(input)));

    [Fact]
    public void DifferentIndexCanDiffer_IsSatisfiable()
        => Assert.True(ArraySolver.IsSatisfiable(SmtParser.Parse("i != j & select(store(a, i, v), j) != v")));

    [Fact]
    public void Differential_AgreesWithFiniteArrayModels()
    {
        var rng = new Random(0xA77);
        for (var trial = 0; trial < 200; trial++)
        {
            var formula = RandomFormula(rng, depth: 2);
            Assert.Equal(FiniteArrayOracle.IsSatisfiable(formula), ArraySolver.IsSatisfiable(formula));
        }
    }

    // ---- random formulas over {arrays a,b; indices/values i,j,u,v; select/store},
    //      with equalities only between element terms (so non-extensionality matches) ----

    private static SmtFormula RandomFormula(Random rng, int depth)
    {
        if (depth <= 0 || rng.NextDouble() < 0.5)
        {
            var left = ElementTerm(rng, depth);
            var right = ElementTerm(rng, depth);
            return rng.Next(2) == 0 ? SmtFormula.Eq(left, right) : new SmtNot(SmtFormula.Eq(left, right));
        }
        return rng.Next(4) switch
        {
            0 => new SmtNot(RandomFormula(rng, depth - 1)),
            1 => new SmtAnd(RandomFormula(rng, depth - 1), RandomFormula(rng, depth - 1)),
            2 => new SmtOr(RandomFormula(rng, depth - 1), RandomFormula(rng, depth - 1)),
            _ => new SmtImplies(RandomFormula(rng, depth - 1), RandomFormula(rng, depth - 1)),
        };
    }

    private static Term ElementTerm(Random rng, int depth)
    {
        if (depth > 0 && rng.NextDouble() < 0.5)
        {
            return Term.Func("select", ArrayTerm(rng, depth - 1), IndexTerm(rng));
        }
        return Term.Constant(new[] { "i", "j", "u", "v" }[rng.Next(4)]);
    }

    private static Term ArrayTerm(Random rng, int depth)
    {
        if (depth > 0 && rng.NextDouble() < 0.5)
        {
            return Term.Func("store", ArrayTerm(rng, depth - 1), IndexTerm(rng), ElementTerm(rng, depth - 1));
        }
        return Term.Constant(rng.Next(2) == 0 ? "a" : "b");
    }

    private static Term IndexTerm(Random rng) => Term.Constant(rng.Next(2) == 0 ? "i" : "j");
}

/// <summary>
/// Brute-force finite-model oracle for the array fragment: indices/values range over
/// {0, 1} and arrays are functions {0,1}→{0,1}. Used only with formulas whose atoms
/// compare element terms (never whole arrays), so non-extensional and extensional
/// semantics coincide — a sound differential check for the read-over-write reduction.
/// </summary>
internal static class FiniteArrayOracle
{
    public static bool IsSatisfiable(SmtFormula formula)
    {
        foreach (var elems in ElementAssignments())
        {
            foreach (var arrays in ArrayAssignments())
            {
                if (Eval(formula, elems, arrays))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IEnumerable<Dictionary<string, int>> ElementAssignments()
    {
        var names = new[] { "i", "j", "u", "v" };
        for (var mask = 0; mask < 16; mask++)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var b = 0; b < names.Length; b++)
            {
                map[names[b]] = (mask >> b) & 1;
            }
            yield return map;
        }
    }

    private static IEnumerable<Dictionary<string, int[]>> ArrayAssignments()
    {
        for (var a = 0; a < 4; a++)
        {
            for (var b = 0; b < 4; b++)
            {
                yield return new Dictionary<string, int[]>(StringComparer.Ordinal)
                {
                    ["a"] = new[] { a & 1, (a >> 1) & 1 },
                    ["b"] = new[] { b & 1, (b >> 1) & 1 },
                };
            }
        }
    }

    private static bool Eval(SmtFormula f, Dictionary<string, int> elems, Dictionary<string, int[]> arrays) => f switch
    {
        SmtBool x => x.Value,
        EqualityAtom e => EvalElement(e.Left, elems, arrays) == EvalElement(e.Right, elems, arrays),
        SmtNot n => !Eval(n.Operand, elems, arrays),
        SmtAnd x => Eval(x.Left, elems, arrays) && Eval(x.Right, elems, arrays),
        SmtOr x => Eval(x.Left, elems, arrays) || Eval(x.Right, elems, arrays),
        SmtImplies x => !Eval(x.Antecedent, elems, arrays) || Eval(x.Consequent, elems, arrays),
        SmtIff x => Eval(x.Left, elems, arrays) == Eval(x.Right, elems, arrays),
        _ => throw new InvalidOperationException(),
    };

    private static int EvalElement(Term t, Dictionary<string, int> elems, Dictionary<string, int[]> arrays)
    {
        if (t.Arguments.Count == 0)
        {
            return elems[t.Symbol];
        }
        // select(array, index)
        var array = EvalArray(t.Arguments[0], elems, arrays);
        var index = EvalElement(t.Arguments[1], elems, arrays);
        return array[index];
    }

    private static int[] EvalArray(Term t, Dictionary<string, int> elems, Dictionary<string, int[]> arrays)
    {
        if (t.Arguments.Count == 0)
        {
            return arrays[t.Symbol];
        }
        // store(array, index, value)
        var array = (int[])EvalArray(t.Arguments[0], elems, arrays).Clone();
        var index = EvalElement(t.Arguments[1], elems, arrays);
        var value = EvalElement(t.Arguments[2], elems, arrays);
        array[index] = value;
        return array;
    }
}
