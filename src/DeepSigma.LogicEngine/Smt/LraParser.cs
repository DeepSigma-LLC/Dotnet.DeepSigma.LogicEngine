using System.Globalization;
using System.Numerics;
using DeepSigma.Mathematics.Algebra;
using DeepSigma.Mathematics.Optimization.Exact;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// Recursive-descent parser for quantifier-free linear arithmetic formulas, e.g.
/// <c>2*x + 3*y &lt;= 5 &amp; (x = y | x &gt;= 1)</c>. Connectives use the same
/// precedence as the propositional parser. Atoms are <c>linExpr REL linExpr</c>
/// with <c>REL ∈ {&lt;=, &lt;, &gt;=, &gt;, =, !=}</c>; <c>=</c> expands to a
/// conjunction of <c>≤</c>/<c>≥</c> and <c>!=</c> to a disjunction of
/// <c>&lt;</c>/<c>&gt;</c>, so the theory never sees a (negated) equality.
/// </summary>
public static class LraParser
{
    public static SmtFormula Parse(string source)
    {
        var tokens = Tokenize(source);
        var state = new State(tokens);
        var formula = state.ParseIff();
        state.Expect(TokenKind.End);
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

    private enum TokenKind
    {
        Identifier, Number, Plus, Minus, Star,
        Le, Lt, Ge, Gt, Eq, Neq,
        Not, And, Or, Implies, Iff,
        True, False, LParen, RParen, End,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
        char Next(int k) => i + k < s.Length ? s[i + k] : '\0';

        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c)
            {
                case '(': tokens.Add(new(TokenKind.LParen, "(", i++)); continue;
                case ')': tokens.Add(new(TokenKind.RParen, ")", i++)); continue;
                case '+': tokens.Add(new(TokenKind.Plus, "+", i++)); continue;
                case '*': tokens.Add(new(TokenKind.Star, "*", i++)); continue;
                case '~': tokens.Add(new(TokenKind.Not, "~", i++)); continue;
                case '&': tokens.Add(new(TokenKind.And, "&", Next(1) == '&' ? Advance2(ref i) : i++)); continue;
                case '|': tokens.Add(new(TokenKind.Or, "|", Next(1) == '|' ? Advance2(ref i) : i++)); continue;
            }
            if (c == '!' && Next(1) == '=') { tokens.Add(new(TokenKind.Neq, "!=", i)); i += 2; continue; }
            if (c == '!') { tokens.Add(new(TokenKind.Not, "!", i++)); continue; }
            if (c == '<' && Next(1) == '=') { tokens.Add(new(TokenKind.Le, "<=", i)); i += 2; continue; }
            if (c == '<' && Next(1) == '-' && Next(2) == '>') { tokens.Add(new(TokenKind.Iff, "<->", i)); i += 3; continue; }
            if (c == '<') { tokens.Add(new(TokenKind.Lt, "<", i++)); continue; }
            if (c == '>' && Next(1) == '=') { tokens.Add(new(TokenKind.Ge, ">=", i)); i += 2; continue; }
            if (c == '>') { tokens.Add(new(TokenKind.Gt, ">", i++)); continue; }
            if (c == '=' && Next(1) == '>') { tokens.Add(new(TokenKind.Implies, "=>", i)); i += 2; continue; }
            if (c == '-' && Next(1) == '>') { tokens.Add(new(TokenKind.Implies, "->", i)); i += 2; continue; }
            if (c == '-') { tokens.Add(new(TokenKind.Minus, "-", i++)); continue; }
            if (c == '=') { tokens.Add(new(TokenKind.Eq, "=", i++)); continue; }
            if (char.IsDigit(c))
            {
                var start = i;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                tokens.Add(new(TokenKind.Number, s[start..i], start));
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var text = s[start..i];
                var kind = text switch
                {
                    "true" or "True" => TokenKind.True,
                    "false" or "False" => TokenKind.False,
                    "and" or "AND" => TokenKind.And,
                    "or" or "OR" => TokenKind.Or,
                    "not" or "NOT" => TokenKind.Not,
                    _ => TokenKind.Identifier,
                };
                tokens.Add(new(kind, text, start));
                continue;
            }
            // '->' starting with '-' is handled above; a lone '-' was already emitted as Minus.
            throw new FormatException($"Unexpected character '{c}' at position {i}.");
        }
        tokens.Add(new(TokenKind.End, string.Empty, s.Length));
        return tokens;
    }

    private static int Advance2(ref int i)
    {
        var pos = i;
        i += 2;
        return pos;
    }

    private sealed class State
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public State(List<Token> tokens) => _tokens = tokens;

        private Token Peek() => _tokens[_pos];
        private Token Advance() => _tokens[_pos++];

        public void Expect(TokenKind kind)
        {
            if (Peek().Kind != kind)
            {
                throw new FormatException($"Expected {kind} at position {Peek().Position}, got '{Peek().Text}'.");
            }
            Advance();
        }

        public SmtFormula ParseIff()
        {
            var left = ParseImplies();
            while (Peek().Kind == TokenKind.Iff)
            {
                Advance();
                left = new SmtIff(left, ParseImplies());
            }
            return left;
        }

        private SmtFormula ParseImplies()
        {
            var left = ParseOr();
            if (Peek().Kind == TokenKind.Implies)
            {
                Advance();
                return new SmtImplies(left, ParseImplies());
            }
            return left;
        }

        private SmtFormula ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Kind == TokenKind.Or)
            {
                Advance();
                left = new SmtOr(left, ParseAnd());
            }
            return left;
        }

        private SmtFormula ParseAnd()
        {
            var left = ParseNot();
            while (Peek().Kind == TokenKind.And)
            {
                Advance();
                left = new SmtAnd(left, ParseNot());
            }
            return left;
        }

        private SmtFormula ParseNot()
        {
            if (Peek().Kind == TokenKind.Not)
            {
                Advance();
                return new SmtNot(ParseNot());
            }
            return ParsePrimary();
        }

        private SmtFormula ParsePrimary()
        {
            switch (Peek().Kind)
            {
                case TokenKind.True:
                    Advance();
                    return SmtFormula.True;
                case TokenKind.False:
                    Advance();
                    return SmtFormula.False;
                case TokenKind.LParen:
                    // A parenthesised group could be a sub-formula or a linear expression
                    // like (x + 1). Try a formula first; an expression starts an atom.
                    return ParseParenthesisedOrAtom();
                default:
                    return ParseAtom();
            }
        }

        private SmtFormula ParseParenthesisedOrAtom()
        {
            // Disambiguate by scanning for a relation before the matching ')'.
            if (GroupIsFormula())
            {
                Advance(); // (
                var inner = ParseIff();
                Expect(TokenKind.RParen);
                return inner;
            }
            return ParseAtom();
        }

        private bool GroupIsFormula()
        {
            // Look ahead from the '(' to its matching ')'; if a connective appears at
            // depth 1 it is a formula group, otherwise it is a linear expression.
            var depth = 0;
            for (var k = _pos; k < _tokens.Count; k++)
            {
                switch (_tokens[k].Kind)
                {
                    case TokenKind.LParen: depth++; break;
                    case TokenKind.RParen:
                        depth--;
                        if (depth == 0) return false;
                        break;
                    case TokenKind.And or TokenKind.Or or TokenKind.Implies or TokenKind.Iff or TokenKind.Not
                        when depth == 1:
                        return true;
                    case TokenKind.End:
                        return false;
                }
            }
            return false;
        }

        private SmtFormula ParseAtom()
        {
            var (leftTerms, leftConstant) = ParseLinearExpression();
            var relation = ParseRelation(out var isEquality, out var isDisequality);
            var (rightTerms, rightConstant) = ParseLinearExpression();

            var terms = Combine(leftTerms, rightTerms, subtractRight: true);
            var constant = rightConstant - leftConstant;
            var ordered = terms.Select(kv => new LinearAtomTerm(kv.Key, kv.Value)).ToList();

            if (isEquality)
            {
                return new SmtAnd(
                    new LinearConstraintAtom(ordered, LinearRelation.LessOrEqual, constant),
                    new LinearConstraintAtom(ordered, LinearRelation.GreaterOrEqual, constant));
            }
            if (isDisequality)
            {
                return new SmtOr(
                    new LinearConstraintAtom(ordered, LinearRelation.Less, constant),
                    new LinearConstraintAtom(ordered, LinearRelation.Greater, constant));
            }
            return new LinearConstraintAtom(ordered, relation, constant);
        }

        private LinearRelation ParseRelation(out bool isEquality, out bool isDisequality)
        {
            isEquality = false;
            isDisequality = false;
            var tok = Peek();
            switch (tok.Kind)
            {
                case TokenKind.Le: Advance(); return LinearRelation.LessOrEqual;
                case TokenKind.Lt: Advance(); return LinearRelation.Less;
                case TokenKind.Ge: Advance(); return LinearRelation.GreaterOrEqual;
                case TokenKind.Gt: Advance(); return LinearRelation.Greater;
                case TokenKind.Eq: Advance(); isEquality = true; return LinearRelation.Equal;
                case TokenKind.Neq: Advance(); isDisequality = true; return LinearRelation.Equal;
                default:
                    throw new FormatException($"Expected a relation at position {tok.Position}, got '{tok.Text}'.");
            }
        }

        private (SortedDictionary<string, Rational> Terms, Rational Constant) ParseLinearExpression()
        {
            var terms = new SortedDictionary<string, Rational>(StringComparer.Ordinal);
            var constant = Rational.Zero;
            var sign = Rational.One;

            ParseProductInto(terms, ref constant, sign);
            while (Peek().Kind is TokenKind.Plus or TokenKind.Minus)
            {
                sign = Advance().Kind == TokenKind.Plus ? Rational.One : -Rational.One;
                ParseProductInto(terms, ref constant, sign);
            }
            return (terms, constant);
        }

        private void ParseProductInto(SortedDictionary<string, Rational> terms, ref Rational constant, Rational sign)
        {
            // term: number ['*' var] | var | '-' term
            if (Peek().Kind == TokenKind.Minus)
            {
                Advance();
                ParseProductInto(terms, ref constant, -sign);
                return;
            }
            if (Peek().Kind == TokenKind.Number)
            {
                var n = Rational.Of(BigInteger.Parse(Advance().Text, CultureInfo.InvariantCulture));
                if (Peek().Kind == TokenKind.Star)
                {
                    Advance();
                    var variable = ExpectIdentifier();
                    AddTerm(terms, variable, sign * n);
                }
                else
                {
                    constant += sign * n;
                }
                return;
            }
            if (Peek().Kind == TokenKind.Identifier)
            {
                AddTerm(terms, Advance().Text, sign);
                return;
            }
            throw new FormatException($"Expected a term at position {Peek().Position}, got '{Peek().Text}'.");
        }

        private string ExpectIdentifier()
        {
            if (Peek().Kind != TokenKind.Identifier)
            {
                throw new FormatException($"Expected a variable at position {Peek().Position}, got '{Peek().Text}'.");
            }
            return Advance().Text;
        }

        private static void AddTerm(SortedDictionary<string, Rational> terms, string variable, Rational coefficient)
        {
            var sum = terms.TryGetValue(variable, out var existing) ? existing + coefficient : coefficient;
            if (sum.IsZero)
            {
                terms.Remove(variable);
            }
            else
            {
                terms[variable] = sum;
            }
        }

        private static SortedDictionary<string, Rational> Combine(
            SortedDictionary<string, Rational> left, SortedDictionary<string, Rational> right, bool subtractRight)
        {
            var result = new SortedDictionary<string, Rational>(left, StringComparer.Ordinal);
            foreach (var (variable, coeff) in right)
            {
                AddTerm(result, variable, subtractRight ? -coeff : coeff);
            }
            return result;
        }
    }
}
