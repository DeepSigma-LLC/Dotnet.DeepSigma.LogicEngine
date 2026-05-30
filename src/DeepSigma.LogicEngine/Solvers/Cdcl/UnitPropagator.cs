namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Boolean Constraint Propagation over the two-watched-literal scheme. A clause
/// can only become unit or conflicting when one of its two watched literals
/// becomes false, so when a literal <c>p</c> is assigned true we only inspect
/// the clauses watching <c>¬p</c>.
/// </summary>
internal sealed class UnitPropagator
{
    private readonly Trail _trail;
    private readonly WatchList _watches;
    private readonly ClauseDatabase _clauses;
    private readonly SolverStatistics _stats;

    public UnitPropagator(Trail trail, WatchList watches, ClauseDatabase clauses, SolverStatistics stats)
    {
        _trail = trail;
        _watches = watches;
        _clauses = clauses;
        _stats = stats;
    }

    /// <summary>
    /// Register an original problem clause. Returns false if the clause makes the
    /// formula immediately unsatisfiable (an empty clause, or a unit clause that
    /// contradicts an existing level-0 assignment).
    /// </summary>
    public bool AddOriginalClause(int[] literals)
    {
        if (literals.Length == 0)
        {
            return false;
        }
        if (literals.Length == 1)
        {
            return _trail.Enqueue(literals[0], reason: null);
        }
        var clause = _clauses.AddOriginal(literals);
        _watches.AttachClause(clause);
        return true;
    }

    /// <summary>
    /// Propagate all pending assignments. Returns the conflicting clause if one
    /// is reached, otherwise null (a fixpoint with no conflict).
    /// </summary>
    public CdclClause? Propagate()
    {
        while (_trail.HasPropagationWork)
        {
            var assigned = _trail.DequeueForPropagation();   // assigned true
            var falseLit = CdclLiterals.Negate(assigned);     // now false
            var watchers = _watches.Watchers(falseLit);

            var conflict = ScanWatchers(watchers, falseLit);
            if (conflict is not null)
            {
                return conflict;
            }
        }
        return null;
    }

    /// <summary>
    /// Walk the clauses watching <paramref name="falseLit"/>, repairing watches
    /// in place. Returns a conflicting clause, or null if all were handled.
    /// </summary>
    private CdclClause? ScanWatchers(List<CdclClause> watchers, int falseLit)
    {
        var read = 0;
        var write = 0;
        while (read < watchers.Count)
        {
            var clause = watchers[read];

            // Ensure the false literal sits at position 1, the other watch at 0.
            if (clause.Literals[0] == falseLit)
            {
                clause.Literals[0] = clause.Literals[1];
                clause.Literals[1] = falseLit;
            }

            var other = clause.Literals[0];
            // If the other watch is already true, the clause is satisfied: keep it.
            if (other != falseLit && _trail.LiteralValue(other) == LBool.True)
            {
                watchers[write++] = clause;
                read++;
                continue;
            }

            if (TryReplaceWatch(clause, falseLit))
            {
                // Clause moved to a new watcher list; drop it from this one.
                read++;
                continue;
            }

            // No replacement found: the clause is unit or conflicting under `other`.
            watchers[write++] = clause;
            read++;

            if (_trail.LiteralValue(other) == LBool.False)
            {
                return FinishOnConflict(watchers, read, write);
            }
            _stats.Propagations++;
            _trail.Enqueue(other, clause);
        }
        watchers.RemoveRange(write, watchers.Count - write);
        return null;
    }

    /// <summary>
    /// Look for a non-false literal beyond the two watch slots to watch instead
    /// of <paramref name="falseLit"/>. On success, swaps it into slot 1 and
    /// attaches the clause to that literal's watcher list.
    /// </summary>
    private bool TryReplaceWatch(CdclClause clause, int falseLit)
    {
        var literals = clause.Literals;
        for (var k = 2; k < literals.Length; k++)
        {
            if (_trail.LiteralValue(literals[k]) != LBool.False)
            {
                literals[1] = literals[k];
                literals[k] = falseLit;
                _watches.Attach(literals[1], clause);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Compact the unprocessed tail of the watcher list before returning a
    /// conflict, so no clause is lost from the watch index.
    /// </summary>
    private CdclClause FinishOnConflict(List<CdclClause> watchers, int read, int write)
    {
        var conflict = watchers[write - 1];
        while (read < watchers.Count)
        {
            watchers[write++] = watchers[read++];
        }
        watchers.RemoveRange(write, watchers.Count - write);
        _trail.HaltPropagation();
        return conflict;
    }
}
