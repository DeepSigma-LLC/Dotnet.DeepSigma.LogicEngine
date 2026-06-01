namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Satisfiability for the theory of equality with uninterpreted functions and
/// predicates (EUF), via the lazy DPLL(T) <see cref="SmtDriver"/> over a
/// congruence-closure theory solver.
/// </summary>
public static class EufSolver
{
    private static readonly EufTheory Theory = new();

    /// <summary>True if some theory model satisfies the formula.</summary>
    public static bool IsSatisfiable(SmtFormula formula) => Solve(formula).IsSatisfiable;

    /// <summary>True if no theory model satisfies the formula.</summary>
    public static bool IsUnsatisfiable(SmtFormula formula) => !IsSatisfiable(formula);

    /// <summary>True if the formula holds in every theory model.</summary>
    public static bool IsValid(SmtFormula formula) => !Solve(new SmtNot(formula)).IsSatisfiable;

    /// <summary>True if the knowledge base theory-entails the query.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query)
        => !Solve(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query))).IsSatisfiable;

    /// <summary>Solve the formula in EUF, returning satisfiability and (if satisfiable) a model.</summary>
    public static SmtResult Solve(SmtFormula formula) => SmtDriver.Solve(formula, Theory);

    /// <summary>
    /// The minimal conflict core (atoms / negated atoms) of an inconsistent
    /// conjunction of theory literals, or null if it is consistent.
    /// </summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals)
        => SmtDriver.ConflictCore(literals, Theory);
}
