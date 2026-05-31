namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Decides when the search should restart (backtrack to the root and re-decide,
/// keeping all learned clauses and activities). Restarts never affect soundness —
/// only how quickly the search escapes unproductive regions.
/// </summary>
internal interface IRestartPolicy
{
    /// <summary>Record a conflict and the LBD of the clause learned from it.</summary>
    void OnConflict(int learnedLbd);

    /// <summary>Whether a restart should happen now (checked when propagation is conflict-free).</summary>
    bool ShouldRestart();

    /// <summary>Notify that a restart has been performed.</summary>
    void OnRestart();
}

/// <summary>
/// A <em>fixed</em>, instance-independent restart schedule: restart once a <c>luby(i)×unit</c>
/// conflict budget is spent (see <see cref="LubyRestartSchedule"/>). Contrast the
/// <see cref="GlucoseRestartPolicy"/>, which adapts to how well the search is going.
/// </summary>
internal sealed class LubyRestartPolicy : IRestartPolicy
{
    private readonly LubyRestartSchedule _schedule;
    private int _conflictsSinceRestart;

    public LubyRestartPolicy(int unit) => _schedule = new LubyRestartSchedule(unit);

    public void OnConflict(int learnedLbd) => _conflictsSinceRestart++;

    public bool ShouldRestart() => _conflictsSinceRestart >= _schedule.CurrentBudget;

    public void OnRestart()
    {
        _schedule.Advance();
        _conflictsSinceRestart = 0;
    }
}

/// <summary>
/// Glucose-style adaptive restarts. Keeps a short window of recent learned-clause
/// LBDs and the long-run average; when the recent average runs sufficiently worse
/// (higher) than the global average, the search is judged unproductive and restarts.
/// A minimum number of conflicts between restarts avoids thrashing.
/// </summary>
internal sealed class GlucoseRestartPolicy : IRestartPolicy
{
    private readonly Queue<int> _window = new();
    private readonly int _windowSize;
    private readonly double _factor;
    private readonly int _minConflictsBetweenRestarts;

    private long _windowSum;
    private long _globalSum;
    private long _globalCount;
    private int _conflictsSinceRestart;

    public GlucoseRestartPolicy(int windowSize = 50, double factor = 0.8, int minConflictsBetweenRestarts = 50)
    {
        _windowSize = windowSize;
        _factor = factor;
        _minConflictsBetweenRestarts = minConflictsBetweenRestarts;
    }

    public void OnConflict(int learnedLbd)
    {
        _globalSum += learnedLbd;
        _globalCount++;
        _conflictsSinceRestart++;

        _window.Enqueue(learnedLbd);
        _windowSum += learnedLbd;
        if (_window.Count > _windowSize)
        {
            _windowSum -= _window.Dequeue();
        }
    }

    public bool ShouldRestart()
    {
        if (_window.Count < _windowSize || _conflictsSinceRestart < _minConflictsBetweenRestarts || _globalCount == 0)
        {
            return false;
        }
        var windowAverage = (double)_windowSum / _window.Count;
        var globalAverage = (double)_globalSum / _globalCount;
        return windowAverage * _factor > globalAverage;
    }

    public void OnRestart()
    {
        // Forget the recent window so the next cycle re-measures from scratch.
        _window.Clear();
        _windowSum = 0;
        _conflictsSinceRestart = 0;
    }
}
