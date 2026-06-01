using System.Collections;

namespace DeepSigma.LogicEngine.Formulas;

/// <summary>
/// Immutable assignment of variable names to truth values. Iteration order
/// is the insertion order; use <see cref="Extend"/> to build models step by step.
/// </summary>
public sealed class Model : IReadOnlyDictionary<string, bool>
{
    private readonly Dictionary<string, bool> _values;

    /// <summary>The empty model, assigning no variables.</summary>
    public static Model Empty { get; } = new(new Dictionary<string, bool>());

    private Model(Dictionary<string, bool> values) => _values = values;

    /// <summary>Creates a model from the given name/value pairs.</summary>
    public static Model Of(params (string Name, bool Value)[] pairs)
    {
        var dict = new Dictionary<string, bool>(pairs.Length);
        foreach (var (n, v) in pairs)
        {
            dict[n] = v;
        }
        return new Model(dict);
    }

    /// <summary>Creates a model copying the given variable assignments.</summary>
    public static Model From(IReadOnlyDictionary<string, bool> values)
        => new(new Dictionary<string, bool>(values));

    /// <summary>Returns a new model with the given variable additionally assigned (overwriting any existing value).</summary>
    public Model Extend(string name, bool value)
    {
        var next = new Dictionary<string, bool>(_values) { [name] = value };
        return new Model(next);
    }

    /// <summary>The truth value assigned to the named variable.</summary>
    public bool this[string key] => _values[key];
    /// <summary>The names of the assigned variables.</summary>
    public IEnumerable<string> Keys => _values.Keys;
    /// <summary>The assigned truth values.</summary>
    public IEnumerable<bool> Values => _values.Values;
    /// <summary>The number of assigned variables.</summary>
    public int Count => _values.Count;
    /// <summary>Returns true if the named variable is assigned in this model.</summary>
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    /// <summary>Attempts to get the truth value of the named variable; returns false if unassigned.</summary>
    public bool TryGetValue(string key, out bool value) => _values.TryGetValue(key, out value);
    /// <summary>Returns an enumerator over the variable/value pairs.</summary>
    public IEnumerator<KeyValuePair<string, bool>> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Renders the model as a sorted brace-delimited list of variable=T/F assignments.</summary>
    public override string ToString()
    {
        if (_values.Count == 0)
        {
            return "{}";
        }
        var ordered = _values.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        return "{ " + string.Join(", ", ordered.Select(kv => $"{kv.Key}={(kv.Value ? "T" : "F")}")) + " }";
    }
}
