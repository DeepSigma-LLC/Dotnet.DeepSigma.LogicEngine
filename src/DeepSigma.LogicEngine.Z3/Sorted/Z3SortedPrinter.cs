using System.Numerics;
using System.Text;

namespace DeepSigma.LogicEngine.Z3.Sorted;

/// <summary>
/// Prints a <see cref="SortedExpr"/> back into the textual syntax accepted by
/// <see cref="Z3SortedParser"/>. The output begins with a declaration prefix for the free
/// variables, so <c>Z3SortedParser.Parse(Z3SortedPrinter.Print(e))</c> round-trips to an
/// expression structurally equal to <paramref name="e"/>.
/// </summary>
internal static class Z3SortedPrinter
{
    private const int PrecQuant = 0;
    private const int PrecIff = 1;
    private const int PrecImplies = 2;
    private const int PrecOr = 3;
    private const int PrecAnd = 4;
    private const int PrecNot = 5;
    private const int PrecCompare = 6;
    private const int PrecAdd = 7;
    private const int PrecMul = 8;
    private const int PrecUnary = 9;

    public static string Print(SortedExpr expression)
    {
        var sb = new StringBuilder();
        WriteDeclarations(expression, sb);
        Write(expression, sb, 0);
        return sb.ToString();
    }

    private static void WriteDeclarations(SortedExpr root, StringBuilder sb)
    {
        var free = new SortedDictionary<string, Sort>(StringComparer.Ordinal);
        CollectFree(root, new HashSet<string>(StringComparer.Ordinal), free);
        if (free.Count == 0) return;

        // Group variables by sort, sorts in a stable order, names alphabetically within each group.
        foreach (var group in free.GroupBy(kv => kv.Value, kv => kv.Key).OrderBy(g => SortOrder(g.Key)))
        {
            sb.Append(SortText(group.Key)).Append(' ').AppendJoin(", ", group).Append("; ");
        }
    }

    private static void CollectFree(SortedExpr e, HashSet<string> bound, SortedDictionary<string, Sort> free)
    {
        switch (e)
        {
            case SortedVar v:
                if (!bound.Contains(v.Name)) free[v.Name] = v.VarSort;
                return;
            case Quantifier q:
                var added = new List<string>();
                foreach (var b in q.BoundVariables)
                {
                    if (bound.Add(b.Name)) added.Add(b.Name);
                }
                CollectFree(q.Body, bound, free);
                foreach (var name in added) bound.Remove(name);
                return;
            default:
                foreach (var child in Children(e)) CollectFree(child, bound, free);
                return;
        }
    }

    private static IEnumerable<SortedExpr> Children(SortedExpr e) => e switch
    {
        NotExpr n => new[] { n.Operand },
        BinaryBool b => new[] { b.Left, b.Right },
        EqExpr eq => new[] { eq.Left, eq.Right },
        BvCompare c => new[] { c.Left, c.Right },
        BvBinary b => new[] { b.Left, b.Right },
        BvUnary u => new[] { u.Operand },
        BvConcat c => new[] { c.High, c.Low },
        BvExtract x => new[] { x.Operand },
        ArithBinary a => new[] { a.Left, a.Right },
        ArithNegate n => new[] { n.Operand },
        ArithCompare c => new[] { c.Left, c.Right },
        StrLength l => new[] { l.Operand },
        StrConcat c => new[] { c.Left, c.Right },
        StrPredicate p => new[] { p.Left, p.Right },
        _ => Array.Empty<SortedExpr>(),
    };

    private static void Write(SortedExpr e, StringBuilder sb, int outer)
    {
        switch (e)
        {
            case BoolLiteral b: sb.Append(b.Value ? "true" : "false"); return;
            case IntLiteral i: sb.Append(i.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); return;
            case RealLiteral r: sb.Append(r.Value.Numerator).Append('/').Append(r.Value.Denominator); return;
            case BitVecLiteral bv: WriteBitVec(bv, sb); return;
            case StringLiteral s: WriteString(s.Value, sb); return;
            case SortedVar v: sb.Append(v.Name); return;

            case NotExpr n:
                WithParens(sb, PrecNot, outer, () => { sb.Append('!'); Write(n.Operand, sb, PrecNot); });
                return;
            case BinaryBool b:
                WriteBinary(sb, outer, b.Op switch { BoolOp.Iff => PrecIff, BoolOp.Implies => PrecImplies, BoolOp.Or => PrecOr, _ => PrecAnd },
                    b.Left, BoolOpText(b.Op), b.Right, rightAssoc: b.Op == BoolOp.Implies);
                return;
            case EqExpr eq:
                WriteBinary(sb, outer, PrecCompare, eq.Left, eq.Negated ? "!=" : "==", eq.Right, rightAssoc: false);
                return;
            case ArithCompare c:
                WriteBinary(sb, outer, PrecCompare, c.Left, ArithCmpText(c.Op), c.Right, rightAssoc: false);
                return;

            case ArithBinary a:
                WriteBinary(sb, outer, a.Op == ArithOp.Mul ? PrecMul : PrecAdd, a.Left, ArithOpText(a.Op), a.Right, rightAssoc: false);
                return;
            case ArithNegate n:
                WithParens(sb, PrecUnary, outer, () => { sb.Append('-'); Write(n.Operand, sb, PrecUnary); });
                return;
            case StrConcat sc:
                WriteBinary(sb, outer, PrecAdd, sc.Left, "++", sc.Right, rightAssoc: false);
                return;
            case StrLength l:
                sb.Append('|'); Write(l.Operand, sb, 0); sb.Append('|');
                return;

            case BvBinary bb: WriteBvBinary(bb, sb, outer); return;
            case BvUnary bu:
                WithParens(sb, PrecUnary, outer, () => { sb.Append(bu.Op == BvUnOp.Not ? '~' : '-'); Write(bu.Operand, sb, PrecUnary); });
                return;
            case BvCompare bc: WriteCall(sb, BvCmpText(bc.Op), bc.Left, bc.Right); return;
            case BvConcat bcc: WriteCall(sb, "concat", bcc.High, bcc.Low); return;
            case BvExtract bx:
                sb.Append("extract(").Append(bx.High).Append(", ").Append(bx.Low).Append(", ");
                Write(bx.Operand, sb, 0); sb.Append(')');
                return;
            case StrPredicate sp: WriteCall(sb, StrPredText(sp.Op), sp.Left, sp.Right); return;

            case Quantifier q: WriteQuantifier(q, sb, outer); return;

            default:
                throw new NotSupportedException($"Cannot print sorted node: {e.GetType().Name}");
        }
    }

