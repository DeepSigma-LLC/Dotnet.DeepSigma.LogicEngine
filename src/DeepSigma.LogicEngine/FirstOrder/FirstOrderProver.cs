namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>Tuning for the first-order prover.</summary>
public sealed record FolOptions
{
    /// <summary>Maximum distinct clauses to generate before giving up with <see cref="FolProofStatus.Unknown"/>.</summary>
    public int MaxClauses { get; init; } = 20_000;

    /// <summary>
    /// Handle equality by <b>paramodulation</b> (with only the reflexivity clause
    /// <c>x = x</c>). When false, equality is axiomatized instead (reflexivity,
    /// symmetry, transitivity, congruence) — or, if <see cref="IncludeEqualityAxioms"/>
    /// is also false, treated as an uninterpreted predicate.
    /// </summary>
    public bool UseParamodulation { get; init; } = true;

    /// <summary>Add full equality axioms when the input uses <c>=</c> and paramodulation is off.</summary>
    public bool IncludeEqualityAxioms { get; init; } = true;

    /// <summary>The default options (paramodulation on, equality axioms on, 20,000-clause budget).</summary>
    public static FolOptions Default { get; } = new();
}

/// <summary>
/// A first-order theorem prover by resolution refutation. Validity and entailment
/// are decided by refuting the negated goal. First-order validity is only
/// semi-decidable, so a verdict is one of: <see cref="FolProofStatus.Proved"/>,
/// <see cref="FolProofStatus.Saturated"/> (provably not valid — a model exists),
/// or <see cref="FolProofStatus.Unknown"/> (budget exhausted). Equality is handled
/// by adding congruence axioms.
/// </summary>
public static class FirstOrderProver
{
    /// <summary>Refute a set of assertions (prove the conjunction unsatisfiable).</summary>
    public static FolProofStatus Refute(IEnumerable<FolFormula> assertions, FolOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= FolOptions.Default;
        var clauses = Clausifier.ClausifyAll(assertions).ToList();
        if (!options.UseParamodulation && options.IncludeEqualityAxioms)
        {
            // Paramodulation + reflexivity resolution handle equality natively; the
            // full axioms are only needed when paramodulation is off.
            clauses.AddRange(EqualityAxioms.For(clauses));
        }
        return new FirstOrderResolver(options.MaxClauses, options.UseParamodulation).Refute(clauses, cancellationToken);
    }

    /// <summary>Is <paramref name="formula"/> valid? Proved = valid; Saturated = not valid; Unknown = budget.</summary>
    public static FolProofStatus IsValid(FolFormula formula, FolOptions? options = null, CancellationToken cancellationToken = default)
        => Refute(new[] { new FolNot(formula) }, options, cancellationToken);

    /// <summary>Does the knowledge base entail the query?</summary>
    public static FolProofStatus Entails(IEnumerable<FolFormula> knowledgeBase, FolFormula query, FolOptions? options = null, CancellationToken cancellationToken = default)
        => Refute(knowledgeBase.Append(new FolNot(query)), options, cancellationToken);
}
