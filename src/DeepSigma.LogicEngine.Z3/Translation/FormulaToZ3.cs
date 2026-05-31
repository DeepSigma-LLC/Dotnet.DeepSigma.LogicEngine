using DeepSigma.LogicEngine.Formulas;
using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3.Translation;

/// <summary>Translates a propositional <see cref="Formula"/> into a Z3 boolean expression.</summary>
internal sealed class FormulaToZ3
{
    private readonly MZ3.Context _ctx;
    private readonly Dictionary<string, MZ3.BoolExpr> _vars = new(StringComparer.Ordinal);

    public FormulaToZ3(MZ3.Context ctx) => _ctx = ctx;

    /// <summary>The boolean constants introduced, by variable name — for reading back a model.</summary>
    public IReadOnlyDictionary<string, MZ3.Expr> Constants
        => _vars.ToDictionary(kv => kv.Key, kv => (MZ3.Expr)kv.Value, StringComparer.Ordinal);

    public MZ3.BoolExpr Translate(Formula formula) => formula switch
    {
        BoolConst c => _ctx.MkBool(c.Value),
        Variable v => Var(v.Name),
        Negation n => _ctx.MkNot(Translate(n.Operand)),
        Conjunction a => _ctx.MkAnd(Translate(a.Left), Translate(a.Right)),
        Disjunction o => _ctx.MkOr(Translate(o.Left), Translate(o.Right)),
        Implication i => _ctx.MkImplies(Translate(i.Antecedent), Translate(i.Consequent)),
        Biconditional b => _ctx.MkIff(Translate(b.Left), Translate(b.Right)),
        _ => throw new NotSupportedException($"Unsupported formula node: {formula.GetType().Name}"),
    };

    private MZ3.BoolExpr Var(string name)
    {
        if (!_vars.TryGetValue(name, out var c))
        {
            c = _ctx.MkBoolConst(name);
            _vars[name] = c;
        }
        return c;
    }
}
