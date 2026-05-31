using System.Numerics;
using System.Text;
using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>
/// The outcome of a Z3 solve. Tri-valued because Z3 is honest about not deciding every
/// query: <see cref="Unknown"/> arises for quantified or nonlinear problems, or when a
/// timeout / cancellation cut the search short. Never collapse <see cref="Unknown"/> into
/// sat/unsat.
/// </summary>
public enum Z3Status
{
    /// <summary>A satisfying model exists (and is available on <see cref="Z3Result.Model"/>).</summary>
    Satisfiable,

    /// <summary>No model exists.</summary>
    Unsatisfiable,

    /// <summary>Z3 did not decide (quantifier/nonlinear incompleteness, timeout, or cancellation).</summary>
    Unknown,
}

/// <summary>The kind of a model value, so callers can read it without parsing <see cref="Z3Value.Text"/>.</summary>
public enum Z3ValueKind
{
    Boolean,
    Integer,
    Rational,
    Other,
}

/// <summary>A single variable's value in a <see cref="Z3Model"/>. <see cref="Text"/> is always Z3's printed form.</summary>
public sealed class Z3Value
{
    public Z3ValueKind Kind { get; }
    public string Text { get; }
    public bool? Boolean { get; }
    public BigInteger? Integer { get; }
    public Rational? Rational { get; }

    internal Z3Value(Z3ValueKind kind, string text, bool? boolean = null, BigInteger? integer = null, Rational? rational = null)
    {
        Kind = kind;
        Text = text;
        Boolean = boolean;
        Integer = integer;
        Rational = rational;
    }

    public override string ToString() => Text;
}

/// <summary>
/// A satisfying assignment from Z3: variable name → typed value. Unlike the native
/// atom→bool model, this exposes the actual values (e.g. <c>x = 3</c>, <c>y = -1/2</c>).
/// </summary>
public sealed class Z3Model
{
    private readonly IReadOnlyDictionary<string, Z3Value> _values;

    internal Z3Model(IReadOnlyDictionary<string, Z3Value> values) => _values = values;

    /// <summary>All assigned values, keyed by variable/constant name.</summary>
    public IReadOnlyDictionary<string, Z3Value> Values => _values;

    /// <summary>The value of <paramref name="name"/>, or null if it was not assigned.</summary>
    public Z3Value? this[string name] => _values.TryGetValue(name, out var v) ? v : null;

    public override string ToString()
    {
        if (_values.Count == 0)
        {
            return "{}";
        }
        var sb = new StringBuilder("{ ");
        sb.AppendJoin(", ", _values.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value.Text}"));
        sb.Append(" }");
        return sb.ToString();
    }
}

/// <summary>The result of a Z3 query: a <see cref="Z3Status"/> and, when satisfiable, a <see cref="Z3Model"/>.</summary>
public sealed class Z3Result
{
    public Z3Status Status { get; }
    public Z3Model? Model { get; }

    internal Z3Result(Z3Status status, Z3Model? model)
    {
        Status = status;
        Model = model;
    }

    /// <summary>True only when the status is <see cref="Z3Status.Satisfiable"/> (Unknown is not satisfiable).</summary>
    public bool IsSatisfiable => Status == Z3Status.Satisfiable;

    /// <summary>True only when the status is <see cref="Z3Status.Unsatisfiable"/>.</summary>
    public bool IsUnsatisfiable => Status == Z3Status.Unsatisfiable;

    /// <summary>True when Z3 did not decide the query.</summary>
    public bool IsUnknown => Status == Z3Status.Unknown;

    public override string ToString() => Model is null ? Status.ToString() : $"{Status} {Model}";
}
