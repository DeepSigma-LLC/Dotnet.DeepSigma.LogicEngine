using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Temporal;
using DeepSigma.LogicEngine.Transitions;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.Temporal;

public class LtlParserTests
{
    [Theory]
    [InlineData("F a")]
    [InlineData("G (a -> X b)")]
    [InlineData("a U b")]
    [InlineData("G F a")]
    [InlineData("!(a U b) <-> ((!a) R (!b))")]
    public void Parses(string input)
    {
        Assert.NotNull(LtlParser.Parse(input));
    }

    [Fact]
    public void Nnf_PushesNegationsToAtoms()
    {
        // ¬(a U b) becomes (¬a) R (¬b)
        var nnf = LtlParser.Parse("!(a U b)").ToNnf();
        Assert.IsType<LtlRelease>(nnf);
    }
}

public class BoundedModelCheckerTests
{
    [Theory]
    [InlineData("F a")]
    [InlineData("G a")]
    [InlineData("G F a")]
    [InlineData("a U b")]
    [InlineData("a & X (!a) & X X a")]
    [InlineData("G (a -> X a)")]
    public void Satisfiable_FormulasFound(string input)
    {
        var result = BoundedModelChecker.FindWitness(LtlParser.Parse(input), maxBound: 6);
        Assert.True(result.Found, $"expected a lasso for {input}");
        Assert.NotNull(result.Trace);
    }

    [Theory]
    [InlineData("F a & G !a")]   // eventually a but always not a
    [InlineData("a & !a")]
    [InlineData("(G a) & (F !a)")]
    public void Unsatisfiable_FormulasNotFound(string input)
    {
        var result = BoundedModelChecker.FindWitness(LtlParser.Parse(input), maxBound: 6);
        Assert.False(result.Found);
    }

    [Fact]
    public void Counterexample_ToggleViolatesGloballyNotX()
    {
        // Toggle: x flips each step starting false → x becomes true, so "G !x" fails.
        var toggle = new TransitionSystem(
            new[] { "x" },
            Initial: new Negation(Formula.Var("x")),
            Transition: new Biconditional(Formula.Var("x'"), new Negation(Formula.Var("x"))));

        var ce = BoundedModelChecker.FindCounterexample(toggle, LtlFormula.Parse("G !x"), maxBound: 6);
        Assert.NotNull(ce);
        // some state on the trace has x true
        Assert.Contains(ce!.States, s => s["x"]);
    }

    [Fact]
    public void NoCounterexample_WhenPropertyHolds()
    {
        // Toggle never has x true on two consecutive steps: G(x -> X !x) holds.
        var toggle = new TransitionSystem(
            new[] { "x" },
            Initial: new Negation(Formula.Var("x")),
            Transition: new Biconditional(Formula.Var("x'"), new Negation(Formula.Var("x"))));

        var property = LtlFormula.Parse("G (x -> X !x)");
        var ce = BoundedModelChecker.FindCounterexample(toggle, property, maxBound: 8);
        Assert.Null(ce);
    }

    [Fact]
    public void Differential_EncoderAgreesWithLassoOracle()
    {
        var rng = new Random(0xB3C);
        var atoms = new[] { "a", "b" };
        for (var trial = 0; trial < 120; trial++)
        {
            var formula = RandomLtl(rng, depth: 3, atoms);
            for (var k = 0; k <= 2; k++)
            {
                var encoderSat = Reasoner.IsSatisfiable(BoundedModelChecker.EncodeSatisfiability(formula, k));
                var oracleSat = LassoOracle.ExistsLasso(formula, atoms, k);
                Assert.Equal(oracleSat, encoderSat);
            }
        }
    }

    private static LtlFormula RandomLtl(Random rng, int depth, string[] atoms)
    {
        if (depth == 0 || rng.NextDouble() < 0.35)
        {
            return LtlFormula.Atom(atoms[rng.Next(atoms.Length)]);
        }
        return rng.Next(9) switch
        {
            0 => new LtlNot(RandomLtl(rng, depth - 1, atoms)),
            1 => new LtlNext(RandomLtl(rng, depth - 1, atoms)),
            2 => new LtlEventually(RandomLtl(rng, depth - 1, atoms)),
            3 => new LtlGlobally(RandomLtl(rng, depth - 1, atoms)),
            4 => new LtlAnd(RandomLtl(rng, depth - 1, atoms), RandomLtl(rng, depth - 1, atoms)),
            5 => new LtlOr(RandomLtl(rng, depth - 1, atoms), RandomLtl(rng, depth - 1, atoms)),
            6 => new LtlUntil(RandomLtl(rng, depth - 1, atoms), RandomLtl(rng, depth - 1, atoms)),
            7 => new LtlRelease(RandomLtl(rng, depth - 1, atoms), RandomLtl(rng, depth - 1, atoms)),
            _ => new LtlImplies(RandomLtl(rng, depth - 1, atoms), RandomLtl(rng, depth - 1, atoms)),
        };
    }
}

