namespace DeepSigma.LogicEngine.Parsing.Infrastructure;

/// <summary>
/// Small, stateless character-scanning helpers shared by the hand-written
/// tokenizers. Each lexer keeps its own scanning loop (the <c>i++ / continue</c>
/// skeleton), its own token type and keyword map, and its own multi-character
/// operator dispatch — these helpers only capture the genuinely identical
/// fragments (lookahead, literal matching, identifier classification, and the
/// "read a run of characters" loop) so error positions and accepted syntax stay
/// exactly as each lexer defines them. Companion to
/// <see cref="ConnectiveChain"/>/<see cref="TokenReader{TToken, TKind}"/>, which
/// cover parsing <em>after</em> tokenization.
/// </summary>
internal static class CharScanner
{
    /// <summary>The character at <paramref name="i"/> + <paramref name="offset"/>, or <c>'\0'</c> past the end.</summary>
    public static char Peek(string s, int i, int offset = 1)
        => i + offset < s.Length ? s[i + offset] : '\0';

    /// <summary>True when the run of length <c>literal.Length</c> starting at <paramref name="i"/> equals <paramref name="literal"/>.</summary>
    public static bool Matches(string s, int i, string literal)
        => i + literal.Length <= s.Length && s.AsSpan(i, literal.Length).SequenceEqual(literal);

    /// <summary>Whether <paramref name="c"/> may begin an identifier (letter or underscore).</summary>
    public static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    /// <summary>Whether <paramref name="c"/> may continue an identifier (letter, digit, or underscore).</summary>
    public static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Returns the index just past the maximal run of characters satisfying
    /// <paramref name="cont"/>, starting at <paramref name="start"/>. The caller
    /// slices <c>s[start..end]</c>, keeping the slice explicit at the call site.
    /// </summary>
    public static int ReadWhile(string s, int start, Func<char, bool> cont)
    {
        var i = start;
        while (i < s.Length && cont(s[i]))
        {
            i++;
        }
        return i;
    }
}
