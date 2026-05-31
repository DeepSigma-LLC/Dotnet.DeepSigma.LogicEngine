namespace DeepSigma.LogicEngine.Z3.Sorted;

/// <summary>
/// The sort (type) of a <see cref="SortedExpr"/>. The sorted layer is what lets the Z3 backend
/// express theories the native engine cannot — <see cref="Bool"/>, fixed-width bit-vectors,
/// unbounded <see cref="Int"/>/<see cref="Real"/> arithmetic (with quantifiers and nonlinear
/// terms), and <see cref="String"/> sequences.
/// </summary>
public abstract record Sort
{
    /// <summary>The boolean sort.</summary>
    public static Sort Bool { get; } = new BoolSort();

    /// <summary>A fixed-width bit-vector sort of the given (positive) <paramref name="width"/> in bits.</summary>
    public static Sort BitVec(int width)
        => width > 0 ? new BitVecSort(width) : throw new ArgumentOutOfRangeException(nameof(width), "Bit-vector width must be positive.");

    /// <summary>The (unbounded) integer sort.</summary>
    public static Sort Int { get; } = new IntSort();

    /// <summary>The real sort.</summary>
    public static Sort Real { get; } = new RealSort();

    /// <summary>The string sort (a sequence of characters).</summary>
    public static Sort String { get; } = new StringSort();

    /// <summary>The bit width, for a bit-vector sort. Throws for non-bit-vector sorts.</summary>
    public int BitWidth => this is BitVecSort bv ? bv.Width : throw new InvalidOperationException("Sort is not a bit-vector sort.");
}

/// <summary>The boolean sort.</summary>
public sealed record BoolSort : Sort;

/// <summary>A fixed-width bit-vector sort.</summary>
public sealed record BitVecSort(int Width) : Sort;

/// <summary>The integer sort.</summary>
public sealed record IntSort : Sort;

/// <summary>The real sort.</summary>
public sealed record RealSort : Sort;

/// <summary>The string sort.</summary>
public sealed record StringSort : Sort;
