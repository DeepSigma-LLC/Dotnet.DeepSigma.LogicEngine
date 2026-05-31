using DeepSigma.LogicEngine.Parsing.Infrastructure;

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
        return new State(Tokenize(source)).ParseComplete();
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
                case '[': if (CharScanner.Peek(s, i) == ']') { tokens.Add(new(Kind.Box, "[]", i)); i += 2; continue; } break;
            }
            if (c == '<' && CharScanner.Peek(s, i) == '>') { tokens.Add(new(Kind.Diamond, "<>", i)); i += 2; continue; }
            if (c == '<' && i + 2 < s.Length && (s[i + 1] is '-' or '=') && s[i + 2] == '>') { tokens.Add(new(Kind.Iff, "<->", i)); i += 3; continue; }
            if ((c == '-' || c == '=') && CharScanner.Peek(s, i) == '>') { tokens.Add(new(Kind.Implies, "->", i)); i += 2; continue; }
            if (CharScanner.IsIdentifierStart(c))
            {
                var start = i;
                i = CharScanner.ReadWhile(s, i, CharScanner.IsIdentifierPart);
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

    private sealed class State : TokenReader<Token, Kind>
    {
        public State(List<Token> tokens) : base(tokens, t => t.Kind, t => t.Text) { }

        public ModalFormula ParseComplete()
        {
            var formula = ParseIff();
            Expect(Kind.End);
            return formula;
        }

        private ModalFormula ParseIff() => ConnectiveChain.LeftAssoc(ParseImplies, () => Accept(Kind.Iff), (l, r) => new ModalIff(l, r));
        private ModalFormula ParseImplies() => ConnectiveChain.RightAssoc(ParseOr, () => Accept(Kind.Implies), ParseImplies, (l, r) => new ModalImplies(l, r));
        private ModalFormula ParseOr() => ConnectiveChain.LeftAssoc(ParseAnd, () => Accept(Kind.Or), (l, r) => new ModalOr(l, r));
        private ModalFormula ParseAnd() => ConnectiveChain.LeftAssoc(ParseUnary, () => Accept(Kind.And), (l, r) => new ModalAnd(l, r));

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
