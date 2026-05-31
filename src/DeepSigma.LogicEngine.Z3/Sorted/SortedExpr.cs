using System.Numerics;
using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.Z3.Sorted;

/// <summary>
/// A sorted (typed) expression for the Z3-only theories. Build with the static factories and
/// operators, then hand a boolean-sorted expression to <see cref="Z3Sorted"/>. This is the
/// front end for theories the native engine cannot express; bit-vectors are the first.
///
/// <para>
/// Operators are bit-vector algebra: <c>+ - *</c> (add/sub/mul, and unary <c>-</c> negate),
/// <c>&amp; | ^</c> (bitwise and/or/xor) and <c>~</c> (bitwise not). Boolean logic, comparisons,
/// shifts, and concat/extract are the named factory methods.
/// </para>
/// </summary>
public abstract record SortedExpr
{
    /// <summary>The sort of this expression.</summary>
    public abstract Sort Sort { get; }

    // --- leaves ---

    /// <summary>A boolean literal.</summary>
    public static SortedExpr Bool(bool value) => new BoolLiteral(value);

    /// <summary>A boolean variable.</summary>
    public static SortedExpr BoolVar(string name) => new SortedVar(name, Sort.Bool);

    /// <summary>A bit-vector literal of the given width (interpreted modulo 2^width).</summary>
    public static SortedExpr BitVec(BigInteger value, int width) => new BitVecLiteral(value, width);

    /// <summary>A bit-vector variable of the given width.</summary>
    public static SortedExpr BitVecVar(string name, int width) => new SortedVar(name, Sort.BitVec(width));

    /// <summary>An integer literal (unbounded).</summary>
    public static SortedExpr Int(BigInteger value) => new IntLiteral(value);

    /// <summary>An integer variable (unbounded).</summary>
    public static SortedExpr IntVar(string name) => new SortedVar(name, Sort.Int);

    /// <summary>A real literal.</summary>
    public static SortedExpr Real(Rational value) => new RealLiteral(value);

    /// <summary>A real variable.</summary>
    public static SortedExpr RealVar(string name) => new SortedVar(name, Sort.Real);

    /// <summary>A string literal.</summary>
    public static SortedExpr Str(string value) => new StringLiteral(value);

    /// <summary>A string variable.</summary>
    public static SortedExpr StringVar(string name) => new SortedVar(name, Sort.String);

    // --- string operations ---

    /// <summary>The length of a string (an integer).</summary>
    public static SortedExpr Length(SortedExpr s) => new StrLength(s);

    /// <summary>Concatenate two strings.</summary>
    public static SortedExpr StringConcat(SortedExpr left, SortedExpr right) => new StrConcat(left, right);

    /// <summary>True if <paramref name="whole"/> contains <paramref name="part"/> as a substring.</summary>
    public static SortedExpr Contains(SortedExpr whole, SortedExpr part) => new StrPredicate(StrPredOp.Contains, whole, part);

    /// <summary>True if <paramref name="prefix"/> is a prefix of <paramref name="whole"/>.</summary>
    public static SortedExpr PrefixOf(SortedExpr prefix, SortedExpr whole) => new StrPredicate(StrPredOp.PrefixOf, prefix, whole);

    /// <summary>True if <paramref name="suffix"/> is a suffix of <paramref name="whole"/>.</summary>
    public static SortedExpr SuffixOf(SortedExpr suffix, SortedExpr whole) => new StrPredicate(StrPredOp.SuffixOf, suffix, whole);

    // --- boolean logic ---

    public static SortedExpr Not(SortedExpr a) => new NotExpr(a);
    public static SortedExpr And(SortedExpr a, SortedExpr b) => new BinaryBool(BoolOp.And, a, b);
    public static SortedExpr Or(SortedExpr a, SortedExpr b) => new BinaryBool(BoolOp.Or, a, b);
    public static SortedExpr Implies(SortedExpr a, SortedExpr b) => new BinaryBool(BoolOp.Implies, a, b);
    public static SortedExpr Iff(SortedExpr a, SortedExpr b) => new BinaryBool(BoolOp.Iff, a, b);

