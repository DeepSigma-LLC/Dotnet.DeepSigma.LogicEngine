namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// The quantifier-free SMT theory to decide a formula under. Selects which of the
/// dedicated facades <see cref="SmtReasoner"/> dispatches to.
/// </summary>
public enum SmtTheory
{
    /// <summary>Equality with uninterpreted functions and predicates (<see cref="EufSolver"/>).</summary>
    Euf,

    /// <summary>Linear real arithmetic (<see cref="LraSolver"/>).</summary>
    Lra,

    /// <summary>The Nelson–Oppen combination of EUF and LRA (<see cref="CombinedSolver"/>).</summary>
    Combined,

    /// <summary>The (non-extensional) theory of arrays over select/store (<see cref="ArraySolver"/>).</summary>
    Arrays,
}

/// <summary>
/// A single discoverable entry point for the quantifier-free SMT theories: pick the
/// <see cref="SmtTheory"/> explicitly and this dispatches to the matching dedicated
/// facade. Use it when you know which theory your formula lives in but don't want to
/// remember the individual facade names.
///
/// <para>
/// Pick the theory by the atoms your formula uses: <see cref="SmtTheory.Euf"/> for
/// equalities and uninterpreted functions/predicates (<c>f(a) = b</c>);
/// <see cref="SmtTheory.Lra"/> for linear arithmetic over the reals
/// (<c>2*x + y &lt;= 3</c>); <see cref="SmtTheory.Arrays"/> for <c>select</c>/<c>store</c>;
/// and <see cref="SmtTheory.Combined"/> for formulas that mix uninterpreted functions
/// with linear arithmetic.
/// </para>
///
/// <para>
/// Richer outputs stay on the dedicated facades: conflict cores via
/// <see cref="EufSolver.ConflictCore"/> / <see cref="LraSolver.ConflictCore"/> /
/// <see cref="CombinedSolver.ConflictCore"/>, and linear <b>integer</b> arithmetic via
/// <see cref="LiaSolver"/> (which needs the integer-variable set and a search bound, so it
/// does not fit this uniform signature).
/// </para>
/// </summary>
/// <seealso cref="EufSolver"/>
/// <seealso cref="LraSolver"/>
/// <seealso cref="LiaSolver"/>
/// <seealso cref="ArraySolver"/>
/// <seealso cref="CombinedSolver"/>
public static class SmtReasoner
{
    /// <summary>True if the formula is satisfiable under <paramref name="theory"/>.</summary>
    public static bool IsSatisfiable(SmtFormula formula, SmtTheory theory, CancellationToken cancellationToken = default) => theory switch
    {
        SmtTheory.Euf => EufSolver.IsSatisfiable(formula, cancellationToken),
        SmtTheory.Lra => LraSolver.IsSatisfiable(formula, cancellationToken),
        SmtTheory.Combined => CombinedSolver.IsSatisfiable(formula, cancellationToken),
        SmtTheory.Arrays => ArraySolver.IsSatisfiable(formula, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(theory), theory, "Unknown SMT theory."),
    };

    /// <summary>True if the formula is unsatisfiable under <paramref name="theory"/>.</summary>
    public static bool IsUnsatisfiable(SmtFormula formula, SmtTheory theory, CancellationToken cancellationToken = default) => !IsSatisfiable(formula, theory, cancellationToken);

    /// <summary>True if the formula holds in every model of <paramref name="theory"/>.</summary>
    public static bool IsValid(SmtFormula formula, SmtTheory theory, CancellationToken cancellationToken = default) => theory switch
    {
        SmtTheory.Euf => EufSolver.IsValid(formula, cancellationToken),
        SmtTheory.Lra => LraSolver.IsValid(formula, cancellationToken),
        SmtTheory.Combined => CombinedSolver.IsValid(formula, cancellationToken),
        SmtTheory.Arrays => ArraySolver.IsValid(formula, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(theory), theory, "Unknown SMT theory."),
    };

    /// <summary>True if the knowledge base theory-entails the query under <paramref name="theory"/>.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query, SmtTheory theory, CancellationToken cancellationToken = default) => theory switch
    {
        SmtTheory.Euf => EufSolver.Entails(knowledgeBase, query, cancellationToken),
        SmtTheory.Lra => LraSolver.Entails(knowledgeBase, query, cancellationToken),
        SmtTheory.Combined => CombinedSolver.Entails(knowledgeBase, query, cancellationToken),
        SmtTheory.Arrays => ArraySolver.Entails(knowledgeBase, query, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(theory), theory, "Unknown SMT theory."),
    };

    /// <summary>
    /// Solve the formula under <paramref name="theory"/>, returning satisfiability and a
    /// model when satisfiable. For <see cref="SmtTheory.Arrays"/> this reduces to EUF over
    /// the read-over-write axiom instances (the same reduction the array decision
    /// procedures use), so the model ranges over that reduction.
    /// </summary>
    public static SmtResult Solve(SmtFormula formula, SmtTheory theory, CancellationToken cancellationToken = default) => theory switch
    {
        SmtTheory.Euf => EufSolver.Solve(formula, cancellationToken),
        SmtTheory.Lra => LraSolver.Solve(formula, cancellationToken),
        SmtTheory.Combined => CombinedSolver.Solve(formula, cancellationToken),
        SmtTheory.Arrays => EufSolver.Solve(ArraySolver.WithArrayAxioms(formula), cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(theory), theory, "Unknown SMT theory."),
    };
}
