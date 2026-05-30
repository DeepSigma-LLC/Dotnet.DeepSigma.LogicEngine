using System.Text;

namespace DeepSigma.LogicEngine.Printing.Infrastructure;

/// <summary>
/// Emit helpers for the fully-parenthesized printers (modal, LTL, CTL, FOL,
/// finite-set). Each printer keeps its own per-node dispatch and "is this operand
/// atomic?" predicate; these helpers capture the shared binary-emit
/// (<c>(l op r)</c>) and operand-parenthesization shapes.
/// </summary>
internal static class ParenPrinter
{
    public static void WriteBinary<T>(StringBuilder sb, T left, string op, T right, Action<T> write)
    {
        sb.Append('(');
        write(left);
        sb.Append(' ').Append(op).Append(' ');
        write(right);
        sb.Append(')');
    }

    public static void WriteOperand<T>(StringBuilder sb, T operand, Func<T, bool> isAtomic, Action<T> write)
    {
        if (isAtomic(operand))
        {
            write(operand);
        }
        else
        {
            sb.Append('(');
            write(operand);
            sb.Append(')');
        }
    }
}
