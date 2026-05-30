namespace DeepSigma.LogicEngine.Solvers.Cdcl;

internal readonly record struct LearnedClause(int[] Literals, int BackjumpLevel, int Lbd);

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
    /// <summary>Recursion guard for clause minimization (deep implication chains stay bounded).</summary>
    private const int MinimizeDepthLimit = 1000;

    private readonly Trail _trail;
    private readonly ClauseDatabase _clauses;
    private readonly VsidsHeap _vsids;
    private readonly bool _minimize;
    private bool[] _seen;
    private readonly List<int> _learned = new();
    private readonly List<int> _touched = new(); // every variable we marked seen, for O(touched) cleanup

    public ConflictAnalyzer(Trail trail, ClauseDatabase clauses, VsidsHeap vsids, bool minimize = false)
    {
        _trail = trail;
        _clauses = clauses;
        _vsids = vsids;
        _minimize = minimize;
        _seen = new bool[trail.VariableCount];
    }

    /// <summary>Grow scratch state to accommodate one more variable.</summary>
    public void AddVariable() => Array.Resize(ref _seen, _seen.Length + 1);

    /// <summary>
    /// When a search fails because an assumption literal was forced false,
    /// compute the subset of assumption literals responsible. Walks the
    /// implication graph of the failing literal, collecting decision literals
    /// (which, during assumption-driven search, are the assumptions) at the
    /// polarity they were assigned. Level-0 facts are excluded.
    /// </summary>
    public IReadOnlyList<int> AnalyzeFinal(int failingAssumption)
    {
        var core = new List<int> { failingAssumption };
        var failingVar = CdclLiterals.Variable(failingAssumption);
        if (_trail.LevelOf(failingVar) == 0)
        {
            // Forced false at the root: the assumption alone is inconsistent.
            return core;
        }

        var touched = new List<int>();
        _seen[failingVar] = true;
        touched.Add(failingVar);

        for (var i = _trail.AssignedCount - 1; i >= 0; i--)
        {
            var literal = _trail.LiteralAt(i);
            var variable = CdclLiterals.Variable(literal);
            if (!_seen[variable])
            {
                continue;
            }
            var reason = _trail.ReasonFor(variable);
            if (reason is null)
            {
                if (_trail.LevelOf(variable) > 0)
                {
                    core.Add(literal); // a decision = an assumption, at its asserted polarity
                }
            }
            else
            {
                foreach (var q in reason.Literals)
                {
                    var qv = CdclLiterals.Variable(q);
                    if (qv != variable && !_seen[qv] && _trail.LevelOf(qv) > 0)
                    {
                        _seen[qv] = true;
                        touched.Add(qv);
                    }
                }
            }
        }

        foreach (var v in touched)
        {
            _seen[v] = false;
        }
        return core;
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

        // Recursive self-subsuming minimization: drop any learned literal whose
        // reason is already implied by the rest of the clause. Sound, and shrinks
        // clauses (smaller clauses propagate more and cost less to keep).
        if (_minimize)
        {
            Minimize();
        }

        var literals = _learned.ToArray();
        // Put a maximum-level literal in slot 1 so the second watch is sound after
        // backjumping (slot 0 is the asserting literal, enqueued immediately).
        PlaceSecondWatch(literals);
        var backjumpLevel = ComputeBackjumpLevel(literals);
        var lbd = LiteralBlockDistance(literals);
        ClearMarks();
        return new LearnedClause(literals, backjumpLevel, lbd);
    }

    /// <summary>
    /// Drop redundant literals (indices ≥ 1): a literal is redundant when its
    /// reason's other literals are all already implied — present in the clause,
    /// level-0 facts, or themselves recursively redundant. Decisions are never
    /// redundant. The asserting literal at index 0 is always kept.
    /// </summary>
    private void Minimize()
    {
        Mark(CdclLiterals.Variable(_learned[0])); // treat the asserting literal as in-clause too

        var kept = new List<int> { _learned[0] };
        for (var i = 1; i < _learned.Count; i++)
        {
            var literal = _learned[i];
            var variable = CdclLiterals.Variable(literal);
            if (_trail.ReasonFor(variable) is null || !IsRedundant(variable, depth: 0))
            {
                kept.Add(literal);
            }
        }
        _learned.Clear();
        _learned.AddRange(kept);
    }

    private bool IsRedundant(int variable, int depth)
    {
        if (depth >= MinimizeDepthLimit)
        {
            return false; // conservative: keep the literal rather than recurse unbounded
        }
        var reason = _trail.ReasonFor(variable);
        if (reason is null)
        {
            return false; // a decision literal cannot be dropped
        }
        foreach (var literal in reason.Literals)
        {
            var v = CdclLiterals.Variable(literal);
            if (v == variable || _trail.LevelOf(v) == 0 || _seen[v])
            {
                continue; // self, root-implied, or already covered by the clause
            }
            if (_trail.ReasonFor(v) is null || !IsRedundant(v, depth + 1))
            {
                return false;
            }
            Mark(v); // proven redundant ⇒ covered for the rest of this analysis
        }
        return true;
    }

    /// <summary>Swap a highest-decision-level literal into slot 1 (no-op for unit clauses).</summary>
    private void PlaceSecondWatch(int[] literals)
    {
        if (literals.Length < 2)
        {
            return;
        }
        var best = 1;
        for (var i = 2; i < literals.Length; i++)
        {
            if (_trail.LevelOf(CdclLiterals.Variable(literals[i])) > _trail.LevelOf(CdclLiterals.Variable(literals[best])))
            {
                best = i;
            }
        }
        (literals[1], literals[best]) = (literals[best], literals[1]);
    }

    /// <summary>The number of distinct decision levels among the clause's literals.</summary>
    private int LiteralBlockDistance(int[] literals)
    {
        var levels = new HashSet<int>();
        foreach (var literal in literals)
        {
            levels.Add(_trail.LevelOf(CdclLiterals.Variable(literal)));
        }
        return levels.Count;
    }

    private void Mark(int variable)
    {
        _seen[variable] = true;
        _touched.Add(variable);
    }

    private void ClearMarks()
    {
        foreach (var variable in _touched)
        {
            _seen[variable] = false;
        }
        _touched.Clear();
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
            Mark(variable);
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
}
