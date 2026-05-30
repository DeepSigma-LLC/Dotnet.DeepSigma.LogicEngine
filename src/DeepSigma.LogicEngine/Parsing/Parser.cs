using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Parsing.Infrastructure;

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
        return new ParserState(tokens).ParseComplete();
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

    private sealed class ParserState : TokenReader<Token, TokenKind>
    {
        public ParserState(List<Token> tokens) : base(tokens, t => t.Kind, t => t.Text) { }

        public Formula ParseComplete()
        {
            var formula = ParseIff();
            Expect(TokenKind.End);
            return formula;
        }

        private Formula ParseIff() => ConnectiveChain.LeftAssoc(ParseImplies, () => Accept(TokenKind.Iff), (l, r) => new Biconditional(l, r));
        private Formula ParseImplies() => ConnectiveChain.RightAssoc(ParseOr, () => Accept(TokenKind.Implies), ParseImplies, (l, r) => new Implication(l, r));
        private Formula ParseOr() => ConnectiveChain.LeftAssoc(ParseAnd, () => Accept(TokenKind.Or), (l, r) => new Disjunction(l, r));
        private Formula ParseAnd() => ConnectiveChain.LeftAssoc(ParseNot, () => Accept(TokenKind.And), (l, r) => new Conjunction(l, r));

        private Formula ParseNot()
        {
            if (Accept(TokenKind.Not))
            {
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
