using DeepSigma.LogicEngine.Parsing.Infrastructure;

namespace DeepSigma.LogicEngine.Smt;

internal enum SmtTokenKind
{
    Identifier,
    True,
    False,
    Not,
    And,
    Or,
    Implies,
    Iff,
    Eq,
    NotEq,
    LParen,
    RParen,
    Comma,
    End,
}

internal readonly record struct SmtToken(SmtTokenKind Kind, string Text, int Position);

/// <summary>
/// Recursive-descent parser for EUF formulas. Precedence (low → high):
/// <c>&lt;-&gt;</c> ; <c>-&gt;</c> (right-assoc) ; <c>|</c> ; <c>&amp;</c> ;
/// <c>!</c> ; atom. An atom is <c>term (= | !=) term</c>, a predicate
/// application <c>P(args)</c> / bare <c>P</c>, or <c>true</c>/<c>false</c>.
/// </summary>
public static class SmtParser
{
    /// <summary>Parse an EUF formula, throwing on a syntax error.</summary>
    public static SmtFormula Parse(string source)
    {
        return new State(Tokenize(source)).ParseComplete();
    }

    /// <summary>Try to parse an EUF formula; returns false on a syntax error.</summary>
    public static bool TryParse(string source, out SmtFormula formula)
    {
        try
        {
            formula = Parse(source);
            return true;
        }
        catch (FormatException)
        {
            formula = null!;
            return false;
        }
    }

