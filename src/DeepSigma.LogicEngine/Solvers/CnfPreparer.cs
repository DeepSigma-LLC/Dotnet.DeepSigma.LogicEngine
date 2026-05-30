using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// A CNF formula prepared for solving, together with the variables of the
/// original input. The original-variable set is what callers care about when a
/// model comes back: any auxiliary variables introduced by the Tseitin
/// transformation must be projected away.
/// </summary>
public readonly record struct PreparedCnf(CnfFormula Cnf, IReadOnlySet<string> OriginalVariables);

/// <summary>
/// Shared formula-to-CNF preparation and model projection used by every solver
/// that accepts a <see cref="Formula"/>. Centralises the policy of "classical
/// CNF when the formula is already conjunctive, Tseitin otherwise" so the
/// solvers do not each reimplement it.
/// </summary>
public static class CnfPreparer
{
    /// <summary>
    /// Simplify the formula and convert it to CNF. Formulas already in
    /// conjunctive form take the classical converter (no auxiliary variables);
    /// all others use the equisatisfiable, linear-size Tseitin transformation.
    /// </summary>
    public static PreparedCnf Prepare(Formula formula)
    {
        var originalVars = Evaluator.Variables(formula);
        var simplified = Simplifier.Simplify(formula);
        var cnf = IsCnfShaped(simplified)
            ? CnfTransformer.ToCnf(simplified)
            : TseitinTransformer.ToCnf(simplified);
        return new PreparedCnf(cnf, originalVars);
    }

    /// <summary>
    /// Project a CNF-level model onto the original variables: drop any auxiliary
    /// variables and give every original variable a value (false when the model
    /// left it unconstrained).
    /// </summary>
    public static Model Project(IReadOnlyDictionary<string, bool> cnfModel, IReadOnlySet<string> originalVariables)
    {
        var projected = new Dictionary<string, bool>(originalVariables.Count, StringComparer.Ordinal);
        foreach (var name in originalVariables)
        {
            projected[name] = cnfModel.TryGetValue(name, out var value) && value;
        }
        return Model.From(projected);
    }

    /// <summary>True if the formula is a conjunction of disjunctions of literals.</summary>
    public static bool IsCnfShaped(Formula formula) => IsConjunctionTree(formula);

    private static bool IsConjunctionTree(Formula f) => f switch
    {
        Conjunction c => IsConjunctionTree(c.Left) && IsConjunctionTree(c.Right),
        _ => IsDisjunctionOfLiterals(f),
    };

    private static bool IsDisjunctionOfLiterals(Formula f) => f switch
    {
        Disjunction d => IsDisjunctionOfLiterals(d.Left) && IsDisjunctionOfLiterals(d.Right),
        BoolConst => true,
        Variable => true,
        Negation n => n.Operand is Variable,
        _ => false,
    };
}
