namespace DeepSigma.LogicEngine.Smt;

internal enum EufAtomKind
{
    Equality,
    Predicate,
}

/// <summary>An asserted theory literal, tagged with the id of the atom it came from.</summary>
internal readonly record struct EufLiteral(int AtomId, bool Positive, EufAtomKind Kind, Term First, Term Second);

/// <summary>
/// Decision procedure for EUF over a set of asserted theory literals. Builds the
/// congruence closure of the asserted equalities (and predicate facts, reduced
/// to equalities with reserved <c>⊤</c>/<c>⊥</c> constants), then checks every
/// asserted disequality. On a violation it returns a minimal-ish conflict core —
/// the subset of asserted atoms responsible — via a proof forest.
///
/// <para>
/// A fresh instance is used per check. Congruence is propagated with a simple
/// fixpoint over application terms; a signature-table/use-list scheme would make
/// it near-linear and is noted as a future refinement.
/// </para>
/// </summary>
internal sealed class CongruenceClosure
{
    private static readonly Term TrueTerm = Term.Constant("⊤");
    private static readonly Term FalseTerm = Term.Constant("⊥");
    private const int BuiltinWitness = -1;

    private readonly List<string> _symbol = new();
    private readonly List<int[]> _args = new();
    private readonly Dictionary<string, int> _hashCons = new(StringComparer.Ordinal);

    private readonly List<int> _classParent = new();
    private readonly List<int> _classSize = new();

    private readonly List<int> _proofParent = new();
    private readonly List<Reason> _proofReason = new();

    private readonly List<int> _applications = new();
    private readonly List<(int Left, int Right, int Witness)> _disequalities = new();

    private readonly int _trueNode;
    private readonly int _falseNode;

    public CongruenceClosure()
    {
        _trueNode = Intern(TrueTerm);
        _falseNode = Intern(FalseTerm);
        _disequalities.Add((_trueNode, _falseNode, BuiltinWitness));
    }

    /// <summary>Feed one asserted literal into the solver.</summary>
    public void Assert(EufLiteral literal)
    {
        if (literal.Kind == EufAtomKind.Equality)
        {
            var left = Intern(literal.First);
            var right = Intern(literal.Second);
            if (literal.Positive)
            {
                Merge(left, right, Reason.Input(literal.AtomId));
            }
            else
            {
                _disequalities.Add((left, right, literal.AtomId));
            }
        }
        else
        {
            var node = Intern(literal.First);
            Merge(node, literal.Positive ? _trueNode : _falseNode, Reason.Input(literal.AtomId));
        }
    }

    /// <summary>
    /// Returns null if the asserted literals are theory-consistent; otherwise a
    /// set of atom ids whose conjunction is already inconsistent.
    /// </summary>
    public IReadOnlySet<int>? FindConflict()
    {
        Propagate();
        foreach (var (left, right, witness) in _disequalities)
        {
            if (Find(left) == Find(right))
            {
                var core = Explain(left, right);
                if (witness != BuiltinWitness)
                {
                    core.Add(witness);
                }
                return core;
            }
        }
        return null;
    }

    /// <summary>
    /// True if the asserted facts force <paramref name="a"/> and <paramref name="b"/>
    /// into the same congruence class. Used by theory combination to read off the
    /// equalities EUF entails between shared variables.
    /// </summary>
    public bool AreEqual(Term a, Term b)
    {
        var x = Intern(a);
        var y = Intern(b);
        Propagate();
        return Find(x) == Find(y);
    }

    // --- term interning ---------------------------------------------------

