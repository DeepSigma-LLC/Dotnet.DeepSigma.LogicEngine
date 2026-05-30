using System.Text;
using DeepSigma.LogicEngine.Printing.Infrastructure;

namespace DeepSigma.LogicEngine.Temporal;

/// <summary>Readable, fully-parenthesized printer for <see cref="LtlFormula"/>.</summary>
internal static class LtlPrinter
{
    public static string Print(LtlFormula f)
    {
        var sb = new StringBuilder();
        Write(f, sb);
        return sb.ToString();
    }

    private static void Write(LtlFormula f, StringBuilder sb)
    {
        switch (f)
        {
            case LtlBool b: sb.Append(b.Value ? "true" : "false"); break;
            case LtlAtom a: sb.Append(a.Name); break;
            case LtlNot n: sb.Append('!'); WriteOperand(n.Operand, sb); break;
            case LtlNext x: sb.Append("X "); WriteOperand(x.Operand, sb); break;
            case LtlEventually e: sb.Append("F "); WriteOperand(e.Operand, sb); break;
            case LtlGlobally g: sb.Append("G "); WriteOperand(g.Operand, sb); break;
            case LtlAnd a: WriteBinary(a.Left, "&", a.Right, sb); break;
            case LtlOr o: WriteBinary(o.Left, "|", o.Right, sb); break;
            case LtlImplies i: WriteBinary(i.Left, "->", i.Right, sb); break;
            case LtlIff bi: WriteBinary(bi.Left, "<->", bi.Right, sb); break;
            case LtlUntil u: WriteBinary(u.Left, "U", u.Right, sb); break;
            case LtlRelease r: WriteBinary(r.Left, "R", r.Right, sb); break;
            case LtlWeakUntil w: WriteBinary(w.Left, "W", w.Right, sb); break;
        }
    }

    private static void WriteOperand(LtlFormula f, StringBuilder sb)
        => ParenPrinter.WriteOperand(sb, f, IsAtomic, x => Write(x, sb));

    private static bool IsAtomic(LtlFormula f)
        => f is LtlAtom or LtlBool or LtlNot or LtlNext or LtlEventually or LtlGlobally;

    private static void WriteBinary(LtlFormula l, string op, LtlFormula r, StringBuilder sb)
        => ParenPrinter.WriteBinary(sb, l, op, r, x => Write(x, sb));
}
