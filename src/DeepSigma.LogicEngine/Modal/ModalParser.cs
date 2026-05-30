namespace DeepSigma.LogicEngine.Modal;

/// <summary>
/// Recursive-descent parser for propositional modal logic. <c>[]</c> is box,
/// <c>&lt;&gt;</c> is diamond (both unary, binding like <c>!</c>). Precedence
/// (low → high): <c>&lt;-&gt;</c> ; <c>-&gt;</c> (right) ; <c>|</c> ; <c>&amp;</c> ;
/// unary <c>!</c>/<c>[]</c>/<c>&lt;&gt;</c> ; atom/paren.
/// </summary>
public static class ModalParser
{
    private enum Kind { Id, True, False, Not, And, Or, Implies, Iff, Box, Diamond, LParen, RParen, End }
    private readonly record struct Token(Kind Kind, string Text, int Position);

    public static ModalFormula Parse(string source)
    {
        var state = new State(Tokenize(source));
        var formula = state.ParseIff();
        state.Expect(Kind.End);
        return formula;
    }

    public static bool TryParse(string source, out ModalFormula formula)
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
                case '[': if (Next() == ']') { tokens.Add(new(Kind.Box, "[]", i)); i += 2; continue; } break;
            }
            if (c == '<' && Next() == '>') { tokens.Add(new(Kind.Diamond, "<>", i)); i += 2; continue; }
            if (c == '<' && i + 2 < s.Length && (s[i + 1] is '-' or '=') && s[i + 2] == '>') { tokens.Add(new(Kind.Iff, "<->", i)); i += 3; continue; }
            if ((c == '-' || c == '=') && Next() == '>') { tokens.Add(new(Kind.Implies, "->", i)); i += 2; continue; }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var text = s[start..i];
                tokens.Add(new(Keyword(text), text, start));
                continue;
            }
            throw new FormatException($"Unexpected character '{c}' at position {i}.");
        }
        tokens.Add(new(Kind.End, string.Empty, s.Length));
        return tokens;
    }

    private static Kind Keyword(string t) => t switch
    {
        "true" or "True" => Kind.True,
        "false" or "False" => Kind.False,
        "not" or "NOT" => Kind.Not,
        "and" or "AND" => Kind.And,
        "or" or "OR" => Kind.Or,
        "box" or "Box" or "BOX" => Kind.Box,
        "dia" or "Dia" or "DIA" or "diamond" => Kind.Diamond,
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

        public ModalFormula ParseIff()
        {
            var left = ParseImplies();
            while (Peek().Kind == Kind.Iff) { Advance(); left = new ModalIff(left, ParseImplies()); }
            return left;
        }

        private ModalFormula ParseImplies()
        {
            var left = ParseOr();
            if (Peek().Kind == Kind.Implies) { Advance(); return new ModalImplies(left, ParseImplies()); }
            return left;
        }

        private ModalFormula ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Kind == Kind.Or) { Advance(); left = new ModalOr(left, ParseAnd()); }
            return left;
        }

        private ModalFormula ParseAnd()
        {
            var left = ParseUnary();
            while (Peek().Kind == Kind.And) { Advance(); left = new ModalAnd(left, ParseUnary()); }
            return left;
        }

        private ModalFormula ParseUnary()
        {
            switch (Peek().Kind)
            {
                case Kind.Not: Advance(); return new ModalNot(ParseUnary());
                case Kind.Box: Advance(); return new ModalBox(ParseUnary());
                case Kind.Diamond: Advance(); return new ModalDiamond(ParseUnary());
                default: return ParsePrimary();
            }
        }

        private ModalFormula ParsePrimary()
        {
            var tok = Peek();
            switch (tok.Kind)
            {
                case Kind.True: Advance(); return ModalFormula.True;
                case Kind.False: Advance(); return ModalFormula.False;
                case Kind.Id: Advance(); return new ModalAtom(tok.Text);
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
