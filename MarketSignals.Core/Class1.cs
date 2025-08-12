namespace MarketSignals.Core;

public readonly record struct OhlcvBar(
    DateTime Timestamp,
    string Symbol,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume);

public readonly record struct HeikinAshiBar(
    DateTime Timestamp,
    string Symbol,
    double Open,
    double High,
    double Low,
    double Close);

public sealed class HeikinAshiCalculator
{
    private bool _isInitialized;
    private double _prevHaOpen;
    private double _prevHaClose;

    public int WarmupBars { get; }

    public HeikinAshiCalculator(int warmupBars = 1)
    {
        if (warmupBars < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupBars), "Warmup bars must be >= 1");
        }
        WarmupBars = warmupBars;
    }

    public void Reset()
    {
        _isInitialized = false;
        _prevHaOpen = 0.0;
        _prevHaClose = 0.0;
    }

    public HeikinAshiBar Update(in OhlcvBar bar)
    {
        // Heikin-Ashi close is the average of the real candle
        double haClose = (bar.Open + bar.High + bar.Low + bar.Close) / 4.0;

        double haOpen;
        if (!_isInitialized)
        {
            // For the very first bar, initialize HA open as average of real open and close
            haOpen = (bar.Open + bar.Close) / 2.0;
            _isInitialized = true;
        }
        else
        {
            // Subsequent bars: average of previous HA open and close
            haOpen = (_prevHaOpen + _prevHaClose) / 2.0;
        }

        // Heikin-Ashi high/low are extremes including haOpen and haClose
        double haHigh = Math.Max(bar.High, Math.Max(haOpen, haClose));
        double haLow = Math.Min(bar.Low, Math.Min(haOpen, haClose));

        // Persist state for next update
        _prevHaOpen = haOpen;
        _prevHaClose = haClose;

        return new HeikinAshiBar(
            Timestamp: bar.Timestamp,
            Symbol: bar.Symbol,
            Open: haOpen,
            High: haHigh,
            Low: haLow,
            Close: haClose);
    }
}
