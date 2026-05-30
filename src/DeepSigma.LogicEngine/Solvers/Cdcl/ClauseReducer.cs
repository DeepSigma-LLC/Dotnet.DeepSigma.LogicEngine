namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Learned-clause deletion policy. When the learned set outgrows a geometrically
/// increasing limit, the least active half is deleted — never touching clauses
/// that are currently a reason on the trail ("locked") or that are unit/binary
/// (cheap to keep and relied upon by propagation).
/// </summary>
internal sealed class ClauseReducer
{
    private readonly ClauseDatabase _clauses;
    private readonly WatchList _watches;
    private readonly Trail _trail;
    private readonly SolverStatistics _stats;
    private readonly double _growth;
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
        _limit = options.InitialLearnedClauseLimit;
    }

    public bool ShouldReduce() => _clauses.LearnedCount >= _limit;

    public void Reduce()
    {
        var ordered = _clauses.Learned.OrderBy(c => c.Activity).ToList();
        var half = ordered.Count / 2;
        var removed = new HashSet<CdclClause>();

        for (var i = 0; i < half; i++)
        {
            var clause = ordered[i];
            if (clause.Length <= 2 || IsLocked(clause))
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
