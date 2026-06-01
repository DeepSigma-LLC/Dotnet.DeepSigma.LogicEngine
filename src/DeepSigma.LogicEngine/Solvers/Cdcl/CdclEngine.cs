namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>Outcome of an assumption-aware search.</summary>
internal enum SearchOutcome
{
    Satisfiable,
    UnsatUnderAssumptions,
    RootUnsat,
}

/// <summary>
/// The reusable CDCL search core, working purely in integer literals over a
/// fixed variable count. Clauses can be added incrementally at the root level,
/// and <see cref="Search"/> may be called repeatedly — under different
/// assumptions — with all learned clauses and variable activity preserved
/// between calls. The string boundary is handled by callers via
/// <see cref="VariableMap"/>.
/// </summary>
internal sealed class CdclEngine
{
    private readonly Trail _trail;
    private readonly WatchList _watches;
    private readonly ClauseDatabase _clauses;
    private readonly VsidsHeap _vsids;
    private readonly UnitPropagator _propagator;
    private readonly ConflictAnalyzer _analyzer;
    private readonly ClauseReducer _reducer;
    private readonly SolverOptions _options;
    private readonly Action<int> _restoreToHeap;

    /// <summary>Set once the formula is unsatisfiable independent of any assumptions.</summary>
    private bool _rootConflict;

    public SolverStatistics Statistics { get; } = new();
    public int VariableCount { get; private set; }

    public CdclEngine(int variableCount, SolverOptions options)
    {
        _options = options;
        VariableCount = variableCount;
        _trail = new Trail(variableCount);
        _watches = new WatchList(variableCount);
        _clauses = new ClauseDatabase(options.ClauseDecay);
        _vsids = new VsidsHeap(variableCount, options.VariableDecay);
        _propagator = new UnitPropagator(_trail, _watches, _clauses, Statistics);
        _analyzer = new ConflictAnalyzer(_trail, _clauses, _vsids, options.MinimizeLearnedClauses);
        _reducer = new ClauseReducer(_clauses, _watches, _trail, Statistics, options);
        _restoreToHeap = _vsids.InsertIfAbsent;
    }

    /// <summary>
    /// Introduce a fresh, unconstrained variable and return its id. Grows all
    /// array-backed state in lockstep — true incremental-SAT <c>newVar()</c>.
    /// </summary>
    public int NewVariable()
    {
        var id = _trail.AddVariable();
        _watches.AddVariable();
        _vsids.AddVariable();
        _analyzer.AddVariable();
        VariableCount = id + 1;
        return id;
    }

    /// <summary>
    /// Add a permanent clause at the root level. Literals already false at level
    /// 0 are dropped; a literal already true makes the clause inert. Returns
    /// false if the clause makes the formula unconditionally unsatisfiable.
    /// </summary>
    public bool AddClause(IReadOnlyList<int> literals)
    {
        if (_rootConflict)
        {
            return false;
        }

        // Clauses are added at the root; a previous solve may have left a full
        // assignment on the trail, so unwind to level 0 before inspecting it.
        _trail.CancelUntil(0, _restoreToHeap);

        var nonFalse = new List<int>(literals.Count);
        foreach (var literal in literals)
        {
            switch (_trail.LiteralValue(literal))
            {
                case LBool.True:
                    return true; // satisfied at the root; nothing to store
                case LBool.Unassigned:
                    nonFalse.Add(literal);
                    break;
                // False literals are permanently false at level 0; drop them.
            }
        }

        switch (nonFalse.Count)
        {
            case 0:
                _rootConflict = true;
                return false;
            case 1:
                if (!_trail.Enqueue(nonFalse[0], reason: null))
                {
                    _rootConflict = true;
                    return false;
                }
                return true;
            default:
                var clause = _clauses.AddOriginal(nonFalse.ToArray());
                _watches.AttachClause(clause);
                return true;
        }
    }

    /// <summary>
    /// Search for a satisfying assignment in which every assumption literal is
    /// true. Returns true if satisfiable. Learned clauses persist for the next
    /// call.
    /// </summary>
    public bool Search(IReadOnlyList<int> assumptions, CancellationToken cancellationToken = default)
        => SearchEx(assumptions, out _, cancellationToken) == SearchOutcome.Satisfiable;

