using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>
/// Resolution refutation prover using the given-clause loop with two standard
/// refinements:
/// <list type="bullet">
/// <item><b>Set of support.</b> Clauses are split into a satisfiable "usable"
/// set (typically the knowledge base) and a "support" set (typically the negated
/// goal). Every inference must involve a support clause or a descendant of one,
/// which focuses search toward the goal and preserves refutation completeness
/// whenever the usable set is itself satisfiable.</item>
/// <item><b>Subsumption.</b> A clause that is a subset of another makes the
/// larger one redundant. New resolvents subsumed by an existing clause are
/// discarded (forward), and existing clauses subsumed by a new one are
/// retired (backward).</item>
/// </list>
/// Bounded by <paramref name="maxClauses"/> to keep runtime finite on hard
/// instances.
/// </summary>
public static class ResolutionRefuter
{
    public const int DefaultMaxClauses = 50_000;

    /// <summary>Refute a CNF formula, treating every clause as part of the support set.</summary>
    public static ResolutionResult Refute(CnfFormula cnf, int maxClauses = DefaultMaxClauses)
        => Refute(usable: CnfFormula.True, support: cnf, maxClauses);

    /// <summary>
    /// Refute <paramref name="usable"/> ∧ <paramref name="support"/> with the
    /// set-of-support strategy seeded from <paramref name="support"/>.
    /// </summary>
    public static ResolutionResult Refute(CnfFormula usable, CnfFormula support, int maxClauses = DefaultMaxClauses)
        => new Engine(maxClauses).Run(usable.Clauses, support.Clauses);

    /// <summary>
    /// Prove KB ⊨ query by refuting KB ∧ ¬query. The knowledge base forms the
    /// usable set and the negated query forms the support set. Classical CNF is
    /// used (no auxiliary variables) so proofs read in the original vocabulary.
    /// </summary>
    public static ResolutionResult Refute(IEnumerable<Formula> knowledgeBase, Formula query, int maxClauses = DefaultMaxClauses)
    {
        var usable = CnfTransformer.ToCnf(Formula.All(knowledgeBase));
        var support = CnfTransformer.ToCnf(new Negation(query));
        return Refute(usable, support, maxClauses);
    }

    /// <summary>
    /// Holds the working state for a single refutation. All derived clauses are
    /// recorded as <see cref="ResolutionStep"/>s for proof reconstruction; the
    /// <c>_active</c> flags track which clauses are still live after subsumption.
    /// </summary>
    private sealed class Engine
    {
        private readonly int _maxClauses;
        private readonly List<ResolutionStep> _steps = new();
        private readonly Dictionary<Clause, int> _index = new();
        private readonly List<bool> _active = new();
        private readonly List<int> _processed = new();
        private readonly List<int> _support = new();

        public Engine(int maxClauses) => _maxClauses = maxClauses;

        public ResolutionResult Run(IReadOnlyList<Clause> usable, IReadOnlyList<Clause> support)
        {
            foreach (var clause in usable)
            {
                var added = AddClause(clause, null, null, null, _processed);
                if (added is { } step && step.Resolvent.IsEmpty)
                {
                    return Refuted(step);
                }
            }
            foreach (var clause in support)
            {
                var added = AddClause(clause, null, null, null, _support);
                if (added is { } step && step.Resolvent.IsEmpty)
                {
                    return Refuted(step);
                }
            }

            return GivenClauseLoop();
        }

        private ResolutionResult GivenClauseLoop()
        {
            while (TryPickGiven(out var givenIndex))
            {
                _processed.Add(givenIndex);
                var given = _steps[givenIndex].Resolvent;

                // Snapshot the processed set: resolving against clauses added
                // later in this same iteration is unnecessary and avoids churn.
                var partners = _processed.ToArray();
                foreach (var partnerIndex in partners)
                {
                    if (partnerIndex == givenIndex || !_active[partnerIndex])
                    {
                        continue;
                    }
                    var result = ResolveAll(given, givenIndex, partnerIndex);
                    if (result is not null)
                    {
                        return result;
                    }
                    if (_steps.Count > _maxClauses)
                    {
                        return new ResolutionResult(false, null, $"Exceeded {_maxClauses} clauses without refutation.");
                    }
                }
            }
            return new ResolutionResult(false, null, "Saturated without producing the empty clause (formula is satisfiable).");
        }

