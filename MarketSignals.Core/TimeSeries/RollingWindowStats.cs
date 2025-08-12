using System.Collections.Concurrent;

namespace MarketSignals.Core.TimeSeries;

public sealed class RollingWindowStats
{
    private readonly int _capacity;
    private readonly Queue<double> _values;
    private double _sum;
    private double _sumSquares;

    public RollingWindowStats(int capacity)
    {
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be >= 2");
        }
        _capacity = capacity;
        _values = new Queue<double>(capacity);
    }

    public int Count => _values.Count;

    public void Reset()
    {
        _values.Clear();
        _sum = 0.0;
        _sumSquares = 0.0;
    }

    public void Add(double x)
    {
        if (_values.Count == _capacity)
        {
            double old = _values.Dequeue();
            _sum -= old;
            _sumSquares -= old * old;
        }
        _values.Enqueue(x);
        _sum += x;
        _sumSquares += x * x;
    }

    public double Mean => _values.Count == 0 ? 0.0 : _sum / _values.Count;

    public double Variance
    {
        get
        {
            if (_values.Count == 0)
            {
                return 0.0;
            }
            double mean = Mean;
            double raw = (_sumSquares / _values.Count) - (mean * mean);
            return raw < 0 ? 0.0 : raw;
        }
    }

    public double StdDev => Math.Sqrt(Variance);

    public double ZScore(double x)
    {
        double sd = StdDev;
        if (sd <= 1e-12)
        {
            return 0.0;
        }
        return (x - Mean) / sd;
    }
}