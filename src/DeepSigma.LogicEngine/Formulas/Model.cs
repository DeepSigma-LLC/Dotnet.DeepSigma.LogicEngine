using System.Collections;

namespace DeepSigma.LogicEngine.Formulas;

/// <summary>
/// Immutable assignment of variable names to truth values. Iteration order
/// is the insertion order; use <see cref="Extend"/> to build models step by step.
/// </summary>
public sealed class Model : IReadOnlyDictionary<string, bool>
{
    private readonly Dictionary<string, bool> _values;

    public static Model Empty { get; } = new(new Dictionary<string, bool>());

    private Model(Dictionary<string, bool> values) => _values = values;

    public static Model Of(params (string Name, bool Value)[] pairs)
    {
        var dict = new Dictionary<string, bool>(pairs.Length);
        foreach (var (n, v) in pairs)
        {
            dict[n] = v;
        }
        return new Model(dict);
    }

    public static Model From(IReadOnlyDictionary<string, bool> values)
        => new(new Dictionary<string, bool>(values));

    public Model Extend(string name, bool value)
    {
        var next = new Dictionary<string, bool>(_values) { [name] = value };
        return new Model(next);
    }

    public bool this[string key] => _values[key];
    public IEnumerable<string> Keys => _values.Keys;
    public IEnumerable<bool> Values => _values.Values;
    public int Count => _values.Count;
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    public bool TryGetValue(string key, out bool value) => _values.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, bool>> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