    private static List<SmtToken> Tokenize(string source)
    {
        var tokens = new List<SmtToken>();
        var i = 0;
        while (i < source.Length)
        {
            var ch = source[i];
            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }
            switch (ch)
            {
                case '(':
                    tokens.Add(new SmtToken(SmtTokenKind.LParen, "(", i++));
                    continue;
                case ')':
                    tokens.Add(new SmtToken(SmtTokenKind.RParen, ")", i++));
                    continue;
                case ',':
                    tokens.Add(new SmtToken(SmtTokenKind.Comma, ",", i++));
                    continue;
                case '~':
                    tokens.Add(new SmtToken(SmtTokenKind.Not, "~", i++));
                    continue;
            }
            if (ch == '!')
            {
                if (CharScanner.Peek(source, i) == '=')
                {
                    tokens.Add(new SmtToken(SmtTokenKind.NotEq, "!=", i));
                    i += 2;
                }
                else
                {
                    tokens.Add(new SmtToken(SmtTokenKind.Not, "!", i++));
                }
                continue;
            }
            if (ch == '=')
            {
                if (CharScanner.Peek(source, i) == '>')
                {
                    tokens.Add(new SmtToken(SmtTokenKind.Implies, "=>", i));
                    i += 2;
                }
                else
                {
                    tokens.Add(new SmtToken(SmtTokenKind.Eq, "=", i++));
                }
                continue;
            }
            if (ch == '&')
            {
                var len = CharScanner.Peek(source, i) == '&' ? 2 : 1;
                tokens.Add(new SmtToken(SmtTokenKind.And, source.Substring(i, len), i));
                i += len;
                continue;
            }
            if (ch == '|')
            {
                var len = CharScanner.Peek(source, i) == '|' ? 2 : 1;
                tokens.Add(new SmtToken(SmtTokenKind.Or, source.Substring(i, len), i));
                i += len;
                continue;
            }
            if (ch == '/' && CharScanner.Peek(source, i) == '\\')
            {
                tokens.Add(new SmtToken(SmtTokenKind.And, "/\\", i));
                i += 2;
                continue;
            }
            if (ch == '\\' && CharScanner.Peek(source, i) == '/')
            {
                tokens.Add(new SmtToken(SmtTokenKind.Or, "\\/", i));
                i += 2;
                continue;
            }
            if (ch == '<' && i + 2 < source.Length && source[i + 1] is '-' or '=' && source[i + 2] == '>')
            {
                tokens.Add(new SmtToken(SmtTokenKind.Iff, source.Substring(i, 3), i));
                i += 3;
                continue;
            }
            if (ch == '-' && CharScanner.Peek(source, i) == '>')
            {
                tokens.Add(new SmtToken(SmtTokenKind.Implies, "->", i));
                i += 2;
                continue;
            }
            if (CharScanner.IsIdentifierStart(ch))
            {
                var start = i;
                i = CharScanner.ReadWhile(source, i, CharScanner.IsIdentifierPart);
                var text = source.Substring(start, i - start);
                tokens.Add(new SmtToken(KeywordKind(text), text, start));
                continue;
            }
            throw new FormatException($"Unexpected character '{ch}' at position {i}.");
        }
        tokens.Add(new SmtToken(SmtTokenKind.End, string.Empty, source.Length));
        return tokens;
    }

    private static SmtTokenKind KeywordKind(string text) => text switch
    {
        "true" or "True" or "TRUE" => SmtTokenKind.True,
        "false" or "False" or "FALSE" => SmtTokenKind.False,
        "and" or "AND" => SmtTokenKind.And,
        "or" or "OR" => SmtTokenKind.Or,
        "not" or "NOT" => SmtTokenKind.Not,
        _ => SmtTokenKind.Identifier,
    };

    private sealed class State : TokenReader<SmtToken, SmtTokenKind>
    {
        public State(List<SmtToken> tokens) : base(tokens, t => t.Kind, t => t.Text) { }

        public SmtFormula ParseComplete()
        {
            var formula = ParseIff();
            Expect(SmtTokenKind.End);
            return formula;
        }

        private SmtFormula ParseIff() => ConnectiveChain.LeftAssoc(ParseImplies, () => Accept(SmtTokenKind.Iff), (l, r) => new SmtIff(l, r));
        private SmtFormula ParseImplies() => ConnectiveChain.RightAssoc(ParseOr, () => Accept(SmtTokenKind.Implies), ParseImplies, (l, r) => new SmtImplies(l, r));
        private SmtFormula ParseOr() => ConnectiveChain.LeftAssoc(ParseAnd, () => Accept(SmtTokenKind.Or), (l, r) => new SmtOr(l, r));
        private SmtFormula ParseAnd() => ConnectiveChain.LeftAssoc(ParseNot, () => Accept(SmtTokenKind.And), (l, r) => new SmtAnd(l, r));

        private SmtFormula ParseNot()
        {
            if (Accept(SmtTokenKind.Not))
            {
                return new SmtNot(ParseNot());
            }
            return ParsePrimary();
        }

        private SmtFormula ParsePrimary()
        {
            var tok = Peek();
            switch (tok.Kind)
            {
                case SmtTokenKind.True:
                    Advance();
                    return SmtFormula.True;
                case SmtTokenKind.False:
                    Advance();
                    return SmtFormula.False;
                case SmtTokenKind.LParen:
                    Advance();
                    var inner = ParseIff();
                    Expect(SmtTokenKind.RParen);
                    return inner;
                case SmtTokenKind.Identifier:
                    return ParseAtom();
                default:
                    throw new FormatException($"Unexpected token '{tok.Text}' ({tok.Kind}) at position {tok.Position}.");
            }
        }

        /// <summary>An atom is a term followed optionally by (= | !=) term; a bare term is a predicate.</summary>
        private SmtFormula ParseAtom()
        {
            var term = ParseTerm();
            switch (Peek().Kind)
            {
                case SmtTokenKind.Eq:
                    Advance();
                    return new EqualityAtom(term, ParseTerm());
                case SmtTokenKind.NotEq:
                    Advance();
                    return new SmtNot(new EqualityAtom(term, ParseTerm()));
                default:
                    return new PredicateAtom(term.Symbol, term.Arguments);
            }
        }

        private Term ParseTerm()
        {
            var tok = Peek();
            if (tok.Kind != SmtTokenKind.Identifier)
            {
                throw new FormatException($"Expected a term at position {tok.Position}, got '{tok.Text}' ({tok.Kind}).");
            }
            Advance();
            if (Peek().Kind != SmtTokenKind.LParen)
            {
                return Term.Constant(tok.Text);
            }
            Advance();
            var arguments = new List<Term> { ParseTerm() };
            while (Peek().Kind == SmtTokenKind.Comma)
            {
                Advance();
                arguments.Add(ParseTerm());
            }
            Expect(SmtTokenKind.RParen);
            return new Term(tok.Text, arguments);
        }
    }
}
