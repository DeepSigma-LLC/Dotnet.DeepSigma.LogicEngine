namespace DeepSigma.LogicEngine.Solvers;

/// <summary>Restart scheduling strategy for the CDCL search.</summary>
public enum RestartStrategy
{
    /// <summary>Luby reluctant-doubling schedule (machine-independent, strong worst-case bounds).</summary>
    Luby,

    /// <summary>Glucose-style adaptive restarts: restart when the recent learned-clause LBD runs worse than the long-run average.</summary>
    Glucose,
}

/// <summary>
/// Tuning knobs for the CDCL solver. Defaults follow common practice
/// (MiniSAT / Glucose-like) and are reasonable across a wide range of instances.
/// </summary>
public sealed record SolverOptions
{
    /// <summary>VSIDS activity decay: variable bump is divided by this each conflict.</summary>
    public double VariableDecay { get; init; } = 0.95;

    /// <summary>Learned-clause activity decay, applied each conflict.</summary>
    public double ClauseDecay { get; init; } = 0.999;

    /// <summary>Conflicts per Luby unit; the restart threshold is luby(i) × this.</summary>
    public int RestartUnit { get; init; } = 100;

    /// <summary>Which restart schedule to use (default: adaptive Glucose).</summary>
    public RestartStrategy RestartStrategy { get; init; } = RestartStrategy.Glucose;

    /// <summary>
    /// Apply recursive self-subsuming minimization to each learned clause,
    /// dropping literals implied by the rest of the clause. On by default.
    /// </summary>
    public bool MinimizeLearnedClauses { get; init; } = true;

    /// <summary>
    /// Prefer deleting high-LBD ("glue"-poor) learned clauses, protecting clauses
    /// whose literal block distance is ≤ 2. When false, deletion is purely by
    /// activity. On by default.
    /// </summary>
    public bool UseLbdClauseDeletion { get; init; } = true;

    /// <summary>
    /// Initial cap on the number of learned clauses before a reduction is
    /// triggered. The cap grows geometrically by
    /// <see cref="LearnedClauseGrowth"/> after each reduction.
    /// </summary>
    public int InitialLearnedClauseLimit { get; init; } = 2000;

    public double LearnedClauseGrowth { get; init; } = 1.1;

    public static SolverOptions Default { get; } = new();
}
