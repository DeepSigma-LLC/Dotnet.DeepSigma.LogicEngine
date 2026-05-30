using System.Text;

namespace DeepSigma.LogicEngine.Modal;

/// <summary>Readable, parenthesized printer for <see cref="ModalFormula"/> using <c>[]</c> and <c>&lt;&gt;</c>.</summary>
internal static class ModalPrinter
{
    public static string Print(ModalFormula f)
    {
        var sb = new StringBuilder();
        Write(f, sb);
        return sb.ToString();
    }

    private static void Write(ModalFormula f, StringBuilder sb)
    {
        switch (f)
        {
            case ModalBool b: sb.Append(b.Value ? "true" : "false"); break;
            case ModalAtom a: sb.Append(a.Name); break;
            case ModalNot n: sb.Append('!'); WriteOperand(n.Operand, sb); break;
            case ModalBox x: sb.Append("[]"); WriteOperand(x.Operand, sb); break;
            case ModalDiamond x: sb.Append("<>"); WriteOperand(x.Operand, sb); break;
            case ModalAnd a: WriteBinary(a.Left, "&", a.Right, sb); break;
            case ModalOr o: WriteBinary(o.Left, "|", o.Right, sb); break;
            case ModalImplies i: WriteBinary(i.Left, "->", i.Right, sb); break;
            case ModalIff bi: WriteBinary(bi.Left, "<->", bi.Right, sb); break;
        }
    }

    private static void WriteOperand(ModalFormula f, StringBuilder sb)
    {
        if (f is ModalAtom or ModalBool or ModalNot or ModalBox or ModalDiamond)
        {
            Write(f, sb);
        }
        else
        {
            sb.Append('(');
            Write(f, sb);
            sb.Append(')');
        }
    }

    private static void WriteBinary(ModalFormula l, string op, ModalFormula r, StringBuilder sb)
    {
        sb.Append('(');
        Write(l, sb);
        sb.Append(' ').Append(op).Append(' ');
        Write(r, sb);
        sb.Append(')');
    }
}
