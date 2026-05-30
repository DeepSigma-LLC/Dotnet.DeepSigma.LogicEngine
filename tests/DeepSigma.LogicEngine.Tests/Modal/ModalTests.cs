using DeepSigma.LogicEngine.Modal;
using DeepSigma.LogicEngine.Reasoning;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Modal;

public class ModalParserTests
{
    [Theory]
    [InlineData("[]p -> p")]
    [InlineData("<>p & []q")]
    [InlineData("[](p -> q) -> ([]p -> []q)")]
    [InlineData("<>[]p <-> ![]<>!p")]
    public void Parses(string input) => Assert.NotNull(ModalParser.Parse(input));
}

public class ModalSolverTests
{
    // The K axiom is valid in every system.
    [Theory]
    [InlineData(ModalSystem.K)]
    [InlineData(ModalSystem.T)]
    [InlineData(ModalSystem.S4)]
    [InlineData(ModalSystem.S5)]
    public void KAxiom_ValidEverywhere(ModalSystem system)
    {
        Assert.True(ModalSolver.IsValid(ModalParser.Parse("[](p -> q) -> ([]p -> []q)"), system));
    }

    [Fact]
    public void TAxiom_ValidInTButNotK()
    {
        var t = ModalParser.Parse("[]p -> p");
        Assert.False(ModalSolver.IsValid(t, ModalSystem.K));
        Assert.True(ModalSolver.IsValid(t, ModalSystem.T));
        Assert.True(ModalSolver.IsValid(t, ModalSystem.S4));
        Assert.True(ModalSolver.IsValid(t, ModalSystem.S5));
    }

    [Fact]
    public void FourAxiom_ValidInS4ButNotT()
    {
        var four = ModalParser.Parse("[]p -> [][]p");
        Assert.False(ModalSolver.IsValid(four, ModalSystem.T));
        Assert.True(ModalSolver.IsValid(four, ModalSystem.S4));
        Assert.True(ModalSolver.IsValid(four, ModalSystem.S5));
    }

    [Fact]
    public void FiveAxiom_ValidInS5ButNotS4()
    {
        var five = ModalParser.Parse("<>p -> []<>p");
        Assert.False(ModalSolver.IsValid(five, ModalSystem.S4));
        Assert.True(ModalSolver.IsValid(five, ModalSystem.S5));
    }

    [Fact]
    public void BAxiom_ValidInBAndS5()
    {
        var b = ModalParser.Parse("p -> []<>p");
        Assert.False(ModalSolver.IsValid(b, ModalSystem.K));
        Assert.True(ModalSolver.IsValid(b, ModalSystem.B));
        Assert.True(ModalSolver.IsValid(b, ModalSystem.S5));
    }

    [Theory]
    [InlineData("<>p & <>!p", true)]   // two distinct successors
    [InlineData("[]p & <>!p", false)]  // all successors p, yet a successor !p
    [InlineData("[]false", true)]      // a dead end (no successors) in K
    public void Satisfiability_K(string input, bool expected)
    {
        Assert.Equal(expected, ModalSolver.IsSatisfiable(ModalParser.Parse(input), ModalSystem.K));
    }

    [Fact]
    public void Differential_EncoderAgreesWithKripkeOracle()
    {
        var rng = new Random(0x33D);
        var atoms = new[] { "p", "q" };
        var systems = new[] { ModalSystem.K, ModalSystem.T, ModalSystem.S4, ModalSystem.S5 };
        foreach (var system in systems)
        {
            for (var trial = 0; trial < 40; trial++)
            {
                var formula = RandomModal(rng, depth: 3, atoms);
                for (var n = 1; n <= 2; n++)
                {
                    var encoderSat = Reasoner.IsSatisfiable(ModalSolver.EncodeAt(formula, system, n));
                    var oracleSat = KripkeOracle.ExistsModel(formula, atoms, system, n);
                    Assert.Equal(oracleSat, encoderSat);
                }
            }
        }
    }

