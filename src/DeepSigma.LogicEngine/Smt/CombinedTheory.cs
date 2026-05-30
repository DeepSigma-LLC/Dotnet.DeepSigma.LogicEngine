using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A Nelson–Oppen combination of the EUF and LRA theories. Both are stably infinite
/// and convex over the reals, so combination reduces to <b>equality propagation</b>:
/// run each theory on its own atoms, and whenever one theory entails an equality
/// between variables shared with the other (a name that occurs both as an arithmetic
/// variable and as an EUF constant), assert that equality in the other theory.
/// Repeat to a fixpoint; a conflict in either theory means the conjunction is
/// unsatisfiable. This decides formulas neither theory can settle alone, e.g.
/// <c>x ≤ y ∧ y ≤ x ∧ f(x) ≠ f(y)</c>.
/// </summary>
internal sealed class CombinedTheory : ITheory
{
    public IReadOnlySet<int>? Check(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
        => ConflictMinimizer.Minimize(asserted, RawCheck);

    private IReadOnlySet<int>? RawCheck(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var lra = new List<(LinearConstraintAtom Atom, bool Value)>();
        var euf = new List<(SmtFormula Atom, bool Value)>();
        foreach (var (atom, value) in asserted)
        {
            switch (atom)
            {
                case LinearConstraintAtom lc: lra.Add((lc, value)); break;
                case EqualityAtom or PredicateAtom: euf.Add((atom, value)); break;
                default: throw new ArgumentException($"Not an EUF/LRA atom: {atom}");
            }
        }

        var shared = SharedVariables(lra, euf);
        var equalities = new HashSet<(string, string)>();
        var all = Enumerable.Range(0, asserted.Count).ToHashSet();

        while (true)
        {
            var lraConstraints = BuildLra(lra, equalities);
            if (HasConflict(lraConstraints))
            {
                return all; // coarse but sound conflict core
            }

            var closure = BuildEuf(euf, equalities);
            if (closure.FindConflict() is not null)
            {
                return all;
            }

            var discovered = false;
            for (var i = 0; i < shared.Count; i++)
            {
                for (var j = i + 1; j < shared.Count; j++)
                {
                    var pair = Order(shared[i], shared[j]);
                    if (equalities.Contains(pair))
                    {
                        continue;
                    }
                    if (closure.AreEqual(Term.Constant(pair.Item1), Term.Constant(pair.Item2))
                        || LraEntailsEqual(lraConstraints, pair.Item1, pair.Item2))
                    {
                        equalities.Add(pair);
                        discovered = true;
                    }
                }
            }

            if (!discovered)
            {
                return null; // fixpoint with no conflict ⇒ consistent
            }
        }
    }

    private static IReadOnlyList<string> SharedVariables(
        IReadOnlyList<(LinearConstraintAtom Atom, bool Value)> lra,
        IReadOnlyList<(SmtFormula Atom, bool Value)> euf)
    {
        var arithmetic = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (atom, _) in lra)
        {
            foreach (var term in atom.Terms)
            {
                arithmetic.Add(term.Variable);
            }
        }

        var constants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (atom, _) in euf)
        {
            switch (atom)
            {
                case EqualityAtom e: CollectConstants(e.Left, constants); CollectConstants(e.Right, constants); break;
                case PredicateAtom p: foreach (var a in p.Arguments) { CollectConstants(a, constants); } break;
            }
        }

        arithmetic.IntersectWith(constants);
        return arithmetic.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static void CollectConstants(Term term, HashSet<string> constants)
    {
        if (term.Arguments.Count == 0)
        {
            constants.Add(term.Symbol);
        }
        else
        {
            foreach (var a in term.Arguments)
            {
                CollectConstants(a, constants);
            }
        }
    }

    private static List<(IReadOnlyList<(string Variable, Rational Coefficient)> Terms, LinearRelation Relation, Rational Constant)> BuildLra(
        IReadOnlyList<(LinearConstraintAtom Atom, bool Value)> lra,
        HashSet<(string, string)> equalities)
    {
        var constraints = new List<(IReadOnlyList<(string, Rational)>, LinearRelation, Rational)>();
        foreach (var (atom, value) in lra)
        {
            var relation = value ? atom.Relation : atom.Relation.Negate();
            var terms = atom.Terms.Select(t => (t.Variable, t.Coefficient)).ToArray();
            constraints.Add((terms, relation, atom.Constant));
        }
        foreach (var (x, y) in equalities)
        {
            constraints.Add((new[] { (x, Rational.One), (y, -Rational.One) }, LinearRelation.Equal, Rational.Zero));
        }
        return constraints;
    }

    private static bool HasConflict(IReadOnlyList<(IReadOnlyList<(string Variable, Rational Coefficient)> Terms, LinearRelation Relation, Rational Constant)> constraints)
        => Solve(constraints, extra: null) is not null;

    private static bool LraEntailsEqual(
        IReadOnlyList<(IReadOnlyList<(string Variable, Rational Coefficient)> Terms, LinearRelation Relation, Rational Constant)> constraints,
        string x, string y)
    {
        var difference = new[] { (x, Rational.One), (y, -Rational.One) };
        // x = y is entailed iff both x < y and x > y are infeasible against the system.
        var below = Solve(constraints, (difference, LinearRelation.Less, Rational.Zero)) is not null;
        var above = Solve(constraints, (difference, LinearRelation.Greater, Rational.Zero)) is not null;
        return below && above;
    }

    private static IReadOnlySet<int>? Solve(
        IReadOnlyList<(IReadOnlyList<(string Variable, Rational Coefficient)> Terms, LinearRelation Relation, Rational Constant)> constraints,
        (IReadOnlyList<(string Variable, Rational Coefficient)> Terms, LinearRelation Relation, Rational Constant)? extra)
    {
        var solver = new ExactLinearFeasibilitySolver();
        var tag = 0;
        foreach (var (terms, relation, constant) in constraints)
        {
            solver.AddConstraint(tag++, terms, relation, constant);
        }
        if (extra is { } e)
        {
            solver.AddConstraint(tag, e.Terms, e.Relation, e.Constant);
        }
        return solver.FindConflict();
    }

    private static CongruenceClosure BuildEuf(IReadOnlyList<(SmtFormula Atom, bool Value)> euf, HashSet<(string, string)> equalities)
    {
        var closure = new CongruenceClosure();
        for (var i = 0; i < euf.Count; i++)
        {
            closure.Assert(ToLiteral(euf[i].Atom, euf[i].Value, i));
        }
        var tag = euf.Count;
        foreach (var (x, y) in equalities)
        {
            closure.Assert(new EufLiteral(tag++, true, EufAtomKind.Equality, Term.Constant(x), Term.Constant(y)));
        }
        return closure;
    }

    private static EufLiteral ToLiteral(SmtFormula atom, bool positive, int atomId) => atom switch
    {
        EqualityAtom e => new EufLiteral(atomId, positive, EufAtomKind.Equality, e.Left, e.Right),
        PredicateAtom p => new EufLiteral(atomId, positive, EufAtomKind.Predicate,
            new Term(p.Symbol, p.Arguments), new Term(p.Symbol, p.Arguments)),
        _ => throw new ArgumentException($"Not an EUF theory atom: {atom}"),
    };

    private static (string, string) Order(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
