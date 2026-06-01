using DeepSigma.LogicEngine.Common;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Solvers;

namespace DeepSigma.LogicEngine.Modal;

/// <summary>The modal system, determining the frame conditions on the accessibility relation.</summary>
public enum ModalSystem
{
    /// <summary>K: no conditions (arbitrary frames).</summary>
    K,

    /// <summary>T: reflexive.</summary>
    T,

    /// <summary>B: reflexive + symmetric.</summary>
    B,

    /// <summary>S4: reflexive + transitive.</summary>
    S4,

    /// <summary>S5: reflexive + symmetric + transitive (equivalence).</summary>
    S5,
}

/// <summary>
/// Decision procedures for propositional modal logic via <b>SAT-based bounded
/// Kripke-model construction</b>: the existence of a model with N worlds (whose
/// accessibility relation satisfies the system's frame conditions) is encoded
/// into a propositional formula and handed to the SAT back-end, increasing N.
///
/// <para>
/// Search is bounded by <c>maxWorlds</c>: a formula reported satisfiable truly
/// is; "not found up to maxWorlds" relies on the finite-model property holding
/// within the bound (the default is ample for typical small formulas, but it is
/// a bound, not a general proof of unsatisfiability).
/// </para>
/// </summary>
public static class ModalSolver
{
    /// <summary>Default bound on the number of worlds in the searched Kripke models.</summary>
    public const int DefaultMaxWorlds = 6;

    /// <summary>
    /// <see cref="Verdict.True"/> if a model of the system with up to <paramref name="maxWorlds"/>
    /// worlds is found (sound); otherwise <see cref="Verdict.Unknown"/> — no model up to the bound
    /// is not a proof of unsatisfiability (the finite-model bound may exceed <paramref name="maxWorlds"/>).
    /// </summary>
    /// <param name="formula">The modal formula to test for satisfiability.</param>
    /// <param name="system">The modal system whose frame conditions the constructed model must satisfy.</param>
    /// <param name="maxWorlds">Largest Kripke model (in worlds) to try.</param>
    /// <param name="cancellationToken">A token to cancel a long-running solve; on cancellation the call throws <see cref="OperationCanceledException"/>.</param>
    public static Verdict IsSatisfiable(ModalFormula formula, ModalSystem system, int maxWorlds = DefaultMaxWorlds, CancellationToken cancellationToken = default)
        => FoundModel(formula, system, maxWorlds, cancellationToken) ? Verdict.True : Verdict.Unknown;

    /// <summary>
    /// <see cref="Verdict.False"/> if a counter-model is found within <paramref name="maxWorlds"/>
    /// worlds (the formula is provably not valid); otherwise <see cref="Verdict.Unknown"/> — a
    /// bounded search cannot prove validity, so it never returns <see cref="Verdict.True"/>.
    /// </summary>
    /// <param name="formula">The modal formula to test for validity.</param>
    /// <param name="system">The modal system whose frame conditions apply.</param>
    /// <param name="maxWorlds">Largest Kripke model (in worlds) searched for a counter-model.</param>
    /// <param name="cancellationToken">A token to cancel a long-running solve; on cancellation the call throws <see cref="OperationCanceledException"/>.</param>
    public static Verdict IsValid(ModalFormula formula, ModalSystem system, int maxWorlds = DefaultMaxWorlds, CancellationToken cancellationToken = default)
        => FoundModel(new ModalNot(formula), system, maxWorlds, cancellationToken) ? Verdict.False : Verdict.Unknown;

