namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Learned-clause deletion policy. When the learned set outgrows a geometrically
/// increasing limit, the lowest-value half is deleted — never touching clauses
/// that are currently a reason on the trail ("locked") or that are unit/binary
/// (cheap to keep and relied upon by propagation). With LBD deletion enabled,
/// "value" is literal block distance first (low LBD = "glue" = most valuable,
/// and an LBD ≤ 2 clause is never deleted), then activity; otherwise it is
/// activity alone.
/// </summary>
internal sealed class ClauseReducer
{
    /// <summary>Clauses this glue-y (few decision levels) are kept regardless of activity.</summary>
    private const int GlueLbdThreshold = 2;

    private readonly ClauseDatabase _clauses;
    private readonly WatchList _watches;
    private readonly Trail _trail;
    private readonly SolverStatistics _stats;
    private readonly double _growth;
    private readonly bool _useLbd;
    private double _limit;

    public ClauseReducer(
        ClauseDatabase clauses,
        WatchList watches,
        Trail trail,
        SolverStatistics stats,
        SolverOptions options)
    {
        _clauses = clauses;
        _watches = watches;
        _trail = trail;
        _stats = stats;
        _growth = options.LearnedClauseGrowth;
        _useLbd = options.UseLbdClauseDeletion;
        _limit = options.InitialLearnedClauseLimit;
    }

    public bool ShouldReduce() => _clauses.LearnedCount >= _limit;

    public void Reduce()
    {
        // Worst clauses first: high LBD then low activity (or low activity alone).
        var ordered = _useLbd
            ? _clauses.Learned.OrderByDescending(c => c.Lbd).ThenBy(c => c.Activity).ToList()
            : _clauses.Learned.OrderBy(c => c.Activity).ToList();
        var target = ordered.Count / 2;
        var removed = new HashSet<CdclClause>();

        foreach (var clause in ordered)
        {
            if (removed.Count >= target)
            {
                break;
            }
            if (clause.Length <= 2 || IsLocked(clause) || (_useLbd && clause.Lbd <= GlueLbdThreshold))
            {
                continue;
            }
            _watches.DetachClause(clause);
            removed.Add(clause);
        }

        _clauses.RemoveLearned(removed);
        _stats.DeletedClauses += removed.Count;
        _limit *= _growth;
    }

    /// <summary>A clause is locked when it is the reason for its first literal.</summary>
    private bool IsLocked(CdclClause clause)
        => _trail.ReasonFor(CdclLiterals.Variable(clause.Literals[0])) == clause;
}