    /// <summary>Equality of two same-sort expressions (yields a boolean).</summary>
    public static SortedExpr Eq(SortedExpr a, SortedExpr b) => new EqExpr(a, b, Negated: false);

    /// <summary>Disequality of two same-sort expressions.</summary>
    public static SortedExpr Distinct(SortedExpr a, SortedExpr b) => new EqExpr(a, b, Negated: true);

    // --- quantifiers (the bound variable(s) must be made with IntVar/RealVar/BitVecVar/BoolVar) ---

    /// <summary>∀ <paramref name="boundVariable"/>. <paramref name="body"/>.</summary>
    public static SortedExpr ForAll(SortedExpr boundVariable, SortedExpr body) => new Quantifier(true, new[] { AsVar(boundVariable) }, body);

    /// <summary>∃ <paramref name="boundVariable"/>. <paramref name="body"/>.</summary>
    public static SortedExpr Exists(SortedExpr boundVariable, SortedExpr body) => new Quantifier(false, new[] { AsVar(boundVariable) }, body);

    /// <summary>∀ over several bound variables.</summary>
    public static SortedExpr ForAll(IReadOnlyList<SortedExpr> boundVariables, SortedExpr body) => new Quantifier(true, boundVariables.Select(AsVar).ToList(), body);

    /// <summary>∃ over several bound variables.</summary>
    public static SortedExpr Exists(IReadOnlyList<SortedExpr> boundVariables, SortedExpr body) => new Quantifier(false, boundVariables.Select(AsVar).ToList(), body);

    private static SortedVar AsVar(SortedExpr e)
        => e as SortedVar ?? throw new ArgumentException("A bound variable must be a variable (IntVar/RealVar/BitVecVar/BoolVar).", nameof(e));

    // --- bit-vector structural ops ---

    /// <summary>Concatenate two bit-vectors (left becomes the high bits); width = sum of widths.</summary>
    public static SortedExpr Concat(SortedExpr high, SortedExpr low) => new BvConcat(high, low);

    /// <summary>Extract bits <paramref name="high"/>..<paramref name="low"/> (inclusive) of a bit-vector.</summary>
    public static SortedExpr Extract(int high, int low, SortedExpr operand) => new BvExtract(high, low, operand);

    // --- bit-vector shifts ---

    public static SortedExpr Shl(SortedExpr a, SortedExpr b) => new BvBinary(BvBinOp.Shl, a, b);
    public static SortedExpr LShr(SortedExpr a, SortedExpr b) => new BvBinary(BvBinOp.LShr, a, b);
    public static SortedExpr AShr(SortedExpr a, SortedExpr b) => new BvBinary(BvBinOp.AShr, a, b);

    // --- bit-vector comparisons (unsigned / signed) → boolean ---

    public static SortedExpr Ult(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Ult, a, b);
    public static SortedExpr Ule(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Ule, a, b);
    public static SortedExpr Ugt(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Ugt, a, b);
    public static SortedExpr Uge(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Uge, a, b);
    public static SortedExpr Slt(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Slt, a, b);
    public static SortedExpr Sle(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Sle, a, b);
    public static SortedExpr Sgt(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Sgt, a, b);
    public static SortedExpr Sge(SortedExpr a, SortedExpr b) => new BvCompare(BvCmpOp.Sge, a, b);

    // --- integer / real comparisons → boolean ---

    public static SortedExpr Lt(SortedExpr a, SortedExpr b) => new ArithCompare(ArithCmp.Lt, a, b);
    public static SortedExpr Le(SortedExpr a, SortedExpr b) => new ArithCompare(ArithCmp.Le, a, b);
    public static SortedExpr Gt(SortedExpr a, SortedExpr b) => new ArithCompare(ArithCmp.Gt, a, b);
    public static SortedExpr Ge(SortedExpr a, SortedExpr b) => new ArithCompare(ArithCmp.Ge, a, b);

    // --- arithmetic operators: bit-vector algebra for bit-vectors, ordinary arithmetic for Int/Real ---

