namespace DeepSigma.LogicEngine.Parsing.Infrastructure;

/// <summary>
/// The boolean-connective precedence layer shared by every logic's parser. The
/// chain (Iff → Implies → Or → And) is structurally identical across parsers,
/// differing only in the formula type, the connective constructors, and which
/// operand level sits beneath And. These helpers capture the exact control flow —
/// a left-associative while-loop and a right-associative self-recursion — so each
/// parser expresses a level as a one-liner. The <c>matchAndConsume</c> delegate
/// consumes the operator token (typically <c>() =&gt; Accept(Kind.Or)</c>).
/// </summary>
internal static class ConnectiveChain
{
    /// <summary>Left-associative binary level: <c>operand (op operand)*</c>.</summary>
    public static TFormula LeftAssoc<TFormula>(Func<TFormula> operand, Func<bool> matchAndConsume, Func<TFormula, TFormula, TFormula> make)
    {
        var left = operand();
        while (matchAndConsume())
        {
            left = make(left, operand());
        }
        return left;
    }

    /// <summary>Right-associative binary level: <c>operand (op self)?</c> (e.g. implication).</summary>
    public static TFormula RightAssoc<TFormula>(Func<TFormula> operand, Func<bool> matchAndConsume, Func<TFormula> self, Func<TFormula, TFormula, TFormula> make)
    {
        var left = operand();
        return matchAndConsume() ? make(left, self()) : left;
    }
}
