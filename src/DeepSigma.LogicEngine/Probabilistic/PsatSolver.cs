using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Probabilistic;

/// <summary>A probabilistic constraint <c>P(Formula) ⋈ Probability</c>.</summary>
public sealed record ProbabilityConstraint(Formula Formula, LinearRelation Relation, Rational Probability)
{
    public static ProbabilityConstraint Exactly(Formula formula, Rational probability)
        => new(formula, LinearRelation.Equal, probability);

    public static ProbabilityConstraint AtMost(Formula formula, Rational probability)
        => new(formula, LinearRelation.LessOrEqual, probability);

    public static ProbabilityConstraint AtLeast(Formula formula, Rational probability)
        => new(formula, LinearRelation.GreaterOrEqual, probability);
}

/// <summary>
/// Probabilistic satisfiability (PSAT): reasoning about probability constraints
/// on logical formulas. Decides whether a set of constraints is <b>coherent</b>
/// (some probability distribution over truth assignments satisfies them all) and
/// computes tight probability <b>bounds</b> on a query — with no independence or
/// structural assumptions. Built on the exact-rational LP solver, so verdicts
/// and bounds are exact.
///
/// <para>
/// <see cref="IsConsistent"/> and <see cref="Bounds"/> enumerate the possible
/// worlds (exact, exponential in the number of atoms). <see cref="IsConsistentScalable"/>
/// uses column generation — solving a restricted LP and pricing new worlds with a
/// MaxSAT step — to avoid enumerating all worlds (equality constraints).
/// </para>
/// </summary>
public static class PsatSolver
{
    /// <summary>True if the constraints are jointly coherent (a satisfying distribution exists).</summary>
    public static bool IsConsistent(IReadOnlyList<ProbabilityConstraint> constraints)
    {
        var atoms = AtomsOf(constraints, query: null);
        var worlds = EnumerateWorlds(atoms);
        var lp = BuildMaster(constraints, atoms, worlds);
        lp.SetObjective(LinearObjectiveSense.Minimize, Zeros(worlds.Count));
        return lp.Solve().Status == ExactLpStatus.Optimal;
    }

    /// <summary>
    /// Coherence by column generation: solves a small restricted LP and prices new
    /// worlds with a MaxSAT step instead of enumerating all 2ⁿ worlds. Exact and
    /// equivalent to <see cref="IsConsistent"/> for equality constraints
    /// <c>P(φ) = p</c> (other relations fall back to enumeration).
    /// </summary>
    public static bool IsConsistentScalable(IReadOnlyList<ProbabilityConstraint> constraints)
        => PsatColumnGeneration.IsConsistent(constraints);

    /// <summary>
    /// The tightest interval [low, high] for P(<paramref name="query"/>) consistent
    /// with the constraints, or null if the constraints are incoherent.
    /// </summary>
    public static (Rational Low, Rational High)? Bounds(IReadOnlyList<ProbabilityConstraint> constraints, Formula query)
    {
        var atoms = AtomsOf(constraints, query);
        var worlds = EnumerateWorlds(atoms);

        var queryMass = worlds.Select(w => Evaluator.Evaluate(query, w) ? Rational.One : Rational.Zero).ToArray();

        var low = BuildMaster(constraints, atoms, worlds);
        low.SetObjective(LinearObjectiveSense.Minimize, queryMass);
        var lowResult = low.Solve();
        if (lowResult.Status != ExactLpStatus.Optimal)
        {
            return null; // incoherent
        }

        var high = BuildMaster(constraints, atoms, worlds);
        high.SetObjective(LinearObjectiveSense.Maximize, queryMass);
        return (lowResult.Objective, high.Solve().Objective);
    }

    /// <summary>
    /// Probability bounds by column generation: generates a feasible world set, then
    /// maximizes / minimizes the query mass, pricing improving worlds with MaxSAT
    /// instead of enumerating all 2ⁿ worlds. Exact and equivalent to <see cref="Bounds"/>
    /// for equality constraints (other relations fall back to enumeration).
    /// </summary>
    public static (Rational Low, Rational High)? BoundsScalable(IReadOnlyList<ProbabilityConstraint> constraints, Formula query)
        => PsatColumnGeneration.Bounds(constraints, query);

    private static ExactLinearProgram BuildMaster(
        IReadOnlyList<ProbabilityConstraint> constraints,
        IReadOnlyList<string> atoms,
        IReadOnlyList<IReadOnlyDictionary<string, bool>> worlds)
    {
        var lp = new ExactLinearProgram(worlds.Count);
        foreach (var c in constraints)
        {
            var row = new Rational[worlds.Count];
            for (var w = 0; w < worlds.Count; w++)
            {
                row[w] = Evaluator.Evaluate(c.Formula, worlds[w]) ? Rational.One : Rational.Zero;
            }
            lp.AddConstraint(row, c.Relation, c.Probability);
        }
        // Probabilities of a partition of worlds sum to 1.
        lp.AddConstraint(Ones(worlds.Count), LinearRelation.Equal, Rational.One);
        return lp;
    }

    internal static IReadOnlyList<string> AtomsOf(IReadOnlyList<ProbabilityConstraint> constraints, Formula? query)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in constraints)
        {
            set.UnionWith(Evaluator.Variables(c.Formula));
        }
        if (query is not null)
        {
            set.UnionWith(Evaluator.Variables(query));
        }
        return set.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    internal static List<IReadOnlyDictionary<string, bool>> EnumerateWorlds(IReadOnlyList<string> atoms)
    {
        var worlds = new List<IReadOnlyDictionary<string, bool>>(1 << atoms.Count);
        for (var mask = 0; mask < (1 << atoms.Count); mask++)
        {
            var w = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (var i = 0; i < atoms.Count; i++)
            {
                w[atoms[i]] = (mask & (1 << i)) != 0;
            }
            worlds.Add(w);
        }
        return worlds;
    }

    private static Rational[] Zeros(int n) => Enumerable.Repeat(Rational.Zero, n).ToArray();
    private static Rational[] Ones(int n) => Enumerable.Repeat(Rational.One, n).ToArray();
}
