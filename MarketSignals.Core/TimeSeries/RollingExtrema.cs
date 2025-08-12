namespace MarketSignals.Core.TimeSeries;

public sealed class RollingWindowMax
{
    private readonly int _capacity;
    private readonly LinkedList<(int index, double value)> _deque = new();
    private int _index;

    public RollingWindowMax(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _index = -1;
    }

    public void Reset()
    {
        _deque.Clear();
        _index = -1;
    }

    public void Add(double value)
    {
        _index++;
        while (_deque.Count > 0 && _deque.Last!.Value.value <= value)
        {
            _deque.RemoveLast();
        }
        _deque.AddLast((_index, value));
        // Remove elements outside window
        while (_deque.Count > 0 && _deque.First!.Value.index <= _index - _capacity)
        {
            _deque.RemoveFirst();
        }
    }

    public double Max => _deque.Count == 0 ? double.NaN : _deque.First!.Value.value;
}

public sealed class RollingWindowMin
{
    private readonly int _capacity;
    private readonly LinkedList<(int index, double value)> _deque = new();
    private int _index;

    public RollingWindowMin(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _index = -1;
    }

    public void Reset()
    {
        _deque.Clear();
        _index = -1;
    }

    public void Add(double value)
    {
        _index++;
        while (_deque.Count > 0 && _deque.Last!.Value.value >= value)
        {
            _deque.RemoveLast();
        }
        _deque.AddLast((_index, value));
        // Remove elements outside window
        while (_deque.Count > 0 && _deque.First!.Value.index <= _index - _capacity)
        {
            _deque.RemoveFirst();
        }
    }

    public double Min => _deque.Count == 0 ? double.NaN : _deque.First!.Value.value;
}