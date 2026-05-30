namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// VSIDS (Variable State Independent Decaying Sum) decision heuristic: a binary
/// max-heap of variables keyed by activity, with a position index so a variable
/// can be re-prioritised in place when its activity is bumped. Activity decay is
/// implemented by growing the bump increment rather than scaling every score;
/// scores are rescaled only on overflow.
/// </summary>
internal sealed class VsidsHeap
{
    private const double RescaleThreshold = 1e100;

    private readonly double[] _activity;
    private readonly int[] _heap;        // heap[i] = variable id
    private readonly int[] _position;    // position[v] = index in _heap, or -1 if absent
    private readonly double _decay;
    private int _size;
    private double _increment = 1.0;

    public VsidsHeap(int variableCount, double decay)
    {
        _activity = new double[variableCount];
        _heap = new int[variableCount];
        _position = new int[variableCount];
        _decay = decay;
        for (var v = 0; v < variableCount; v++)
        {
            _heap[v] = v;
            _position[v] = v;
        }
        _size = variableCount;
    }

    public bool IsEmpty => _size == 0;

    public double ActivityOf(int variable) => _activity[variable];

    public void Bump(int variable)
    {
        _activity[variable] += _increment;
        if (_activity[variable] > RescaleThreshold)
        {
            Rescale();
        }
        if (_position[variable] >= 0)
        {
            SiftUp(_position[variable]);
        }
    }

    public void Decay() => _increment /= _decay;

    public void InsertIfAbsent(int variable)
    {
        if (_position[variable] >= 0)
        {
            return;
        }
        var index = _size++;
        _heap[index] = variable;
        _position[variable] = index;
        SiftUp(index);
    }

    /// <summary>Remove and return the highest-activity variable.</summary>
    public int RemoveMax()
    {
        var max = _heap[0];
        var last = _heap[--_size];
        _heap[0] = last;
        _position[last] = 0;
        _position[max] = -1;
        if (_size > 0)
        {
            SiftDown(0);
        }
        return max;
    }

    private void Rescale()
    {
        for (var v = 0; v < _activity.Length; v++)
        {
            _activity[v] *= 1.0 / RescaleThreshold;
        }
        _increment *= 1.0 / RescaleThreshold;
    }

    private void SiftUp(int index)
    {
        var variable = _heap[index];
        var activity = _activity[variable];
        while (index > 0)
        {
            var parent = (index - 1) >> 1;
            var parentVar = _heap[parent];
            if (_activity[parentVar] >= activity)
            {
                break;
            }
            _heap[index] = parentVar;
            _position[parentVar] = index;
            index = parent;
        }
        _heap[index] = variable;
        _position[variable] = index;
    }

    private void SiftDown(int index)
    {
        var variable = _heap[index];
        var activity = _activity[variable];
        while (true)
        {
            var left = (index << 1) + 1;
            if (left >= _size)
            {
                break;
            }
            var right = left + 1;
            var child = (right < _size && _activity[_heap[right]] > _activity[_heap[left]]) ? right : left;
            var childVar = _heap[child];
            if (activity >= _activity[childVar])
            {
                break;
            }
            _heap[index] = childVar;
            _position[childVar] = index;
            index = child;
        }
        _heap[index] = variable;
        _position[variable] = index;
    }
}