    private static void WriteBvBinary(BvBinary b, StringBuilder sb, int outer)
    {
        switch (b.Op)
        {
            case BvBinOp.Add: WriteBinary(sb, outer, PrecAdd, b.Left, "+", b.Right, false); return;
            case BvBinOp.Sub: WriteBinary(sb, outer, PrecAdd, b.Left, "-", b.Right, false); return;
            case BvBinOp.Mul: WriteBinary(sb, outer, PrecMul, b.Left, "*", b.Right, false); return;
            default: WriteCall(sb, BvBinFunc(b.Op), b.Left, b.Right); return;
        }
    }

    private static void WriteQuantifier(Quantifier q, StringBuilder sb, int outer)
    {
        WithParens(sb, PrecQuant, outer, () =>
        {
            sb.Append(q.IsForall ? "forall " : "exists ");
            sb.AppendJoin(", ", q.BoundVariables.Select(v => $"{SortText(v.VarSort)} {v.Name}"));
            sb.Append(" . ");
            Write(q.Body, sb, PrecQuant);
        });
    }

    // --- helpers ---

    private static void WriteBinary(StringBuilder sb, int outer, int prec, SortedExpr left, string op, SortedExpr right, bool rightAssoc)
    {
        WithParens(sb, prec, outer, () =>
        {
            Write(left, sb, rightAssoc ? prec + 1 : prec);
            sb.Append(' ').Append(op).Append(' ');
            Write(right, sb, rightAssoc ? prec : prec + 1);
        });
    }

    private static void WriteCall(StringBuilder sb, string name, SortedExpr a, SortedExpr b)
    {
        sb.Append(name).Append('(');
        Write(a, sb, 0);
        sb.Append(", ");
        Write(b, sb, 0);
        sb.Append(')');
    }

    private static void WithParens(StringBuilder sb, int prec, int outer, Action body)
    {
        var paren = prec < outer;
        if (paren) sb.Append('(');
        body();
        if (paren) sb.Append(')');
    }

    private static void WriteBitVec(BitVecLiteral bv, StringBuilder sb)
    {
        // Reduce to the canonical unsigned value, then emit exactly Width binary digits as #b….
        var modulus = BigInteger.One << bv.Width;
        var value = ((bv.Value % modulus) + modulus) % modulus;
        sb.Append("#b");
        for (var bit = bv.Width - 1; bit >= 0; bit--)
        {
            sb.Append((value & (BigInteger.One << bit)) != 0 ? '1' : '0');
        }
    }

    private static void WriteString(string value, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var ch in value)
        {
            sb.Append(ch switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\t' => "\\t", _ => ch.ToString() });
        }
        sb.Append('"');
    }

    private static string BoolOpText(BoolOp op) => op switch
    {
        BoolOp.And => "&", BoolOp.Or => "|", BoolOp.Implies => "->", BoolOp.Iff => "<->",
        _ => throw new NotSupportedException(),
    };

    private static string ArithOpText(ArithOp op) => op switch { ArithOp.Add => "+", ArithOp.Sub => "-", _ => "*" };

    private static string ArithCmpText(ArithCmp op) => op switch
    {
        ArithCmp.Lt => "<", ArithCmp.Le => "<=", ArithCmp.Gt => ">", _ => ">=",
    };

    private static string BvBinFunc(BvBinOp op) => op switch
    {
        BvBinOp.And => "bvand", BvBinOp.Or => "bvor", BvBinOp.Xor => "bvxor",
        BvBinOp.Shl => "shl", BvBinOp.LShr => "lshr", BvBinOp.AShr => "ashr",
        _ => throw new NotSupportedException(),
    };

    private static string BvCmpText(BvCmpOp op) => op switch
    {
        BvCmpOp.Ult => "ult", BvCmpOp.Ule => "ule", BvCmpOp.Ugt => "ugt", BvCmpOp.Uge => "uge",
        BvCmpOp.Slt => "slt", BvCmpOp.Sle => "sle", BvCmpOp.Sgt => "sgt", _ => "sge",
    };

    private static string StrPredText(StrPredOp op) => op switch
    {
        StrPredOp.Contains => "contains", StrPredOp.PrefixOf => "prefixof", _ => "suffixof",
    };

    private static string SortText(Sort sort) => sort switch
    {
        BoolSort => "bool", IntSort => "int", RealSort => "real", StringSort => "string",
        BitVecSort bv => $"bv{bv.Width}",
        _ => throw new NotSupportedException(),
    };

    private static int SortOrder(Sort sort) => sort switch
    {
        BoolSort => 0, BitVecSort => 1, IntSort => 2, RealSort => 3, StringSort => 4, _ => 5,
    };
}
