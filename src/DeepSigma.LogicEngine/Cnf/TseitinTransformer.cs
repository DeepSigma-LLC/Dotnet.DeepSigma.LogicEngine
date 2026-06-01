using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// Tseitin transformation. Produces an equisatisfiable CNF whose size is
/// linear in the input. Auxiliary variables are introduced with a prefix
/// chosen so as not to clash with any free variable of the input.
///
/// <para>
/// Turning a formula into CNF by the naive method (distributing <c>∨</c> over
/// <c>∧</c>) can blow the size up exponentially. Tseitin avoids that: it gives
/// each subformula its own fresh variable and adds a few small clauses stating
/// that the variable is <em>equivalent</em> to that subformula (e.g. for
/// <c>g ⇔ (a ∧ b)</c> it emits the clauses encoding both directions). The whole
/// formula then reduces to "the top variable is true" plus those definitions —
/// linear in the input size. The result is <b>equisatisfiable</b> rather than
/// logically equivalent: it has exactly the same satisfiable/unsatisfiable
/// verdict, and any model restricted to the original variables is a model of the
/// input — the auxiliary variables are the only difference. Use this when you only
/// care about satisfiability; use <see cref="CnfTransformer"/> when you need a
/// logically equivalent CNF.
/// </para>
/// </summary>
public static class TseitinTransformer
{
    /// <summary>Produce an equisatisfiable CNF whose size is linear in the input.</summary>
    public static CnfFormula ToCnf(Formula formula)
    {
        var simplified = Simplifier.Simplify(formula);
        if (simplified is BoolConst bc)
        {
            return bc.Value ? CnfFormula.True : CnfFormula.False;
        }

        var existing = Evaluator.Variables(simplified);
        var prefix = FreshPrefix(existing);

        var state = new TseitinState(prefix);
        var rootLit = state.Encode(simplified);

        // Assert the root is true.
        state.Clauses.Add(Clause.Of(rootLit));
        return new CnfFormula(state.Clauses);
    }

    private static string FreshPrefix(IReadOnlySet<string> existing)
    {
        var prefix = "__t";
        while (existing.Any(n => n.StartsWith(prefix, StringComparison.Ordinal)))
        {
            prefix += "_";
        }
        return prefix;
    }

    private sealed class TseitinState
    {
        private readonly string _prefix;
        private int _next;
        private readonly Dictionary<Formula, Literal> _cache = new();

        public List<Clause> Clauses { get; } = new();

        public TseitinState(string prefix) => _prefix = prefix;

        private string FreshName() => _prefix + _next++;

        public Literal Encode(Formula formula)
        {
            switch (formula)
            {
                case Variable v:
                    return Literal.Positive(v.Name);
                case Negation { Operand: Variable nv }:
                    return Literal.Negative(nv.Name);
            }

            if (_cache.TryGetValue(formula, out var cached))
            {
                return cached;
            }

            var aux = Literal.Positive(FreshName());

            switch (formula)
            {
                case Negation n:
                {
                    var a = Encode(n.Operand);
                    // aux ↔ ¬a
                    Clauses.Add(Clause.Of(aux.Negate(), a.Negate()));
                    Clauses.Add(Clause.Of(aux, a));
                    break;
                }
                case Conjunction c:
                {
                    var a = Encode(c.Left);
                    var b = Encode(c.Right);
                    // aux ↔ (a ∧ b)
                    Clauses.Add(Clause.Of(aux.Negate(), a));
                    Clauses.Add(Clause.Of(aux.Negate(), b));
                    Clauses.Add(Clause.Of(aux, a.Negate(), b.Negate()));
                    break;
                }
                case Disjunction d:
                {
                    var a = Encode(d.Left);
                    var b = Encode(d.Right);
                    // aux ↔ (a ∨ b)
                    Clauses.Add(Clause.Of(aux.Negate(), a, b));
                    Clauses.Add(Clause.Of(aux, a.Negate()));
                    Clauses.Add(Clause.Of(aux, b.Negate()));
                    break;
                }
                case Implication imp:
                {
                    var a = Encode(imp.Antecedent);
                    var b = Encode(imp.Consequent);
                    // aux ↔ (¬a ∨ b)
                    Clauses.Add(Clause.Of(aux.Negate(), a.Negate(), b));
                    Clauses.Add(Clause.Of(aux, a));
                    Clauses.Add(Clause.Of(aux, b.Negate()));
                    break;
                }
                case Biconditional bi:
                {
                    var a = Encode(bi.Left);
                    var b = Encode(bi.Right);
                    // aux ↔ (a ↔ b)
                    Clauses.Add(Clause.Of(aux.Negate(), a.Negate(), b));
                    Clauses.Add(Clause.Of(aux.Negate(), a, b.Negate()));
                    Clauses.Add(Clause.Of(aux, a.Negate(), b.Negate()));
                    Clauses.Add(Clause.Of(aux, a, b));
                    break;
                }
                case BoolConst k:
                    if (k.Value)
                    {
                        Clauses.Add(Clause.Of(aux));
                    }
                    else
                    {
                        Clauses.Add(Clause.Of(aux.Negate()));
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown node: {formula.GetType().Name}");
            }

            _cache[formula] = aux;
            return aux;
        }
    }
}
