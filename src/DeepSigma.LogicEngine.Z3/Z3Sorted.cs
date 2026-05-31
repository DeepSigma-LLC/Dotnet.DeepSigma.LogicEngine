using DeepSigma.LogicEngine.Z3.Sorted;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>
/// Solves the sorted Z3-only theories built with <see cref="SortedExpr"/> — fixed-width
/// <b>bit-vectors</b>, unbounded <b>integer/real</b> arithmetic (including quantifiers and
/// nonlinear terms), and <b>strings</b>. These are problems the native engine cannot express at
/// all, so Z3 is the only backend. Returns a tri-valued <see cref="Z3Result"/>; a satisfying model
/// exposes each bit-vector value as an (unsigned) integer.
/// </summary>
public static class Z3Sorted
{
    /// <summary>Solve a boolean-sorted constraint, returning satisfiability and (when SAT) a model.</summary>
    public static Z3Result Solve(SortedExpr constraint, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        if (constraint.Sort is not BoolSort)
        {
            throw new ArgumentException("The top-level constraint must be boolean-sorted.", nameof(constraint));
        }
        cancellationToken.ThrowIfCancellationRequested();
        using var session = new Z3Session(timeout, cancellationToken);
        var translator = new SortedToZ3(session.Context);
        session.Solver.Assert((Microsoft.Z3.BoolExpr)translator.Translate(constraint));
        return Z3Solving.Run(session, translator.Constants);
    }

    /// <summary>True if the constraint is satisfiable. (Unknown reports false.)</summary>
    public static bool IsSatisfiable(SortedExpr constraint, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(constraint, cancellationToken, timeout).IsSatisfiable;

    /// <summary>True if the constraint holds for every assignment (its negation is unsatisfiable).</summary>
    public static bool IsValid(SortedExpr constraint, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(SortedExpr.Not(constraint), cancellationToken, timeout).IsUnsatisfiable;
}
