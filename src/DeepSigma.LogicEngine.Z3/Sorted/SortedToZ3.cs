using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3.Sorted;

/// <summary>Translates a <see cref="SortedExpr"/> into a Z3 expression (boolean, bit-vector, arithmetic, quantifier, and string theories).</summary>
internal sealed class SortedToZ3
{
    private readonly MZ3.Context _ctx;
    private readonly Dictionary<string, MZ3.Expr> _consts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _boundVariableNames = new(StringComparer.Ordinal);

    public SortedToZ3(MZ3.Context ctx) => _ctx = ctx;

    /// <summary>The free constants introduced, by name — for reading back a model (bound variables excluded).</summary>
    public IReadOnlyDictionary<string, MZ3.Expr> Constants
        => _consts.Where(kv => !_boundVariableNames.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    public MZ3.Expr Translate(SortedExpr expr) => expr switch
    {
        BoolLiteral b => _ctx.MkBool(b.Value),
        BitVecLiteral bv => _ctx.MkBV(bv.Value.ToString(), (uint)bv.Width),
        IntLiteral i => _ctx.MkInt(i.Value.ToString()),
        RealLiteral r => _ctx.MkReal($"{r.Value.Numerator}/{r.Value.Denominator}"),
        StringLiteral s => _ctx.MkString(s.Value),
        SortedVar v => Var(v),
        NotExpr n => _ctx.MkNot(Bool(n.Operand)),
        BinaryBool b => Boolean(b),
        EqExpr e => e.Negated ? _ctx.MkNot(_ctx.MkEq(Translate(e.Left), Translate(e.Right))) : _ctx.MkEq(Translate(e.Left), Translate(e.Right)),
        BvCompare c => Compare(c),
        BvBinary b => Binary(b),
        BvUnary u => Unary(u),
        BvConcat c => _ctx.MkConcat(Bv(c.High), Bv(c.Low)),
        BvExtract x => _ctx.MkExtract((uint)x.High, (uint)x.Low, Bv(x.Operand)),
        ArithBinary a => Arithmetic(a),
        ArithNegate n => _ctx.MkUnaryMinus(Arith(n.Operand)),
        ArithCompare c => ArithComparison(c),
        Quantifier q => Quantify(q),
        StrLength l => _ctx.MkLength(Seq(l.Operand)),
        StrConcat c => _ctx.MkConcat(Seq(c.Left), Seq(c.Right)),
        StrPredicate p => StringPredicate(p),
        _ => throw new NotSupportedException($"Unsupported sorted node: {expr.GetType().Name}"),
    };

    private MZ3.BoolExpr StringPredicate(StrPredicate p) => p.Op switch
    {
        StrPredOp.Contains => _ctx.MkContains(Seq(p.Left), Seq(p.Right)),
        StrPredOp.PrefixOf => _ctx.MkPrefixOf(Seq(p.Left), Seq(p.Right)),
        StrPredOp.SuffixOf => _ctx.MkSuffixOf(Seq(p.Left), Seq(p.Right)),
        _ => throw new NotSupportedException($"Unknown string predicate: {p.Op}"),
    };

    private MZ3.BoolExpr Quantify(Quantifier q)
    {
        var bound = new MZ3.Expr[q.BoundVariables.Count];
        for (var i = 0; i < bound.Length; i++)
        {
            bound[i] = Var(q.BoundVariables[i]);
            _boundVariableNames.Add(q.BoundVariables[i].Name);
        }
        var body = Bool(q.Body);
        return q.IsForall ? _ctx.MkForall(bound, body) : _ctx.MkExists(bound, body);
    }

    private MZ3.BoolExpr Bool(SortedExpr e) => (MZ3.BoolExpr)Translate(e);
    private MZ3.BitVecExpr Bv(SortedExpr e) => (MZ3.BitVecExpr)Translate(e);
    private MZ3.ArithExpr Arith(SortedExpr e) => (MZ3.ArithExpr)Translate(e);
    private MZ3.SeqExpr Seq(SortedExpr e) => (MZ3.SeqExpr)Translate(e);

    private MZ3.Expr Var(SortedVar v)
    {
        if (!_consts.TryGetValue(v.Name, out var c))
        {
            c = v.VarSort switch
            {
                BoolSort => _ctx.MkBoolConst(v.Name),
                BitVecSort bv => _ctx.MkBVConst(v.Name, (uint)bv.Width),
                IntSort => _ctx.MkIntConst(v.Name),
                RealSort => _ctx.MkRealConst(v.Name),
                StringSort => _ctx.MkConst(v.Name, _ctx.StringSort),
                _ => throw new NotSupportedException($"Unsupported variable sort: {v.VarSort}"),
            };
            _consts[v.Name] = c;
        }
        return c;
    }

    private MZ3.ArithExpr Arithmetic(ArithBinary a)
    {
        var l = Arith(a.Left);
        var r = Arith(a.Right);
        return a.Op switch
        {
            ArithOp.Add => _ctx.MkAdd(l, r),
            ArithOp.Sub => _ctx.MkSub(l, r),
            ArithOp.Mul => _ctx.MkMul(l, r),
            _ => throw new NotSupportedException($"Unknown arithmetic op: {a.Op}"),
        };
    }

    private MZ3.BoolExpr ArithComparison(ArithCompare c)
    {
        var l = Arith(c.Left);
        var r = Arith(c.Right);
        return c.Op switch
        {
            ArithCmp.Lt => _ctx.MkLt(l, r),
            ArithCmp.Le => _ctx.MkLe(l, r),
            ArithCmp.Gt => _ctx.MkGt(l, r),
            ArithCmp.Ge => _ctx.MkGe(l, r),
            _ => throw new NotSupportedException($"Unknown comparison op: {c.Op}"),
        };
    }

    private MZ3.BoolExpr Boolean(BinaryBool b) => b.Op switch
    {
        BoolOp.And => _ctx.MkAnd(Bool(b.Left), Bool(b.Right)),
        BoolOp.Or => _ctx.MkOr(Bool(b.Left), Bool(b.Right)),
        BoolOp.Implies => _ctx.MkImplies(Bool(b.Left), Bool(b.Right)),
        BoolOp.Iff => _ctx.MkIff(Bool(b.Left), Bool(b.Right)),
        _ => throw new NotSupportedException($"Unknown boolean op: {b.Op}"),
    };

    private MZ3.BitVecExpr Binary(BvBinary b)
    {
        var l = Bv(b.Left);
        var r = Bv(b.Right);
        return b.Op switch
        {
            BvBinOp.Add => _ctx.MkBVAdd(l, r),
            BvBinOp.Sub => _ctx.MkBVSub(l, r),
            BvBinOp.Mul => _ctx.MkBVMul(l, r),
            BvBinOp.And => _ctx.MkBVAND(l, r),
            BvBinOp.Or => _ctx.MkBVOR(l, r),
            BvBinOp.Xor => _ctx.MkBVXOR(l, r),
            BvBinOp.Shl => _ctx.MkBVSHL(l, r),
            BvBinOp.LShr => _ctx.MkBVLSHR(l, r),
            BvBinOp.AShr => _ctx.MkBVASHR(l, r),
            _ => throw new NotSupportedException($"Unknown bit-vector op: {b.Op}"),
        };
    }

    private MZ3.BitVecExpr Unary(BvUnary u)
    {
        var x = Bv(u.Operand);
        return u.Op switch
        {
            BvUnOp.Not => _ctx.MkBVNot(x),
            BvUnOp.Neg => _ctx.MkBVNeg(x),
            _ => throw new NotSupportedException($"Unknown unary op: {u.Op}"),
        };
    }

    private MZ3.BoolExpr Compare(BvCompare c)
    {
        var l = Bv(c.Left);
        var r = Bv(c.Right);
        return c.Op switch
        {
            BvCmpOp.Ult => _ctx.MkBVULT(l, r),
            BvCmpOp.Ule => _ctx.MkBVULE(l, r),
            BvCmpOp.Ugt => _ctx.MkBVUGT(l, r),
            BvCmpOp.Uge => _ctx.MkBVUGE(l, r),
            BvCmpOp.Slt => _ctx.MkBVSLT(l, r),
            BvCmpOp.Sle => _ctx.MkBVSLE(l, r),
            BvCmpOp.Sgt => _ctx.MkBVSGT(l, r),
            BvCmpOp.Sge => _ctx.MkBVSGE(l, r),
            _ => throw new NotSupportedException($"Unknown comparison op: {c.Op}"),
        };
    }
}
