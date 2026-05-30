namespace DeepSigma.LogicEngine.Solvers.Cdcl;

internal enum LBool : byte
{
    Unassigned = 0,
    True = 1,
    False = 2,
}

/// <summary>
/// The partial assignment and its history. Owns, per variable, the current
/// value, the decision level at which it was assigned, the implying (reason)
/// clause, and the saved polarity used for phase saving — all mutated together
/// on assignment and unwound atomically on backjump. The trail is the ordered
/// list of assigned literals; <see cref="QHead"/> marks the propagation
/// frontier.
/// </summary>
internal sealed class Trail
{
    private LBool[] _value;
    private int[] _level;
    private CdclClause?[] _reason;
    private bool[] _savedPhase;
    private readonly List<int> _trail = new();
    private readonly List<int> _trailLimits = new();

    public int VariableCount { get; private set; }
    public int QHead { get; private set; }

    public Trail(int variableCount)
    {
        VariableCount = variableCount;
        _value = new LBool[variableCount];
        _level = new int[variableCount];
        _reason = new CdclClause?[variableCount];
        _savedPhase = new bool[variableCount];
    }

    /// <summary>Append a fresh, unassigned variable and return its id.</summary>
    public int AddVariable()
    {
        var id = VariableCount++;
        Array.Resize(ref _value, VariableCount);
        Array.Resize(ref _level, VariableCount);
        Array.Resize(ref _reason, VariableCount);
        Array.Resize(ref _savedPhase, VariableCount);
        return id;
    }

    public int DecisionLevel => _trailLimits.Count;

    /// <summary>Number of literals currently assigned (trail length).</summary>
    public int AssignedCount => _trail.Count;

    /// <summary>The literal at the given trail position.</summary>
    public int LiteralAt(int trailIndex) => _trail[trailIndex];

    public LBool Value(int variable) => _value[variable];

    public int LevelOf(int variable) => _level[variable];

    public CdclClause? ReasonFor(int variable) => _reason[variable];

    public bool SavedPhase(int variable) => _savedPhase[variable];

    public bool HasPropagationWork => QHead < _trail.Count;

    public int DequeueForPropagation() => _trail[QHead++];

    /// <summary>Advance the propagation frontier to the end of the trail.</summary>
    public void HaltPropagation() => QHead = _trail.Count;

    /// <summary>The truth value of a literal under the current assignment.</summary>
    public LBool LiteralValue(int literal)
    {
        var value = _value[CdclLiterals.Variable(literal)];
        if (value == LBool.Unassigned)
        {
            return LBool.Unassigned;
        }
        var wantsTrue = !CdclLiterals.IsNegated(literal);
        var isTrue = value == LBool.True;
        return wantsTrue == isTrue ? LBool.True : LBool.False;
    }

    /// <summary>
    /// Assign a literal true with the given reason (null for a decision or a
    /// level-0 fact). Returns false if the variable is already assigned to the
    /// opposite value (a conflict on enqueue).
    /// </summary>
    public bool Enqueue(int literal, CdclClause? reason)
    {
        var variable = CdclLiterals.Variable(literal);
        if (_value[variable] != LBool.Unassigned)
        {
            return LiteralValue(literal) == LBool.True;
        }
        var isTrue = !CdclLiterals.IsNegated(literal);
        _value[variable] = isTrue ? LBool.True : LBool.False;
        _level[variable] = DecisionLevel;
        _reason[variable] = reason;
        _savedPhase[variable] = isTrue;
        _trail.Add(literal);
        return true;
    }

    public void NewDecisionLevel() => _trailLimits.Add(_trail.Count);

    public void Decide(int literal)
    {
        NewDecisionLevel();
        Enqueue(literal, reason: null);
    }

    /// <summary>
    /// Undo every assignment made above <paramref name="targetLevel"/>, invoking
    /// <paramref name="onUnassign"/> for each freed variable (used to restore it
    /// to the decision heap). Saved phases are deliberately preserved.
    /// </summary>
    public void CancelUntil(int targetLevel, Action<int> onUnassign)
    {
        if (DecisionLevel <= targetLevel)
        {
            return;
        }
        var firstToUndo = _trailLimits[targetLevel];
        for (var i = _trail.Count - 1; i >= firstToUndo; i--)
        {
            var variable = CdclLiterals.Variable(_trail[i]);
            _value[variable] = LBool.Unassigned;
            _reason[variable] = null;
            onUnassign(variable);
        }
        _trail.RemoveRange(firstToUndo, _trail.Count - firstToUndo);
        _trailLimits.RemoveRange(targetLevel, _trailLimits.Count - targetLevel);
        QHead = _trail.Count;
    }
}
