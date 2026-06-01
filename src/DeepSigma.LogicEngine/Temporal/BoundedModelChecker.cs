using DeepSigma.LogicEngine.Common;
using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Transitions;

namespace DeepSigma.LogicEngine.Temporal;

/// <summary>A bounded trace: states at positions 0..k that loops back from k to <see cref="LoopStart"/>.</summary>
public sealed record LtlTrace(IReadOnlyList<IReadOnlyDictionary<string, bool>> States, int LoopStart);

/// <summary>Outcome of a bounded search: a witnessing lasso trace at some bound, or none found up to the bound.</summary>
public sealed record LtlBmcResult(bool Found, int Bound, LtlTrace? Trace);

/// <summary>
/// Bounded model checking for LTL. Encodes "there is an ultimately-periodic
/// (lasso) trace of bound k satisfying φ" into a propositional formula solved by
/// the SAT back-end, increasing k until a witness is found or the bound is
/// reached. The bounded encoding follows LTL's semantics over lasso traces
/// directly (a back-loop from step k to a chosen step l).
///
/// <para>
/// BMC is complete only up to the search bound: "not found up to k" is not a
/// proof of unsatisfiability. Satisfiable formulas are found at the smallest k
/// admitting a lasso witness.
/// </para>
/// </summary>
public static class BoundedModelChecker
{
    /// <summary>Search for a lasso trace satisfying the LTL formula, for bounds 0..<paramref name="maxBound"/>.</summary>
    /// <param name="formula">The LTL formula to satisfy.</param>
    /// <param name="maxBound">Largest trace length k to try. A witness found is real; "not found" is bounded — not a proof of unsatisfiability.</param>
    public static LtlBmcResult CheckSatisfiable(LtlFormula formula, int maxBound = 10)
    {
        var nnf = formula.ToNnf();
        var atoms = formula.Atoms();
        var hit = BoundedSearch.FirstNonNull(0, maxBound, k =>
        {
            var model = Reasoner.FindModel(Encode(nnf, k, transitionSystem: null));
            return model is null ? null : new LtlBmcResult(true, k, ExtractTrace(model, atoms, k));
        });
        return hit ?? new LtlBmcResult(false, maxBound, null);
    }

    /// <summary>
    /// Search for a counterexample trace of <paramref name="system"/> violating
    /// <paramref name="property"/> (i.e. a system trace satisfying ¬property),
    /// for bounds 0..<paramref name="maxBound"/>. Returns the trace or null if
    /// none is found within the bound.
    /// </summary>
    /// <param name="system">The transition system to check (current state by name, next state by the primed name).</param>
    /// <param name="property">The LTL property expected to hold on every run; a returned trace violates it.</param>
    /// <param name="maxBound">Largest trace length k searched. A counterexample found is real; "none found" is bounded, not a proof the property holds.</param>
    public static LtlTrace? FindCounterexample(TransitionSystem system, LtlFormula property, int maxBound = 10)
    {
        var negated = new LtlNot(property).ToNnf();
        var atoms = new HashSet<string>(system.StateVariables, StringComparer.Ordinal);
        atoms.UnionWith(property.Atoms());
        return BoundedSearch.FirstNonNull(0, maxBound, k =>
        {
            var model = Reasoner.FindModel(Encode(negated, k, system));
            return model is null ? null : ExtractTrace(model, atoms, k);
        });
    }

    /// <summary>As <see cref="CheckSatisfiable(LtlFormula, int)"/>, but solving each bounded encoding with the supplied engine (e.g. a Z3-backed <see cref="ISatSolver"/>).</summary>
    /// <param name="formula">The LTL formula to satisfy.</param>
    /// <param name="solver">The SAT engine to solve each bounded lasso encoding with.</param>
    /// <param name="maxBound">Largest trace length k to try; the bound semantics are unchanged.</param>
    public static LtlBmcResult CheckSatisfiable(LtlFormula formula, ISatSolver solver, int maxBound = 10)
    {
        var nnf = formula.ToNnf();
        var atoms = formula.Atoms();
        var hit = BoundedSearch.FirstNonNull(0, maxBound, k =>
        {
            var model = Reasoner.FindModel(Encode(nnf, k, transitionSystem: null), solver);
            return model is null ? null : new LtlBmcResult(true, k, ExtractTrace(model, atoms, k));
        });
        return hit ?? new LtlBmcResult(false, maxBound, null);
    }

    /// <summary>As <see cref="FindCounterexample(TransitionSystem, LtlFormula, int)"/>, but solving with the supplied engine.</summary>
    /// <param name="system">The transition system to check.</param>
    /// <param name="property">The LTL property expected to hold; a returned trace violates it.</param>
    /// <param name="solver">The SAT engine to solve each bounded encoding with.</param>
    /// <param name="maxBound">Largest trace length k searched.</param>
    public static LtlTrace? FindCounterexample(TransitionSystem system, LtlFormula property, ISatSolver solver, int maxBound = 10)
    {
        var negated = new LtlNot(property).ToNnf();
        var atoms = new HashSet<string>(system.StateVariables, StringComparer.Ordinal);
        atoms.UnionWith(property.Atoms());
        return BoundedSearch.FirstNonNull(0, maxBound, k =>
        {
            var model = Reasoner.FindModel(Encode(negated, k, system), solver);
            return model is null ? null : ExtractTrace(model, atoms, k);
        });
    }

    // --- encoding ---------------------------------------------------------

