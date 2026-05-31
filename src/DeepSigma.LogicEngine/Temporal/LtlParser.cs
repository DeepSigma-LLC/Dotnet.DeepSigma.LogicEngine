using DeepSigma.LogicEngine.Parsing.Infrastructure;

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
        return new State(Tokenize(source)).ParseComplete();
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
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c)
            {
                case '(': tokens.Add(new(Kind.LParen, "(", i++)); continue;
                case ')': tokens.Add(new(Kind.RParen, ")", i++)); continue;
                case '!': case '~': tokens.Add(new(Kind.Not, "!", i++)); continue;
                case '&': tokens.Add(new(Kind.And, "&", i)); i += CharScanner.Peek(s, i) == '&' ? 2 : 1; continue;
                case '|': tokens.Add(new(Kind.Or, "|", i)); i += CharScanner.Peek(s, i) == '|' ? 2 : 1; continue;
            }
            if (c == '<' && i + 2 < s.Length && (s[i + 1] is '-' or '=') && s[i + 2] == '>') { tokens.Add(new(Kind.Iff, "<->", i)); i += 3; continue; }
            if ((c == '-' || c == '=') && CharScanner.Peek(s, i) == '>') { tokens.Add(new(Kind.Implies, "->", i)); i += 2; continue; }
            if (CharScanner.IsIdentifierStart(c))
            {
                var start = i;
                i = CharScanner.ReadWhile(s, i, CharScanner.IsIdentifierPart);
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

    private sealed class State : TokenReader<Token, Kind>
    {
        public State(List<Token> tokens) : base(tokens, t => t.Kind, t => t.Text) { }

        public LtlFormula ParseComplete()
        {
            var formula = ParseIff();
            Expect(Kind.End);
            return formula;
        }

        private LtlFormula ParseIff() => ConnectiveChain.LeftAssoc(ParseImplies, () => Accept(Kind.Iff), (l, r) => new LtlIff(l, r));
        private LtlFormula ParseImplies() => ConnectiveChain.RightAssoc(ParseOr, () => Accept(Kind.Implies), ParseImplies, (l, r) => new LtlImplies(l, r));
        private LtlFormula ParseOr() => ConnectiveChain.LeftAssoc(ParseAnd, () => Accept(Kind.Or), (l, r) => new LtlOr(l, r));

        // The And operand is the temporal-binary level (U/R/W), kept separate from the connective chain.
        private LtlFormula ParseAnd() => ConnectiveChain.LeftAssoc(ParseTemporalBinary, () => Accept(Kind.And), (l, r) => new LtlAnd(l, r));

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
