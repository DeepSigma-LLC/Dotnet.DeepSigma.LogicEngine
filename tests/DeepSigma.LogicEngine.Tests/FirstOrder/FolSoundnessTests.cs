using DeepSigma.LogicEngine.FirstOrder;
using Xunit;

namespace DeepSigma.LogicEngine.Tests.FirstOrder;

public class FolSoundnessTests
{
    // The resolution prover must never refute a satisfiable set of formulas. We
    // generate random formulas over a small fixed signature, certify satisfiability
    // with a brute-force finite-model search over a 2-element domain, and assert the
    // prover does NOT report Proved for any set that has a model.
    [Fact]
    public void NeverRefutesASatisfiableSet()
    {
        var rng = new Random(0xF01);
        // Equality axioms are unnecessary here: a real-equality model is also a model
        // when '=' is read as an uninterpreted predicate, so the soundness check still
        // holds, and dropping them keeps the test fast.
        var options = new FolOptions { MaxClauses = 300, UseParamodulation = false, IncludeEqualityAxioms = false };
        var checkedSatisfiable = 0;

        for (var trial = 0; trial < 60; trial++)
        {
            var assertions = Enumerable.Range(0, 1 + rng.Next(2))
                .Select(_ => Generate(rng, depth: 2, new List<string>()))
                .ToList();

            if (!FolModelOracle.HasModel(assertions))
            {
                continue; // no 2-element model — make no claim
            }
            checkedSatisfiable++;
            Assert.NotEqual(FolProofStatus.Proved, FirstOrderProver.Refute(assertions, options));
        }

        Assert.True(checkedSatisfiable > 10, $"expected many satisfiable cases, saw {checkedSatisfiable}");
    }

    private static FolFormula Generate(Random rng, int depth, List<string> scope)
    {
        if (depth <= 0 || rng.NextDouble() < 0.4)
        {
            return Atom(rng, scope);
        }
        switch (rng.Next(6))
        {
            case 0: return new FolNot(Generate(rng, depth - 1, scope));
            case 1: return new FolAnd(Generate(rng, depth - 1, scope), Generate(rng, depth - 1, scope));
            case 2: return new FolOr(Generate(rng, depth - 1, scope), Generate(rng, depth - 1, scope));
            case 3: return new FolImplies(Generate(rng, depth - 1, scope), Generate(rng, depth - 1, scope));
            default:
                var name = "x" + depth;
                var inner = new List<string>(scope) { name };
                var body = Generate(rng, depth - 1, inner);
                return rng.Next(2) == 0 ? new FolForall(name, body) : new FolExists(name, body);
        }
    }

    private static FolFormula Atom(Random rng, List<string> scope) => rng.Next(3) switch
    {
        0 => new FolPredicate("P", new[] { Term(rng, scope, 2) }),
        1 => new FolPredicate("Q", new[] { Term(rng, scope, 2) }),
        _ => new FolEquals(Term(rng, scope, 2), Term(rng, scope, 2)),
    };

    private static FolTerm Term(Random rng, List<string> scope, int depth)
    {
        if (scope.Count > 0 && rng.NextDouble() < 0.4)
        {
            return new FolVar(scope[rng.Next(scope.Count)]);
        }
        if (depth > 0 && rng.NextDouble() < 0.3)
        {
            return FolTerm.Func("f", Term(rng, scope, depth - 1));
        }
        return FolTerm.Constant(rng.Next(2) == 0 ? "a" : "b");
    }
}

/// <summary>
/// Brute-force finite-model oracle over the signature {constants a, b; unary
/// function f; unary predicates P, Q; equality} on the domain {0, 1}. Reports
/// whether a set of formulas has a model — used to certify satisfiability so the
/// resolution prover's soundness can be checked differentially.
/// </summary>
internal static class FolModelOracle
{
    private sealed record Interpretation(int A, int B, int[] F, bool[] P, bool[] Q);

    public static bool HasModel(IReadOnlyList<FolFormula> assertions)
    {
        foreach (var interp in Enumerate())
        {
            if (assertions.All(f => Eval(f, interp, new Dictionary<string, int>(StringComparer.Ordinal))))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<Interpretation> Enumerate()
    {
        for (var a = 0; a < 2; a++)
        for (var b = 0; b < 2; b++)
        for (var f0 = 0; f0 < 2; f0++)
        for (var f1 = 0; f1 < 2; f1++)
        for (var p = 0; p < 4; p++)
        for (var q = 0; q < 4; q++)
        {
            yield return new Interpretation(
                a, b, new[] { f0, f1 },
                new[] { (p & 1) != 0, (p & 2) != 0 },
                new[] { (q & 1) != 0, (q & 2) != 0 });
        }
    }

    private static bool Eval(FolFormula f, Interpretation m, Dictionary<string, int> env)
    {
        switch (f)
        {
            case FolBool b: return b.Value;
            case FolPredicate p:
                var arg = EvalTerm(p.Arguments[0], m, env);
                return p.Symbol == "P" ? m.P[arg] : m.Q[arg];
            case FolEquals e: return EvalTerm(e.Left, m, env) == EvalTerm(e.Right, m, env);
            case FolNot n: return !Eval(n.Operand, m, env);
            case FolAnd x: return Eval(x.Left, m, env) && Eval(x.Right, m, env);
            case FolOr x: return Eval(x.Left, m, env) || Eval(x.Right, m, env);
            case FolImplies x: return !Eval(x.Antecedent, m, env) || Eval(x.Consequent, m, env);
            case FolIff x: return Eval(x.Left, m, env) == Eval(x.Right, m, env);
            case FolForall q: return EvalQuantified(q.Variable, q.Body, m, env, requireAll: true);
            case FolExists q: return EvalQuantified(q.Variable, q.Body, m, env, requireAll: false);
            default: throw new InvalidOperationException();
        }
    }

    private static bool EvalQuantified(string variable, FolFormula body, Interpretation m, Dictionary<string, int> env, bool requireAll)
    {
        var had = env.TryGetValue(variable, out var saved);
        try
        {
            for (var d = 0; d < 2; d++)
            {
                env[variable] = d;
                var holds = Eval(body, m, env);
                if (requireAll && !holds) { return false; }
                if (!requireAll && holds) { return true; }
            }
            return requireAll;
        }
        finally
        {
            if (had) { env[variable] = saved; } else { env.Remove(variable); }
        }
    }

    private static int EvalTerm(FolTerm t, Interpretation m, Dictionary<string, int> env) => t switch
    {
        FolVar v => env[v.Name],
        FolFunc f when f.Symbol == "a" => m.A,
        FolFunc f when f.Symbol == "b" => m.B,
        FolFunc f when f.Symbol == "f" => m.F[EvalTerm(f.Arguments[0], m, env)],
        _ => throw new InvalidOperationException($"Unexpected symbol: {t}"),
    };
}
