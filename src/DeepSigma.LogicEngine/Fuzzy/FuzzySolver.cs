using DeepSigma.LogicEngine.Smt;
using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.Fuzzy;

/// <summary>
/// Decision procedures for many-valued (fuzzy) logic over the Gödel and
/// Łukasiewicz t-norms. A fuzzy formula's truth value is encoded as a real
/// variable constrained by the (piecewise-linear) semantics of each connective,
/// reducing validity and satisfiability questions to <b>linear real arithmetic</b>
/// — solved by the existing <see cref="LraSolver"/> (DPLL(T) + exact simplex).
/// All reasoning is exact. The reduction itself is the public <see cref="FuzzyEncoder"/>,
/// so the same questions can also be sent to the optional Z3 backend.
/// </summary>
public static class FuzzySolver
{
    /// <summary>
    /// True if the formula's value is ≥ <paramref name="threshold"/> (default 1,
    /// i.e. a fuzzy tautology) under every assignment of its variables in [0,1].
    /// </summary>
    public static bool IsValid(FuzzyFormula formula, FuzzyLogic logic, Rational? threshold = null)
        => !LraSolver.IsSatisfiable(FuzzyEncoder.ValidityCounterexampleQuery(formula, logic, threshold));

    /// <summary>
    /// True if some assignment gives the formula a value ≥ <paramref name="threshold"/>
    /// (default 1).
    /// </summary>
    public static bool IsSatisfiable(FuzzyFormula formula, FuzzyLogic logic, Rational? threshold = null)
        => LraSolver.IsSatisfiable(FuzzyEncoder.SatisfiabilityQuery(formula, logic, threshold));

    /// <summary>
    /// True if no assignment reaches <paramref name="threshold"/> (default 1) — the negation of
    /// <see cref="IsSatisfiable"/>.
    /// </summary>
    public static bool IsUnsatisfiable(FuzzyFormula formula, FuzzyLogic logic, Rational? threshold = null)
        => !IsSatisfiable(formula, logic, threshold);
}
