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
    public static SmtFormula Parse(string source)
    {
        var tokens = Tokenize(source);
        var state = new State(tokens);
        var formula = state.ParseIff();
        state.Expect(SmtTokenKind.End);
        return formula;
    }

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
                if (Next(source, i) == '=')
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
                if (Next(source, i) == '>')
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
                var len = Next(source, i) == '&' ? 2 : 1;
                tokens.Add(new SmtToken(SmtTokenKind.And, source.Substring(i, len), i));
                i += len;
                continue;
            }
            if (ch == '|')
            {
                var len = Next(source, i) == '|' ? 2 : 1;
                tokens.Add(new SmtToken(SmtTokenKind.Or, source.Substring(i, len), i));
                i += len;
                continue;
            }
            if (ch == '/' && Next(source, i) == '\\')
            {
                tokens.Add(new SmtToken(SmtTokenKind.And, "/\\", i));
                i += 2;
                continue;
            }
            if (ch == '\\' && Next(source, i) == '/')
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
            if (ch == '-' && Next(source, i) == '>')
            {
                tokens.Add(new SmtToken(SmtTokenKind.Implies, "->", i));
                i += 2;
                continue;
            }
            if (char.IsLetter(ch) || ch == '_')
            {
                var start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    i++;
                }
                var text = source.Substring(start, i - start);
                tokens.Add(new SmtToken(KeywordKind(text), text, start));
                continue;
            }
            throw new FormatException($"Unexpected character '{ch}' at position {i}.");
        }
        tokens.Add(new SmtToken(SmtTokenKind.End, string.Empty, source.Length));
        return tokens;
    }

    private static char Next(string source, int i) => i + 1 < source.Length ? source[i + 1] : '\0';

    private static SmtTokenKind KeywordKind(string text) => text switch
    {
        "true" or "True" or "TRUE" => SmtTokenKind.True,
        "false" or "False" or "FALSE" => SmtTokenKind.False,
        "and" or "AND" => SmtTokenKind.And,
        "or" or "OR" => SmtTokenKind.Or,
        "not" or "NOT" => SmtTokenKind.Not,
        _ => SmtTokenKind.Identifier,
    };

    private sealed class State
    {
        private readonly List<SmtToken> _tokens;
        private int _pos;

        public State(List<SmtToken> tokens) => _tokens = tokens;

        private SmtToken Peek() => _tokens[_pos];
        private SmtToken Advance() => _tokens[_pos++];

        public void Expect(SmtTokenKind kind)
        {
            if (Peek().Kind != kind)
            {
                throw new FormatException($"Expected {kind} at position {Peek().Position}, got '{Peek().Text}' ({Peek().Kind}).");
            }
            Advance();
        }

        public SmtFormula ParseIff()
        {
            var left = ParseImplies();
            while (Peek().Kind == SmtTokenKind.Iff)
            {
                Advance();
                left = new SmtIff(left, ParseImplies());
            }
            return left;
        }

        private SmtFormula ParseImplies()
        {
            var left = ParseOr();
            if (Peek().Kind == SmtTokenKind.Implies)
            {
                Advance();
                return new SmtImplies(left, ParseImplies());
            }
            return left;
        }

        private SmtFormula ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Kind == SmtTokenKind.Or)
            {
                Advance();
                left = new SmtOr(left, ParseAnd());
            }
            return left;
        }

        private SmtFormula ParseAnd()
        {
            var left = ParseNot();
            while (Peek().Kind == SmtTokenKind.And)
            {
                Advance();
                left = new SmtAnd(left, ParseNot());
            }
            return left;
        }

        private SmtFormula ParseNot()
        {
            if (Peek().Kind == SmtTokenKind.Not)
            {
                Advance();
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
