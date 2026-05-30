namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Tuning knobs for the CDCL solver. Defaults follow common practice
/// (MiniSAT-like) and are reasonable across a wide range of instances.
/// </summary>
public sealed record SolverOptions
{
    /// <summary>VSIDS activity decay: variable bump is divided by this each conflict.</summary>
    public double VariableDecay { get; init; } = 0.95;

    /// <summary>Learned-clause activity decay, applied each conflict.</summary>
    public double ClauseDecay { get; init; } = 0.999;

    /// <summary>Conflicts per Luby unit; the restart threshold is luby(i) × this.</summary>
    public int RestartUnit { get; init; } = 100;

    /// <summary>
    /// Initial cap on the number of learned clauses before a reduction is
    /// triggered. The cap grows geometrically by
    /// <see cref="LearnedClauseGrowth"/> after each reduction.
    /// </summary>
    public int InitialLearnedClauseLimit { get; init; } = 2000;

    public double LearnedClauseGrowth { get; init; } = 1.1;

    public static SolverOptions Default { get; } = new();
}
