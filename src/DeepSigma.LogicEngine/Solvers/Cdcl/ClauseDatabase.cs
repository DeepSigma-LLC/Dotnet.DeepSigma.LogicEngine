namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Stores the problem (original) clauses and the learned clauses, and maintains
/// learned-clause activity for the deletion policy. The clause-activity bump is
/// kept large (rather than decaying every clause) and rescaled on overflow —
/// the same O(1) trick used for variable activity.
/// </summary>
internal sealed class ClauseDatabase
{
    private const double RescaleThreshold = 1e20;

    private readonly List<CdclClause> _original = new();
    private readonly List<CdclClause> _learned = new();
    private readonly double _decay;
    private double _increment = 1.0;

    public ClauseDatabase(double clauseDecay) => _decay = clauseDecay;

    public IReadOnlyList<CdclClause> Learned => _learned;
    public int LearnedCount => _learned.Count;

    public CdclClause AddOriginal(int[] literals)
    {
        var clause = new CdclClause(literals, learned: false);
        _original.Add(clause);
        return clause;
    }

    public CdclClause AddLearned(int[] literals, int lbd = 0)
    {
        var clause = new CdclClause(literals, learned: true) { Lbd = lbd };
        _learned.Add(clause);
        BumpActivity(clause);
        return clause;
    }

    public void BumpActivity(CdclClause clause)
    {
        clause.Activity += _increment;
        if (clause.Activity > RescaleThreshold)
        {
            Rescale();
        }
    }

    public void DecayActivity() => _increment /= _decay;

    /// <summary>Replace the learned-clause set, keeping only those not removed.</summary>
    public void RemoveLearned(HashSet<CdclClause> toRemove)
    {
        if (toRemove.Count == 0)
        {
            return;
        }
        _learned.RemoveAll(toRemove.Contains);
    }

    private void Rescale()
    {
        foreach (var clause in _learned)
        {
            clause.Activity *= 1.0 / RescaleThreshold;
        }
        _increment *= 1.0 / RescaleThreshold;
    }
}
