using DeepSigma.LogicEngine.Parsing.Infrastructure;

namespace DeepSigma.LogicEngine.Ctl;

/// <summary>
/// Recursive-descent parser for CTL. Unary temporal operators <c>EX EG EF AX AF AG</c>
/// prefix a formula; the until forms are <c>E[φ U ψ]</c> and <c>A[φ U ψ]</c>.
/// Connectives (loosest to tightest): <c>&lt;-&gt;</c>, <c>-&gt;</c>, <c>|</c>, <c>&amp;</c>,
/// <c>!</c>. Atoms are identifiers (<c>true</c>/<c>false</c> are the constants;
/// <c>EX,EG,EF,AX,AF,AG,E,A,U</c> are reserved).
/// </summary>
public static class CtlParser
{
    /// <summary>Parse a CTL formula, throwing on a syntax error.</summary>
    public static CtlFormula Parse(string source) => new State(Tokenize(source)).ParseComplete();

    /// <summary>Try to parse a CTL formula; returns false on a syntax error.</summary>
    public static bool TryParse(string source, out CtlFormula formula)
    {
        try { formula = Parse(source); return true; }
        catch (FormatException) { formula = null!; return false; }
    }

    private enum Kind { Id, LParen, RParen, LBracket, RBracket, Not, And, Or, Implies, Iff, End }

    private readonly record struct Token(Kind Kind, string Text);

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
                case '(': tokens.Add(new(Kind.LParen, "(")); i++; continue;
                case ')': tokens.Add(new(Kind.RParen, ")")); i++; continue;
                case '[': tokens.Add(new(Kind.LBracket, "[")); i++; continue;
                case ']': tokens.Add(new(Kind.RBracket, "]")); i++; continue;
                case '!': tokens.Add(new(Kind.Not, "!")); i++; continue;
                case '&': tokens.Add(new(Kind.And, "&")); i++; continue;
                case '|': tokens.Add(new(Kind.Or, "|")); i++; continue;
                case '<':
                    if (i + 2 < s.Length && s[i + 1] == '-' && s[i + 2] == '>') { tokens.Add(new(Kind.Iff, "<->")); i += 3; continue; }
                    throw new FormatException("Expected '<->'.");
                case '-':
                    if (i + 1 < s.Length && s[i + 1] == '>') { tokens.Add(new(Kind.Implies, "->")); i += 2; continue; }
                    throw new FormatException("Expected '->'.");
            }
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                var start = i;
                i = CharScanner.ReadWhile(s, i, CharScanner.IsIdentifierPart);
                tokens.Add(new(Kind.Id, s[start..i]));
                continue;
            }
            throw new FormatException($"Unexpected character '{c}' at position {i}.");
        }
        tokens.Add(new(Kind.End, string.Empty));
        return tokens;
    }

    private sealed class State : TokenReader<Token, Kind>
    {
        private static readonly HashSet<string> UnaryOps = new(StringComparer.Ordinal) { "EX", "EG", "EF", "AX", "AF", "AG" };

        public State(List<Token> tokens) : base(tokens, t => t.Kind, t => t.Text) { }

        public CtlFormula ParseComplete()
        {
            var formula = ParseIff();
            Expect(Kind.End);
            return formula;
        }

        private CtlFormula ParseIff() => ConnectiveChain.LeftAssoc(ParseImplies, () => Accept(Kind.Iff), (l, r) => new CtlIff(l, r));
        private CtlFormula ParseImplies() => ConnectiveChain.RightAssoc(ParseOr, () => Accept(Kind.Implies), ParseImplies, (l, r) => new CtlImplies(l, r));
        private CtlFormula ParseOr() => ConnectiveChain.LeftAssoc(ParseAnd, () => Accept(Kind.Or), (l, r) => new CtlOr(l, r));
        private CtlFormula ParseAnd() => ConnectiveChain.LeftAssoc(ParseUnary, () => Accept(Kind.And), (l, r) => new CtlAnd(l, r));

        private CtlFormula ParseUnary()
        {
            if (Accept(Kind.Not))
            {
                return new CtlNot(ParseUnary());
            }
            if (PeekKind() == Kind.Id && UnaryOps.Contains(Peek().Text))
            {
                var op = Advance().Text;
                var operand = ParseUnary();
                return op switch
                {
                    "EX" => new CtlEX(operand),
                    "EG" => new CtlEG(operand),
                    "EF" => new CtlEF(operand),
                    "AX" => new CtlAX(operand),
                    "AG" => new CtlAG(operand),
                    _ => new CtlAF(operand),
                };
            }
            if (PeekKind() == Kind.Id && (Peek().Text == "E" || Peek().Text == "A"))
            {
                var path = Advance().Text;
                Expect(Kind.LBracket);
                var left = ParseIff();
                ExpectKeyword("U");
                var right = ParseIff();
                Expect(Kind.RBracket);
                return path == "E" ? new CtlEU(left, right) : new CtlAU(left, right);
            }
            return ParsePrimary();
        }

        private CtlFormula ParsePrimary()
        {
            if (Accept(Kind.LParen))
            {
                var inner = ParseIff();
                Expect(Kind.RParen);
                return inner;
            }
            if (PeekKind() == Kind.Id)
            {
                var text = Advance().Text;
                return text switch
                {
                    "true" => CtlFormula.True,
                    "false" => CtlFormula.False,
                    _ => new CtlAtom(text),
                };
            }
            throw new FormatException($"Expected a formula but found '{Peek().Text}'.");
        }

        private void ExpectKeyword(string keyword)
        {
            if (PeekKind() != Kind.Id || Peek().Text != keyword)
            {
                throw new FormatException($"Expected '{keyword}' but found '{Peek().Text}'.");
            }
            Advance();
        }
    }
}
