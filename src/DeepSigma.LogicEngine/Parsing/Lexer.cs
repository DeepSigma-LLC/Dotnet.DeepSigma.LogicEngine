using static DeepSigma.LogicEngine.Parsing.Infrastructure.CharScanner;

namespace DeepSigma.LogicEngine.Parsing;

internal enum TokenKind
{
    Identifier,
    True,
    False,
    Not,
    And,
    Or,
    Implies,
    Iff,
    LParen,
    RParen,
    End,
}

internal readonly record struct Token(TokenKind Kind, string Text, int Position);

internal static class Lexer
{
    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < source.Length)
        {
            var ch = source[i];
            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }
            if (ch == '(')
            {
                tokens.Add(new Token(TokenKind.LParen, "(", i));
                i++;
                continue;
            }
            if (ch == ')')
            {
                tokens.Add(new Token(TokenKind.RParen, ")", i));
                i++;
                continue;
            }
            if (ch == '!' || ch == '~')
            {
                tokens.Add(new Token(TokenKind.Not, ch.ToString(), i));
                i++;
                continue;
            }
            if (ch == '&')
            {
                var len = Peek(source, i) == '&' ? 2 : 1;
                tokens.Add(new Token(TokenKind.And, source.Substring(i, len), i));
                i += len;
                continue;
            }
            if (ch == '|')
            {
                var len = Peek(source, i) == '|' ? 2 : 1;
                tokens.Add(new Token(TokenKind.Or, source.Substring(i, len), i));
                i += len;
                continue;
            }
            if (Matches(source, i, "/\\"))
            {
                tokens.Add(new Token(TokenKind.And, "/\\", i));
                i += 2;
                continue;
            }
            if (Matches(source, i, "\\/"))
            {
                tokens.Add(new Token(TokenKind.Or, "\\/", i));
                i += 2;
                continue;
            }
            if (ch == '<' && i + 2 < source.Length && Peek(source, i) == '-' && Peek(source, i, 2) == '>')
            {
                tokens.Add(new Token(TokenKind.Iff, "<->", i));
                i += 3;
                continue;
            }
            if (ch == '<' && i + 2 < source.Length && Peek(source, i) == '=' && Peek(source, i, 2) == '>')
            {
                tokens.Add(new Token(TokenKind.Iff, "<=>", i));
                i += 3;
                continue;
            }
            if (ch == '-' && Peek(source, i) == '>')
            {
                tokens.Add(new Token(TokenKind.Implies, "->", i));
                i += 2;
                continue;
            }
            if (ch == '=' && Peek(source, i) == '>')
            {
                tokens.Add(new Token(TokenKind.Implies, "=>", i));
                i += 2;
                continue;
            }
            if (IsIdentifierStart(ch))
            {
                var start = i;
                i = ReadWhile(source, i, IsIdentifierPart);
                var text = source.Substring(start, i - start);
                var kind = text switch
                {
                    "true" or "True" or "TRUE" => TokenKind.True,
                    "false" or "False" or "FALSE" => TokenKind.False,
                    "and" or "AND" => TokenKind.And,
                    "or" or "OR" => TokenKind.Or,
                    "not" or "NOT" => TokenKind.Not,
                    _ => TokenKind.Identifier,
                };
                tokens.Add(new Token(kind, text, start));
                continue;
            }
            throw new FormatException($"Unexpected character '{ch}' at position {i}.");
        }
        tokens.Add(new Token(TokenKind.End, string.Empty, source.Length));
        return tokens;
    }
}
