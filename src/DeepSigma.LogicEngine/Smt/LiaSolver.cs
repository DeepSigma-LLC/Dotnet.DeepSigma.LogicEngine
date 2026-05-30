using System.Numerics;

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

    public static bool IsSatisfiable(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => SmtDriver.Solve(formula, new LiaTheory(integerVariables, bound)).IsSatisfiable;

    public static bool IsUnsatisfiable(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => !IsSatisfiable(formula, integerVariables, bound);

    /// <summary>True if the formula holds for every integer assignment within the box.</summary>
    public static bool IsValid(SmtFormula formula, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => !IsSatisfiable(new SmtNot(formula), integerVariables, bound);

    /// <summary>True if the knowledge base entails the query in LIA (within the box).</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => !IsSatisfiable(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query)), integerVariables, bound);

    /// <summary>The (minimized) conflict core of an inconsistent conjunction of LIA literals, or null if consistent.</summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals, IReadOnlyCollection<string> integerVariables, int bound = DefaultBound)
        => SmtDriver.ConflictCore(literals, new LiaTheory(integerVariables, bound));

    /// <summary>A satisfying integer assignment for the integer variables, or null if unsatisfiable.</summary>
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
