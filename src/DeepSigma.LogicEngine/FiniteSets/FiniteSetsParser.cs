namespace DeepSigma.LogicEngine.FiniteSets;

/// <summary>
/// Recursive-descent parser for finite-set formulas. Grammar (loosest to tightest):
/// <c>&lt;-&gt;</c>, <c>-&gt;</c>, <c>|</c>/<c>or</c>, <c>&amp;</c>/<c>and</c>,
/// <c>!</c>/<c>not</c>, then atoms. Atoms are membership (<c>x in A</c>), set
/// relations (<c>A &lt;= B</c>/<c>subset</c>, <c>A &lt; B</c>, <c>A = B</c>,
/// <c>A != B</c>, <c>A &gt;= B</c>, <c>A &gt; B</c>), <c>disjoint(A, B)</c>, and
/// cardinality (<c>|A| &lt;= k</c>). Set expressions use <c>+</c>/<c>union</c>/∪,
/// <c>*</c>/<c>inter</c>/∩, <c>~</c>/<c>compl</c> (prefix), <c>\</c> (difference),
/// <c>^</c>/Δ (symmetric difference), and the constants <c>U</c> and <c>empty</c>/∅.
/// </summary>
public static class FiniteSetsParser
{
    public static SetFormula Parse(string source)
    {
        var state = new State(Tokenize(source));
        var formula = state.ParseIff();
        state.Expect(Kind.End);
        return formula;
    }

    public static bool TryParse(string source, out SetFormula formula)
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

