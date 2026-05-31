using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Z3.Translation;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>
/// Z3-backed propositional reasoning, mirroring <see cref="Reasoning.Reasoner"/> but powered by
/// Z3 and returning a tri-valued <see cref="Z3Result"/>. Opt-in alternative to the native engine.
/// </summary>
public static class Z3Reasoner
{
    /// <summary>Solve a propositional formula, returning satisfiability and (when SAT) a model.</summary>
    public static Z3Result Solve(Formula formula, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var session = new Z3Session(timeout, cancellationToken);
        var translator = new FormulaToZ3(session.Context);
        session.Solver.Assert(translator.Translate(formula));
        return Z3Solving.Run(session, translator.Constants);
    }

    /// <summary>True if the formula is satisfiable. (An <see cref="Z3Status.Unknown"/> result reports false.)</summary>
    public static bool IsSatisfiable(Formula formula, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(formula, cancellationToken, timeout).IsSatisfiable;

    /// <summary>True if the formula is valid (its negation is unsatisfiable).</summary>
    public static bool IsValid(Formula formula, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(new Negation(formula), cancellationToken, timeout).IsUnsatisfiable;

    /// <summary>True if the knowledge base entails the query.</summary>
    public static bool Entails(IEnumerable<Formula> knowledgeBase, Formula query, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        => Solve(new Conjunction(Formula.All(knowledgeBase), new Negation(query)), cancellationToken, timeout).IsUnsatisfiable;
}