        /// <summary>Resolve the given clause against a partner on every shared pivot.</summary>
        private ResolutionResult? ResolveAll(Clause given, int givenIndex, int partnerIndex)
        {
            var partner = _steps[partnerIndex].Resolvent;
            foreach (var pivot in PivotCandidates(given, partner))
            {
                var resolvent = Resolve(given, partner, pivot);
                if (resolvent.IsTautology() || _index.ContainsKey(resolvent) || IsSubsumed(resolvent))
                {
                    continue;
                }
                var step = AddClause(resolvent, givenIndex, partnerIndex, pivot, _support)!;
                RetireClausesSubsumedBy(resolvent, step.Index);
                if (resolvent.IsEmpty)
                {
                    return Refuted(step);
                }
            }
            return null;
        }

        /// <summary>Pick the shortest active support clause (a strong unit-first heuristic).</summary>
        private bool TryPickGiven(out int givenIndex)
        {
            givenIndex = -1;
            var bestLength = int.MaxValue;
            var bestSlot = -1;
            for (var slot = 0; slot < _support.Count; slot++)
            {
                var idx = _support[slot];
                if (!_active[idx])
                {
                    continue;
                }
                var length = _steps[idx].Resolvent.Literals.Count;
                if (length < bestLength)
                {
                    bestLength = length;
                    bestSlot = slot;
                    givenIndex = idx;
                }
            }
            if (bestSlot < 0)
            {
                return false;
            }
            _support.RemoveAt(bestSlot);
            return true;
        }

        private ResolutionStep? AddClause(Clause clause, int? left, int? right, string? pivot, List<int> bucket)
        {
            if (clause.IsTautology() || _index.ContainsKey(clause))
            {
                return null;
            }
            var step = new ResolutionStep(_steps.Count, clause, left, right, pivot);
            _index[clause] = step.Index;
            _steps.Add(step);
            _active.Add(true);
            bucket.Add(step.Index);
            return step;
        }

        private bool IsSubsumed(Clause candidate)
        {
            for (var i = 0; i < _steps.Count; i++)
            {
                if (_active[i] && Subsumes(_steps[i].Resolvent, candidate))
                {
                    return true;
                }
            }
            return false;
        }

        private void RetireClausesSubsumedBy(Clause clause, int exceptIndex)
        {
            for (var i = 0; i < _steps.Count; i++)
            {
                if (i != exceptIndex && _active[i] && Subsumes(clause, _steps[i].Resolvent))
                {
                    _active[i] = false;
                }
            }
        }

        private ResolutionResult Refuted(ResolutionStep emptyClause)
            => new(true, new ResolutionProof(_steps, emptyClause.Index));

        /// <summary>True if every literal of <paramref name="subset"/> appears in <paramref name="superset"/>.</summary>
        private static bool Subsumes(Clause subset, Clause superset)
        {
            if (subset.Literals.Count > superset.Literals.Count)
            {
                return false;
            }
            foreach (var literal in subset.Literals)
            {
                if (!superset.Literals.Contains(literal))
                {
                    return false;
                }
            }
            return true;
        }

        private static IEnumerable<string> PivotCandidates(Clause a, Clause b)
        {
            foreach (var lit in a.Literals)
            {
                var opposite = lit.Negate();
                foreach (var other in b.Literals)
                {
                    if (other.Equals(opposite))
                    {
                        yield return lit.Variable;
                        break;
                    }
                }
            }
        }

        private static Clause Resolve(Clause a, Clause b, string pivot)
        {
            var literals = new List<Literal>(a.Literals.Count + b.Literals.Count);
            foreach (var lit in a.Literals)
            {
                if (lit.Variable != pivot)
                {
                    literals.Add(lit);
                }
            }
            foreach (var lit in b.Literals)
            {
                if (lit.Variable != pivot)
                {
                    literals.Add(lit);
                }
            }
            return new Clause(literals);
        }
    }
}
