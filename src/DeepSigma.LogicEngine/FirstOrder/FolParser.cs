namespace DeepSigma.LogicEngine.FirstOrder;

/// <summary>
/// Recursive-descent parser for first-order formulas. Quantifiers: <c>forall x. φ</c>,
/// <c>exists x. φ</c> (or ∀/∃). Connectives <c>! &amp; | -&gt; &lt;-&gt;</c>. Atoms are
/// <c>P(t, …)</c> / bare <c>P</c> (predicates) and <c>t = u</c> / <c>t != u</c>
/// (equality); terms are variables (those bound by an enclosing quantifier),
/// constants, and function applications <c>f(t, …)</c>.
/// </summary>
public static class FolParser
{
    public static FolFormula Parse(string source)
    {
        var state = new State(Tokenize(source));
        var formula = state.ParseFormula();
        state.Expect(Kind.End);
        return formula;
    }

    private enum Kind { Id, LParen, RParen, Comma, Dot, Eq, Neq, Not, And, Or, Implies, Iff, Forall, Exists, End }

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
                case ',': tokens.Add(new(Kind.Comma, ",")); i++; continue;
                case '.': tokens.Add(new(Kind.Dot, ".")); i++; continue;
                case '=': tokens.Add(new(Kind.Eq, "=")); i++; continue;
                case '&': tokens.Add(new(Kind.And, "&")); i++; continue;
                case '|': tokens.Add(new(Kind.Or, "|")); i++; continue;
                case '∀': tokens.Add(new(Kind.Forall, "∀")); i++; continue;
                case '∃': tokens.Add(new(Kind.Exists, "∃")); i++; continue;
                case '¬': tokens.Add(new(Kind.Not, "¬")); i++; continue;
                case '∧': tokens.Add(new(Kind.And, "∧")); i++; continue;
                case '∨': tokens.Add(new(Kind.Or, "∨")); i++; continue;
                case '→': tokens.Add(new(Kind.Implies, "→")); i++; continue;
                case '↔': tokens.Add(new(Kind.Iff, "↔")); i++; continue;
                case '!':
                    if (i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new(Kind.Neq, "!=")); i += 2; }
                    else { tokens.Add(new(Kind.Not, "!")); i++; }
                    continue;
                case '<':
                    if (Matches(s, i, "<->")) { tokens.Add(new(Kind.Iff, "<->")); i += 3; continue; }
                    throw new FormatException("Expected '<->'.");
                case '-':
                    if (Matches(s, i, "->")) { tokens.Add(new(Kind.Implies, "->")); i += 2; continue; }
                    throw new FormatException("Expected '->'.");
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) { i++; }
                var text = s[start..i];
                tokens.Add(new(Keyword(text), text));
                continue;
            }
            throw new FormatException($"Unexpected character '{c}' at position {i}.");
        }
        tokens.Add(new(Kind.End, string.Empty));
        return tokens;
    }

    private static bool Matches(string s, int i, string token)
        => i + token.Length <= s.Length && s.AsSpan(i, token.Length).SequenceEqual(token);

    private static Kind Keyword(string text) => text switch
    {
        "forall" => Kind.Forall,
        "exists" => Kind.Exists,
        "not" => Kind.Not,
        "and" => Kind.And,
        "or" => Kind.Or,
        _ => Kind.Id,
    };

    private sealed class State
    {
        private readonly List<Token> _tokens;
        private readonly HashSet<string> _bound = new(StringComparer.Ordinal);
        private int _pos;

        public State(List<Token> tokens) => _tokens = tokens;

        private Token Peek => _tokens[_pos];

        private Token Advance() => _tokens[_pos++];

        private bool Accept(Kind kind)
        {
            if (Peek.Kind == kind) { _pos++; return true; }
            return false;
        }

        public void Expect(Kind kind)
        {
            if (!Accept(kind))
            {
                throw new FormatException($"Expected {kind} but found '{Peek.Text}'.");
            }
        }

        public FolFormula ParseFormula() => ParseIff();

        private FolFormula ParseIff()
        {
            var left = ParseImplies();
            while (Accept(Kind.Iff))
            {
                left = new FolIff(left, ParseImplies());
            }
            return left;
        }

        private FolFormula ParseImplies()
        {
            var left = ParseOr();
            if (Accept(Kind.Implies))
            {
                return new FolImplies(left, ParseImplies());
            }
            return left;
        }

        private FolFormula ParseOr()
        {
            var left = ParseAnd();
            while (Accept(Kind.Or))
            {
                left = new FolOr(left, ParseAnd());
            }
            return left;
        }

        private FolFormula ParseAnd()
        {
            var left = ParseUnary();
            while (Accept(Kind.And))
            {
                left = new FolAnd(left, ParseUnary());
            }
            return left;
        }

        private FolFormula ParseUnary()
        {
            if (Accept(Kind.Not))
            {
                return new FolNot(ParseUnary());
            }
            if (Peek.Kind is Kind.Forall or Kind.Exists)
            {
                var isForall = Advance().Kind == Kind.Forall;
                var variable = ExpectId();
                Expect(Kind.Dot);
                var added = _bound.Add(variable);
                var body = ParseIff(); // quantifier body extends as far right as possible
                if (added)
                {
                    _bound.Remove(variable);
                }
                return isForall ? new FolForall(variable, body) : new FolExists(variable, body);
            }
            if (Accept(Kind.LParen))
            {
                var inner = ParseIff();
                Expect(Kind.RParen);
                return inner;
            }
            return ParseAtom();
        }

        private FolFormula ParseAtom()
        {
            var term = ParseTerm();
            if (Accept(Kind.Eq))
            {
                return new FolEquals(term, ParseTerm());
            }
            if (Accept(Kind.Neq))
            {
                return new FolNot(new FolEquals(term, ParseTerm()));
            }
            // No relation: the term must be a predicate application or a 0-ary predicate.
            return term is FolFunc f
                ? new FolPredicate(f.Symbol, f.Args)
                : throw new FormatException($"Expected a predicate or relation, found a variable '{term}'.");
        }

        private FolTerm ParseTerm()
        {
            var name = ExpectId();
            if (Accept(Kind.LParen))
            {
                var args = new List<FolTerm> { ParseTerm() };
                while (Accept(Kind.Comma))
                {
                    args.Add(ParseTerm());
                }
                Expect(Kind.RParen);
                return new FolFunc(name, args);
            }
            return _bound.Contains(name) ? new FolVar(name) : new FolFunc(name, Array.Empty<FolTerm>());
        }

        private string ExpectId()
        {
            if (Peek.Kind != Kind.Id)
            {
                throw new FormatException($"Expected an identifier but found '{Peek.Text}'.");
            }
            return Advance().Text;
        }
    }
}