    private static ModalFormula RandomModal(Random rng, int depth, string[] atoms)
    {
        if (depth == 0 || rng.NextDouble() < 0.35)
        {
            return ModalFormula.Atom(atoms[rng.Next(atoms.Length)]);
        }
        return rng.Next(7) switch
        {
            0 => new ModalNot(RandomModal(rng, depth - 1, atoms)),
            1 => new ModalBox(RandomModal(rng, depth - 1, atoms)),
            2 => new ModalDiamond(RandomModal(rng, depth - 1, atoms)),
            3 => new ModalAnd(RandomModal(rng, depth - 1, atoms), RandomModal(rng, depth - 1, atoms)),
            4 => new ModalOr(RandomModal(rng, depth - 1, atoms), RandomModal(rng, depth - 1, atoms)),
            5 => new ModalImplies(RandomModal(rng, depth - 1, atoms), RandomModal(rng, depth - 1, atoms)),
            _ => new ModalIff(RandomModal(rng, depth - 1, atoms), RandomModal(rng, depth - 1, atoms)),
        };
    }
}

/// <summary>
/// Independent brute-force Kripke-model oracle: enumerate all frames on n worlds
/// satisfying the system's conditions and all valuations, and check whether the
/// formula holds at world 0. Used to differentially validate the SAT encoder.
/// </summary>
internal static class KripkeOracle
{
    public static bool ExistsModel(ModalFormula formula, string[] atoms, ModalSystem system, int n)
    {
        var edges = n * n;
        var valBits = atoms.Length * n;
        for (long r = 0; r < (1L << edges); r++)
        {
            var rel = new bool[n, n];
            for (var u = 0; u < n; u++)
            {
                for (var v = 0; v < n; v++)
                {
                    rel[u, v] = (r & (1L << (u * n + v))) != 0;
                }
            }
            if (!FrameValid(rel, n, system))
            {
                continue;
            }
            for (long val = 0; val < (1L << valBits); val++)
            {
                var valuation = new bool[atoms.Length, n];
                for (var a = 0; a < atoms.Length; a++)
                {
                    for (var w = 0; w < n; w++)
                    {
                        valuation[a, w] = (val & (1L << (a * n + w))) != 0;
                    }
                }
                if (Eval(formula, 0, rel, valuation, atoms, n))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool FrameValid(bool[,] r, int n, ModalSystem system)
    {
        var reflexive = system is ModalSystem.T or ModalSystem.B or ModalSystem.S4 or ModalSystem.S5;
        var symmetric = system is ModalSystem.B or ModalSystem.S5;
        var transitive = system is ModalSystem.S4 or ModalSystem.S5;
        for (var u = 0; u < n; u++)
        {
            if (reflexive && !r[u, u]) return false;
            for (var v = 0; v < n; v++)
            {
                if (symmetric && r[u, v] && !r[v, u]) return false;
                if (transitive)
                {
                    for (var x = 0; x < n; x++)
                    {
                        if (r[u, v] && r[v, x] && !r[u, x]) return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool Eval(ModalFormula f, int w, bool[,] r, bool[,] val, string[] atoms, int n)
    {
        switch (f)
        {
            case ModalBool b: return b.Value;
            case ModalAtom a: return val[Array.IndexOf(atoms, a.Name), w];
            case ModalNot u: return !Eval(u.Operand, w, r, val, atoms, n);
            case ModalAnd x: return Eval(x.Left, w, r, val, atoms, n) && Eval(x.Right, w, r, val, atoms, n);
            case ModalOr x: return Eval(x.Left, w, r, val, atoms, n) || Eval(x.Right, w, r, val, atoms, n);
            case ModalImplies x: return !Eval(x.Left, w, r, val, atoms, n) || Eval(x.Right, w, r, val, atoms, n);
            case ModalIff x: return Eval(x.Left, w, r, val, atoms, n) == Eval(x.Right, w, r, val, atoms, n);
            case ModalBox x:
                for (var v = 0; v < n; v++) { if (r[w, v] && !Eval(x.Operand, v, r, val, atoms, n)) return false; }
                return true;
            case ModalDiamond x:
                for (var v = 0; v < n; v++) { if (r[w, v] && Eval(x.Operand, v, r, val, atoms, n)) return true; }
                return false;
            default: throw new InvalidOperationException();
        }
    }
}
