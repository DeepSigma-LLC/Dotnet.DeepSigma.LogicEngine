namespace DeepSigma.LogicEngine.FiniteGroups;

/// <summary>
/// Optional structural requirements for a sought group. Commutativity is encoded
/// directly into the SAT search (cheap); order-based requirements (cyclicity,
/// exponent, required element orders) are checked on the decoded group, since
/// they would otherwise need power-of-element variables in the encoding.
/// </summary>
public sealed record GroupSpec
{
    /// <summary>If set, require the group to be abelian (true) or non-abelian (false).</summary>
    public bool? Abelian { get; init; }

    /// <summary>If set, require the group to be cyclic (true) or non-cyclic (false).</summary>
    public bool? Cyclic { get; init; }

    /// <summary>If set, require this exact group exponent (lcm of element orders).</summary>
    public int? Exponent { get; init; }

    /// <summary>If set, require an element of each listed order to exist.</summary>
    public IReadOnlyList<int>? RequiredElementOrders { get; init; }

    /// <summary>
    /// Add (sound, partial) lex-leader symmetry-breaking clauses to prune relabelings
    /// when enumerating/counting. Never affects correctness — counting still dedups by
    /// isomorphism class — it only reduces the number of labeled models explored.
    /// </summary>
    public bool UseLexLeader { get; init; }

    /// <summary>True if any requirement must be checked on the decoded group rather than in SAT.</summary>
    internal bool HasPostFilter => Cyclic is not null || Exponent is not null || RequiredElementOrders is not null;
}