    private int Intern(Term term)
    {
        var childIds = new int[term.Arguments.Count];
        for (var i = 0; i < childIds.Length; i++)
        {
            childIds[i] = Intern(term.Arguments[i]);
        }
        var key = Key(term.Symbol, childIds);
        if (_hashCons.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var id = _symbol.Count;
        _symbol.Add(term.Symbol);
        _args.Add(childIds);
        _classParent.Add(id);
        _classSize.Add(1);
        _proofParent.Add(-1);
        _proofReason.Add(default);
        _hashCons[key] = id;
        if (childIds.Length > 0)
        {
            _applications.Add(id);
        }
        return id;
    }

    private static string Key(string symbol, int[] childIds) => $"{symbol}({string.Join(",", childIds)})";

    // --- union-find (equality representatives) ----------------------------

    private int Find(int node)
    {
        var root = node;
        while (_classParent[root] != root)
        {
            root = _classParent[root];
        }
        while (_classParent[node] != root)
        {
            var next = _classParent[node];
            _classParent[node] = root;
            node = next;
        }
        return root;
    }

    private void Merge(int a, int b, Reason reason)
    {
        var ra = Find(a);
        var rb = Find(b);
        if (ra == rb)
        {
            return;
        }
        AddProofEdge(a, b, reason);
        if (_classSize[ra] < _classSize[rb])
        {
            (ra, rb) = (rb, ra);
        }
        _classParent[rb] = ra;
        _classSize[ra] += _classSize[rb];
    }

    /// <summary>Propagate congruence to a fixpoint: equal arguments imply equal applications.</summary>
    private void Propagate()
    {
        bool changed;
        do
        {
            changed = false;
            for (var i = 0; i < _applications.Count; i++)
            {
                for (var j = i + 1; j < _applications.Count; j++)
                {
                    var a = _applications[i];
                    var b = _applications[j];
                    if (Find(a) != Find(b) && AreCongruent(a, b))
                    {
                        Merge(a, b, Reason.Congruence(a, b));
                        changed = true;
                    }
                }
            }
        }
        while (changed);
    }

    private bool AreCongruent(int a, int b)
    {
        if (!string.Equals(_symbol[a], _symbol[b], StringComparison.Ordinal))
        {
            return false;
        }
        var argsA = _args[a];
        var argsB = _args[b];
        if (argsA.Length != argsB.Length)
        {
            return false;
        }
        for (var i = 0; i < argsA.Length; i++)
        {
            if (Find(argsA[i]) != Find(argsB[i]))
            {
                return false;
            }
        }
        return true;
    }

    // --- proof forest + explanation ---------------------------------------

    private void AddProofEdge(int a, int b, Reason reason)
    {
        MakeRoot(a);
        _proofParent[a] = b;
        _proofReason[a] = reason;
    }

    private void MakeRoot(int x)
    {
        var prev = -1;
        var prevReason = default(Reason);
        var cur = x;
        while (cur != -1)
        {
            var nextNode = _proofParent[cur];
            var nextReason = _proofReason[cur];
            _proofParent[cur] = prev;
            _proofReason[cur] = prevReason;
            prev = cur;
            prevReason = nextReason;
            cur = nextNode;
        }
    }

    private HashSet<int> Explain(int a, int b)
    {
        var atoms = new HashSet<int>();
        var pending = new Queue<(int, int)>();
        var seen = new HashSet<(int, int)>();
        pending.Enqueue((a, b));

        while (pending.Count > 0)
        {
            var (x, y) = pending.Dequeue();
            if (x == y)
            {
                continue;
            }
            var key = x < y ? (x, y) : (y, x);
            if (!seen.Add(key))
            {
                continue;
            }
            ExpandPath(x, y, atoms, pending);
        }
        return atoms;
    }

    /// <summary>Walk the unique proof-forest path between x and y, expanding edge reasons.</summary>
    private void ExpandPath(int x, int y, HashSet<int> atoms, Queue<(int, int)> pending)
    {
        var ancestorsOfX = new HashSet<int>();
        for (var n = x; n != -1; n = _proofParent[n])
        {
            ancestorsOfX.Add(n);
        }
        var lca = y;
        while (!ancestorsOfX.Contains(lca))
        {
            lca = _proofParent[lca];
        }
        for (var n = x; n != lca; n = _proofParent[n])
        {
            ExpandReason(_proofReason[n], atoms, pending);
        }
        for (var n = y; n != lca; n = _proofParent[n])
        {
            ExpandReason(_proofReason[n], atoms, pending);
        }
    }

    private void ExpandReason(Reason reason, HashSet<int> atoms, Queue<(int, int)> pending)
    {
        if (reason.IsInput)
        {
            atoms.Add(reason.AtomId);
            return;
        }
        var argsA = _args[reason.App1];
        var argsB = _args[reason.App2];
        for (var i = 0; i < argsA.Length; i++)
        {
            pending.Enqueue((argsA[i], argsB[i]));
        }
    }

    private readonly record struct Reason(bool IsInput, int AtomId, int App1, int App2)
    {
        public static Reason Input(int atomId) => new(true, atomId, -1, -1);
        public static Reason Congruence(int app1, int app2) => new(false, -1, app1, app2);
    }
}
