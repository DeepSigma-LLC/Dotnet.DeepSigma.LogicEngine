using System.Text;
using DeepSigma.LogicEngine.Printing.Infrastructure;

namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>Renders first-order terms and formulas to readable text.</summary>
internal static class FolPrinter
{
    public static string Print(FolTerm term)
    {
        var sb = new StringBuilder();
        WriteTerm(term, sb);
        return sb.ToString();
    }

    public static string Print(FolFormula formula)
    {
        var sb = new StringBuilder();
        WriteFormula(formula, sb);
        return sb.ToString();
    }

    private static void WriteTerm(FolTerm t, StringBuilder sb)
    {
        switch (t)
        {
            case FolVar v:
                sb.Append('?').Append(v.Name);
                break;
            case FolFunc f when f.IsConstant:
                sb.Append(f.Symbol);
                break;
            case FolFunc f:
                sb.Append(f.Symbol).Append('(');
                WriteArgs(f.Arguments, sb);
                sb.Append(')');
                break;
        }
    }

    private static void WriteArgs(IReadOnlyList<FolTerm> args, StringBuilder sb)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            WriteTerm(args[i], sb);
        }
    }

    private static void WriteFormula(FolFormula f, StringBuilder sb)
    {
        switch (f)
        {
            case FolBool b: sb.Append(b.Value ? "true" : "false"); break;
            case FolPredicate p:
                sb.Append(p.Symbol);
                if (p.Arguments.Count > 0) { sb.Append('('); WriteArgs(p.Arguments, sb); sb.Append(')'); }
                break;
            case FolEquals e: WriteTerm(e.Left, sb); sb.Append(" = "); WriteTerm(e.Right, sb); break;
            case FolNot n: sb.Append('!'); WriteOperand(n.Operand, sb); break;
            case FolAnd x: WriteBinary(x.Left, "&", x.Right, sb); break;
            case FolOr x: WriteBinary(x.Left, "|", x.Right, sb); break;
            case FolImplies x: WriteBinary(x.Antecedent, "->", x.Consequent, sb); break;
            case FolIff x: WriteBinary(x.Left, "<->", x.Right, sb); break;
            case FolForall q: sb.Append("forall ").Append(q.Variable).Append(". "); WriteOperand(q.Body, sb); break;
            case FolExists q: sb.Append("exists ").Append(q.Variable).Append(". "); WriteOperand(q.Body, sb); break;
        }
    }

    private static void WriteOperand(FolFormula f, StringBuilder sb)
        => ParenPrinter.WriteOperand(sb, f, IsAtomic, x => WriteFormula(x, sb));

    private static bool IsAtomic(FolFormula f)
        => f is FolPredicate or FolEquals or FolBool or FolNot;

    private static void WriteBinary(FolFormula l, string op, FolFormula r, StringBuilder sb)
        => ParenPrinter.WriteBinary(sb, l, op, r, x => WriteFormula(x, sb));
}
