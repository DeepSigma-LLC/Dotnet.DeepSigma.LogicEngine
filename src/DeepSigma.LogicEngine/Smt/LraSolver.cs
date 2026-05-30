namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Satisfiability for quantifier-free linear real arithmetic (LRA), via the lazy
/// DPLL(T) <see cref="SmtDriver"/> over an exact-rational simplex theory solver.
/// </summary>
public static class LraSolver
{
    private static readonly LraTheory Theory = new();

    public static bool IsSatisfiable(SmtFormula formula) => Solve(formula).IsSatisfiable;

    public static bool IsUnsatisfiable(SmtFormula formula) => !IsSatisfiable(formula);

    /// <summary>True if the formula holds for every assignment of the real variables.</summary>
    public static bool IsValid(SmtFormula formula) => !Solve(new SmtNot(formula)).IsSatisfiable;

    /// <summary>True if the knowledge base entails the query in LRA.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query)
        => !Solve(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query))).IsSatisfiable;

    public static SmtResult Solve(SmtFormula formula) => SmtDriver.Solve(formula, Theory);

    /// <summary>
    /// The minimal conflict core of an inconsistent conjunction of linear
    /// constraint literals, or null if it is consistent.
    /// </summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals)
        => SmtDriver.ConflictCore(literals, Theory);
}
