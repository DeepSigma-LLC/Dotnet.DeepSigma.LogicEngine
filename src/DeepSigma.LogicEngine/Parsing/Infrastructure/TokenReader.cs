namespace DeepSigma.LogicEngine.Parsing.Infrastructure;

/// <summary>
/// Shared cursor over a token list for the recursive-descent parsers. Each parser
/// keeps its own token record and <c>Kind</c> enum and supplies selectors for the
/// kind and display text; this base provides the identical <c>Peek/Advance/Check/
/// Accept/Expect</c> plumbing (plus <c>Position</c>/<c>Reset</c> for backtracking)
/// that every parser otherwise re-implements.
/// </summary>
internal abstract class TokenReader<TToken, TKind> where TKind : struct, Enum
{
    private readonly IReadOnlyList<TToken> _tokens;
    private readonly Func<TToken, TKind> _kindOf;
    private readonly Func<TToken, string> _textOf;
    private int _pos;

    protected TokenReader(IReadOnlyList<TToken> tokens, Func<TToken, TKind> kindOf, Func<TToken, string> textOf)
    {
        _tokens = tokens;
        _kindOf = kindOf;
        _textOf = textOf;
    }

    /// <summary>The current token index (for save/restore backtracking).</summary>
    protected int Position => _pos;

    protected void Reset(int position) => _pos = position;

    protected TToken Peek(int offset = 0) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];

    protected TKind PeekKind(int offset = 0) => _kindOf(Peek(offset));

    protected TToken Advance() => _tokens[_pos++];

    protected bool Check(TKind kind) => EqualityComparer<TKind>.Default.Equals(PeekKind(), kind);

    protected bool Accept(TKind kind)
    {
        if (Check(kind))
        {
            _pos++;
            return true;
        }
        return false;
    }

    protected void Expect(TKind kind, string? label = null)
    {
        if (!Accept(kind))
        {
            throw new FormatException($"Expected {label ?? kind.ToString()} but found '{_textOf(Peek())}'.");
        }
    }
}
