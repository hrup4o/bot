using MarketSignals.Core.TimeSeries;

namespace MarketSignals.Core.Features;

public sealed class HeikenAshiMetricsEngine : IFeatureEngine
{
    private readonly RollingWindowStats _rangeStats;
    private readonly double _dojiBodyRatioThreshold;
    private readonly double _wideRangeZThreshold;

    private readonly Dictionary<string, double> _features = new();
    private bool _isWarm;

    public string Name => "ha.metrics";

    public int WarmupBars { get; }

    public bool IsWarm => _isWarm;

    public HeikenAshiMetricsEngine(int warmupBars = 50, double dojiBodyRatioThreshold = 0.1, double wideRangeZThreshold = 1.5)
    {
        if (warmupBars < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupBars), "Warmup must be >= 2");
        }
        WarmupBars = warmupBars;
        _rangeStats = new RollingWindowStats(warmupBars);
        _dojiBodyRatioThreshold = dojiBodyRatioThreshold;
        _wideRangeZThreshold = wideRangeZThreshold;
    }

    public void Reset()
    {
        _rangeStats.Reset();
        _features.Clear();
        _isWarm = false;
    }

    public void Update(in OhlcvBar bar, in HeikinAshiBar ha)
    {
        double body = ha.Close - ha.Open;
        double range = Math.Max(ha.High - ha.Low, 0.0);

        double upperWick = Math.Max(ha.High - Math.Max(ha.Open, ha.Close), 0.0);
        double lowerWick = Math.Max(Math.Min(ha.Open, ha.Close) - ha.Low, 0.0);

        double safeDiv(double numerator, double denom)
        {
            return Math.Abs(denom) < 1e-12 ? 0.0 : numerator / denom;
        }

        double bodyRatio = safeDiv(Math.Abs(body), range);
        double upperWickRatio = safeDiv(upperWick, range);
        double lowerWickRatio = safeDiv(lowerWick, range);

        // Compute z-score BEFORE adding current to stats to avoid peeking
        double zRange = _rangeStats.Count >= 2 ? _rangeStats.ZScore(range) : 0.0;

        // Update stats after computing z
        _rangeStats.Add(range);
        if (_rangeStats.Count >= WarmupBars)
        {
            _isWarm = true;
        }

        double buyPressure = Math.Max(body, 0.0);
        double sellPressure = Math.Max(-body, 0.0);
        double netPressure = body;

        int isBullish = body >= 0 ? 1 : 0;
        int isDoji = bodyRatio <= _dojiBodyRatioThreshold ? 1 : 0;
        int isWideRange = zRange >= _wideRangeZThreshold ? 1 : 0;

        _features["ha.body"] = body;
        _features["ha.range"] = range;
        _features["ha.upper_wick"] = upperWick;
        _features["ha.lower_wick"] = lowerWick;
        _features["ha.body_ratio"] = bodyRatio;
        _features["ha.upper_wick_ratio"] = upperWickRatio;
        _features["ha.lower_wick_ratio"] = lowerWickRatio;
        _features["ha.zscore_range"] = zRange;
        _features["ha.buy_pressure"] = buyPressure;
        _features["ha.sell_pressure"] = sellPressure;
        _features["ha.net_pressure"] = netPressure;
        _features["ha.is_bullish"] = isBullish;
        _features["ha.is_doji"] = isDoji;
        _features["ha.is_wide_range"] = isWideRange;
    }

    public IReadOnlyDictionary<string, double> GetFeatures() => _features;
}