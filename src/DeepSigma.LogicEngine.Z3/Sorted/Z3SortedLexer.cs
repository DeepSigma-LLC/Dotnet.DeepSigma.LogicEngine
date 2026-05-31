using System.Text;

namespace DeepSigma.LogicEngine.Z3.Sorted;

internal enum TokenKind
{
    Identifier, Number, BitVecLiteral, StringLiteral,
    True, False, Forall, Exists,
    Plus, Minus, Star, Slash, Tilde, PlusPlus,
    Eq, Neq, Lt, Le, Gt, Ge,
    And, Or, Not, Implies, Iff, Pipe,
    LParen, RParen, Comma, Semicolon, Dot,
    End,
}

internal readonly record struct Token(TokenKind Kind, string Text, int Position);

/// <summary>Tokenizer for the sorted-layer syntax (see <see cref="Z3SortedParser"/>).</summary>
internal static class Lexer
{
    public static IReadOnlyList<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < source.Length)
        {
            var ch = source[i];
            if (char.IsWhiteSpace(ch)) { i++; continue; }

            switch (ch)
            {
                case '(': tokens.Add(new Token(TokenKind.LParen, "(", i++)); continue;
                case ')': tokens.Add(new Token(TokenKind.RParen, ")", i++)); continue;
                case ',': tokens.Add(new Token(TokenKind.Comma, ",", i++)); continue;
                case ';': tokens.Add(new Token(TokenKind.Semicolon, ";", i++)); continue;
                case '.': tokens.Add(new Token(TokenKind.Dot, ".", i++)); continue;
                case '*': tokens.Add(new Token(TokenKind.Star, "*", i++)); continue;
                case '/': tokens.Add(new Token(TokenKind.Slash, "/", i++)); continue;
                case '~': tokens.Add(new Token(TokenKind.Tilde, "~", i++)); continue;
                case '&': tokens.Add(new Token(TokenKind.And, "&", i++)); continue;
                case '|': tokens.Add(new Token(TokenKind.Pipe, "|", i++)); continue;
            }

            if (ch == '+')
            {
                if (Next(source, i) == '+') { tokens.Add(new Token(TokenKind.PlusPlus, "++", i)); i += 2; }
                else { tokens.Add(new Token(TokenKind.Plus, "+", i++)); }
                continue;
            }
            if (ch == '-')
            {
                if (Next(source, i) == '>') { tokens.Add(new Token(TokenKind.Implies, "->", i)); i += 2; }
                else { tokens.Add(new Token(TokenKind.Minus, "-", i++)); }
                continue;
            }
            if (ch == '<')
            {
                if (Next(source, i) == '-' && Next(source, i, 2) == '>') { tokens.Add(new Token(TokenKind.Iff, "<->", i)); i += 3; }
                else if (Next(source, i) == '=') { tokens.Add(new Token(TokenKind.Le, "<=", i)); i += 2; }
                else { tokens.Add(new Token(TokenKind.Lt, "<", i++)); }
                continue;
            }
            if (ch == '>')
            {
                if (Next(source, i) == '=') { tokens.Add(new Token(TokenKind.Ge, ">=", i)); i += 2; }
                else { tokens.Add(new Token(TokenKind.Gt, ">", i++)); }
                continue;
            }
            if (ch == '=')
            {
                if (Next(source, i) == '=') { tokens.Add(new Token(TokenKind.Eq, "==", i)); i += 2; continue; }
                throw new FormatException($"Unexpected '=' at position {i} (did you mean '=='?).");
            }
            if (ch == '!')
            {
                if (Next(source, i) == '=') { tokens.Add(new Token(TokenKind.Neq, "!=", i)); i += 2; }
                else { tokens.Add(new Token(TokenKind.Not, "!", i++)); }
                continue;
            }
            if (ch == '#')
            {
                i = ReadBitVecLiteral(source, i, tokens);
                continue;
            }
            if (ch == '"')
            {
                i = ReadString(source, i, tokens);
                continue;
            }
            if (char.IsDigit(ch))
            {
                var start = i;
                while (i < source.Length && char.IsDigit(source[i])) i++;
                tokens.Add(new Token(TokenKind.Number, source[start..i], start));
                continue;
            }
            if (char.IsLetter(ch) || ch == '_')
            {
                var start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                var text = source[start..i];
                tokens.Add(new Token(KeywordKind(text), text, start));
                continue;
            }

            throw new FormatException($"Unexpected character '{ch}' at position {i}.");
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, source.Length));
        return tokens;
    }

    private static char Next(string s, int i, int offset = 1) => i + offset < s.Length ? s[i + offset] : '\0';

    private static TokenKind KeywordKind(string text) => text switch
    {
        "true" => TokenKind.True,
        "false" => TokenKind.False,
        "forall" => TokenKind.Forall,
        "exists" => TokenKind.Exists,
        _ => TokenKind.Identifier,
    };

    // #b<bits> or #x<hex-digits>; the token text keeps the radix marker as its first character.
    private static int ReadBitVecLiteral(string source, int start, List<Token> tokens)
    {
        var radix = Next(source, start);
        var i = start + 2;
        if (radix == 'b')
        {
            var begin = i;
            while (i < source.Length && (source[i] == '0' || source[i] == '1')) i++;
            if (i == begin) throw new FormatException($"Empty bit-vector literal at position {start}.");
            tokens.Add(new Token(TokenKind.BitVecLiteral, "b" + source[begin..i], start));
            return i;
        }
        if (radix == 'x')
        {
            var begin = i;
            while (i < source.Length && Uri.IsHexDigit(source[i])) i++;
            if (i == begin) throw new FormatException($"Empty bit-vector literal at position {start}.");
            tokens.Add(new Token(TokenKind.BitVecLiteral, "x" + source[begin..i], start));
            return i;
        }
        throw new FormatException($"Expected '#b' or '#x' bit-vector literal at position {start}.");
    }

    private static int ReadString(string source, int start, List<Token> tokens)
    {
        var sb = new StringBuilder();
        var i = start + 1;
        while (i < source.Length && source[i] != '"')
        {
            if (source[i] == '\\' && i + 1 < source.Length)
            {
                var esc = source[i + 1];
                sb.Append(esc switch { 'n' => '\n', 't' => '\t', '"' => '"', '\\' => '\\', _ => esc });
                i += 2;
                continue;
            }
            sb.Append(source[i++]);
        }
        if (i >= source.Length) throw new FormatException($"Unterminated string literal starting at position {start}.");
        tokens.Add(new Token(TokenKind.StringLiteral, sb.ToString(), start));
        return i + 1;
    }
}
