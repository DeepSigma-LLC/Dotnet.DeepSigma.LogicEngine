using System.Text;
using DeepSigma.LogicEngine.Printing.Infrastructure;

namespace DeepSigma.LogicEngine.FiniteSets;

/// <summary>Renders finite-set expressions and formulas back to readable text (precedence-aware).</summary>
internal static class FiniteSetsPrinter
{
    public static string Print(SetExpr expr)
    {
        var sb = new StringBuilder();
        WriteSet(expr, sb);
        return sb.ToString();
    }

    public static string Print(ElementExpr element)
        => element is ElementVar v ? v.Name : element.ToString()!;

    public static string Print(SetFormula formula)
    {
        var sb = new StringBuilder();
        WriteFormula(formula, sb);
        return sb.ToString();
    }

    private static void WriteSet(SetExpr e, StringBuilder sb)
    {
        switch (e)
        {
            case SetVar v: sb.Append(v.Name); break;
            case SetConst c: sb.Append(c.Full ? "U" : "∅"); break;
            case SetCompl n: sb.Append('~'); WriteSetOperand(n.Operand, sb); break;
            case SetUnion x: WriteSetBinary(x.Left, "∪", x.Right, sb); break;
            case SetInter x: WriteSetBinary(x.Left, "∩", x.Right, sb); break;
            case SetDiff x: WriteSetBinary(x.Left, "\\", x.Right, sb); break;
            case SetSymDiff x: WriteSetBinary(x.Left, "Δ", x.Right, sb); break;
            default: throw new InvalidOperationException($"Unknown set expression: {e.GetType().Name}");
        }
    }

    private static void WriteSetOperand(SetExpr e, StringBuilder sb)
        => ParenPrinter.WriteOperand(sb, e, IsAtomicSet, x => WriteSet(x, sb));

    private static bool IsAtomicSet(SetExpr e)
        => e is SetVar or SetConst or SetCompl;

    private static void WriteSetBinary(SetExpr l, string op, SetExpr r, StringBuilder sb)
        => ParenPrinter.WriteBinary(sb, l, op, r, x => WriteSet(x, sb));

    private static void WriteFormula(SetFormula f, StringBuilder sb)
    {
        switch (f)
        {
            case MemberRel m: sb.Append(Print(m.Element)).Append(" ∈ "); WriteSet(m.Set, sb); break;
            case SubsetRel s: WriteSet(s.Left, sb); sb.Append(s.Proper ? " ⊂ " : " ⊆ "); WriteSet(s.Right, sb); break;
            case EqualRel q: WriteSet(q.Left, sb); sb.Append(" = "); WriteSet(q.Right, sb); break;
            case DisjointRel d: sb.Append("disjoint("); WriteSet(d.Left, sb); sb.Append(", "); WriteSet(d.Right, sb); sb.Append(')'); break;
            case CardRel c: sb.Append('|'); WriteSet(c.Set, sb); sb.Append("| ").Append(OpText(c.Op)).Append(' ').Append(c.Bound); break;
            case SetNot n: sb.Append('!'); WriteFormulaOperand(n.Operand, sb); break;
            case SetAnd a: WriteFormulaBinary(a.Left, "&", a.Right, sb); break;
            case SetOr o: WriteFormulaBinary(o.Left, "|", o.Right, sb); break;
            case SetImplies i: WriteFormulaBinary(i.Left, "->", i.Right, sb); break;
            case SetIff bi: WriteFormulaBinary(bi.Left, "<->", bi.Right, sb); break;
            default: throw new InvalidOperationException($"Unknown set formula: {f.GetType().Name}");
        }
    }

    private static void WriteFormulaOperand(SetFormula f, StringBuilder sb)
        => ParenPrinter.WriteOperand(sb, f, IsAtomicFormula, x => WriteFormula(x, sb));

    private static bool IsAtomicFormula(SetFormula f)
        => f is MemberRel or SubsetRel or EqualRel or DisjointRel or CardRel or SetNot;

    private static void WriteFormulaBinary(SetFormula l, string op, SetFormula r, StringBuilder sb)
        => ParenPrinter.WriteBinary(sb, l, op, r, x => WriteFormula(x, sb));

    private static string OpText(CardOp op) => op switch
    {
        CardOp.Equal => "=",
        CardOp.LessOrEqual => "<=",
        CardOp.Less => "<",
        CardOp.GreaterOrEqual => ">=",
        CardOp.Greater => ">",
        _ => throw new InvalidOperationException(),
    };
}
