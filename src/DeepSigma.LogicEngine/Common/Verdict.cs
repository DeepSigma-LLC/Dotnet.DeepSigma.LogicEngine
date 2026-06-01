namespace DeepSigma.LogicEngine.Common;

/// <summary>
/// A three-valued answer from a <b>bounded</b> decision procedure — modal satisfiability, LTL
/// bounded model checking, and bounded-box LIA. These search only up to a finite bound, so a
/// witness found within the bound settles the question (<see cref="True"/> or <see cref="False"/>),
/// but finding nothing is <i>not</i> a proof of the opposite — a larger bound might decide it — so
/// the result is <see cref="Unknown"/>. This mirrors the honesty of the first-order prover's
/// <c>FolProofStatus</c> and the optional Z3 engine's <c>Unknown</c>.
///
/// <para>
/// Consequently these procedures can soundly <i>establish</i> existence (a model, or a
/// counter-model) but cannot <i>prove</i> non-existence under a fixed bound: e.g. a bounded
/// <c>IsValid</c> returns <see cref="False"/> when a counter-model is found, otherwise
/// <see cref="Unknown"/> — never a bound-relative <see cref="True"/>.
/// </para>
/// </summary>
public enum Verdict
{
    /// <summary>The queried property holds — established by a witness found within the bound.</summary>
    True,

    /// <summary>The queried property fails — established by a counter-witness found within the bound.</summary>
    False,

    /// <summary>Undetermined within the bound; neither side was witnessed, and a larger bound might decide it.</summary>
    Unknown,
}
