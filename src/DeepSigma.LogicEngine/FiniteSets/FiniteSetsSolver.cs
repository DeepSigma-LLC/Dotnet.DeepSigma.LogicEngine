using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

namespace DeepSigma.LogicEngine.FiniteSets;

/// <summary>A concrete satisfying interpretation found by <see cref="FiniteSetsSolver.FindModel"/>.</summary>
public sealed record FiniteSetModel(
    int Universe,
    IReadOnlyDictionary<string, IReadOnlySet<int>> Sets,
    IReadOnlyDictionary<string, int> Elements);

/// <summary>
/// Decision procedures for finite-set logic over a bounded universe. Each set is
/// encoded as a vector of membership booleans over the universe slots, set
/// operations as per-slot Boolean ops, relations as their pointwise definitions,
/// cardinality via the sequential-counter encoder, and named elements as one-hot
/// slot selectors — reducing every question to propositional SAT (solved by
/// <see cref="Reasoner"/>).
///
/// <para>
/// <b>Semantics.</b> For the <i>cardinality-free</i> fragment a universe with one
/// slot per Venn region (2^#setvars) is a complete decision — that is the default.
/// Formulas using cardinality are decided <i>relative to the universe size</i>
/// (like bounded model checking): pass an explicit <c>universe</c> to control it.
/// </para>
/// </summary>
public static class FiniteSetsSolver
{
    /// <summary>The default universe is 2^min(#setvars, this); cardinality questions may need an explicit size.</summary>
    public const int MaxAutoExponent = 8;

    /// <summary>True if the formula holds in some interpretation over the (bounded) universe.</summary>
    public static bool IsSatisfiable(SetFormula formula, int? universe = null)
    {
        var encoder = new Encoder(ResolveUniverse(formula, universe));
        return Reasoner.IsSatisfiable(encoder.Encode(formula));
    }

    /// <summary>True if the formula holds in every interpretation over the (bounded) universe.</summary>
    public static bool IsValid(SetFormula formula, int? universe = null)
        => !IsSatisfiable(SetFormula.Not(formula), universe);

    /// <summary>A concrete satisfying interpretation, or null if the formula is unsatisfiable.</summary>
    public static FiniteSetModel? FindModel(SetFormula formula, int? universe = null)
    {
        var encoder = new Encoder(ResolveUniverse(formula, universe));
        var model = Reasoner.FindModel(encoder.Encode(formula));
        return model is null ? null : encoder.Decode(model);
    }

    private static int ResolveUniverse(SetFormula formula, int? universe)
    {
        if (universe is int u)
        {
            return u >= 1 ? u : throw new ArgumentOutOfRangeException(nameof(universe), "Universe size must be ≥ 1.");
        }
        var setVarCount = CollectSetVars(formula).Count;
        return 1 << Math.Min(setVarCount, MaxAutoExponent);
    }