    /// <summary>
    /// <see cref="Verdict.False"/> if a model is found within <paramref name="maxWorlds"/> worlds;
    /// otherwise <see cref="Verdict.Unknown"/> (no model up to the bound is not a proof of
    /// unsatisfiability).
    /// </summary>
    /// <param name="formula">The modal formula to test.</param>
    /// <param name="system">The modal system whose frame conditions apply.</param>
    /// <param name="maxWorlds">Largest Kripke model (in worlds) searched.</param>
    /// <param name="cancellationToken">A token to cancel a long-running solve; on cancellation the call throws <see cref="OperationCanceledException"/>.</param>
    public static Verdict IsUnsatisfiable(ModalFormula formula, ModalSystem system, int maxWorlds = DefaultMaxWorlds, CancellationToken cancellationToken = default)
        => FoundModel(formula, system, maxWorlds, cancellationToken) ? Verdict.False : Verdict.Unknown;

    /// <summary>As <see cref="IsSatisfiable(ModalFormula, ModalSystem, int, CancellationToken)"/>, but solving the bounded SAT encoding with the supplied engine (e.g. a Z3-backed <see cref="ISatSolver"/>).</summary>
    /// <param name="formula">The modal formula to test for satisfiability.</param>
    /// <param name="system">The modal system whose frame conditions the constructed model must satisfy.</param>
    /// <param name="solver">The SAT engine to solve each bounded encoding with.</param>
    /// <param name="maxWorlds">Largest Kripke model (in worlds) to try; the bound semantics are unchanged.</param>
    /// <param name="cancellationToken">A token to cancel a long-running solve; on cancellation the call throws <see cref="OperationCanceledException"/>.</param>
    public static Verdict IsSatisfiable(ModalFormula formula, ModalSystem system, ISatSolver solver, int maxWorlds = DefaultMaxWorlds, CancellationToken cancellationToken = default)
        => FoundModel(formula, system, solver, maxWorlds, cancellationToken) ? Verdict.True : Verdict.Unknown;

    /// <summary>As <see cref="IsValid(ModalFormula, ModalSystem, int, CancellationToken)"/>, but solving with the supplied engine.</summary>
    /// <param name="formula">The modal formula to test for validity.</param>
    /// <param name="system">The modal system whose frame conditions apply.</param>
    /// <param name="solver">The SAT engine to solve each bounded encoding with.</param>
    /// <param name="maxWorlds">Largest Kripke model (in worlds) searched for a counter-model.</param>
    /// <param name="cancellationToken">A token to cancel a long-running solve; on cancellation the call throws <see cref="OperationCanceledException"/>.</param>
    public static Verdict IsValid(ModalFormula formula, ModalSystem system, ISatSolver solver, int maxWorlds = DefaultMaxWorlds, CancellationToken cancellationToken = default)
        => FoundModel(new ModalNot(formula), system, solver, maxWorlds, cancellationToken) ? Verdict.False : Verdict.Unknown;

    /// <summary>As <see cref="IsUnsatisfiable(ModalFormula, ModalSystem, int, CancellationToken)"/>, but solving with the supplied engine.</summary>
    /// <param name="formula">The modal formula to test.</param>
    /// <param name="system">The modal system whose frame conditions apply.</param>
    /// <param name="solver">The SAT engine to solve each bounded encoding with.</param>
    /// <param name="maxWorlds">Largest Kripke model (in worlds) searched.</param>
    /// <param name="cancellationToken">A token to cancel a long-running solve; on cancellation the call throws <see cref="OperationCanceledException"/>.</param>
    public static Verdict IsUnsatisfiable(ModalFormula formula, ModalSystem system, ISatSolver solver, int maxWorlds = DefaultMaxWorlds, CancellationToken cancellationToken = default)
        => FoundModel(formula, system, solver, maxWorlds, cancellationToken) ? Verdict.False : Verdict.Unknown;

    /// <summary>True if a frame-valid Kripke model of the formula with up to <paramref name="maxWorlds"/> worlds exists.</summary>
    private static bool FoundModel(ModalFormula formula, ModalSystem system, int maxWorlds, CancellationToken cancellationToken)
        => BoundedSearch.Any(1, maxWorlds, n => Reasoner.IsSatisfiable(EncodeAt(formula, system, n), cancellationToken), cancellationToken);

