using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Z3.Translation;
using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>Which SMT theory Z3 should decide the formula under (a superset of the native
/// <c>SmtTheory</c> — Z3 adds complete, <b>unbounded</b> linear integer arithmetic).</summary>
public enum Z3SmtTheory
{
    /// <summary>Equality with uninterpreted functions/predicates.</summary>
    Euf,

    /// <summary>Linear real arithmetic.</summary>
    Lra,

    /// <summary>Linear integer arithmetic — unbounded (no <c>[-bound, bound]</c> box, unlike the native LIA).</summary>
    Lia,

    /// <summary>Mixed uninterpreted functions + linear arithmetic (the native Nelson–Oppen case).</summary>
    Combined,

    /// <summary>Arrays over select/store (reduced to EUF via the same read-over-write axioms the native engine uses).</summary>
    Arrays,
}

/// <summary>
/// Z3-backed SMT solving over the project's existing <see cref="SmtFormula"/> AST. Compared with
/// the native solvers this is complete (no bounded LIA box), generally faster at scale, and
/// supports a timeout / <see cref="CancellationToken"/>. Returns a tri-valued <see cref="Z3Result"/>.
/// </summary>
public static class Z3SmtReasoner
{
    /// <summary>
    /// Solve <paramref name="formula"/> under <paramref name="theory"/>.
    /// <paramref name="integerVariables"/> is required for <see cref="Z3SmtTheory.Lia"/> (the
    /// names constrained to the integers) and ignored otherwise.
    /// </summary>
    public static Z3Result Solve(
        SmtFormula formula,
        Z3SmtTheory theory,
        IReadOnlyCollection<string>? integerVariables = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (theory == Z3SmtTheory.Lia && integerVariables is null)
        {
            throw new ArgumentNullException(nameof(integerVariables), "LIA requires the set of integer variables.");
        }

        // Arrays reduce to EUF via the same read-over-write axiom instances the native ArraySolver uses.
        var prepared = theory == Z3SmtTheory.Arrays ? ArraySolver.WithArrayAxioms(formula) : formula;

        using var session = new Z3Session(timeout, cancellationToken);
        var termSort = theory is Z3SmtTheory.Euf or Z3SmtTheory.Arrays
            ? (MZ3.Sort)session.Context.MkUninterpretedSort("U")
            : session.Context.RealSort;
        var translator = new SmtToZ3(session.Context, termSort, theory == Z3SmtTheory.Lia ? integerVariables : null);
        session.Solver.Assert(translator.Translate(prepared));
        return Z3Solving.Run(session, translator.Constants);
    }

    /// <summary>True if the formula is satisfiable under the theory. (Unknown reports false.)</summary>
    public static bool IsSatisfiable(SmtFormula formula, Z3SmtTheory theory, IReadOnlyCollection<string>? integerVariables = null, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(formula, theory, integerVariables, cancellationToken, timeout).IsSatisfiable;

    /// <summary>True if the formula is unsatisfiable under the theory. (Unknown reports false.)</summary>
    public static bool IsUnsatisfiable(SmtFormula formula, Z3SmtTheory theory, IReadOnlyCollection<string>? integerVariables = null, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(formula, theory, integerVariables, cancellationToken, timeout).IsUnsatisfiable;

    /// <summary>True if the formula holds in every model of the theory (its negation is unsatisfiable).</summary>
    public static bool IsValid(SmtFormula formula, Z3SmtTheory theory, IReadOnlyCollection<string>? integerVariables = null, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(new SmtNot(formula), theory, integerVariables, cancellationToken, timeout).IsUnsatisfiable;

    /// <summary>True if the knowledge base theory-entails the query.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query, Z3SmtTheory theory, IReadOnlyCollection<string>? integerVariables = null, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query)), theory, integerVariables, cancellationToken, timeout).IsUnsatisfiable;
}
