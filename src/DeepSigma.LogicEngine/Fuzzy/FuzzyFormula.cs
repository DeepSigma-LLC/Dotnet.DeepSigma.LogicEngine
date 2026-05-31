using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.Fuzzy;

/// <summary>
/// A formula of many-valued (fuzzy) logic: variables take truth values in the
/// real interval [0, 1], and connectives are interpreted by a t-norm family
/// (see <see cref="FuzzyLogic"/>). The AST is t-norm-agnostic; the chosen logic
/// determines the semantics at solving time.
///
/// <para>
/// Instead of just true/false, a fuzzy truth value is a number in [0, 1] — a
/// "degree of truth" (0 = false, 1 = true, ½ = half-true). To give the connectives
/// meaning we need a function that combines two such degrees for AND; that function
/// is called a <em>t-norm</em>, and it fixes the matching OR, →, and ¬ as well.
/// Different t-norms give different fuzzy logics (e.g. Gödel uses min/max,
/// Łukasiewicz uses bounded sums — see <see cref="FuzzyLogic"/>). This type stores
/// only the formula's structure; the t-norm is supplied at solving time, which is
/// why the same AST can be evaluated under any fuzzy logic.
/// </para>
/// </summary>
public abstract record FuzzyFormula
{
    public static FuzzyFormula Constant(Rational value) => new FuzzyConst(value);
    public static FuzzyFormula Var(string name) => new FuzzyVar(name);
    public static FuzzyFormula Not(FuzzyFormula f) => new FuzzyNot(f);
    public static FuzzyFormula And(FuzzyFormula a, FuzzyFormula b) => new FuzzyAnd(a, b);
    public static FuzzyFormula Or(FuzzyFormula a, FuzzyFormula b) => new FuzzyOr(a, b);
    public static FuzzyFormula Implies(FuzzyFormula a, FuzzyFormula b) => new FuzzyImplies(a, b);

    public static FuzzyFormula operator !(FuzzyFormula f) => new FuzzyNot(f);
    public static FuzzyFormula operator &(FuzzyFormula a, FuzzyFormula b) => new FuzzyAnd(a, b);
    public static FuzzyFormula operator |(FuzzyFormula a, FuzzyFormula b) => new FuzzyOr(a, b);
}

public sealed record FuzzyConst(Rational Value) : FuzzyFormula;
public sealed record FuzzyVar(string Name) : FuzzyFormula;
public sealed record FuzzyNot(FuzzyFormula Operand) : FuzzyFormula;
public sealed record FuzzyAnd(FuzzyFormula Left, FuzzyFormula Right) : FuzzyFormula;
public sealed record FuzzyOr(FuzzyFormula Left, FuzzyFormula Right) : FuzzyFormula;
public sealed record FuzzyImplies(FuzzyFormula Left, FuzzyFormula Right) : FuzzyFormula;

/// <summary>The t-norm family giving the connectives their semantics.</summary>
public enum FuzzyLogic
{
    /// <summary>Gödel: AND = min, OR = max, → = (x ≤ y ? 1 : y), ¬x = 1 − x.</summary>
    Godel,

    /// <summary>Łukasiewicz: AND = max(0, x+y−1), OR = min(1, x+y), → = min(1, 1−x+y), ¬x = 1 − x.</summary>
    Lukasiewicz,
}
