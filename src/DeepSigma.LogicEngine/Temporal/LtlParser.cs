namespace DeepSigma.LogicEngine.Temporal;

/// <summary>
/// Recursive-descent parser for LTL. Precedence (low → high): <c>&lt;-&gt;</c> ;
/// <c>-&gt;</c> (right) ; <c>|</c> ; <c>&amp;</c> ; <c>U</c>/<c>R</c>/<c>W</c>
/// (right) ; unary <c>!</c>/<c>X</c>/<c>F</c>/<c>G</c> ; atom/paren. The single
/// uppercase letters X F G U R W are reserved temporal operators.
/// </summary>
public static class LtlParser
{
    private enum Kind { Id, True, False, Not, And, Or, Implies, Iff, Next, Eventually, Globally, Until, Release, Weak, LParen, RParen, End }
    private readonly record struct Token(Kind Kind, string Text, int Position);

    public static LtlFormula Parse(string source)
    {
        var tokens = Tokenize(source);
        var state = new State(tokens);
        var formula = state.ParseIff();
        state.Expect(Kind.End);
        return formula;
    }

    public static bool TryParse(string source, out LtlFormula formula)
    {
        try { formula = Parse(source); return true; }
        catch (FormatException) { formula = null!; return false; }
    }

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
        char Next() => i + 1 < s.Length ? s[i + 1] : '\0';
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c)
            {
                case '(': tokens.Add(new(Kind.LParen, "(", i++)); continue;
                case ')': tokens.Add(new(Kind.RParen, ")", i++)); continue;
                case '!': case '~': tokens.Add(new(Kind.Not, "!", i++)); continue;
                case '&': tokens.Add(new(Kind.And, "&", i)); i += Next() == '&' ? 2 : 1; continue;
                case '|': tokens.Add(new(Kind.Or, "|", i)); i += Next() == '|' ? 2 : 1; continue;
            }
            if (c == '<' && i + 2 < s.Length && (s[i + 1] is '-' or '=') && s[i + 2] == '>') { tokens.Add(new(Kind.Iff, "<->", i)); i += 3; continue; }
            if ((c == '-' || c == '=') && Next() == '>') { tokens.Add(new(Kind.Implies, "->", i)); i += 2; continue; }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var text = s[start..i];
                tokens.Add(new(KeywordKind(text), text, start));
                continue;
            }
            throw new FormatException($"Unexpected character '{c}' at position {i}.");
        }
        tokens.Add(new(Kind.End, string.Empty, s.Length));
        return tokens;
    }

    private static Kind KeywordKind(string t) => t switch
    {
        "true" or "True" => Kind.True,
        "false" or "False" => Kind.False,
        "not" or "NOT" => Kind.Not,
        "and" or "AND" => Kind.And,
        "or" or "OR" => Kind.Or,
        "X" or "next" => Kind.Next,
        "F" or "eventually" => Kind.Eventually,
        "G" or "globally" or "always" => Kind.Globally,
        "U" or "until" => Kind.Until,
        "R" or "release" => Kind.Release,
        "W" or "weak" => Kind.Weak,
        _ => Kind.Id,
    };

    private sealed class State
    {
        private readonly List<Token> _tokens;
        private int _pos;
        public State(List<Token> tokens) => _tokens = tokens;
        private Token Peek() => _tokens[_pos];
        private Token Advance() => _tokens[_pos++];

        public void Expect(Kind kind)
        {
            if (Peek().Kind != kind)
            {
                throw new FormatException($"Expected {kind} at position {Peek().Position}, got '{Peek().Text}'.");
            }
            Advance();
        }

        public LtlFormula ParseIff()
        {
            var left = ParseImplies();
            while (Peek().Kind == Kind.Iff) { Advance(); left = new LtlIff(left, ParseImplies()); }
            return left;
        }

        private LtlFormula ParseImplies()
        {
            var left = ParseOr();
            if (Peek().Kind == Kind.Implies) { Advance(); return new LtlImplies(left, ParseImplies()); }
            return left;
        }

        private LtlFormula ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Kind == Kind.Or) { Advance(); left = new LtlOr(left, ParseAnd()); }
            return left;
        }

        private LtlFormula ParseAnd()
        {
            var left = ParseTemporalBinary();
            while (Peek().Kind == Kind.And) { Advance(); left = new LtlAnd(left, ParseTemporalBinary()); }
            return left;
        }

        private LtlFormula ParseTemporalBinary()
        {
            var left = ParseUnary();
            switch (Peek().Kind)
            {
                case Kind.Until: Advance(); return new LtlUntil(left, ParseTemporalBinary());
                case Kind.Release: Advance(); return new LtlRelease(left, ParseTemporalBinary());
                case Kind.Weak: Advance(); return new LtlWeakUntil(left, ParseTemporalBinary());
                default: return left;
            }
        }

        private LtlFormula ParseUnary()
        {
            switch (Peek().Kind)
            {
                case Kind.Not: Advance(); return new LtlNot(ParseUnary());
                case Kind.Next: Advance(); return new LtlNext(ParseUnary());
                case Kind.Eventually: Advance(); return new LtlEventually(ParseUnary());
                case Kind.Globally: Advance(); return new LtlGlobally(ParseUnary());
                default: return ParsePrimary();
            }
        }

        private LtlFormula ParsePrimary()
        {
            var tok = Peek();
            switch (tok.Kind)
            {
                case Kind.True: Advance(); return LtlFormula.True;
                case Kind.False: Advance(); return LtlFormula.False;
                case Kind.Id: Advance(); return new LtlAtom(tok.Text);
                case Kind.LParen:
                    Advance();
                    var inner = ParseIff();
                    Expect(Kind.RParen);
                    return inner;
                default:
                    throw new FormatException($"Unexpected token '{tok.Text}' ({tok.Kind}) at position {tok.Position}.");
            }
        }
    }
}