    private static bool FoundModel(ModalFormula formula, ModalSystem system, ISatSolver solver, int maxWorlds, CancellationToken cancellationToken)
        => BoundedSearch.Any(1, maxWorlds, n => Reasoner.IsSatisfiable(EncodeAt(formula, system, n), solver, cancellationToken), cancellationToken);

    /// <summary>Encode "∃ Kripke model on worlds 0..n−1 (frame-valid) with the formula true at world 0".</summary>
    internal static Formula EncodeAt(ModalFormula formula, ModalSystem system, int n)
    {
        var subs = formula.Subformulas();
        var index = new Dictionary<ModalFormula, int>();
        for (var s = 0; s < subs.Count; s++)
        {
            index[subs[s]] = s;
        }

        Formula Holds(ModalFormula sub, int w) => Formula.Var($"h_{index[sub]}_{w}");
        Formula Access(int u, int v) => Formula.Var($"R_{u}_{v}");

        var parts = new List<Formula>();

        // Defining constraints for every subformula at every world.
        foreach (var sub in subs)
        {
            for (var w = 0; w < n; w++)
            {
                var here = Holds(sub, w);
                switch (sub)
                {
                    case ModalAtom:
                        break; // free valuation variable
                    case ModalBool b:
                        parts.Add(new Biconditional(here, Formula.Const(b.Value)));
                        break;
                    case ModalNot u:
                        parts.Add(new Biconditional(here, new Negation(Holds(u.Operand, w))));
                        break;
                    case ModalAnd x:
                        parts.Add(new Biconditional(here, new Conjunction(Holds(x.Left, w), Holds(x.Right, w))));
                        break;
                    case ModalOr x:
                        parts.Add(new Biconditional(here, new Disjunction(Holds(x.Left, w), Holds(x.Right, w))));
                        break;
                    case ModalImplies x:
                        parts.Add(new Biconditional(here, new Implication(Holds(x.Left, w), Holds(x.Right, w))));
                        break;
                    case ModalIff x:
                        parts.Add(new Biconditional(here, new Biconditional(Holds(x.Left, w), Holds(x.Right, w))));
                        break;
                    case ModalBox x:
                        parts.Add(new Biconditional(here, Formula.All(
                            Enumerable.Range(0, n).Select(v => (Formula)new Implication(Access(w, v), Holds(x.Operand, v))))));
                        break;
                    case ModalDiamond x:
                        parts.Add(new Biconditional(here, Formula.Any(
                            Enumerable.Range(0, n).Select(v => (Formula)new Conjunction(Access(w, v), Holds(x.Operand, v))))));
                        break;
                }
            }
        }

        AddFrameConstraints(parts, system, n, Access);

        // The formula holds at the designated world 0.
        parts.Add(Holds(formula, 0));
        return Formula.All(parts);
    }

    private static void AddFrameConstraints(List<Formula> parts, ModalSystem system, int n, Func<int, int, Formula> access)
    {
        var reflexive = system is ModalSystem.T or ModalSystem.B or ModalSystem.S4 or ModalSystem.S5;
        var symmetric = system is ModalSystem.B or ModalSystem.S5;
        var transitive = system is ModalSystem.S4 or ModalSystem.S5;

        if (reflexive)
        {
            for (var w = 0; w < n; w++)
            {
                parts.Add(access(w, w));
            }
        }
        if (symmetric)
        {
            for (var u = 0; u < n; u++)
            {
                for (var v = 0; v < n; v++)
                {
                    parts.Add(new Implication(access(u, v), access(v, u)));
                }
            }
        }
        if (transitive)
        {
            for (var u = 0; u < n; u++)
            {
                for (var v = 0; v < n; v++)
                {
                    for (var x = 0; x < n; x++)
                    {
                        parts.Add(new Implication(new Conjunction(access(u, v), access(v, x)), access(u, x)));
                    }
                }
            }
        }
    }
}
