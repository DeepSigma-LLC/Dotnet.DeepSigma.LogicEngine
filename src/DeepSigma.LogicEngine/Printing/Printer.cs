using System.Text;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Printing.Infrastructure;

namespace DeepSigma.LogicEngine.Printing;

/// <summary>
/// Precedence-aware pretty-printer. Emits minimal parentheses and uses
/// ASCII operators: <c>!</c>, <c>&amp;</c>, <c>|</c>, <c>-&gt;</c>, <c>&lt;-&gt;</c>.
/// </summary>
public static class Printer
{
    private const int PrecIff = 1;
    private const int PrecImplies = 2;
    private const int PrecOr = 3;
    private const int PrecAnd = 4;
    private const int PrecNot = 5;
    private const int PrecAtom = 6;

    /// <summary>Render the formula as a string with minimal parentheses.</summary>
    public static string Print(Formula formula)
    {
        var sb = new StringBuilder();
        Write(formula, sb, 0);
        return sb.ToString();
    }

    private static void Write(Formula f, StringBuilder sb, int outerPrec)
    {
        switch (f)
        {
            case BoolConst c:
                sb.Append(c.Value ? "true" : "false");
                return;
            case Variable v:
                sb.Append(v.Name);
                return;
            case Negation n:
                PrecedencePrinter.WriteWithParens(sb, PrecNot, outerPrec, () =>
                {
                    sb.Append('!');
                    Write(n.Operand, sb, PrecNot);
                });
                return;
            case Conjunction a:
                WriteBinary(sb, outerPrec, PrecAnd, a.Left, "&", a.Right, rightAssoc: false);
                return;
            case Disjunction o:
                WriteBinary(sb, outerPrec, PrecOr, o.Left, "|", o.Right, rightAssoc: false);
                return;
            case Implication i:
                WriteBinary(sb, outerPrec, PrecImplies, i.Antecedent, "->", i.Consequent, rightAssoc: true);
                return;
            case Biconditional b:
                WriteBinary(sb, outerPrec, PrecIff, b.Left, "<->", b.Right, rightAssoc: false);
                return;
        }
    }

    private static void WriteBinary(StringBuilder sb, int outerPrec, int prec, Formula left, string op, Formula right, bool rightAssoc)
        => PrecedencePrinter.WriteBinary(sb, outerPrec, prec, left, op, right, rightAssoc, (x, p) => Write(x, sb, p));
}
