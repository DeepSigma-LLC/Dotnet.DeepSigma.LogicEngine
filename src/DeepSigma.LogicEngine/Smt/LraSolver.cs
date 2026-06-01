namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Satisfiability for quantifier-free linear real arithmetic (LRA), via the lazy
/// DPLL(T) <see cref="SmtDriver"/> over an exact-rational simplex theory solver.
/// </summary>
public static class LraSolver
{
    private static readonly LraTheory Theory = new();

    /// <summary>True if some assignment of the real variables satisfies the formula.</summary>
    public static bool IsSatisfiable(SmtFormula formula, CancellationToken cancellationToken = default) => Solve(formula, cancellationToken).IsSatisfiable;

    /// <summary>True if no assignment of the real variables satisfies the formula.</summary>
    public static bool IsUnsatisfiable(SmtFormula formula, CancellationToken cancellationToken = default) => !IsSatisfiable(formula, cancellationToken);

    /// <summary>True if the formula holds for every assignment of the real variables.</summary>
    public static bool IsValid(SmtFormula formula, CancellationToken cancellationToken = default) => !Solve(new SmtNot(formula), cancellationToken).IsSatisfiable;

    /// <summary>True if the knowledge base entails the query in LRA.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query, CancellationToken cancellationToken = default)
        => !Solve(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query)), cancellationToken).IsSatisfiable;

    /// <summary>Solve the formula in LRA, returning satisfiability and (if satisfiable) a model.</summary>
    public static SmtResult Solve(SmtFormula formula, CancellationToken cancellationToken = default) => SmtDriver.Solve(formula, Theory, cancellationToken);

    /// <summary>
    /// The minimal conflict core of an inconsistent conjunction of linear
    /// constraint literals, or null if it is consistent.
    /// </summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals)
        => SmtDriver.ConflictCore(literals, Theory);
}