/// <summary>
/// Independent (simulation-based) LTL semantics over ultimately-periodic (lasso)
/// traces, used to differentially check the bounded encoder. Deliberately
/// formulated differently from the encoder (forward simulation vs. range
/// expansion) so the two are unlikely to share a bug.
/// </summary>
internal static class LassoOracle
{
    public static bool ExistsLasso(LtlFormula formula, string[] atoms, int k)
    {
        var positions = k + 1;
        var bits = positions * atoms.Length;
        for (long mask = 0; mask < (1L << bits); mask++)
        {
            var states = new bool[positions, atoms.Length];
            for (var i = 0; i < positions; i++)
            {
                for (var a = 0; a < atoms.Length; a++)
                {
                    states[i, a] = (mask & (1L << (i * atoms.Length + a))) != 0;
                }
            }
            for (var l = 0; l <= k; l++)
            {
                if (Eval(formula, 0, states, atoms, l, k))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int Succ(int pos, int l, int k) => pos < k ? pos + 1 : l;

    private static bool Holds(string atom, int pos, bool[,] states, string[] atoms)
        => states[pos, Array.IndexOf(atoms, atom)];

    private static bool Eval(LtlFormula f, int pos, bool[,] states, string[] atoms, int l, int k)
    {
        var steps = 2 * (k + 1);
        switch (f)
        {
            case LtlBool b: return b.Value;
            case LtlAtom a: return Holds(a.Name, pos, states, atoms);
            case LtlNot n: return !Eval(n.Operand, pos, states, atoms, l, k);
            case LtlAnd x: return Eval(x.Left, pos, states, atoms, l, k) && Eval(x.Right, pos, states, atoms, l, k);
            case LtlOr x: return Eval(x.Left, pos, states, atoms, l, k) || Eval(x.Right, pos, states, atoms, l, k);
            case LtlImplies x: return !Eval(x.Left, pos, states, atoms, l, k) || Eval(x.Right, pos, states, atoms, l, k);
            case LtlIff x: return Eval(x.Left, pos, states, atoms, l, k) == Eval(x.Right, pos, states, atoms, l, k);
            case LtlNext x: return Eval(x.Operand, Succ(pos, l, k), states, atoms, l, k);
            case LtlEventually e:
            {
                var p = pos;
                for (var s = 0; s < steps; s++) { if (Eval(e.Operand, p, states, atoms, l, k)) return true; p = Succ(p, l, k); }
                return false;
            }
            case LtlGlobally g:
            {
                var p = pos;
                for (var s = 0; s < steps; s++) { if (!Eval(g.Operand, p, states, atoms, l, k)) return false; p = Succ(p, l, k); }
                return true;
            }
            case LtlUntil u:
            {
                var p = pos;
                for (var s = 0; s < steps; s++)
                {
                    if (Eval(u.Right, p, states, atoms, l, k)) return true;
                    if (!Eval(u.Left, p, states, atoms, l, k)) return false;
                    p = Succ(p, l, k);
                }
                return false;
            }
            case LtlRelease r:
            {
                var p = pos;
                for (var s = 0; s < steps; s++)
                {
                    if (!Eval(r.Right, p, states, atoms, l, k)) return false;
                    if (Eval(r.Left, p, states, atoms, l, k)) return true;
                    p = Succ(p, l, k);
                }
                return true;
            }
            case LtlWeakUntil w:
            {
                var p = pos;
                for (var s = 0; s < steps; s++)
                {
                    if (Eval(w.Right, p, states, atoms, l, k)) return true;
                    if (!Eval(w.Left, p, states, atoms, l, k)) return false;
                    p = Succ(p, l, k);
                }
                return true;
            }
            default: throw new InvalidOperationException();
        }
    }
}
