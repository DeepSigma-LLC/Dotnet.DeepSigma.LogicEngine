using System.Numerics;
using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers.MaxSat;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;
using static DeepSigma.LogicEngine.Probabilistic.RationalVectors;

namespace DeepSigma.LogicEngine.Probabilistic;

/// <summary>
/// Scalable PSAT via <b>column generation</b>. The full PSAT linear program has one
/// variable per possible world (exponentially many). Instead of materializing them
/// all, we solve a restricted LP over a small working set of worlds and use its
/// exact <b>dual prices</b> to <i>price</i> the next world to bring in — the world
/// whose column most improves the objective. Pricing is itself an optimization over
/// assignments, solved as a <see cref="MaxSatSolver">MaxSAT</see> problem; when no
/// world prices in, the restricted optimum is the true optimum.
///
/// <para>
/// <b>Coherence</b> minimizes total infeasibility (artificial mass on each equality):
/// coherent iff that minimum is zero. <b>Bounds</b> first generates a feasible world
/// set, then maximizes / minimizes the query mass, generating improving worlds by
/// reduced cost. All arithmetic is exact, so verdicts and bounds are exact.
/// </para>
/// </summary>
internal static class PsatColumnGeneration
{
    /// <summary>A formula weighted by a rational coefficient, used to build pricing objectives.</summary>
    private readonly record struct WeightedFormula(Formula Formula, Rational Weight);

    public static bool IsConsistent(IReadOnlyList<ProbabilityConstraint> constraints)
    {
        if (HasInequality(constraints))
        {
            return PsatSolver.IsConsistent(constraints); // exact fallback (handles <=, >=)
        }

        var atoms = PsatSolver.AtomsOf(constraints, query: null);
        return GenerateFeasibleWorlds(constraints, atoms, out _);
    }

    public static (Rational Low, Rational High)? Bounds(IReadOnlyList<ProbabilityConstraint> constraints, Formula query)
    {
        if (HasInequality(constraints))
        {
            return PsatSolver.Bounds(constraints, query); // exact fallback
        }

        // Query atoms join the atom set so worlds can evaluate the query (and any
        // query-only atom stays free, exactly as in the enumeration solver).
        var atoms = PsatSolver.AtomsOf(constraints, query);
        if (!GenerateFeasibleWorlds(constraints, atoms, out var worlds))
        {
            return null; // incoherent
        }

        var high = Optimize(constraints, query, atoms, worlds, maximize: true);
        var low = Optimize(constraints, query, atoms, worlds, maximize: false);
        if (high is null || low is null)
        {
            return PsatSolver.Bounds(constraints, query); // defensive: exact fallback on stall
        }
        return (low.Value, high.Value);
    }

    private static bool HasInequality(IReadOnlyList<ProbabilityConstraint> constraints)
        => constraints.Any(c => c.Relation != LinearRelation.Equal);

    // ---- Phase 1: feasibility (minimize infeasibility, grow worlds until zero) ----

    /// <summary>
    /// Generate worlds until the restricted master can satisfy every constraint
    /// exactly (coherent) or no world improves the residual infeasibility (incoherent).
    /// On success, <paramref name="worlds"/> is a feasible working set to optimize over.
    /// </summary>
    private static bool GenerateFeasibleWorlds(
        IReadOnlyList<ProbabilityConstraint> constraints,
        IReadOnlyList<string> atoms,
        out List<IReadOnlyDictionary<string, bool>> worlds)
    {
        var formulas = constraints.Select(c => c.Formula).ToArray();
        var targetProbabilities = constraints.Select(c => c.Probability).Append(Rational.One).ToArray();
        var lpRowCount = constraints.Count + 1;
        var normalizationRowIndex = constraints.Count;

        worlds = new List<IReadOnlyDictionary<string, bool>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddWorld(worlds, seen, atoms, AllFalse(atoms)); // seed

        foreach (var _ in Rounds(atoms.Count, lpRowCount))
        {
            var result = SolveInfeasibilityMaster(worlds, formulas, targetProbabilities, lpRowCount, normalizationRowIndex);
            if (result.Objective == Rational.Zero)
            {
                return true;
            }

            var weighted = ConstraintWeights(formulas, result.Duals);
            var priced = PriceWorld(weighted, atoms);
            // reduced cost > 0 ⇔ y·column > 0 ⇔ EvalWeighted + normalization-dual > 0
            var value = EvalWeighted(priced, weighted) + result.Duals[normalizationRowIndex];
            if (value <= Rational.Zero)
            {
                return false; // no improving world: infeasibility is irreducible ⇒ incoherent
            }
            if (!AddWorld(worlds, seen, atoms, priced))
            {
                return PsatSolver.IsConsistent(constraints); // defensive exact fallback
            }
        }

        return PsatSolver.IsConsistent(constraints);
    }

