using System.Text;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Precedence-aware printer for <see cref="SmtFormula"/>. Uses ASCII operators
/// (<c>=</c>, <c>!=</c>, <c>!</c>, <c>&amp;</c>, <c>|</c>, <c>-&gt;</c>,
/// <c>&lt;-&gt;</c>) and emits minimal parentheses.
/// </summary>
internal static class SmtPrinter
{
    private const int PrecIff = 1;
    private const int PrecImplies = 2;
    private const int PrecOr = 3;
    private const int PrecAnd = 4;
    private const int PrecNot = 5;
    private const int PrecAtom = 6;

    public static string Print(SmtFormula formula)
    {
        var sb = new StringBuilder();
        Write(formula, sb, 0);
        return sb.ToString();
    }

    private static void Write(SmtFormula f, StringBuilder sb, int outerPrec)
    {
        switch (f)
        {
            case SmtBool b:
                sb.Append(b.Value ? "true" : "false");
                return;
            case EqualityAtom eq:
                sb.Append(eq.Left).Append(" = ").Append(eq.Right);
                return;
            case PredicateAtom pred:
                WritePredicate(pred, sb);
                return;
            case SmtNot n:
                WriteNot(n, sb, outerPrec);
                return;
            case SmtAnd a:
                WriteBinary(sb, outerPrec, PrecAnd, a.Left, "&", a.Right, rightAssoc: false);
                return;
            case SmtOr o:
                WriteBinary(sb, outerPrec, PrecOr, o.Left, "|", o.Right, rightAssoc: false);
                return;
            case SmtImplies i:
                WriteBinary(sb, outerPrec, PrecImplies, i.Antecedent, "->", i.Consequent, rightAssoc: true);
                return;
            case SmtIff bi:
                WriteBinary(sb, outerPrec, PrecIff, bi.Left, "<->", bi.Right, rightAssoc: false);
                return;
        }
    }

    private static void WritePredicate(PredicateAtom pred, StringBuilder sb)
    {
        sb.Append(pred.Symbol);
        if (pred.Arguments.Count > 0)
        {
            sb.Append('(').AppendJoin(", ", pred.Arguments).Append(')');
        }
    }

    private static void WriteNot(SmtNot n, StringBuilder sb, int outerPrec)
    {
        var paren = PrecNot < outerPrec;
        if (paren)
        {
            sb.Append('(');
        }
        sb.Append('!');
        // An equality atom contains '=', so wrap it under negation for clarity.
        if (n.Operand is EqualityAtom)
        {
            sb.Append('(');
            Write(n.Operand, sb, 0);
            sb.Append(')');
        }
        else
        {
            Write(n.Operand, sb, PrecNot);
        }
        if (paren)
        {
            sb.Append(')');
        }
    }

    private static void WriteBinary(StringBuilder sb, int outerPrec, int prec, SmtFormula left, string op, SmtFormula right, bool rightAssoc)
    {
        var paren = prec < outerPrec;
        if (paren)
        {
            sb.Append('(');
        }
        Write(left, sb, rightAssoc ? prec + 1 : prec);
        sb.Append(' ').Append(op).Append(' ');
        Write(right, sb, rightAssoc ? prec : prec + 1);
        if (paren)
        {
            sb.Append(')');
        }
    }
}
