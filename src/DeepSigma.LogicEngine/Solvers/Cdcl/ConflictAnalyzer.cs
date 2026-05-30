namespace DeepSigma.LogicEngine.Solvers.Cdcl;

internal readonly record struct LearnedClause(int[] Literals, int BackjumpLevel);

/// <summary>
/// First-UIP (Unique Implication Point) conflict analysis. Starting from the
/// conflicting clause, it resolves backward along the trail against the reason
/// clauses of current-level literals until exactly one current-level literal
/// remains — the UIP. The learned clause is that UIP (negated, at index 0) plus
/// every lower-level literal encountered; after backjumping it is asserting:
/// unit, immediately forcing the UIP literal.
/// </summary>
internal sealed class ConflictAnalyzer
{
    private readonly Trail _trail;
    private readonly ClauseDatabase _clauses;
    private readonly VsidsHeap _vsids;
    private readonly bool[] _seen;
    private readonly List<int> _learned = new();

    public ConflictAnalyzer(Trail trail, ClauseDatabase clauses, VsidsHeap vsids)
    {
        _trail = trail;
        _clauses = clauses;
        _vsids = vsids;
        _seen = new bool[trail.VariableCount];
    }

    public LearnedClause Analyze(CdclClause conflict)
    {
        _learned.Clear();
        _learned.Add(0); // placeholder for the asserting literal

        var currentLevel = _trail.DecisionLevel;
        var pathCount = 0;
        var trailIndex = _trail.AssignedCount - 1;
        var clause = conflict;
        var uip = 0;

        // The first clause is the conflict (resolve every literal); each later
        // clause is a reason whose implied literal — the one we are resolving on
        // — must be skipped to avoid re-counting it.
        var skipVariable = -1;

        do
        {
            BumpClause(clause);
            pathCount += MarkLiterals(clause, currentLevel, skipVariable);

            // Advance to the most recently assigned literal still marked `seen`.
            while (!_seen[CdclLiterals.Variable(_trail.LiteralAt(trailIndex))])
            {
                trailIndex--;
            }
            uip = _trail.LiteralAt(trailIndex);
            var uipVariable = CdclLiterals.Variable(uip);
            _seen[uipVariable] = false;
            pathCount--;

            if (pathCount > 0)
            {
                skipVariable = uipVariable;
                clause = _trail.ReasonFor(uipVariable)!;
            }
            trailIndex--;
        }
        while (pathCount > 0);

        _learned[0] = CdclLiterals.Negate(uip);

        // Future refinement: recursive self-subsuming clause minimization would
        // hook in here, dropping any learned literal whose reason clause is
        // already implied by the other learned literals (using the live `_seen`
        // marks). It shrinks learned clauses and improves performance but is not
        // required for correctness, so it is left out for clarity.

        var literals = _learned.ToArray();
        var backjumpLevel = ComputeBackjumpLevel(literals);
        ClearSeen(literals);
        return new LearnedClause(literals, backjumpLevel);
    }

    /// <summary>
    /// Mark unseen, non-level-0 literals of the clause. Current-level literals
    /// extend the resolution frontier (counted); lower-level literals go into
    /// the learned clause. Returns how many current-level literals were added.
    /// </summary>
    private int MarkLiterals(CdclClause clause, int currentLevel, int skipVariable)
    {
        var added = 0;
        foreach (var literal in clause.Literals)
        {
            var variable = CdclLiterals.Variable(literal);
            _vsids.Bump(variable);
            if (variable == skipVariable || _seen[variable] || _trail.LevelOf(variable) == 0)
            {
                continue;
            }
            _seen[variable] = true;
            if (_trail.LevelOf(variable) == currentLevel)
            {
                added++;
            }
            else
            {
                _learned.Add(literal);
            }
        }
        return added;
    }

    private void BumpClause(CdclClause clause)
    {
        if (clause.Learned)
        {
            _clauses.BumpActivity(clause);
        }
    }

    /// <summary>The backjump level is the second-highest level in the clause (0 if unit).</summary>
    private int ComputeBackjumpLevel(int[] literals)
    {
        var level = 0;
        for (var i = 1; i < literals.Length; i++)
        {
            level = Math.Max(level, _trail.LevelOf(CdclLiterals.Variable(literals[i])));
        }
        return level;
    }

    private void ClearSeen(int[] literals)
    {
        foreach (var literal in literals)
        {
            _seen[CdclLiterals.Variable(literal)] = false;
        }
    }
}
