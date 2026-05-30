using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// A modern CDCL (Conflict-Driven Clause Learning) SAT solver: watched-literal
/// unit propagation, first-UIP clause learning, non-chronological backjumping,
/// VSIDS branching, Luby restarts, and learned-clause deletion. Each call to
/// <see cref="Solve(CnfFormula)"/> runs on isolated state. For repeated solving
/// of a growing formula with reuse of learned clauses, see
/// <see cref="IncrementalCdclSolver"/>.
/// </summary>
public sealed class CdclSolver : ISatSolver
{
    private readonly SolverOptions _options;

    public CdclSolver(SolverOptions? options = null) => _options = options ?? SolverOptions.Default;

    /// <summary>Statistics from the most recent solve.</summary>
    public SolverStatistics Statistics { get; private set; } = new();

    public SatResult Solve(Formula formula)
    {
        var prepared = CnfPreparer.Prepare(formula);
        var result = Solve(prepared.Cnf);
        return result is { IsSatisfiable: true, Model: not null }
            ? SatResult.Satisfiable(CnfPreparer.Project(result.Model, prepared.OriginalVariables))
            : result;
    }

    public SatResult Solve(CnfFormula formula)
    {
        var (map, clauses) = VariableMap.Encode(formula);
        var engine = new CdclEngine(map.Count, _options);

        foreach (var clause in clauses)
        {
            if (!engine.AddClause(clause))
            {
                Statistics = engine.Statistics;
                return SatResult.Unsatisfiable;
            }
        }

        var satisfiable = engine.Search(Array.Empty<int>());
        Statistics = engine.Statistics;
        if (!satisfiable)
        {
            return SatResult.Unsatisfiable;
        }
        var assignment = map.Decode(engine.IsTrue);
        return SatResult.Satisfiable(Model.From(assignment));
    }
}
