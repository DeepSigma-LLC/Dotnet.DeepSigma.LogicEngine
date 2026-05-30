using DeepSigma.LogicEngine.Smt;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Fuzzy;

/// <summary>
/// Decision procedures for many-valued (fuzzy) logic over the Gödel and
/// Łukasiewicz t-norms. A fuzzy formula's truth value is encoded as a real
/// variable constrained by the (piecewise-linear) semantics of each connective,
/// reducing validity and satisfiability questions to <b>linear real arithmetic</b>
/// — solved by the existing <see cref="LraSolver"/> (DPLL(T) + exact simplex).
/// All reasoning is exact.
/// </summary>
public static class FuzzySolver
{
    /// <summary>
    /// True if the formula's value is ≥ <paramref name="threshold"/> (default 1,
    /// i.e. a fuzzy tautology) under every assignment of its variables in [0,1].
    /// </summary>
    public static bool IsValid(FuzzyFormula formula, FuzzyLogic logic, Rational? threshold = null)
    {
        var t = threshold ?? Rational.One;
        var encoder = new Encoder(logic);
        var top = encoder.Encode(formula);
        // Valid iff there is NO assignment with value < threshold.
        var query = new SmtAnd(encoder.Constraints(), Bound(top, LinearRelation.Less, t));
        return !LraSolver.IsSatisfiable(query);
    }

    /// <summary>
    /// True if some assignment gives the formula a value ≥ <paramref name="threshold"/>
    /// (default 1).
    /// </summary>
    public static bool IsSatisfiable(FuzzyFormula formula, FuzzyLogic logic, Rational? threshold = null)
    {
        var t = threshold ?? Rational.One;
        var encoder = new Encoder(logic);
        var top = encoder.Encode(formula);
        var query = new SmtAnd(encoder.Constraints(), Bound(top, LinearRelation.GreaterOrEqual, t));
        return LraSolver.IsSatisfiable(query);
    }

    private static SmtFormula Bound(string variable, LinearRelation relation, Rational constant)
        => new LinearConstraintAtom(new[] { new LinearAtomTerm(variable, Rational.One) }, relation, constant);

    /// <summary>Builds the LRA constraints defining each subformula's value variable.</summary>
    private sealed class Encoder
    {
        private readonly FuzzyLogic _logic;
        private readonly List<SmtFormula> _constraints = new();
        private readonly Dictionary<FuzzyFormula, string> _memo = new();
        private readonly HashSet<string> _variables = new(StringComparer.Ordinal);
        private int _counter;

        public Encoder(FuzzyLogic logic) => _logic = logic;