    /// <summary>
    /// Like <see cref="Search"/> but distinguishes a conflict independent of the
    /// assumptions (<see cref="SearchOutcome.RootUnsat"/>, permanently
    /// unsatisfiable) from one caused by the assumptions
    /// (<see cref="SearchOutcome.UnsatUnderAssumptions"/>), in which case
    /// <paramref name="failedAssumptions"/> is the responsible subset.
    /// </summary>
    public SearchOutcome SearchEx(IReadOnlyList<int> assumptions, out IReadOnlyList<int> failedAssumptions, CancellationToken cancellationToken = default)
    {
        failedAssumptions = Array.Empty<int>();
        if (_rootConflict)
        {
            return SearchOutcome.RootUnsat;
        }
        _trail.CancelUntil(0, _restoreToHeap);

        var restart = CreateRestartPolicy();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var conflict = _propagator.Propagate();
            CdclInvariants.AssertTrailLevelsMonotonic(_trail);

            if (conflict is not null)
            {
                Statistics.Conflicts++;
                if (_trail.DecisionLevel == 0)
                {
                    _rootConflict = true;
                    return SearchOutcome.RootUnsat;
                }
                restart.OnConflict(HandleConflict(conflict));
            }
            else if (restart.ShouldRestart())
            {
                Statistics.Restarts++;
                _trail.CancelUntil(0, _restoreToHeap);
                restart.OnRestart();
            }
            else
            {
                switch (NextDecision(assumptions, out var literal))
                {
                    case DecisionOutcome.Satisfiable:
                        CdclInvariants.AssertModelComplete(_trail);
                        return SearchOutcome.Satisfiable;
                    case DecisionOutcome.UnsatisfiableUnderAssumptions:
                        failedAssumptions = _analyzer.AnalyzeFinal(literal);
                        return SearchOutcome.UnsatUnderAssumptions;
                    default:
                        Statistics.Decisions++;
                        _trail.Decide(literal);
                        break;
                }
            }
        }
    }

    public bool IsTrue(int variable) => _trail.Value(variable) == LBool.True;

    private IRestartPolicy CreateRestartPolicy() => _options.RestartStrategy switch
    {
        RestartStrategy.Glucose => new GlucoseRestartPolicy(),
        _ => new LubyRestartPolicy(_options.RestartUnit),
    };

    /// <summary>Analyze a conflict, learn, backjump, and return the learned clause's LBD.</summary>
    private int HandleConflict(CdclClause conflict)
    {
        var conflictLevel = _trail.DecisionLevel;
        var learned = _analyzer.Analyze(conflict);
        CdclInvariants.AssertBackjumpProgress(learned, conflictLevel);

        _trail.CancelUntil(learned.BackjumpLevel, _restoreToHeap);
        LearnAndAssert(learned);

        _vsids.Decay();
        _clauses.DecayActivity();

        if (_reducer.ShouldReduce())
        {
            _reducer.Reduce();
        }
        return learned.Lbd;
    }

    private void LearnAndAssert(LearnedClause learned)
    {
        var literals = learned.Literals;
        Statistics.LearnedClauses++;
        if (literals.Length == 1)
        {
            _trail.Enqueue(literals[0], reason: null);
            return;
        }
        var clause = _clauses.AddLearned(literals, learned.Lbd);
        _watches.AttachClause(clause);
        _trail.Enqueue(literals[0], clause);
    }

    private enum DecisionOutcome
    {
        Decided,
        Satisfiable,
        UnsatisfiableUnderAssumptions,
    }

    /// <summary>
    /// Choose the next literal to assign: first any unsatisfied assumption (in
    /// order), then the highest-activity unassigned variable with its saved
    /// phase. Assumptions are re-scanned each call so they survive backjumps.
    /// </summary>
    private DecisionOutcome NextDecision(IReadOnlyList<int> assumptions, out int literal)
    {
        foreach (var assumption in assumptions)
        {
            switch (_trail.LiteralValue(assumption))
            {
                case LBool.False:
                    literal = assumption; // the failing assumption, for analyzeFinal
                    return DecisionOutcome.UnsatisfiableUnderAssumptions;
                case LBool.Unassigned:
                    literal = assumption;
                    return DecisionOutcome.Decided;
                // True: assumption already satisfied; check the next one.
            }
        }

        while (!_vsids.IsEmpty)
        {
            var variable = _vsids.RemoveMax();
            if (_trail.Value(variable) != LBool.Unassigned)
            {
                continue;
            }
            literal = _trail.SavedPhase(variable)
                ? CdclLiterals.Positive(variable)
                : CdclLiterals.Negative(variable);
            return DecisionOutcome.Decided;
        }

        literal = 0;
        return DecisionOutcome.Satisfiable;
    }
}
