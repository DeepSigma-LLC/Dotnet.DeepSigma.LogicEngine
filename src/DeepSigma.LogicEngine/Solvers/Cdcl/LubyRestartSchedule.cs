namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Luby restart schedule. The conflict budget before the i-th restart is
/// <c>luby(i) × unit</c>, where the Luby sequence is 1,1,2,1,1,2,4,1,1,2,1,1,2,4,8,…
/// — a strategy with strong worst-case guarantees. The caller counts conflicts
/// and asks whether the current budget is exhausted.
///
/// <para>
/// Why restart at all: a single long search can get stuck deep in an unlucky part
/// of the space, so periodically abandoning the current attempt (while keeping the
/// learned clauses) often finds an answer faster. But the best restart frequency
/// is unknown in advance. The Luby sequence hedges by interleaving many short runs
/// with occasional longer ones; this universal schedule is provably within a
/// logarithmic factor of the best fixed restart length for any instance — so it is
/// a safe default when nothing is known about the problem.
/// </para>
/// </summary>
internal sealed class LubyRestartSchedule
{
    private readonly int _unit;
    private int _index = 1;

    public LubyRestartSchedule(int unit) => _unit = unit;

    /// <summary>The conflict budget for the current restart cycle.</summary>
    public int CurrentBudget => Luby(_index) * _unit;

    /// <summary>Advance to the next cycle in the sequence.</summary>
    public void Advance() => _index++;

    /// <summary>
    /// The i-th term of the Luby sequence (i ≥ 1) via reluctant doubling.
    /// </summary>
    public static int Luby(int i)
    {
        // Find the smallest k with (2^k - 1) >= i.
        var k = 1;
        while ((1 << k) - 1 < i)
        {
            k++;
        }
        if (i == (1 << k) - 1)
        {
            return 1 << (k - 1);
        }
        // Recurse within the preceding block.
        return Luby(i - (1 << (k - 1)) + 1);
    }
}