    /// <summary>Encode "∃ lasso of exactly bound k satisfying the formula" (for testing).</summary>
    internal static Formula EncodeSatisfiability(LtlFormula formula, int k) => Encode(formula.ToNnf(), k, transitionSystem: null);

    private static string LoopVar(int l) => $"__ltl_loop_{l}";

    private static Formula Encode(LtlFormula nnf, int k, TransitionSystem? transitionSystem)
    {
        var selectors = Enumerable.Range(0, k + 1).Select(l => Formula.Var(LoopVar(l))).ToList();
        var parts = new List<Formula> { Cardinality.ExactlyOne(selectors) };

        if (transitionSystem is not null)
        {
            parts.Add(Unroller.Unroll(transitionSystem, k));
        }

        for (var l = 0; l <= k; l++)
        {
            var body = Translate(nnf, 0, l, k);
            if (transitionSystem is not null)
            {
                body = new Conjunction(BackEdge(transitionSystem, k, l), body);
            }
            parts.Add(new Implication(selectors[l], body));
        }
        return Formula.All(parts);
    }

    /// <summary>The back-loop transition from step k to step l must be a real transition of the system.</summary>
    private static Formula BackEdge(TransitionSystem system, int k, int l)
        => FormulaRewriter.RenameVariables(system.Transition,
            name => name.EndsWith('\'') ? Unroller.At(name[..^1], l) : Unroller.At(name, k));

    /// <summary>Successor position on a lasso with loop start l: i+1, wrapping from k back to l.</summary>
    private static int Successor(int i, int l, int k) => i < k ? i + 1 : l;

    /// <summary>The positions reachable from i in successor order: i..k then once around the loop l..k.</summary>
    private static List<int> ReachOrder(int i, int l, int k)
    {
        var order = new List<int>();
        for (var j = i; j <= k; j++)
        {
            order.Add(j);
        }
        for (var j = l; j <= k; j++)
        {
            order.Add(j);
        }
        return order;
    }

    /// <summary>The set of distinct positions reachable from i: {min(i,l) .. k}.</summary>
    private static IEnumerable<int> ReachSet(int i, int l, int k) => Enumerable.Range(Math.Min(i, l), k - Math.Min(i, l) + 1);

    /// <summary>Bounded LTL translation _l⟦ψ⟧^i over the lasso with loop start l and bound k.</summary>
    private static Formula Translate(LtlFormula psi, int i, int l, int k)
    {
        switch (psi)
        {
            case LtlBool b:
                return Formula.Const(b.Value);
            case LtlAtom a:
                return Formula.Var(Unroller.At(a.Name, i));
            case LtlNot n: // NNF: operand is an atom
                return new Negation(Translate(n.Operand, i, l, k));
            case LtlAnd x:
                return new Conjunction(Translate(x.Left, i, l, k), Translate(x.Right, i, l, k));
            case LtlOr x:
                return new Disjunction(Translate(x.Left, i, l, k), Translate(x.Right, i, l, k));
            case LtlNext x:
                return Translate(x.Operand, Successor(i, l, k), l, k);
            case LtlEventually e:
                return Formula.Any(ReachSet(i, l, k).Select(j => Translate(e.Operand, j, l, k)));
            case LtlGlobally g:
                return Formula.All(ReachSet(i, l, k).Select(j => Translate(g.Operand, j, l, k)));
            case LtlUntil u:
                return TranslateUntil(u, i, l, k);
            case LtlRelease r:
                return TranslateRelease(r, i, l, k);
            default:
                throw new InvalidOperationException($"Unexpected LTL node in NNF: {psi.GetType().Name}");
        }
    }

    private static Formula TranslateUntil(LtlUntil u, int i, int l, int k)
    {
        var order = ReachOrder(i, l, k);
        var disjuncts = new List<Formula>(order.Count);
        for (var m = 0; m < order.Count; m++)
        {
            var rightHolds = Translate(u.Right, order[m], l, k);
            var leftBefore = Formula.All(order.Take(m).Select(p => Translate(u.Left, p, l, k)));
            disjuncts.Add(new Conjunction(rightHolds, leftBefore));
        }
        return Formula.Any(disjuncts);
    }

    private static Formula TranslateRelease(LtlRelease r, int i, int l, int k)
    {
        var order = ReachOrder(i, l, k);
        var rightAlways = Formula.All(order.Select(p => Translate(r.Right, p, l, k)));
        var releasedEarly = new List<Formula>(order.Count);
        for (var m = 0; m < order.Count; m++)
        {
            var leftHolds = Translate(r.Left, order[m], l, k);
            var rightUpToHere = Formula.All(order.Take(m + 1).Select(p => Translate(r.Right, p, l, k)));
            releasedEarly.Add(new Conjunction(leftHolds, rightUpToHere));
        }
        return new Disjunction(rightAlways, Formula.Any(releasedEarly));
    }

    private static LtlTrace ExtractTrace(Model model, IReadOnlySet<string> atoms, int k)
    {
        var states = new List<IReadOnlyDictionary<string, bool>>(k + 1);
        for (var i = 0; i <= k; i++)
        {
            var state = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var atom in atoms)
            {
                state[atom] = model.TryGetValue(Unroller.At(atom, i), out var v) && v;
            }
            states.Add(state);
        }
        var loopStart = 0;
        for (var l = 0; l <= k; l++)
        {
            if (model.TryGetValue(LoopVar(l), out var on) && on)
            {
                loopStart = l;
                break;
            }
        }
        return new LtlTrace(states, loopStart);
    }
}