    public static SortedExpr operator +(SortedExpr a, SortedExpr b)
        => a.Sort is BitVecSort ? new BvBinary(BvBinOp.Add, a, b) : new ArithBinary(ArithOp.Add, a, b);
    public static SortedExpr operator -(SortedExpr a, SortedExpr b)
        => a.Sort is BitVecSort ? new BvBinary(BvBinOp.Sub, a, b) : new ArithBinary(ArithOp.Sub, a, b);
    public static SortedExpr operator *(SortedExpr a, SortedExpr b)
        => a.Sort is BitVecSort ? new BvBinary(BvBinOp.Mul, a, b) : new ArithBinary(ArithOp.Mul, a, b);
    public static SortedExpr operator -(SortedExpr a)
        => a.Sort is BitVecSort ? new BvUnary(BvUnOp.Neg, a) : new ArithNegate(a);

    // --- bit-vector bitwise operators ---

    public static SortedExpr operator &(SortedExpr a, SortedExpr b) => new BvBinary(BvBinOp.And, a, b);
    public static SortedExpr operator |(SortedExpr a, SortedExpr b) => new BvBinary(BvBinOp.Or, a, b);
    public static SortedExpr operator ^(SortedExpr a, SortedExpr b) => new BvBinary(BvBinOp.Xor, a, b);
    public static SortedExpr operator ~(SortedExpr a) => new BvUnary(BvUnOp.Not, a);
}

internal enum BoolOp { And, Or, Implies, Iff }
internal enum BvBinOp { Add, Sub, Mul, And, Or, Xor, Shl, LShr, AShr }
internal enum BvUnOp { Not, Neg }
internal enum BvCmpOp { Ult, Ule, Ugt, Uge, Slt, Sle, Sgt, Sge }
internal enum ArithOp { Add, Sub, Mul }
internal enum ArithCmp { Lt, Le, Gt, Ge }

internal sealed record BoolLiteral(bool Value) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal sealed record SortedVar(string Name, Sort VarSort) : SortedExpr
{
    public override Sort Sort => VarSort;
}

internal sealed record BitVecLiteral(BigInteger Value, int Width) : SortedExpr
{
    public override Sort Sort => Sort.BitVec(Width);
}

internal sealed record NotExpr(SortedExpr Operand) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal sealed record BinaryBool(BoolOp Op, SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal sealed record EqExpr(SortedExpr Left, SortedExpr Right, bool Negated) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal sealed record BvCompare(BvCmpOp Op, SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal sealed record BvBinary(BvBinOp Op, SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Left.Sort;
}

internal sealed record BvUnary(BvUnOp Op, SortedExpr Operand) : SortedExpr
{
    public override Sort Sort => Operand.Sort;
}

internal sealed record BvConcat(SortedExpr High, SortedExpr Low) : SortedExpr
{
    public override Sort Sort => Sort.BitVec(High.Sort.BitWidth + Low.Sort.BitWidth);
}

internal sealed record BvExtract(int High, int Low, SortedExpr Operand) : SortedExpr
{
    public override Sort Sort => Sort.BitVec(High - Low + 1);
}

internal sealed record IntLiteral(BigInteger Value) : SortedExpr
{
    public override Sort Sort => Sort.Int;
}

internal sealed record RealLiteral(Rational Value) : SortedExpr
{
    public override Sort Sort => Sort.Real;
}

internal sealed record ArithBinary(ArithOp Op, SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Left.Sort;
}

internal sealed record ArithNegate(SortedExpr Operand) : SortedExpr
{
    public override Sort Sort => Operand.Sort;
}

internal sealed record ArithCompare(ArithCmp Op, SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal sealed record Quantifier(bool IsForall, IReadOnlyList<SortedVar> BoundVariables, SortedExpr Body) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}

internal enum StrPredOp { Contains, PrefixOf, SuffixOf }

internal sealed record StringLiteral(string Value) : SortedExpr
{
    public override Sort Sort => Sort.String;
}

internal sealed record StrLength(SortedExpr Operand) : SortedExpr
{
    public override Sort Sort => Sort.Int;
}

internal sealed record StrConcat(SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Sort.String;
}

internal sealed record StrPredicate(StrPredOp Op, SortedExpr Left, SortedExpr Right) : SortedExpr
{
    public override Sort Sort => Sort.Bool;
}
