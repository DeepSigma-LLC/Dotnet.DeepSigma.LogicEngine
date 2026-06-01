namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Satisfiability for quantifier-free formulas mixing <b>EUF</b> (equality +
/// uninterpreted functions/predicates) and <b>LRA</b> (linear real arithmetic) in
/// one solve, via a Nelson–Oppen <see cref="CombinedTheory">combination</see> over
/// the lazy DPLL(T) loop. Variables shared by name between the two theories are the
/// bridge: an equality one theory entails is propagated to the other. This decides
/// formulas neither <see cref="EufSolver"/> nor <see cref="LraSolver"/> can settle
/// alone — e.g. <c>x ≤ y ∧ y ≤ x ∧ f(x) ≠ f(y)</c> is unsatisfiable.
/// </summary>
public static class CombinedSolver
{
    private static readonly CombinedTheory Theory = new();

    /// <summary>True if some EUF+LRA interpretation satisfies the formula.</summary>
    public static bool IsSatisfiable(SmtFormula formula) => SmtDriver.Solve(formula, Theory).IsSatisfiable;

    /// <summary>True if no EUF+LRA interpretation satisfies the formula.</summary>
    public static bool IsUnsatisfiable(SmtFormula formula) => !IsSatisfiable(formula);

    /// <summary>True if the formula holds under every EUF+LRA interpretation.</summary>
    public static bool IsValid(SmtFormula formula) => !SmtDriver.Solve(new SmtNot(formula), Theory).IsSatisfiable;

    /// <summary>True if the knowledge base entails the query in the combined theory.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query)
        => !IsSatisfiable(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query)));

    /// <summary>Solve the formula in the combined EUF+LRA theory, returning satisfiability and (if satisfiable) a model.</summary>
    public static SmtResult Solve(SmtFormula formula) => SmtDriver.Solve(formula, Theory);

    /// <summary>The (minimized) conflict core of an inconsistent conjunction of EUF/LRA literals, or null if consistent.</summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals)
        => SmtDriver.ConflictCore(literals, Theory);
}
