namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// Mutable counters describing a single solve. Collaborators increment these
/// through internal setters; consumers read them after solving for observability.
/// </summary>
public sealed class SolverStatistics
{
    public long Decisions { get; internal set; }
    public long Propagations { get; internal set; }
    public long Conflicts { get; internal set; }
    public long Restarts { get; internal set; }
    public long LearnedClauses { get; internal set; }
    public long DeletedClauses { get; internal set; }

    public override string ToString()
        => $"decisions={Decisions}, propagations={Propagations}, conflicts={Conflicts}, " +
           $"restarts={Restarts}, learned={LearnedClauses}, deleted={DeletedClauses}";
}