    private enum Kind
    {
        Id, Int, LParen, RParen, Comma, Bar,
        And, Not, Implies, Iff,
        Union, Inter, Compl, Diff, SymDiff,
        In, Subset, Lt, Le, Gt, Ge, Eq, Neq,
        Disjoint, Empty, Univ,
        End,
    }

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
                case '|': tokens.Add(new(Kind.Bar, "|")); i++; continue;
                case '&': tokens.Add(new(Kind.And, "&")); i++; continue;
                case '~': tokens.Add(new(Kind.Compl, "~")); i++; continue;
                case '+': case '∪': tokens.Add(new(Kind.Union, "∪")); i++; continue;
                case '*': case '∩': tokens.Add(new(Kind.Inter, "∩")); i++; continue;
                case '\\': tokens.Add(new(Kind.Diff, "\\")); i++; continue;
                case '^': case 'Δ': tokens.Add(new(Kind.SymDiff, "Δ")); i++; continue;
                case '∈': tokens.Add(new(Kind.In, "∈")); i++; continue;
                case '⊆': tokens.Add(new(Kind.Subset, "⊆")); i++; continue;
                case '⊂': tokens.Add(new(Kind.Lt, "⊂")); i++; continue;
                case '∅': tokens.Add(new(Kind.Empty, "∅")); i++; continue;
                case '=': tokens.Add(new(Kind.Eq, "=")); i++; continue;
                case '{':
                    if (i + 1 < s.Length && s[i + 1] == '}') { tokens.Add(new(Kind.Empty, "{}")); i += 2; continue; }
                    throw new FormatException("Expected '}' after '{'.");
                case '!':
                    if (i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new(Kind.Neq, "!=")); i += 2; }
                    else { tokens.Add(new(Kind.Not, "!")); i++; }
                    continue;
                case '<':
                    if (Match(s, i, "<->")) { tokens.Add(new(Kind.Iff, "<->")); i += 3; }
                    else if (Match(s, i, "<=")) { tokens.Add(new(Kind.Le, "<=")); i += 2; }
                    else { tokens.Add(new(Kind.Lt, "<")); i++; }
                    continue;
                case '>':
                    if (Match(s, i, ">=")) { tokens.Add(new(Kind.Ge, ">=")); i += 2; }
                    else { tokens.Add(new(Kind.Gt, ">")); i++; }
                    continue;
                case '-':
                    if (Match(s, i, "->")) { tokens.Add(new(Kind.Implies, "->")); i += 2; continue; }
                    throw new FormatException("Unexpected '-' (did you mean '->'?).");
            }

            if (char.IsDigit(c))
            {
                var start = i;
                while (i < s.Length && char.IsDigit(s[i])) { i++; }
                tokens.Add(new(Kind.Int, s[start..i]));
                continue;
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

    private static bool Match(string s, int i, string token)
        => i + token.Length <= s.Length && s.AsSpan(i, token.Length).SequenceEqual(token);

    private static Kind Keyword(string text) => text switch
    {
        "in" => Kind.In,
        "and" => Kind.And,
        "or" => Kind.Bar,
        "not" => Kind.Not,
        "union" => Kind.Union,
        "inter" => Kind.Inter,
        "compl" => Kind.Compl,
        "subset" => Kind.Subset,
        "disjoint" => Kind.Disjoint,
        "U" => Kind.Univ,
        "empty" => Kind.Empty,
        _ => Kind.Id,
    };

    private sealed class State
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public State(List<Token> tokens) => _tokens = tokens;

        private Token Peek(int offset = 0) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];

        private Token Advance() => _tokens[_pos++];

        private bool Check(Kind kind) => Peek().Kind == kind;

        private bool Accept(Kind kind)
        {
            if (Check(kind)) { _pos++; return true; }
            return false;
        }

        public void Expect(Kind kind)
        {
            if (!Accept(kind))
            {
                throw new FormatException($"Expected {kind} but found '{Peek().Text}'.");
            }
        }

        // ---- Formula grammar ----

        public SetFormula ParseIff()
        {
            var left = ParseImplies();
            while (Accept(Kind.Iff))
            {
                left = SetFormula.Iff(left, ParseImplies());
            }
            return left;
        }

        private SetFormula ParseImplies()
        {
            var left = ParseOr();
            if (Accept(Kind.Implies))
            {
                return SetFormula.Implies(left, ParseImplies()); // right-associative
            }
            return left;
        }

        private SetFormula ParseOr()
        {
            // After a complete left operand a '|' is the boolean-or operator; a
            // cardinality '|…|' only ever begins an operand (handled in ParseAtom).
            var left = ParseAnd();
            while (Accept(Kind.Bar))
            {
                left = SetFormula.Or(left, ParseAnd());
            }
            return left;
        }

        private SetFormula ParseAnd()
        {
            var left = ParsePrimary();
            while (Accept(Kind.And))
            {
                left = SetFormula.And(left, ParsePrimary());
            }
            return left;
        }

        private SetFormula ParsePrimary()
        {
            if (Accept(Kind.Not))
            {
                return SetFormula.Not(ParsePrimary());
            }
            if (Check(Kind.LParen))
            {
                var save = _pos;
                try
                {
                    Advance();
                    var inner = ParseIff();
                    Expect(Kind.RParen);
                    return inner;
                }
                catch (FormatException)
                {
                    _pos = save; // not a grouped formula; reparse as an atom (parenthesized set relation)
                }
            }
            return ParseAtom();
        }

        private SetFormula ParseAtom()
        {
            if (Check(Kind.Bar))
            {
                return ParseCardinality();
            }
            if (Accept(Kind.Disjoint))
            {
                Expect(Kind.LParen);
                var a = ParseSetExpr();
                Expect(Kind.Comma);
                var b = ParseSetExpr();
                Expect(Kind.RParen);
                return SetFormula.Disjoint(a, b);
            }
            if (Check(Kind.Id) && Peek(1).Kind == Kind.In)
            {
                var element = ElementExpr.Var(Advance().Text);
                Advance(); // 'in'
                return SetFormula.Member(element, ParseSetExpr());
            }
            return ParseRelation();
        }

        private SetFormula ParseCardinality()
        {
            Expect(Kind.Bar);
            var set = ParseSetExpr();
            Expect(Kind.Bar);
            var op = Advance();
            var bound = ParseInt();
            return op.Kind switch
            {
                Kind.Le => SetFormula.Card(set, CardOp.Le, bound),
                Kind.Lt => SetFormula.Card(set, CardOp.Lt, bound),
                Kind.Ge => SetFormula.Card(set, CardOp.Ge, bound),
                Kind.Gt => SetFormula.Card(set, CardOp.Gt, bound),
                Kind.Eq => SetFormula.Card(set, CardOp.Eq, bound),
                Kind.Neq => SetFormula.Not(SetFormula.Card(set, CardOp.Eq, bound)),
                _ => throw new FormatException($"Expected a comparison after '|...|' but found '{op.Text}'."),
            };
        }

        private SetFormula ParseRelation()
        {
            var left = ParseSetExpr();
            var op = Advance();
            var right = ParseSetExpr();
            return op.Kind switch
            {
                Kind.Subset or Kind.Le => SetFormula.Subset(left, right),
                Kind.Lt => SetFormula.ProperSubset(left, right),
                Kind.Ge => SetFormula.Subset(right, left),
                Kind.Gt => SetFormula.ProperSubset(right, left),
                Kind.Eq => SetFormula.Equal(left, right),
                Kind.Neq => SetFormula.Not(SetFormula.Equal(left, right)),
                _ => throw new FormatException($"Expected a set relation but found '{op.Text}'."),
            };
        }

        private int ParseInt()
        {
            if (!Check(Kind.Int))
            {
                throw new FormatException($"Expected an integer but found '{Peek().Text}'.");
            }
            return int.Parse(Advance().Text);
        }

        // ---- Set-expression grammar ----

        public SetExpr ParseSetExpr() => ParseSetUnion();

        private SetExpr ParseSetUnion()
        {
            var left = ParseSetInter();
            while (true)
            {
                if (Accept(Kind.Union)) { left = SetExpr.Union(left, ParseSetInter()); }
                else if (Accept(Kind.Diff)) { left = SetExpr.Difference(left, ParseSetInter()); }
                else if (Accept(Kind.SymDiff)) { left = SetExpr.SymmetricDifference(left, ParseSetInter()); }
                else { return left; }
            }
        }

        private SetExpr ParseSetInter()
        {
            var left = ParseSetUnary();
            while (Accept(Kind.Inter))
            {
                left = SetExpr.Intersect(left, ParseSetUnary());
            }
            return left;
        }

        private SetExpr ParseSetUnary()
        {
            if (Accept(Kind.Compl))
            {
                return SetExpr.Complement(ParseSetUnary());
            }
            return ParseSetPrimary();
        }

        private SetExpr ParseSetPrimary()
        {
            if (Accept(Kind.LParen))
            {
                var inner = ParseSetUnion();
                Expect(Kind.RParen);
                return inner;
            }
            if (Accept(Kind.Univ)) { return SetExpr.Universe; }
            if (Accept(Kind.Empty)) { return SetExpr.Empty; }
            if (Check(Kind.Id)) { return SetExpr.Var(Advance().Text); }
            throw new FormatException($"Expected a set expression but found '{Peek().Text}'.");
        }
    }
}
