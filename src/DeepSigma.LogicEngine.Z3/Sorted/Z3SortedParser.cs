using System.Globalization;
using System.Numerics;
using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.Z3.Sorted;

/// <summary>
/// Parses the textual syntax for the typed Z3 sorted layer into a <see cref="SortedExpr"/>.
///
/// <para>
/// Because the layer is multi-sorted, the syntax opens with a (possibly empty) <b>declaration
/// prefix</b> that gives every free variable a sort, followed by a single boolean expression:
/// </para>
/// <code>
/// bv8 x, y;  x + 1 == 0 &amp; y == ~x
/// int n;     forall int n . n + 1 &gt; n
/// real x;    2*x == 1
/// string s;  s ++ "bar" == "foobar" &amp; |s| == 3
/// </code>
///
/// <para>
/// <b>Sorts:</b> <c>bool</c>, <c>int</c>, <c>real</c>, <c>string</c>, and <c>bv&lt;N&gt;</c>
/// (a width-<c>N</c> bit-vector, e.g. <c>bv8</c>). <b>Connectives:</b> <c>!</c> (not), <c>&amp;</c>
/// (and), <c>|</c> (or), <c>-&gt;</c> (implies), <c>&lt;-&gt;</c> (iff). <b>Relations:</b> <c>==</c>,
/// <c>!=</c>, and <c>&lt; &lt;= &gt; &gt;=</c> (arithmetic only). <b>Arithmetic:</b> <c>+ - *</c>
/// and unary <c>-</c> (bit-vector algebra for bit-vectors, ordinary arithmetic for int/real).
/// <b>Strings:</b> <c>++</c> (concat) and <c>|s|</c> (length). <b>Bit-vector</b> bitwise-not is the
/// prefix <c>~</c>; the remaining bit-vector and string operations are function-style:
/// <c>bvand bvor bvxor shl lshr ashr ult ule ugt uge slt sle sgt sge concat extract</c> and
/// <c>contains prefixof suffixof len</c>. <b>Quantifiers:</b> <c>forall</c>/<c>exists</c> with typed
/// binders, e.g. <c>forall int x, int y . x + y == y + x</c>. <b>Literals:</b> decimals (typed from
/// context), <c>#b1010</c>/<c>#xAB</c> bit-vector literals (width from the digits), <c>p/q</c>
/// rationals, and <c>"..."</c> strings.
/// </para>
/// </summary>
public static class Z3SortedParser
{
    /// <summary>Parses a sorted-layer expression. Throws <see cref="FormatException"/> on malformed input.</summary>
    public static SortedExpr Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new State(Lexer.Tokenize(source)).ParseProgram();
    }

    /// <summary>Attempts to parse a sorted-layer expression, returning false on malformed input.</summary>
    public static bool TryParse(string source, out SortedExpr expression)
    {
        try
        {
            expression = Parse(source);
            return true;
        }
        catch (FormatException)
        {
            expression = null!;
            return false;
        }
    }

    // The known function-style operators (name -> arity), so an identifier followed by '(' can be
    // told apart from a variable use.
    private static readonly IReadOnlyDictionary<string, int> Functions = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["bvand"] = 2, ["bvor"] = 2, ["bvxor"] = 2, ["bvnot"] = 1,
        ["shl"] = 2, ["lshr"] = 2, ["ashr"] = 2,
        ["ult"] = 2, ["ule"] = 2, ["ugt"] = 2, ["uge"] = 2,
        ["slt"] = 2, ["sle"] = 2, ["sgt"] = 2, ["sge"] = 2,
        ["concat"] = 2, ["extract"] = 3,
        ["contains"] = 2, ["prefixof"] = 2, ["suffixof"] = 2, ["len"] = 1,
    };

    private sealed class State
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _pos;
        private readonly Dictionary<string, Sort> _declared = new(StringComparer.Ordinal);
        private readonly List<Dictionary<string, Sort>> _scopes = new();

        public State(IReadOnlyList<Token> tokens) => _tokens = tokens;

        // --- token cursor ---

        private Token Peek(int offset = 0) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];
        private TokenKind Kind => Peek().Kind;
        private Token Advance() => _tokens[_pos++];
        private bool Accept(TokenKind kind)
        {
            if (Kind != kind) return false;
            _pos++;
            return true;
        }

        private Token Expect(TokenKind kind, string what)
        {
            if (Kind != kind)
            {
                throw new FormatException($"Expected {what} at position {Peek().Position}, got '{Peek().Text}'.");
            }
            return Advance();
        }

        // --- program: declarations then a boolean expression ---

        public SortedExpr ParseProgram()
        {
            while (IsSortStart())
            {
                ParseDeclaration();
            }
            var expression = ParseIff(null);
            Expect(TokenKind.End, "end of input");
            if (expression.Sort is not BoolSort)
            {
                throw new FormatException("The top-level expression must be boolean-sorted.");
            }
            return expression;
        }

        private bool IsSortStart()
            => Kind == TokenKind.Identifier && TryReadSortName(Peek().Text, out _);

        private void ParseDeclaration()
        {
            var sort = ParseSort();
            do
            {
                var name = Expect(TokenKind.Identifier, "a variable name").Text;
                if (Functions.ContainsKey(name) || TryReadSortName(name, out _))
                {
                    throw new FormatException($"'{name}' is a reserved word and cannot be a variable name.");
                }
                if (!_declared.TryAdd(name, sort))
                {
                    throw new FormatException($"Variable '{name}' is declared more than once.");
                }
            }
            while (Accept(TokenKind.Comma));
            Expect(TokenKind.Semicolon, "';' after a declaration");
        }

        private Sort ParseSort()
        {
            var tok = Expect(TokenKind.Identifier, "a sort (bool / int / real / string / bv<N>)");
            if (!TryReadSortName(tok.Text, out var sort))
            {
                throw new FormatException($"Unknown sort '{tok.Text}' at position {tok.Position}.");
            }
            return sort;
        }

        private static bool TryReadSortName(string text, out Sort sort)
        {
            switch (text)
            {
                case "bool": sort = Sort.Bool; return true;
                case "int": sort = Sort.Int; return true;
                case "real": sort = Sort.Real; return true;
                case "string": sort = Sort.String; return true;
            }
            if (text.StartsWith("bv", StringComparison.Ordinal) && text.Length > 2
                && int.TryParse(text.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var width) && width > 0)
            {
                sort = Sort.BitVec(width);
                return true;
            }
            sort = null!;
            return false;
        }

        // --- precedence ladder (lowest to highest) ---

        private SortedExpr ParseIff(Sort? expected)
        {
            var left = ParseImplies(expected);
            while (Accept(TokenKind.Iff))
            {
                left = SortedExpr.Iff(RequireBool(left), RequireBool(ParseImplies(expected)));
            }
            return left;
        }

        private SortedExpr ParseImplies(Sort? expected)
        {
            var left = ParseOr(expected);
            if (Accept(TokenKind.Implies))
            {
                return SortedExpr.Implies(RequireBool(left), RequireBool(ParseImplies(expected)));
            }
            return left;
        }

        private SortedExpr ParseOr(Sort? expected)
        {
            var left = ParseAnd(expected);
            while (Kind == TokenKind.Pipe)
            {
                Advance();
                left = SortedExpr.Or(RequireBool(left), RequireBool(ParseAnd(expected)));
            }
            return left;
        }

        private SortedExpr ParseAnd(Sort? expected)
        {
            var left = ParseNot(expected);
            while (Accept(TokenKind.And))
            {
                left = SortedExpr.And(RequireBool(left), RequireBool(ParseNot(expected)));
            }
            return left;
        }

        private SortedExpr ParseNot(Sort? expected)
        {
            if (Accept(TokenKind.Not))
            {
                return SortedExpr.Not(RequireBool(ParseNot(expected)));
            }
            if (Kind is TokenKind.Forall or TokenKind.Exists)
            {
                return ParseQuantifier();
            }
            return ParseRelation();
        }

        private SortedExpr ParseQuantifier()
        {
            var isForall = Advance().Kind == TokenKind.Forall;
            var binders = new List<SortedExpr>();
            var scope = new Dictionary<string, Sort>(StringComparer.Ordinal);
            do
            {
                var sort = ParseSort();
                var name = Expect(TokenKind.Identifier, "a bound-variable name").Text;
                if (Functions.ContainsKey(name) || TryReadSortName(name, out _))
                {
                    throw new FormatException($"'{name}' is a reserved word and cannot be a bound variable.");
                }
                scope[name] = sort;
                binders.Add(MakeVar(name, sort));
            }
            while (Accept(TokenKind.Comma));
            Expect(TokenKind.Dot, "'.' before the quantifier body");

            _scopes.Add(scope);
            var body = ParseIff(null);
            _scopes.RemoveAt(_scopes.Count - 1);

            if (body.Sort is not BoolSort)
            {
                throw new FormatException("A quantifier body must be boolean-sorted.");
            }
            return isForall ? SortedExpr.ForAll(binders, body) : SortedExpr.Exists(binders, body);
        }

        // A relation: a term, optionally followed by ==, !=, or an arithmetic comparison.
        private SortedExpr ParseRelation()
        {
            var left = ParseAdditive(null);
            switch (Kind)
            {
                case TokenKind.Eq:
                case TokenKind.Neq:
                {
                    var negated = Advance().Kind == TokenKind.Neq;
                    var right = ParseAdditive(SortOf(left));
                    Reconcile(ref left, ref right);
                    RequireSameSort(left, right, "==");
                    return negated ? SortedExpr.Distinct(left, right) : SortedExpr.Eq(left, right);
                }
                case TokenKind.Lt:
                case TokenKind.Le:
                case TokenKind.Gt:
                case TokenKind.Ge:
                {
                    var op = Advance().Kind;
                    var right = ParseAdditive(SortOf(left));
                    Reconcile(ref left, ref right);
                    RequireArithmetic(left, right);
                    return op switch
                    {
                        TokenKind.Lt => SortedExpr.Lt(left, right),
                        TokenKind.Le => SortedExpr.Le(left, right),
                        TokenKind.Gt => SortedExpr.Gt(left, right),
                        _ => SortedExpr.Ge(left, right),
                    };
                }
                default:
                    // No relational operator: hand back the term as-is (any sort). The boolean
                    // connectives, the quantifier body, and the top-level program enforce booleanness.
                    return left;
            }
        }

        private SortedExpr ParseAdditive(Sort? expected)
        {
            var left = ParseMultiplicative(expected);
            while (Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.PlusPlus)
            {
                var op = Advance().Kind;
                var right = ParseMultiplicative(expected ?? SortOf(left));
                Reconcile(ref left, ref right);
                left = op switch
                {
                    TokenKind.Plus => left + right,
                    TokenKind.Minus => left - right,
                    _ => SortedExpr.StringConcat(left, right),
                };
            }
            return left;
        }

        private SortedExpr ParseMultiplicative(Sort? expected)
        {
            var left = ParseUnary(expected);
            while (Kind == TokenKind.Star)
            {
                Advance();
                var right = ParseUnary(expected ?? SortOf(left));
                Reconcile(ref left, ref right);
                left *= right;
            }
            return left;
        }

        private SortedExpr ParseUnary(Sort? expected)
        {
            if (Accept(TokenKind.Minus))
            {
                return -ParseUnary(expected);
            }
            if (Accept(TokenKind.Tilde))
            {
                return ~ParseUnary(expected);
            }
            return ParsePrimary(expected);
        }

        private SortedExpr ParsePrimary(Sort? expected)
        {
            var tok = Peek();
            switch (tok.Kind)
            {
                case TokenKind.LParen:
                {
                    Advance();
                    var inner = ParseIff(expected);
                    Expect(TokenKind.RParen, "')'");
                    return inner;
                }
                case TokenKind.Pipe:
                {
                    Advance();
                    var s = ParseAdditive(Sort.String);
                    Expect(TokenKind.Pipe, "'|' to close a length");
                    if (s.Sort is not StringSort)
                    {
                        throw new FormatException("The operand of | … | must be a string.");
                    }
                    return SortedExpr.Length(s);
                }
                case TokenKind.True:
                    Advance();
                    return SortedExpr.Bool(true);
                case TokenKind.False:
                    Advance();
                    return SortedExpr.Bool(false);
                case TokenKind.Number:
                    return ParseNumber(expected);
                case TokenKind.BitVecLiteral:
                    Advance();
                    return ReadBitVecLiteral(tok.Text);
                case TokenKind.StringLiteral:
                    Advance();
                    return SortedExpr.Str(tok.Text);
                case TokenKind.Identifier:
                    return Functions.ContainsKey(tok.Text) && Peek(1).Kind == TokenKind.LParen
                        ? ParseCall()
                        : ParseVariable();
                default:
                    throw new FormatException($"Unexpected token '{tok.Text}' at position {tok.Position}.");
            }
        }

        private SortedExpr ParseNumber(Sort? expected)
        {
            var value = BigInteger.Parse(Advance().Text, CultureInfo.InvariantCulture);
            if (Accept(TokenKind.Slash))
            {
                var denominator = BigInteger.Parse(Expect(TokenKind.Number, "a denominator").Text, CultureInfo.InvariantCulture);
                return SortedExpr.Real(Rational.Of(value, denominator));
            }
            // A bare integer: type it from context when known, otherwise leave it an integer literal
            // (Reconcile fixes literal-first cases such as `1 + x` once the sibling sort is known).
            return expected switch
            {
                BitVecSort bv => SortedExpr.BitVec(value, bv.Width),
                RealSort => SortedExpr.Real(Rational.Of(value)),
                _ => SortedExpr.Int(value),
            };
        }

        private static SortedExpr ReadBitVecLiteral(string text)
        {
            // text is the digits after '#b' / '#x', with the radix marker kept as the first char.
            var radix = text[0];
            var digits = text[1..];
            if (radix == 'b')
            {
                var value = BigInteger.Zero;
                foreach (var ch in digits)
                {
                    value = (value << 1) + (ch == '1' ? BigInteger.One : BigInteger.Zero);
                }
                return SortedExpr.BitVec(value, digits.Length);
            }
            else
            {
                var value = BigInteger.Zero;
                foreach (var ch in digits)
                {
                    value = (value << 4) + Convert.ToInt32(ch.ToString(), 16);
                }
                return SortedExpr.BitVec(value, digits.Length * 4);
            }
        }

        private SortedExpr ParseCall()
        {
            var name = Advance().Text;
            Expect(TokenKind.LParen, "'('");
            var args = new List<SortedExpr> { ParseIff(null) };
            while (Accept(TokenKind.Comma))
            {
                args.Add(ParseIff(null));
            }
            Expect(TokenKind.RParen, "')'");
            if (args.Count != Functions[name])
            {
                throw new FormatException($"'{name}' takes {Functions[name]} argument(s), got {args.Count}.");
            }
            return BuildCall(name, args);
        }

        private static SortedExpr BuildCall(string name, IReadOnlyList<SortedExpr> a) => name switch
        {
            "bvand" => a[0] & a[1],
            "bvor" => a[0] | a[1],
            "bvxor" => a[0] ^ a[1],
            "bvnot" => ~a[0],
            "shl" => SortedExpr.Shl(a[0], a[1]),
            "lshr" => SortedExpr.LShr(a[0], a[1]),
            "ashr" => SortedExpr.AShr(a[0], a[1]),
            "ult" => SortedExpr.Ult(a[0], a[1]),
            "ule" => SortedExpr.Ule(a[0], a[1]),
            "ugt" => SortedExpr.Ugt(a[0], a[1]),
            "uge" => SortedExpr.Uge(a[0], a[1]),
            "slt" => SortedExpr.Slt(a[0], a[1]),
            "sle" => SortedExpr.Sle(a[0], a[1]),
            "sgt" => SortedExpr.Sgt(a[0], a[1]),
            "sge" => SortedExpr.Sge(a[0], a[1]),
            "concat" => SortedExpr.Concat(a[0], a[1]),
            "extract" => SortedExpr.Extract(ConstInt(a[0], "extract"), ConstInt(a[1], "extract"), a[2]),
            "contains" => SortedExpr.Contains(a[0], a[1]),
            "prefixof" => SortedExpr.PrefixOf(a[0], a[1]),
            "suffixof" => SortedExpr.SuffixOf(a[0], a[1]),
            "len" => SortedExpr.Length(a[0]),
            _ => throw new FormatException($"Unknown function '{name}'."),
        };

        private static int ConstInt(SortedExpr e, string where)
            => e is IntLiteral i ? (int)i.Value
                : throw new FormatException($"'{where}' requires integer-literal indices.");

        private SortedExpr ParseVariable()
        {
            var tok = Advance();
            var sort = Lookup(tok.Text)
                ?? throw new FormatException($"Undeclared variable '{tok.Text}' at position {tok.Position}.");
            return MakeVar(tok.Text, sort);
        }

        private Sort? Lookup(string name)
        {
            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                if (_scopes[i].TryGetValue(name, out var s)) return s;
            }
            return _declared.TryGetValue(name, out var declared) ? declared : null;
        }

        private static SortedExpr MakeVar(string name, Sort sort) => sort switch
        {
            BoolSort => SortedExpr.BoolVar(name),
            IntSort => SortedExpr.IntVar(name),
            RealSort => SortedExpr.RealVar(name),
            StringSort => SortedExpr.StringVar(name),
            BitVecSort bv => SortedExpr.BitVecVar(name, bv.Width),
            _ => throw new FormatException($"Unsupported sort for variable '{name}'."),
        };

        // --- literal sort reconciliation ---
        // A bare integer literal is sort-polymorphic until paired with a concrete sibling; once the
        // sibling's sort is known, retype the literal to match (bit-vector / real).
        private static void Reconcile(ref SortedExpr left, ref SortedExpr right)
        {
            if (left is IntLiteral li && right.Sort is not IntSort)
            {
                left = Retype(li, right.Sort);
            }
            else if (right is IntLiteral ri && left.Sort is not IntSort)
            {
                right = Retype(ri, left.Sort);
            }
        }

        private static SortedExpr Retype(IntLiteral literal, Sort target) => target switch
        {
            BitVecSort bv => SortedExpr.BitVec(literal.Value, bv.Width),
            RealSort => SortedExpr.Real(Rational.Of(literal.Value)),
            IntSort => literal,
            _ => literal, // leave as-is; a later same-sort check reports the mismatch
        };

        // The sort to propagate to a sibling: unknown for a bare integer literal (let the sibling drive).
        private static Sort? SortOf(SortedExpr e) => e is IntLiteral ? null : e.Sort;

        private static SortedExpr RequireBool(SortedExpr e)
            => e.Sort is BoolSort ? e : throw new FormatException($"Expected a boolean expression but found {Describe(e.Sort)}.");

        private static void RequireSameSort(SortedExpr a, SortedExpr b, string op)
        {
            if (!a.Sort.Equals(b.Sort))
            {
                throw new FormatException($"'{op}' needs operands of the same sort, got {Describe(a.Sort)} and {Describe(b.Sort)}.");
            }
        }

        private static void RequireArithmetic(SortedExpr a, SortedExpr b)
        {
            if (a.Sort is not (IntSort or RealSort) || b.Sort is not (IntSort or RealSort) || !a.Sort.Equals(b.Sort))
            {
                throw new FormatException("Comparisons < <= > >= apply to int or real operands of the same sort (use ult/slt/… for bit-vectors).");
            }
        }

        private static string Describe(Sort sort) => sort switch
        {
            BoolSort => "bool",
            IntSort => "int",
            RealSort => "real",
            StringSort => "string",
            BitVecSort bv => $"bv{bv.Width}",
            _ => sort.ToString() ?? "?",
        };
    }
}
