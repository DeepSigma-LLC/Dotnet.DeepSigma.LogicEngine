using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// A CDCL solver that retains its learned clauses and variable activity across
/// solves. Build the formula from an initial CNF, optionally add more clauses,
/// and call <see cref="Solve"/> or <see cref="SolveUnder"/> repeatedly. This is
/// far more efficient than rebuilding a fresh solver when the formula grows
/// monotonically — for example, enumerating models by adding a blocking clause
/// after each solution.
///
/// <para>
/// The variable universe is fixed at construction: every variable referenced by
/// later clauses or assumptions must already appear in the initial formula.
/// </para>
/// </summary>
public sealed class IncrementalCdclSolver
{
    private readonly VariableMap _map;
    private readonly CdclEngine _engine;

    public IncrementalCdclSolver(CnfFormula formula, SolverOptions? options = null)
        : this(formula, Array.Empty<string>(), options)
    {
    }

    /// <summary>
    /// Construct from a formula plus extra variable names to include in the
    /// universe up front — useful when later clauses or assumptions will mention
    /// variables that do not appear in <paramref name="formula"/> itself.
    /// </summary>
    public IncrementalCdclSolver(CnfFormula formula, IEnumerable<string> additionalVariables, SolverOptions? options = null)
    {
        List<int[]> clauses;
        (_map, clauses) = VariableMap.Encode(formula);
        foreach (var name in additionalVariables)
        {
            _map.GetOrAdd(name);
        }
        _engine = new CdclEngine(_map.Count, options ?? SolverOptions.Default);
        foreach (var clause in clauses)
        {
            _engine.AddClause(clause);
        }
    }

    public SolverStatistics Statistics => _engine.Statistics;

    /// <summary>
    /// Add a permanent clause over already-known variables. Returns false if the
    /// clause makes the formula unconditionally unsatisfiable.
    /// </summary>
    public bool AddClause(IReadOnlyCollection<Literal> clause)
        => _engine.AddClause(Encode(clause));

    /// <summary>
    /// Introduce a fresh variable by name if it is not already known, growing the
    /// solver's universe. Idempotent. Lets callers (e.g. MaxSAT) add selector and
    /// relaxation variables after construction.
    /// </summary>
    public void NewVariable(string name)
    {
        if (_map.TryGetId(name, out _))
        {
            return;
        }
        var mapId = _map.GetOrAdd(name);
        var engineId = _engine.NewVariable();
        System.Diagnostics.Debug.Assert(mapId == engineId, "VariableMap and engine variable ids must stay in lockstep.");
    }

    public SatResult Solve() => SolveUnder(Array.Empty<Literal>());

    /// <summary>
    /// Solve under assumptions and, on failure, report the responsible subset of
    /// assumptions (empty if the formula is unsatisfiable regardless of them).
    /// </summary>
    public UnsatCoreResult SolveUnderWithCore(IReadOnlyCollection<Literal> assumptions)
    {
        var encoded = Encode(assumptions);
        var inputSet = new HashSet<int>(encoded);
        var outcome = _engine.SearchEx(encoded, out var failed);
        if (outcome == SearchOutcome.Satisfiable)
        {
            return UnsatCoreResult.Satisfiable(Model.From(_map.Decode(_engine.IsTrue)));
        }

        var core = new List<Literal>();
        var seen = new HashSet<int>();
        foreach (var lit in failed)
        {
            if (inputSet.Contains(lit) && seen.Add(lit))
            {
                core.Add(new Literal(_map.NameOf(CdclLiterals.Variable(lit)), CdclLiterals.IsNegated(lit)));
            }
        }
        return UnsatCoreResult.Unsatisfiable(core);
    }

    /// <summary>
    /// Solve subject to the given assumption literals being true. The
    /// assumptions constrain only this call; the next call may use different
    /// ones. Returns unsatisfiable either when the formula is contradictory or
    /// when the assumptions cannot be met.
    /// </summary>
    public SatResult SolveUnder(IReadOnlyCollection<Literal> assumptions)
    {
        var encoded = Encode(assumptions);
        if (!_engine.Search(encoded))
        {
            return SatResult.Unsatisfiable;
        }
        return SatResult.Satisfiable(Model.From(_map.Decode(_engine.IsTrue)));
    }

    private int[] Encode(IReadOnlyCollection<Literal> literals)
    {
        var encoded = new int[literals.Count];
        var i = 0;
        foreach (var literal in literals)
        {
            if (!_map.TryGetId(literal.Variable, out var id))
            {
                throw new InvalidOperationException(
                    $"Variable '{literal.Variable}' is not part of this solver's initial formula.");
            }
            encoded[i++] = CdclLiterals.Make(id, literal.Negated);
        }
        return encoded;
    }
}
