using System.Text;

namespace DeepSigma.LogicEngine.Printing.Infrastructure;

/// <summary>
/// Emit helpers for the precedence-aware printers (propositional and SMT) that
/// parenthesize minimally. Each printer keeps its own precedence constants and
/// per-node dispatch; these helpers capture the shared "parenthesize iff the
/// inner precedence is lower than the context" rule and the right-associativity
/// handling for binary operators.
/// </summary>
internal static class PrecedencePrinter
{
    public static void WriteWithParens(StringBuilder sb, int prec, int outerPrec, Action body)
    {
        var parenthesize = prec < outerPrec;
        if (parenthesize)
        {
            sb.Append('(');
        }
        body();
        if (parenthesize)
        {
            sb.Append(')');
        }
    }

    public static void WriteBinary<T>(StringBuilder sb, int outerPrec, int prec, T left, string op, T right, bool rightAssoc, Action<T, int> write)
    {
        WriteWithParens(sb, prec, outerPrec, () =>
        {
            write(left, rightAssoc ? prec + 1 : prec);
            sb.Append(' ').Append(op).Append(' ');
            write(right, rightAssoc ? prec : prec + 1);
        });
    }
}
