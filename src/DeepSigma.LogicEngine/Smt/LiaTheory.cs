using System.Numerics;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Linear integer arithmetic as a DPLL(T) theory, by branch-and-bound over the
/// exact-rational LRA relaxation. A conjunction of linear atoms is consistent in
/// LIA iff it has an integer solution with every integer variable inside the box
/// [−Bound, Bound]: solve the rational relaxation; if a variable that must be
/// integral takes a non-integral (or strict-boundary) value, branch on
/// <c>x ≤ ⌊v⌋</c> ∨ <c>x ≥ ⌊v⌋+1</c> and recurse. The finite box guarantees
/// termination; the relaxation pre-check still settles many cases definitely
/// (relaxation infeasible ⇒ UNSAT regardless of the box).
/// </summary>
internal sealed class LiaTheory : ITheory
{
    private readonly IReadOnlySet<string> _integerVariables;
    private readonly Rational _bound;

    /// <summary>The integer assignment from the most recent consistent check (for model extraction).</summary>
    public IReadOnlyDictionary<string, BigInteger>? LastModel { get; private set; }

    public LiaTheory(IReadOnlyCollection<string> integerVariables, int bound)
    {
        _integerVariables = new HashSet<string>(integerVariables, StringComparer.Ordinal);
        _bound = Rational.Of(bound);
    }

    public IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var constraints = new List<Constraint>(asserted.Count);
        for (var i = 0; i < asserted.Count; i++)
        {
            if (asserted[i].Atom is not LinearConstraintAtom atom)
            {
                throw new ArgumentException($"Not an arithmetic theory atom: {asserted[i].Atom}");
            }
            var relation = asserted[i].Value ? atom.Relation : Negate(atom.Relation);
            var terms = atom.Terms.Select(t => (t.Variable, t.Coefficient)).ToArray();
            constraints.Add(new Constraint(i, terms, relation, atom.Constant));
        }

        // Pre-check the rational relaxation (no box): an infeasible relaxation is a
        // definite, minimal conflict over the asserted atoms.
        var relaxationConflict = Solve(constraints, branches: Array.Empty<Constraint>(), withBox: false, out _);
        if (relaxationConflict is not null)
        {
            return relaxationConflict;
        }

        if (BranchAndBound(constraints, new List<Constraint>(), out var model))
        {
            LastModel = model;
            return null;
        }

        // Feasible over the reals but no integer point in the box: the whole asserted
        // set is the conflict (a coarse but sound core).
        return Enumerable.Range(0, asserted.Count).ToHashSet();
    }

    private bool BranchAndBound(List<Constraint> baseConstraints, List<Constraint> branches, out Dictionary<string, BigInteger> model)
    {
        model = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
        var conflict = Solve(baseConstraints, branches, withBox: true, out var assignment);
        if (conflict is not null || assignment is null)
        {
            return false; // this node is integer-infeasible
        }

        foreach (var variable in _integerVariables)
        {
            var value = assignment.TryGetValue(variable, out var v) ? v : DeltaRational.Zero;
            if (IsExactInteger(value))
            {
                continue;
            }

            var floor = Floor(value);
            var low = new Constraint(-1, new[] { (variable, Rational.One) }, LinearRelation.LessOrEqual, Rational.Of(floor));
            var high = new Constraint(-1, new[] { (variable, Rational.One) }, LinearRelation.GreaterOrEqual, Rational.Of(floor + BigInteger.One));

            branches.Add(low);
            if (BranchAndBound(baseConstraints, branches, out model))
            {
                return true;
            }
            branches[^1] = high;
            var found = BranchAndBound(baseConstraints, branches, out model);
            branches.RemoveAt(branches.Count - 1);
            return found;
        }

        // Every integer variable is exactly integral: read off the integer solution.
        foreach (var variable in _integerVariables)
        {
            var value = assignment.TryGetValue(variable, out var v) ? v : DeltaRational.Zero;
            model[variable] = value.Value.Numerator; // denominator is 1 here
        }
        return true;
    }

    private IReadOnlySet<int>? Solve(
        IReadOnlyList<Constraint> baseConstraints,
        IReadOnlyList<Constraint> branches,
        bool withBox,
        out IReadOnlyDictionary<string, DeltaRational>? assignment)
    {
        var solver = new ExactLinearFeasibilitySolver();
        foreach (var c in baseConstraints)
        {
            solver.AddConstraint(c.Tag, c.Terms, c.Relation, c.Constant);
        }
        foreach (var c in branches)
        {
            solver.AddConstraint(c.Tag, c.Terms, c.Relation, c.Constant);
        }
        if (withBox)
        {
            foreach (var variable in _integerVariables)
            {
                solver.AddConstraint(-1, new[] { (variable, Rational.One) }, LinearRelation.LessOrEqual, _bound);
                solver.AddConstraint(-1, new[] { (variable, Rational.One) }, LinearRelation.GreaterOrEqual, -_bound);
            }
        }

        var conflict = solver.FindConflict();
        assignment = conflict is null ? solver.Assignment() : null;
        return conflict;
    }

    private static bool IsExactInteger(DeltaRational value)
        => value.Delta.IsZero && value.Value.Denominator == BigInteger.One;

    /// <summary>The integer to branch at: floor of the rational part, nudged for a strict boundary.</summary>
    private static BigInteger Floor(DeltaRational value)
    {
        var v = value.Value;
        var quotient = BigInteger.DivRem(v.Numerator, v.Denominator, out var remainder);
        if (remainder.Sign < 0)
        {
            quotient -= BigInteger.One; // floor toward −∞ for negative non-integers
        }
        if (remainder.IsZero && value.Delta.Sign < 0)
        {
            quotient -= BigInteger.One; // value is just below an integer ⇒ floor to the next integer down
        }
        return quotient;
    }

    private static LinearRelation Negate(LinearRelation relation) => relation switch
    {
        LinearRelation.LessOrEqual => LinearRelation.Greater,
        LinearRelation.Less => LinearRelation.GreaterOrEqual,
        LinearRelation.GreaterOrEqual => LinearRelation.Less,
        LinearRelation.Greater => LinearRelation.LessOrEqual,
        _ => throw new ArgumentException("Equality atoms are split before reaching the theory."),
    };

    private readonly record struct Constraint(
        int Tag,
        IReadOnlyList<(string Variable, Rational Coefficient)> Terms,
        LinearRelation Relation,
        Rational Constant);
}