    /// <summary>
    /// Restricted master: world masses plus a pair of non-negative artificials per
    /// row absorbing deficit/surplus. Minimize the artificial mass.
    /// </summary>
    private static ExactLpResult SolveInfeasibilityMaster(
        IReadOnlyList<IReadOnlyDictionary<string, bool>> worlds,
        Formula[] formulas,
        Rational[] targetProbabilities,
        int lpRowCount,
        int normalizationRowIndex)
    {
        var worldCount = worlds.Count;
        var varCount = worldCount + 2 * lpRowCount; // [x_w...][a+_r, a-_r ...]
        var lp = new ExactLinearProgram(varCount);

        var objective = Zeros(varCount);
        for (var r = 0; r < lpRowCount; r++)
        {
            objective[worldCount + 2 * r] = Rational.One;     // a+_r
            objective[worldCount + 2 * r + 1] = Rational.One; // a-_r
        }

        for (var r = 0; r < lpRowCount; r++)
        {
            var row = Zeros(varCount);
            for (var w = 0; w < worldCount; w++)
            {
                row[w] = Membership(r, normalizationRowIndex, formulas, worlds[w]);
            }
            row[worldCount + 2 * r] = Rational.One;      // +a+_r
            row[worldCount + 2 * r + 1] = -Rational.One; // -a-_r
            lp.AddConstraint(row, LinearRelation.Equal, targetProbabilities[r]);
        }

        lp.SetObjective(LinearObjectiveSense.Minimize, objective);
        return lp.Solve();
    }

    // ---- Phase 2: optimize the query mass over the (extensible) feasible worlds ----

    private static Rational? Optimize(
        IReadOnlyList<ProbabilityConstraint> constraints,
        Formula query,
        IReadOnlyList<string> atoms,
        List<IReadOnlyDictionary<string, bool>> worlds,
        bool maximize)
    {
        var formulas = constraints.Select(c => c.Formula).ToArray();
        var targetProbabilities = constraints.Select(c => c.Probability).Append(Rational.One).ToArray();
        var lpRowCount = constraints.Count + 1;
        var normalizationRowIndex = constraints.Count;
        var seen = new HashSet<string>(worlds.Select(w => Key(atoms, w)), StringComparer.Ordinal);

        Rational objective = Rational.Zero;
        foreach (var _ in Rounds(atoms.Count, lpRowCount))
        {
            var result = SolveQueryMaster(worlds, formulas, targetProbabilities, lpRowCount, normalizationRowIndex, query, maximize);
            if (result.Status != ExactLpStatus.Optimal)
            {
                return null; // should not happen (feasible & bounded); defensive
            }
            objective = result.Objective;

            // Pricing objective by reduced cost:
            //   maximize:  c_j - y·A_j  with c_j = [w⊨Q]            (weights: +1·Q, -y_i·φ_i;  const -y_norm)
            //   minimize:  y·A_j - c_j  with c_j = [w⊨Q]            (weights: +y_i·φ_i, -1·Q;   const +y_norm)
            var weighted = QueryWeights(formulas, query, result.Duals, maximize);
            var priced = PriceWorld(weighted, atoms);
            var constant = maximize ? -result.Duals[normalizationRowIndex] : result.Duals[normalizationRowIndex];
            var value = EvalWeighted(priced, weighted) + constant;
            if (value <= Rational.Zero)
            {
                return objective; // no improving world: restricted optimum is the true optimum
            }
            if (!AddWorld(worlds, seen, atoms, priced))
            {
                return null; // defensive stall ⇒ caller falls back to enumeration
            }
        }

        return null;
    }

    /// <summary>Pure master (no artificials): equality constraints + normalization, optimize query mass.</summary>
    private static ExactLpResult SolveQueryMaster(
        IReadOnlyList<IReadOnlyDictionary<string, bool>> worlds,
        Formula[] formulas,
        Rational[] targetProbabilities,
        int lpRowCount,
        int normalizationRowIndex,
        Formula query,
        bool maximize)
    {
        var worldCount = worlds.Count;
        var lp = new ExactLinearProgram(worldCount);

        for (var r = 0; r < lpRowCount; r++)
        {
            var row = Zeros(worldCount);
            for (var w = 0; w < worldCount; w++)
            {
                row[w] = Membership(r, normalizationRowIndex, formulas, worlds[w]);
            }
            lp.AddConstraint(row, LinearRelation.Equal, targetProbabilities[r]);
        }

        var objective = Zeros(worldCount);
        for (var w = 0; w < worldCount; w++)
        {
            objective[w] = Evaluator.Evaluate(query, worlds[w]) ? Rational.One : Rational.Zero;
        }
        lp.SetObjective(maximize ? LinearObjectiveSense.Maximize : LinearObjectiveSense.Minimize, objective);
        return lp.Solve();
    }

    // ---- Pricing (shared): find the assignment maximizing Σ weightᵢ·[w ⊨ formulaᵢ] ----

    private static IReadOnlyList<WeightedFormula> ConstraintWeights(Formula[] formulas, IReadOnlyList<Rational> duals)
        => formulas.Select((f, i) => new WeightedFormula(f, duals[i])).ToArray();

