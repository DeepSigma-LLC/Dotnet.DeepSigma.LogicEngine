using DeepSigma.LogicEngine.Smt;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;
using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3.Translation;

/// <summary>
/// Translates an <see cref="SmtFormula"/> into a Z3 boolean expression under a per-theory
/// <b>sort policy</b>: EUF terms live in one uninterpreted sort; arithmetic variables are
/// <c>Real</c> (or <c>Int</c> for the named integer variables, coerced into the Real sum so the
/// linear arithmetic is uniform but the model is integral and <em>unbounded</em>). For the
/// combined theory the term sort is <c>Real</c>, so a name used both as an arithmetic variable
/// and an EUF constant becomes one shared Z3 constant — the Nelson–Oppen interface, for free.
/// </summary>
internal sealed class SmtToZ3
{
    private readonly MZ3.Context _ctx;
    private readonly MZ3.Sort _termSort;
    private readonly HashSet<string> _integerVariables;
    private readonly Dictionary<string, MZ3.Expr> _consts = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Symbol, int Arity), MZ3.FuncDecl> _funcs = new();

    public SmtToZ3(MZ3.Context ctx, MZ3.Sort termSort, IReadOnlyCollection<string>? integerVariables)
    {
        _ctx = ctx;
        _termSort = termSort;
        _integerVariables = integerVariables is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(integerVariables, StringComparer.Ordinal);
    }

    /// <summary>The constants introduced, by name — for reading back a model.</summary>
    public IReadOnlyDictionary<string, MZ3.Expr> Constants => _consts;

    public MZ3.BoolExpr Translate(SmtFormula formula) => formula switch
    {
        SmtBool b => _ctx.MkBool(b.Value),
        SmtNot n => _ctx.MkNot(Translate(n.Operand)),
        SmtAnd a => _ctx.MkAnd(Translate(a.Left), Translate(a.Right)),
        SmtOr o => _ctx.MkOr(Translate(o.Left), Translate(o.Right)),
        SmtImplies i => _ctx.MkImplies(Translate(i.Antecedent), Translate(i.Consequent)),
        SmtIff bi => _ctx.MkIff(Translate(bi.Left), Translate(bi.Right)),
        EqualityAtom e => _ctx.MkEq(TranslateTerm(e.Left), TranslateTerm(e.Right)),
        PredicateAtom p => Predicate(p),
        LinearConstraintAtom lc => Linear(lc),
        _ => throw new NotSupportedException($"Unsupported SMT node: {formula.GetType().Name}"),
    };

    private MZ3.BoolExpr Predicate(PredicateAtom predicate)
    {
        if (predicate.Arguments.Count == 0)
        {
            return (MZ3.BoolExpr)GetOrAdd(predicate.Symbol, () => _ctx.MkBoolConst(predicate.Symbol));
        }
        var decl = Func(predicate.Symbol, predicate.Arguments.Count, _ctx.BoolSort);
        return (MZ3.BoolExpr)_ctx.MkApp(decl, predicate.Arguments.Select(TranslateTerm).ToArray());
    }

    private MZ3.Expr TranslateTerm(Term term)
    {
        if (term.Arguments.Count == 0)
        {
            return GetOrAdd(term.Symbol, () => _ctx.MkConst(term.Symbol, _termSort));
        }
        var decl = Func(term.Symbol, term.Arguments.Count, _termSort);
        return _ctx.MkApp(decl, term.Arguments.Select(TranslateTerm).ToArray());
    }

    private MZ3.BoolExpr Linear(LinearConstraintAtom atom)
    {
        var summands = new List<MZ3.ArithExpr>(atom.Terms.Count);
        foreach (var term in atom.Terms)
        {
            summands.Add((MZ3.ArithExpr)_ctx.MkMul(Numeral(term.Coefficient), ArithVar(term.Variable)));
        }
        MZ3.ArithExpr lhs = summands.Count == 0 ? _ctx.MkReal(0) : _ctx.MkAdd(summands.ToArray());
        MZ3.ArithExpr rhs = Numeral(atom.Constant);
        return atom.Relation switch
        {
            LinearRelation.LessOrEqual => _ctx.MkLe(lhs, rhs),
            LinearRelation.Less => _ctx.MkLt(lhs, rhs),
            LinearRelation.GreaterOrEqual => _ctx.MkGe(lhs, rhs),
            LinearRelation.Greater => _ctx.MkGt(lhs, rhs),
            LinearRelation.Equal => _ctx.MkEq(lhs, rhs),
            _ => throw new NotSupportedException($"Unknown relation: {atom.Relation}"),
        };
    }

    private MZ3.RatNum Numeral(Rational r) => (MZ3.RatNum)_ctx.MkReal($"{r.Numerator}/{r.Denominator}");

    private MZ3.ArithExpr ArithVar(string name)
    {
        if (_integerVariables.Contains(name))
        {
            var integer = (MZ3.IntExpr)GetOrAdd(name, () => _ctx.MkIntConst(name));
            return _ctx.MkInt2Real(integer);
        }
        return (MZ3.ArithExpr)GetOrAdd(name, () => _ctx.MkRealConst(name));
    }

    private MZ3.Expr GetOrAdd(string name, Func<MZ3.Expr> make)
    {
        if (!_consts.TryGetValue(name, out var c))
        {
            c = make();
            _consts[name] = c;
        }
        return c;
    }

    private MZ3.FuncDecl Func(string symbol, int arity, MZ3.Sort range)
    {
        var key = (symbol, arity);
        if (!_funcs.TryGetValue(key, out var decl))
        {
            var domain = new MZ3.Sort[arity];
            Array.Fill(domain, _termSort);
            decl = _ctx.MkFuncDecl(symbol, domain, range);
            _funcs[key] = decl;
        }
        return decl;
    }
}
