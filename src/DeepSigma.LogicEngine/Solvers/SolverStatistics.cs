namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Mutable counters describing a single solve. Collaborators increment these
/// through internal setters; consumers read them after solving for observability.
/// </summary>
public sealed class SolverStatistics
{
    /// <summary>Number of branching decisions made.</summary>
    public long Decisions { get; internal set; }

    /// <summary>Number of unit propagations performed.</summary>
    public long Propagations { get; internal set; }

    /// <summary>Number of conflicts encountered.</summary>
    public long Conflicts { get; internal set; }

    /// <summary>Number of search restarts.</summary>
    public long Restarts { get; internal set; }

    /// <summary>Number of clauses learned from conflicts.</summary>
    public long LearnedClauses { get; internal set; }

    /// <summary>Number of learned clauses deleted during reductions.</summary>
    public long DeletedClauses { get; internal set; }

    /// <summary>A compact string of all counters.</summary>
    public override string ToString()
        => $"decisions={Decisions}, propagations={Propagations}, conflicts={Conflicts}, " +
           $"restarts={Restarts}, learned={LearnedClauses}, deleted={DeletedClauses}";
}
