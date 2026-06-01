using System.Numerics;
using DeepSigma.LogicEngine.Common;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>A satisfying integer assignment found by <see cref="LiaSolver.FindModel"/>.</summary>
public sealed record LiaModel(IReadOnlyDictionary<string, BigInteger> Values);

/// <summary>
/// Satisfiability for quantifier-free <b>linear integer arithmetic</b> (LIA), via
/// the lazy DPLL(T) <see cref="SmtDriver"/> over a branch-and-bound theory on the
/// exact-rational simplex. The named <c>integerVariables</c> are constrained to
/// the integers within the box [−bound, bound]; all other variables are real
/// (so mixed integer–real systems work). Decisions are exact and complete within
/// the box.
/// </summary>
public static class LiaSolver
{
    /// <summary>Default per-variable integer domain bound; override for larger ranges.</summary>
    public const int DefaultBound = 1000;

    /// <summary>
    /// <see cref="Verdict.True"/> if a satisfying assignment is found in the integer box (a real
    /// solution, sound); otherwise <see cref="Verdict.Unknown"/> — no solution in [−bound, bound] is
    /// not a proof of global unsatisfiability (a solution may lie outside the box).
    /// </summary>
    /// <param name="formula">The formula to decide; its linear-arithmetic atoms range over the integer and any real variables.</param>
    /// <param name="integerVariables">Variable names constrained to the integers; every other variable stays real.</param>
    /// <param name="bound">Per-variable integer box: each integer variable ranges over [−bound, bound].</param>
    public static Verdict IsSatisfiable(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => HasModel(formula, integerVariables, bound) ? Verdict.True : Verdict.Unknown;

    /// <summary>
    /// <see cref="Verdict.False"/> if a satisfying assignment is found in the box; otherwise
    /// <see cref="Verdict.Unknown"/> (no solution in the box is not a proof of unsatisfiability).
    /// </summary>
    /// <param name="formula">The formula to decide.</param>
    /// <param name="integerVariables">Variable names constrained to the integers; every other variable stays real.</param>
    /// <param name="bound">Per-variable integer box: each integer variable ranges over [−bound, bound].</param>
    public static Verdict IsUnsatisfiable(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => HasModel(formula, integerVariables, bound) ? Verdict.False : Verdict.Unknown;

    /// <summary>
    /// <see cref="Verdict.False"/> if a counter-model is found in the box (the formula is provably not
    /// valid); otherwise <see cref="Verdict.Unknown"/> — a bounded box cannot prove validity, so this
    /// never returns <see cref="Verdict.True"/>.
    /// </summary>
    /// <param name="formula">The formula to check for validity.</param>
    /// <param name="integerVariables">Variable names constrained to the integers; every other variable stays real.</param>
    /// <param name="bound">Per-variable integer box: each integer variable ranges over [−bound, bound].</param>
    public static Verdict IsValid(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => HasModel(new SmtNot(formula), integerVariables, bound) ? Verdict.False : Verdict.Unknown;

    /// <summary>
    /// <see cref="Verdict.False"/> if a box counter-example shows the knowledge base does not entail the
    /// query; otherwise <see cref="Verdict.Unknown"/> — a bounded box cannot prove entailment, so this
    /// never returns <see cref="Verdict.True"/>.
    /// </summary>
    /// <param name="knowledgeBase">The premises, conjoined.</param>
    /// <param name="query">The formula to test for entailment.</param>
    /// <param name="integerVariables">Variable names constrained to the integers; every other variable stays real.</param>
    /// <param name="bound">Per-variable integer box: each integer variable ranges over [−bound, bound].</param>
    public static Verdict Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => HasModel(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query)), integerVariables, bound) ? Verdict.False : Verdict.Unknown;

    /// <summary>True if a satisfying assignment exists within the [−bound, bound] integer box.</summary>
    private static bool HasModel(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound)
        => SmtDriver.Solve(formula, new LiaTheory(integerVariables, bound)).IsSatisfiable;

    /// <summary>The (minimized) conflict core of an inconsistent conjunction of LIA literals, or null if consistent.</summary>
    /// <param name="literals">The conjunction of theory literals to test for consistency.</param>
    /// <param name="integerVariables">Variable names constrained to the integers; every other variable stays real.</param>
    /// <param name="bound">Per-variable integer box: each integer variable ranges over [−bound, bound].</param>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => SmtDriver.ConflictCore(literals, new LiaTheory(integerVariables, bound));

    /// <summary>A satisfying integer assignment for the integer variables, or null if unsatisfiable.</summary>
    /// <param name="formula">The formula to solve.</param>
    /// <param name="integerVariables">Variable names constrained to the integers; every other variable stays real.</param>
    /// <param name="bound">Per-variable integer box: each integer variable ranges over [−bound, bound]. The search is complete within this box.</param>
    public static LiaModel? FindModel(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
    {
        var theory = new LiaTheory(integerVariables, bound);
        var result = SmtDriver.Solve(formula, theory);
        if (!result.IsSatisfiable || theory.LastModel is null)
        {
            return null;
        }
        return new LiaModel(theory.LastModel);
    }
}