    private static HashSet<string> CollectSetVars(SetFormula formula)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        void Walk(SetExpr e)
        {
            switch (e)
            {
                case SetVar v: names.Add(v.Name); break;
                case SetCompl n: Walk(n.Operand); break;
                case SetUnion x: Walk(x.Left); Walk(x.Right); break;
                case SetInter x: Walk(x.Left); Walk(x.Right); break;
                case SetDiff x: Walk(x.Left); Walk(x.Right); break;
                case SetSymDiff x: Walk(x.Left); Walk(x.Right); break;
            }
        }
        void WalkF(SetFormula f)
        {
            switch (f)
            {
                case MemberRel m: Walk(m.Set); break;
                case SubsetRel s: Walk(s.Left); Walk(s.Right); break;
                case EqualRel q: Walk(q.Left); Walk(q.Right); break;
                case DisjointRel d: Walk(d.Left); Walk(d.Right); break;
                case CardRel c: Walk(c.Set); break;
                case SetNot n: WalkF(n.Operand); break;
                case SetAnd a: WalkF(a.Left); WalkF(a.Right); break;
                case SetOr o: WalkF(o.Left); WalkF(o.Right); break;
                case SetImplies i: WalkF(i.Left); WalkF(i.Right); break;
                case SetIff bi: WalkF(bi.Left); WalkF(bi.Right); break;
            }
        }
        WalkF(formula);
        return names;
    }

    /// <summary>Encodes a finite-set formula to propositional logic over a fixed universe of n slots.</summary>
    private sealed class Encoder
    {
        private readonly int _n;
        private readonly List<Formula> _domain = new();          // element one-hot selectors
        private readonly HashSet<string> _setVars = new(StringComparer.Ordinal);
        private readonly HashSet<string> _elemVars = new(StringComparer.Ordinal);

        public Encoder(int n) => _n = n;

        public Formula Encode(SetFormula formula)
        {
            var body = EncodeFormula(formula);
            return Formula.All(_domain.Append(body));
        }

        public FiniteSetModel Decode(Model model)
        {
            var sets = new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal);
            foreach (var s in _setVars)
            {
                var members = new SortedSet<int>();
                for (var i = 0; i < _n; i++)
                {
                    if (model.TryGetValue(MemberVar(s, i), out var v) && v)
                    {
                        members.Add(i);
                    }
                }
                sets[s] = members;
            }

            var elements = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var e in _elemVars)
            {
                for (var i = 0; i < _n; i++)
                {
                    if (model.TryGetValue(ElementSel(e, i), out var v) && v)
                    {
                        elements[e] = i;
                        break;
                    }
                }
            }
            return new FiniteSetModel(_n, sets, elements);
        }

        private Formula EncodeFormula(SetFormula f) => f switch
        {
            MemberRel m => EncodeMember(m),
            SubsetRel s => EncodeSubset(s),
            EqualRel q => EncodeEqual(EncodeSet(q.Left), EncodeSet(q.Right)),
            DisjointRel d => EncodeDisjoint(EncodeSet(d.Left), EncodeSet(d.Right)),
            CardRel c => EncodeCard(c),
            SetNot n => Formula.Not(EncodeFormula(n.Operand)),
            SetAnd a => EncodeFormula(a.Left) & EncodeFormula(a.Right),
            SetOr o => EncodeFormula(o.Left) | EncodeFormula(o.Right),
            SetImplies i => Formula.Implies(EncodeFormula(i.Left), EncodeFormula(i.Right)),
            SetIff bi => Formula.Iff(EncodeFormula(bi.Left), EncodeFormula(bi.Right)),
            _ => throw new InvalidOperationException($"Unknown set formula: {f.GetType().Name}"),
        };

        private Formula[] EncodeSet(SetExpr e)
        {
            switch (e)
            {
                case SetVar v:
                    _setVars.Add(v.Name);
                    return Enumerable.Range(0, _n).Select(i => Formula.Var(MemberVar(v.Name, i))).ToArray();
                case SetConst c:
                    return Enumerable.Repeat(c.Full ? Formula.True : Formula.False, _n).ToArray();
                case SetCompl n:
                    return EncodeSet(n.Operand).Select(Formula.Not).ToArray();
                case SetUnion x:
                    return Zip(EncodeSet(x.Left), EncodeSet(x.Right), (a, b) => a | b);
                case SetInter x:
                    return Zip(EncodeSet(x.Left), EncodeSet(x.Right), (a, b) => a & b);
                case SetDiff x:
                    return Zip(EncodeSet(x.Left), EncodeSet(x.Right), (a, b) => a & Formula.Not(b));
                case SetSymDiff x:
                    return Zip(EncodeSet(x.Left), EncodeSet(x.Right), (a, b) => (a | b) & Formula.Not(a & b));
                default:
                    throw new InvalidOperationException($"Unknown set expression: {e.GetType().Name}");
            }
        }

        private Formula EncodeMember(MemberRel m)
        {
            var name = ((ElementVar)m.Element).Name;
            RegisterElement(name);
            var set = EncodeSet(m.Set);
            return Formula.Any(Enumerable.Range(0, _n).Select(i => Formula.Var(ElementSel(name, i)) & set[i]));
        }

        private Formula EncodeSubset(SubsetRel s)
        {
            var a = EncodeSet(s.Left);
            var b = EncodeSet(s.Right);
            var subset = Subset(a, b);
            return s.Proper ? subset & Formula.Not(EncodeEqual(a, b)) : subset;
        }

        private static Formula Subset(Formula[] a, Formula[] b)
            => Formula.All(a.Zip(b, (ai, bi) => Formula.Implies(ai, bi)));

        private static Formula EncodeEqual(Formula[] a, Formula[] b)
            => Formula.All(a.Zip(b, (ai, bi) => Formula.Iff(ai, bi)));

        private static Formula EncodeDisjoint(Formula[] a, Formula[] b)
            => Formula.All(a.Zip(b, (ai, bi) => Formula.Not(ai & bi)));

        private Formula EncodeCard(CardRel c)
        {
            // The auxiliary-free Cardinality encoder is used (not SequentialCounter)
            // because several cardinality atoms are conjoined here and a counter
            // encoding would reuse the same auxiliary-variable names across atoms.
            // It also self-clamps out-of-range bounds (k<0 ⇒ False, k≥n ⇒ True).
            var slots = EncodeSet(c.Set);
            return c.Op switch
            {
                CardOp.Eq => Cardinality.ExactlyK(slots, c.Bound),
                CardOp.Le => Cardinality.AtMostK(slots, c.Bound),
                CardOp.Lt => Cardinality.AtMostK(slots, c.Bound - 1),
                CardOp.Ge => Cardinality.AtLeastK(slots, c.Bound),
                CardOp.Gt => Cardinality.AtLeastK(slots, c.Bound + 1),
                _ => throw new InvalidOperationException(),
            };
        }

        private void RegisterElement(string name)
        {
            if (_elemVars.Add(name))
            {
                var selectors = Enumerable.Range(0, _n).Select(i => Formula.Var(ElementSel(name, i))).ToList();
                _domain.Add(Cardinality.ExactlyOne(selectors));
            }
        }

        private static Formula[] Zip(Formula[] a, Formula[] b, Func<Formula, Formula, Formula> op)
        {
            var result = new Formula[a.Length];
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = op(a[i], b[i]);
            }
            return result;
        }

        private static string MemberVar(string set, int slot) => $"__m_{set}_{slot}";
        private static string ElementSel(string element, int slot) => $"__x_{element}_{slot}";
    }
}
