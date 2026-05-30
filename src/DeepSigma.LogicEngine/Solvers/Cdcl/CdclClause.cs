namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// A clause in the CDCL core: an array of integer literals plus learned-clause
/// bookkeeping. The two watched literals are held positionally at indices 0 and
/// 1; the propagator maintains this convention. A clause object doubles as a
/// reason reference on the trail, so reference identity is meaningful.
/// </summary>
internal sealed class CdclClause
{
    public int[] Literals { get; }
    public bool Learned { get; }
    public double Activity { get; set; }

    /// <summary>
    /// Literal block distance: the number of distinct decision levels among the
    /// clause's literals at the moment it was learned. Low LBD ("glue") clauses
    /// are the most valuable to keep. Zero for original clauses.
    /// </summary>
    public int Lbd { get; set; }

    public CdclClause(int[] literals, bool learned)
    {
        Literals = literals;
        Learned = learned;
    }

    public int Length => Literals.Length;
}
