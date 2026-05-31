using DeepSigma.LogicEngine.FiniteSets;

namespace DeepSigma.LogicEngine.Tests.FiniteSets;

/// <summary>
/// Independent brute-force reference for finite-set logic over a universe of n
/// slots: enumerate every assignment of the set variables (each a subset) and the
/// element variables (each a slot), evaluate the formula directly, and report
/// satisfiability / validity. Used to differentially validate the SAT encoder.
/// </summary>
internal static class FiniteSetOracle
{
    public static bool IsSatisfiable(SetFormula f, int n, string[] sets, string[] elements)
        => Interpretations(n, sets, elements).Any(interp => Eval(f, n, interp.Sets, interp.Elements));

    public static bool IsValid(SetFormula f, int n, string[] sets, string[] elements)
        => Interpretations(n, sets, elements).All(interp => Eval(f, n, interp.Sets, interp.Elements));

    public static bool Evaluate(
        SetFormula f, int n,
        IReadOnlyDictionary<string, IReadOnlySet<int>> sets,
        IReadOnlyDictionary<string, int> elements)
    {
        var vectors = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        foreach (var (name, members) in sets)
        {
            var v = new bool[n];
            foreach (var i in members)
            {
                v[i] = true;
            }
            vectors[name] = v;
        }
        return Eval(f, n, vectors, elements);
    }

    private static IEnumerable<(Dictionary<string, bool[]> Sets, Dictionary<string, int> Elements)> Interpretations(
        int n, string[] sets, string[] elements)
    {
        var setBits = sets.Length * n;
        var elementCombos = (long)Math.Pow(n, elements.Length);
        for (long mask = 0; mask < (1L << setBits); mask++)
        {
            var setVectors = new Dictionary<string, bool[]>(StringComparer.Ordinal);
            for (var s = 0; s < sets.Length; s++)
            {
                var v = new bool[n];
                for (var i = 0; i < n; i++)
                {
                    v[i] = (mask & (1L << (s * n + i))) != 0;
                }
                setVectors[sets[s]] = v;
            }
            for (long combo = 0; combo < elementCombos; combo++)
            {
                var elementSlots = new Dictionary<string, int>(StringComparer.Ordinal);
                var rest = combo;
                for (var e = 0; e < elements.Length; e++)
                {
                    elementSlots[elements[e]] = (int)(rest % n);
                    rest /= n;
                }
                yield return (setVectors, elementSlots);
            }
        }
    }

    private static bool Eval(SetFormula f, int n, Dictionary<string, bool[]> sets, IReadOnlyDictionary<string, int> elements)
    {
        switch (f)
        {
            case MemberRel m: return EvalSet(m.Set, n, sets)[elements[((ElementVar)m.Element).Name]];
            case SubsetRel s:
                var sl = EvalSet(s.Left, n, sets);
                var sr = EvalSet(s.Right, n, sets);
                var subset = !sl.Where((t, i) => t && !sr[i]).Any();
                return s.Proper ? subset && !sl.SequenceEqual(sr) : subset;
            case EqualRel q: return EvalSet(q.Left, n, sets).SequenceEqual(EvalSet(q.Right, n, sets));
            case DisjointRel d:
                var dl = EvalSet(d.Left, n, sets);
                var dr = EvalSet(d.Right, n, sets);
                return !dl.Where((t, i) => t && dr[i]).Any();
            case CardRel c:
                var count = EvalSet(c.Set, n, sets).Count(b => b);
                return c.Op switch
                {
                    CardOp.Equal => count == c.Bound,
                    CardOp.LessOrEqual => count <= c.Bound,
                    CardOp.Less => count < c.Bound,
                    CardOp.GreaterOrEqual => count >= c.Bound,
                    CardOp.Greater => count > c.Bound,
                    _ => throw new InvalidOperationException(),
                };
            case SetNot x: return !Eval(x.Operand, n, sets, elements);
            case SetAnd x: return Eval(x.Left, n, sets, elements) && Eval(x.Right, n, sets, elements);
            case SetOr x: return Eval(x.Left, n, sets, elements) || Eval(x.Right, n, sets, elements);
            case SetImplies x: return !Eval(x.Left, n, sets, elements) || Eval(x.Right, n, sets, elements);
            case SetIff x: return Eval(x.Left, n, sets, elements) == Eval(x.Right, n, sets, elements);
            default: throw new InvalidOperationException($"Unknown formula: {f.GetType().Name}");
        }
    }

    private static bool[] EvalSet(SetExpr e, int n, Dictionary<string, bool[]> sets)
    {
        switch (e)
        {
            case SetVar v: return sets.TryGetValue(v.Name, out var vec) ? vec : new bool[n];
            case SetConst c:
                var constant = new bool[n];
                if (c.Full)
                {
                    Array.Fill(constant, true);
                }
                return constant;
            case SetCompl x:
                return EvalSet(x.Operand, n, sets).Select(b => !b).ToArray();
            case SetUnion x: return Combine(EvalSet(x.Left, n, sets), EvalSet(x.Right, n, sets), (a, b) => a || b);
            case SetInter x: return Combine(EvalSet(x.Left, n, sets), EvalSet(x.Right, n, sets), (a, b) => a && b);
            case SetDiff x: return Combine(EvalSet(x.Left, n, sets), EvalSet(x.Right, n, sets), (a, b) => a && !b);
            case SetSymDiff x: return Combine(EvalSet(x.Left, n, sets), EvalSet(x.Right, n, sets), (a, b) => a ^ b);
            default: throw new InvalidOperationException($"Unknown set expression: {e.GetType().Name}");
        }
    }

    private static bool[] Combine(bool[] a, bool[] b, Func<bool, bool, bool> op)
    {
        var result = new bool[a.Length];
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = op(a[i], b[i]);
        }
        return result;
    }
}