        /// <summary>The full constraint conjunction, including [0,1] domains for every variable.</summary>
        public SmtFormula Constraints()
        {
            var all = new List<SmtFormula>(_constraints);
            foreach (var v in _variables)
            {
                all.Add(Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One)));
                all.Add(Lin(LinearRelation.LessOrEqual, Rational.One, (v, Rational.One)));
            }
            return SmtFormula.All(all);
        }

        public string Encode(FuzzyFormula f)
        {
            if (_memo.TryGetValue(f, out var existing))
            {
                return existing;
            }

            string result;
            switch (f)
            {
                case FuzzyVar v:
                    result = "fa_" + v.Name;
                    _variables.Add(result);
                    return _memo[f] = result; // no defining constraint; it is a free value
                case FuzzyConst c:
                    result = Fresh();
                    Define(result, c.Value); // v = c
                    break;
                case FuzzyNot n:
                    result = Fresh();
                    var nx = Encode(n.Operand);
                    // v = 1 - x  ⇔  v + x = 1
                    Equal(Rational.One, (result, Rational.One), (nx, Rational.One));
                    break;
                case FuzzyAnd a:
                    result = EncodeAnd(Encode(a.Left), Encode(a.Right));
                    break;
                case FuzzyOr o:
                    result = EncodeOr(Encode(o.Left), Encode(o.Right));
                    break;
                case FuzzyImplies i:
                    result = EncodeImplies(Encode(i.Left), Encode(i.Right));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown fuzzy node: {f.GetType().Name}");
            }
            return _memo[f] = result;
        }

        private string EncodeAnd(string x, string y)
        {
            var v = Fresh();
            if (_logic == FuzzyLogic.Godel)
            {
                // v = min(x, y): v ≤ x, v ≤ y, (v ≥ x ∨ v ≥ y)
                _constraints.Add(Lin(LinearRelation.LessOrEqual, Rational.Zero, (v, Rational.One), (x, -Rational.One)));
                _constraints.Add(Lin(LinearRelation.LessOrEqual, Rational.Zero, (v, Rational.One), (y, -Rational.One)));
                _constraints.Add(new SmtOr(
                    Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One), (x, -Rational.One)),
                    Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One), (y, -Rational.One))));
            }
            else
            {
                // v = max(0, x + y - 1): v ≥ 0, v ≥ x+y-1, (v ≤ 0 ∨ v ≤ x+y-1)
                _constraints.Add(Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One)));
                _constraints.Add(Lin(LinearRelation.GreaterOrEqual, -Rational.One, (v, Rational.One), (x, -Rational.One), (y, -Rational.One)));
                _constraints.Add(new SmtOr(
                    Lin(LinearRelation.LessOrEqual, Rational.Zero, (v, Rational.One)),
                    Lin(LinearRelation.LessOrEqual, -Rational.One, (v, Rational.One), (x, -Rational.One), (y, -Rational.One))));
            }
            return v;
        }

        private string EncodeOr(string x, string y)
        {
            var v = Fresh();
            if (_logic == FuzzyLogic.Godel)
            {
                // v = max(x, y): v ≥ x, v ≥ y, (v ≤ x ∨ v ≤ y)
                _constraints.Add(Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One), (x, -Rational.One)));
                _constraints.Add(Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One), (y, -Rational.One)));
                _constraints.Add(new SmtOr(
                    Lin(LinearRelation.LessOrEqual, Rational.Zero, (v, Rational.One), (x, -Rational.One)),
                    Lin(LinearRelation.LessOrEqual, Rational.Zero, (v, Rational.One), (y, -Rational.One))));
            }
            else
            {
                // v = min(1, x + y): v ≤ 1, v ≤ x+y, (v ≥ 1 ∨ v ≥ x+y)
                _constraints.Add(Lin(LinearRelation.LessOrEqual, Rational.One, (v, Rational.One)));
                _constraints.Add(Lin(LinearRelation.LessOrEqual, Rational.Zero, (v, Rational.One), (x, -Rational.One), (y, -Rational.One)));
                _constraints.Add(new SmtOr(
                    Lin(LinearRelation.GreaterOrEqual, Rational.One, (v, Rational.One)),
                    Lin(LinearRelation.GreaterOrEqual, Rational.Zero, (v, Rational.One), (x, -Rational.One), (y, -Rational.One))));
            }
            return v;
        }

        private string EncodeImplies(string x, string y)
        {
            var v = Fresh();
            if (_logic == FuzzyLogic.Godel)
            {
                // v = (x ≤ y ? 1 : y): (x ≤ y ∧ v = 1) ∨ (x > y ∧ v = y)
                var branchTrue = new SmtAnd(
                    Lin(LinearRelation.LessOrEqual, Rational.Zero, (x, Rational.One), (y, -Rational.One)),
                    EqualFormula(Rational.One, (v, Rational.One)));
                var branchElse = new SmtAnd(
                    Lin(LinearRelation.Greater, Rational.Zero, (x, Rational.One), (y, -Rational.One)),
                    EqualFormula(Rational.Zero, (v, Rational.One), (y, -Rational.One))); // v = y
                _constraints.Add(new SmtOr(branchTrue, branchElse));
            }
            else
            {
                // v = min(1, 1 - x + y): v ≤ 1, v ≤ 1-x+y, (v ≥ 1 ∨ v ≥ 1-x+y)
                _constraints.Add(Lin(LinearRelation.LessOrEqual, Rational.One, (v, Rational.One)));
                _constraints.Add(Lin(LinearRelation.LessOrEqual, Rational.One, (v, Rational.One), (x, Rational.One), (y, -Rational.One)));
                _constraints.Add(new SmtOr(
                    Lin(LinearRelation.GreaterOrEqual, Rational.One, (v, Rational.One)),
                    Lin(LinearRelation.GreaterOrEqual, Rational.One, (v, Rational.One), (x, Rational.One), (y, -Rational.One))));
            }
            return v;
        }

        private void Define(string v, Rational value) => Equal(value, (v, Rational.One));

        // Assert (Σ termᵢ) = constant by adding ≤ and ≥.
        private void Equal(Rational constant, params (string Var, Rational Coeff)[] terms)
        {
            _constraints.Add(Lin(LinearRelation.LessOrEqual, constant, terms));
            _constraints.Add(Lin(LinearRelation.GreaterOrEqual, constant, terms));
        }

        private static SmtFormula EqualFormula(Rational constant, params (string Var, Rational Coeff)[] terms)
            => new SmtAnd(Lin(LinearRelation.LessOrEqual, constant, terms), Lin(LinearRelation.GreaterOrEqual, constant, terms));

        private string Fresh()
        {
            var name = "fv_" + _counter++;
            _variables.Add(name);
            return name;
        }

        private static SmtFormula Lin(LinearRelation relation, Rational constant, params (string Var, Rational Coeff)[] terms)
            => new LinearConstraintAtom(terms.Select(t => new LinearAtomTerm(t.Var, t.Coeff)).ToList(), relation, constant);
    }
}
