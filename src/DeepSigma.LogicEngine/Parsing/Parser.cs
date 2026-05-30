using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Parsing;

/// <summary>
/// Recursive-descent parser. Precedence low → high:
/// <c>&lt;-&gt;</c> ; <c>-&gt;</c> (right-associative) ; <c>|</c> ; <c>&amp;</c> ; <c>!</c> ; atom/parens.
/// </summary>
public static class Parser
{
    public static Formula Parse(string source)
    {
        var tokens = Lexer.Tokenize(source);
        var p = new ParserState(tokens);
        var result = p.ParseIff();
        p.Expect(TokenKind.End);
        return result;
    }

    public static bool TryParse(string source, out Formula formula)
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

    private sealed class ParserState
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public ParserState(List<Token> tokens) => _tokens = tokens;

        private Token Peek() => _tokens[_pos];
        private Token Advance() => _tokens[_pos++];

        public void Expect(TokenKind kind)
        {
            if (Peek().Kind != kind)
            {
                throw new FormatException($"Expected {kind} at position {Peek().Position}, got '{Peek().Text}' ({Peek().Kind}).");
            }
            Advance();
        }

        public Formula ParseIff()
        {
            var left = ParseImplies();
            while (Peek().Kind == TokenKind.Iff)
            {
                Advance();
                var right = ParseImplies();
                left = new Biconditional(left, right);
            }
            return left;
        }

        private Formula ParseImplies()
        {
            var left = ParseOr();
            if (Peek().Kind == TokenKind.Implies)
            {
                Advance();
                var right = ParseImplies();
                return new Implication(left, right);
            }
            return left;
        }

        private Formula ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Kind == TokenKind.Or)
            {
                Advance();
                var right = ParseAnd();
                left = new Disjunction(left, right);
            }
            return left;
        }

        private Formula ParseAnd()
        {
            var left = ParseNot();
            while (Peek().Kind == TokenKind.And)
            {
                Advance();
                var right = ParseNot();
                left = new Conjunction(left, right);
            }
            return left;
        }

        private Formula ParseNot()
        {
            if (Peek().Kind == TokenKind.Not)
            {
                Advance();
                return new Negation(ParseNot());
            }
            return ParseAtom();
        }

        private Formula ParseAtom()
        {
            var tok = Peek();
            switch (tok.Kind)
            {
                case TokenKind.True:
                    Advance();
                    return Formula.True;
                case TokenKind.False:
                    Advance();
                    return Formula.False;
                case TokenKind.Identifier:
                    Advance();
                    return new Variable(tok.Text);
                case TokenKind.LParen:
                    Advance();
                    var inner = ParseIff();
                    Expect(TokenKind.RParen);
                    return inner;
                default:
                    throw new FormatException($"Unexpected token '{tok.Text}' ({tok.Kind}) at position {tok.Position}.");
            }
        }
    }
}
