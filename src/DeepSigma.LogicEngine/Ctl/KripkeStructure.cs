namespace DeepSigma.LogicEngine.Ctl;

/// <summary>
/// A finite Kripke structure: states <c>0 .. StateCount-1</c>, a transition relation,
/// and a labelling of which atomic propositions hold in each state. CTL is evaluated
/// over this explicit graph. The relation need not be total; a state with no
/// successors is a dead end (then <c>EX</c> is false and <c>AX</c> vacuously true).
/// </summary>
public sealed class KripkeStructure
{
    private readonly List<int>[] _successors;
    private readonly HashSet<string>[] _labels;

    public int StateCount { get; }

    public KripkeStructure(
        int stateCount,
        IEnumerable<(int From, int To)> transitions,
        IReadOnlyDictionary<int, IEnumerable<string>> labels)
    {
        if (stateCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stateCount), "A Kripke structure needs at least one state.");
        }
        StateCount = stateCount;
        _successors = new List<int>[stateCount];
        _labels = new HashSet<string>[stateCount];
        for (var s = 0; s < stateCount; s++)
        {
            _successors[s] = new List<int>();
            _labels[s] = new HashSet<string>(StringComparer.Ordinal);
        }
        foreach (var (from, to) in transitions)
        {
            _successors[from].Add(to);
        }
        foreach (var (state, atoms) in labels)
        {
            foreach (var atom in atoms)
            {
                _labels[state].Add(atom);
            }
        }
    }

    public IReadOnlyList<int> Successors(int state) => _successors[state];

    public bool Holds(int state, string atom) => _labels[state].Contains(atom);
}
