using DeepSigma.LogicEngine.Fuzzy;
using DeepSigma.Mathematics.Algebra;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Fuzzy;

public class FuzzySolverTests
{
    private static FuzzyFormula A => FuzzyFormula.Var("a");
    private static FuzzyFormula B => FuzzyFormula.Var("b");

    [Fact]
    public void ExcludedMiddle_ValidInLukasiewicz_NotGodel()
    {
        var em = FuzzyFormula.Or(A, FuzzyFormula.Not(A)); // a ∨ ¬a
        Assert.True(FuzzySolver.IsValid(em, FuzzyLogic.Lukasiewicz));   // min(1, a+(1-a)) = 1
        Assert.False(FuzzySolver.IsValid(em, FuzzyLogic.Godel));        // max(a,1-a) = 0.5 at a=0.5
    }

    [Fact]
    public void NonContradiction_ValidInLukasiewicz()
    {
        var nc = FuzzyFormula.Not(FuzzyFormula.And(A, FuzzyFormula.Not(A))); // ¬(a ∧ ¬a)
        Assert.True(FuzzySolver.IsValid(nc, FuzzyLogic.Lukasiewicz));        // a⊗¬a = 0, ¬0 = 1
    }

    [Fact]
    public void SelfImplication_ValidInBoth()
    {
        var f = FuzzyFormula.Implies(A, A);
        Assert.True(FuzzySolver.IsValid(f, FuzzyLogic.Godel));
        Assert.True(FuzzySolver.IsValid(f, FuzzyLogic.Lukasiewicz));
    }

    [Fact]
    public void ModusPonens_ValidInLukasiewicz()
    {
        // (a ∧ (a -> b)) -> b is a Łukasiewicz tautology.
        var mp = FuzzyFormula.Implies(FuzzyFormula.And(A, FuzzyFormula.Implies(A, B)), B);
        Assert.True(FuzzySolver.IsValid(mp, FuzzyLogic.Lukasiewicz));
    }

    [Fact]
    public void Variable_NotValid_ButSatisfiable()
    {
        Assert.False(FuzzySolver.IsValid(A, FuzzyLogic.Godel));        // a can be 0
        Assert.True(FuzzySolver.IsSatisfiable(A, FuzzyLogic.Godel));   // a can be 1
    }

    [Fact]
    public void GodelConjunction_ThresholdReachability()
    {
        var f = FuzzyFormula.And(A, FuzzyFormula.Not(A)); // min(a, 1-a), max value 0.5
        Assert.True(FuzzySolver.IsSatisfiable(f, FuzzyLogic.Godel, Rational.Of(1, 2)));
        Assert.False(FuzzySolver.IsSatisfiable(f, FuzzyLogic.Godel, Rational.Of(3, 5))); // 0.6 unreachable
    }

    [Theory]
    [InlineData(FuzzyLogic.Godel)]
    [InlineData(FuzzyLogic.Lukasiewicz)]
    public void Differential_GridSanity(FuzzyLogic logic)
    {
        var rng = new Random(0xF5);
        var atoms = new[] { "a", "b" };
        var grid = new[] { Rational.Zero, Rational.Of(1, 4), Rational.Of(1, 2), Rational.Of(3, 4), Rational.One };

        for (var trial = 0; trial < 40; trial++)
        {
            var f = RandomFuzzy(rng, 3, atoms);
            var threshold = grid[rng.Next(grid.Length)];

            var gridSat = GridFindsAtLeast(f, logic, atoms, grid, threshold);
            var gridValid = GridAllAtLeast(f, logic, atoms, grid, threshold);

            // A grid witness ⇒ the continuous solver must agree it is satisfiable.
            if (gridSat)
            {
                Assert.True(FuzzySolver.IsSatisfiable(f, logic, threshold));
            }
            // If the solver declares validity, the grid (⊂ reals) cannot refute it.
            if (FuzzySolver.IsValid(f, logic, threshold))
            {
                Assert.True(gridValid);
            }
        }
    }

    private static bool GridFindsAtLeast(FuzzyFormula f, FuzzyLogic logic, string[] atoms, Rational[] grid, Rational threshold)
        => EnumerateGrid(atoms, grid).Any(v => Eval(f, v, logic) >= threshold);

    private static bool GridAllAtLeast(FuzzyFormula f, FuzzyLogic logic, string[] atoms, Rational[] grid, Rational threshold)
        => EnumerateGrid(atoms, grid).All(v => Eval(f, v, logic) >= threshold);

    private static IEnumerable<Dictionary<string, Rational>> EnumerateGrid(string[] atoms, Rational[] grid)
    {
        var n = atoms.Length;
        var total = (int)Math.Pow(grid.Length, n);
        for (var code = 0; code < total; code++)
        {
            var v = new Dictionary<string, Rational>(StringComparer.Ordinal);
            var c = code;
            foreach (var atom in atoms)
            {
                v[atom] = grid[c % grid.Length];
                c /= grid.Length;
            }
            yield return v;
        }
    }

    private static Rational Eval(FuzzyFormula f, Dictionary<string, Rational> val, FuzzyLogic logic)
    {
        Rational Min(Rational x, Rational y) => x <= y ? x : y;
        Rational Max(Rational x, Rational y) => x >= y ? x : y;
        switch (f)
        {
            case FuzzyConst c: return c.Value;
            case FuzzyVar v: return val[v.Name];
            case FuzzyNot n: return Rational.One - Eval(n.Operand, val, logic);
            case FuzzyAnd a:
                var ax = Eval(a.Left, val, logic); var ay = Eval(a.Right, val, logic);
                return logic == FuzzyLogic.Godel ? Min(ax, ay) : Max(Rational.Zero, ax + ay - Rational.One);
            case FuzzyOr o:
                var ox = Eval(o.Left, val, logic); var oy = Eval(o.Right, val, logic);
                return logic == FuzzyLogic.Godel ? Max(ox, oy) : Min(Rational.One, ox + oy);
            case FuzzyImplies i:
                var ix = Eval(i.Left, val, logic); var iy = Eval(i.Right, val, logic);
                return logic == FuzzyLogic.Godel ? (ix <= iy ? Rational.One : iy) : Min(Rational.One, Rational.One - ix + iy);
            default: throw new InvalidOperationException();
        }
    }

    private static FuzzyFormula RandomFuzzy(Random rng, int depth, string[] atoms)
    {
        if (depth == 0 || rng.NextDouble() < 0.4)
        {
            return FuzzyFormula.Var(atoms[rng.Next(atoms.Length)]);
        }
        return rng.Next(4) switch
        {
            0 => FuzzyFormula.Not(RandomFuzzy(rng, depth - 1, atoms)),
            1 => FuzzyFormula.And(RandomFuzzy(rng, depth - 1, atoms), RandomFuzzy(rng, depth - 1, atoms)),
            2 => FuzzyFormula.Or(RandomFuzzy(rng, depth - 1, atoms), RandomFuzzy(rng, depth - 1, atoms)),
            _ => FuzzyFormula.Implies(RandomFuzzy(rng, depth - 1, atoms), RandomFuzzy(rng, depth - 1, atoms)),
        };
    }
}