    private static IReadOnlyList<WeightedFormula> QueryWeights(Formula[] formulas, Formula query, IReadOnlyList<Rational> duals, bool maximize)
    {
        var list = new List<WeightedFormula>(formulas.Length + 1)
        {
            new(query, maximize ? Rational.One : -Rational.One),
        };
        for (var i = 0; i < formulas.Length; i++)
        {
            list.Add(new WeightedFormula(formulas[i], maximize ? -duals[i] : duals[i]));
        }
        return list;
    }

    private static Rational EvalWeighted(IReadOnlyDictionary<string, bool> world, IReadOnlyList<WeightedFormula> weighted)
    {
        var sum = Rational.Zero;
        foreach (var wf in weighted)
        {
            if (Evaluator.Evaluate(wf.Formula, world))
            {
                sum += wf.Weight;
            }
        }
        return sum;
    }

    /// <summary>
    /// Maximize Σ weightᵢ·[w ⊨ formulaᵢ] as MaxSAT: assert a fresh indicator bᵢ ⇔ formulaᵢ
    /// (hard clauses) and add a unit soft clause rewarding bᵢ on the side that raises the
    /// objective, weighted by |weightᵢ| scaled to integers via the common denominator.
    /// </summary>
    private static IReadOnlyDictionary<string, bool> PriceWorld(IReadOnlyList<WeightedFormula> weighted, IReadOnlyList<string> atoms)
    {
        var scaled = ScaleToIntegers(weighted);

        var hard = new List<IReadOnlyList<Literal>>();
        var soft = new List<SoftClause>();
        for (var i = 0; i < weighted.Count; i++)
        {
            if (scaled[i] == 0)
            {
                continue;
            }
            var indicator = $"__psat_b{i}";
            foreach (var clause in CnfTransformer.ToCnf(Formula.Iff(Formula.Var(indicator), weighted[i].Formula)).Clauses)
            {
                hard.Add(clause.Literals);
            }
            var wantTrue = weighted[i].Weight > Rational.Zero;
            soft.Add(new SoftClause(new[] { wantTrue ? Literal.Positive(indicator) : Literal.Negative(indicator) }, scaled[i]));
        }

        var model = soft.Count == 0
            ? (IReadOnlyDictionary<string, bool>)new Dictionary<string, bool>()
            // The pricing problem encodes indicator ⇔ formula, which is always satisfiable, so the model is non-null.
            : new MaxSatSolver(hard, soft).Solve().Model!;

        var world = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var atom in atoms)
        {
            world[atom] = model.TryGetValue(atom, out var v) && v;
        }
        return world;
    }

    private static long[] ScaleToIntegers(IReadOnlyList<WeightedFormula> weighted)
    {
        var denominator = BigInteger.One;
        foreach (var wf in weighted)
        {
            denominator = Lcm(denominator, wf.Weight.Denominator);
        }
        var weights = new long[weighted.Count];
        for (var i = 0; i < weighted.Count; i++)
        {
            var scaled = BigInteger.Abs(weighted[i].Weight.Numerator) * (denominator / weighted[i].Weight.Denominator);
            weights[i] = (long)scaled;
        }
        return weights;
    }

    private static BigInteger Lcm(BigInteger a, BigInteger b)
        => b.IsZero ? a : a / BigInteger.GreatestCommonDivisor(a, b) * b;

    // ---- Helpers ----

    private static Rational Membership(int row, int normalizationRowIndex, Formula[] formulas, IReadOnlyDictionary<string, bool> world)
    {
        if (row == normalizationRowIndex)
        {
            return Rational.One; // every world contributes to the total mass
        }
        return Evaluator.Evaluate(formulas[row], world) ? Rational.One : Rational.Zero;
    }

    /// <summary>At most one fresh world is added per round; bounded by the total worlds.</summary>
    private static IEnumerable<int> Rounds(int atomCount, int lpRowCount)
    {
        var max = (atomCount < 20 ? (1L << atomCount) : long.MaxValue) + lpRowCount + 2;
        for (long i = 0; i < max; i++)
        {
            yield return 0;
        }
    }

    private static bool AddWorld(
        List<IReadOnlyDictionary<string, bool>> worlds,
        HashSet<string> seen,
        IReadOnlyList<string> atoms,
        IReadOnlyDictionary<string, bool> world)
    {
        if (!seen.Add(Key(atoms, world)))
        {
            return false;
        }
        worlds.Add(world);
        return true;
    }

    private static string Key(IReadOnlyList<string> atoms, IReadOnlyDictionary<string, bool> world)
        => string.Create(atoms.Count, (atoms, world), static (span, s) =>
        {
            for (var i = 0; i < s.atoms.Count; i++)
            {
                span[i] = s.world.TryGetValue(s.atoms[i], out var v) && v ? '1' : '0';
            }
        });

    private static Dictionary<string, bool> AllFalse(IReadOnlyList<string> atoms)
    {
        var w = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var atom in atoms)
        {
            w[atom] = false;
        }
        return w;
    }

}
