using System.Text;
using DeepSigma.LogicEngine.Printing.Infrastructure;

namespace DeepSigma.LogicEngine.Ctl;

/// <summary>Renders CTL formulas to readable text (precedence-aware).</summary>
internal static class CtlPrinter
{
    public static string Print(CtlFormula f)
    {
        var sb = new StringBuilder();
        Write(f, sb);
        return sb.ToString();
    }

    private static void Write(CtlFormula f, StringBuilder sb)
    {
        switch (f)
        {
            case CtlBool b: sb.Append(b.Value ? "true" : "false"); break;
            case CtlAtom a: sb.Append(a.Name); break;
            case CtlNot n: sb.Append('!'); WriteUnary(n.Operand, sb); break;
            case CtlEX x: sb.Append("EX "); WriteUnary(x.Operand, sb); break;
            case CtlEG x: sb.Append("EG "); WriteUnary(x.Operand, sb); break;
            case CtlEF x: sb.Append("EF "); WriteUnary(x.Operand, sb); break;
            case CtlAX x: sb.Append("AX "); WriteUnary(x.Operand, sb); break;
            case CtlAG x: sb.Append("AG "); WriteUnary(x.Operand, sb); break;
            case CtlAF x: sb.Append("AF "); WriteUnary(x.Operand, sb); break;
            case CtlEU u: sb.Append("E["); Write(u.Left, sb); sb.Append(" U "); Write(u.Right, sb); sb.Append(']'); break;
            case CtlAU u: sb.Append("A["); Write(u.Left, sb); sb.Append(" U "); Write(u.Right, sb); sb.Append(']'); break;
            case CtlAnd x: WriteBinary(x.Left, "&", x.Right, sb); break;
            case CtlOr x: WriteBinary(x.Left, "|", x.Right, sb); break;
            case CtlImplies x: WriteBinary(x.Antecedent, "->", x.Consequent, sb); break;
            case CtlIff x: WriteBinary(x.Left, "<->", x.Right, sb); break;
            default: throw new InvalidOperationException($"Unknown CTL node: {f.GetType().Name}");
        }
    }

    private static void WriteUnary(CtlFormula f, StringBuilder sb)
        => ParenPrinter.WriteOperand(sb, f, IsAtomic, x => Write(x, sb));

    private static bool IsAtomic(CtlFormula f)
        => f is CtlAtom or CtlBool or CtlNot or CtlEX or CtlEG or CtlEF or CtlAX or CtlAG or CtlAF or CtlEU or CtlAU;

    private static void WriteBinary(CtlFormula l, string op, CtlFormula r, StringBuilder sb)
        => ParenPrinter.WriteBinary(sb, l, op, r, x => Write(x, sb));
}
