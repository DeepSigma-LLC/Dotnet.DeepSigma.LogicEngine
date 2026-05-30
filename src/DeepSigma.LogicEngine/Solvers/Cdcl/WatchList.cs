namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// The two-watched-literal index: for each literal, the clauses currently
/// watching it. References clauses; it does not own them. The propagator
/// mutates a literal's watcher list in place during propagation, so
/// <see cref="Watchers"/> exposes the backing list directly.
/// </summary>
internal sealed class WatchList
{
    private List<CdclClause>[] _watchers;

    public WatchList(int variableCount)
    {
        _watchers = new List<CdclClause>[variableCount * 2];
        for (var i = 0; i < _watchers.Length; i++)
        {
            _watchers[i] = new List<CdclClause>();
        }
    }

    /// <summary>Grow the index to accommodate one more variable (two literals).</summary>
    public void AddVariable()
    {
        var oldLength = _watchers.Length;
        Array.Resize(ref _watchers, oldLength + 2);
        _watchers[oldLength] = new List<CdclClause>();
        _watchers[oldLength + 1] = new List<CdclClause>();
    }

    public List<CdclClause> Watchers(int literal) => _watchers[literal];

    public void Attach(int literal, CdclClause clause) => _watchers[literal].Add(clause);

    public void Detach(int literal, CdclClause clause) => _watchers[literal].Remove(clause);

    /// <summary>Attach a clause to both of its watched literals (positions 0 and 1).</summary>
    public void AttachClause(CdclClause clause)
    {
        Attach(clause.Literals[0], clause);
        Attach(clause.Literals[1], clause);
    }

    /// <summary>Detach a clause from both of its watched literals.</summary>
    public void DetachClause(CdclClause clause)
    {
        Detach(clause.Literals[0], clause);
        Detach(clause.Literals[1], clause);
    }
}
