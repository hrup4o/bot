using MarketSignals.Core.TimeSeries;

namespace MarketSignals.Core.Features;

public sealed class SpikeHeuristicsEngine : IFeatureEngine
{
    private readonly int _atrPeriod;
    private readonly int _lookbackBreakout;
    private readonly double _spikeZThreshold;

    private readonly Dictionary<string, double> _features = new();

    private bool _hasPrevClose;
    private double _prevClose;

    private int _atrCount;
    private double _atrSum;
    private double _atrRma;

    private readonly RollingWindowStats _rangeStats;
    private readonly RollingWindowMax _hh;
    private readonly RollingWindowMin _ll;

    private int _barsProcessed;
    private bool _isWarm;

    public string Name => "spike";
    public int WarmupBars { get; }
    public bool IsWarm => _isWarm;

    public SpikeHeuristicsEngine(int atrPeriod = 14, int lookbackBreakout = 20, double spikeZThreshold = 2.0)
    {
        if (atrPeriod < 1) throw new ArgumentOutOfRangeException(nameof(atrPeriod));
        if (lookbackBreakout < 1) throw new ArgumentOutOfRangeException(nameof(lookbackBreakout));
        if (spikeZThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(spikeZThreshold));

        _atrPeriod = atrPeriod;
        _lookbackBreakout = lookbackBreakout;
        _spikeZThreshold = spikeZThreshold;

        _rangeStats = new RollingWindowStats(atrPeriod);
        _hh = new RollingWindowMax(lookbackBreakout);
        _ll = new RollingWindowMin(lookbackBreakout);

        WarmupBars = Math.Max(atrPeriod, lookbackBreakout);
    }

    public void Reset()
    {
        _features.Clear();
        _hasPrevClose = false;
        _prevClose = 0.0;
        _atrCount = 0;
        _atrSum = 0.0;
        _atrRma = 0.0;
        _rangeStats.Reset();
        _hh.Reset();
        _ll.Reset();
        _barsProcessed = 0;
        _isWarm = false;
    }

    public void Update(in OhlcvBar bar, in HeikinAshiBar ha)
    {
        _barsProcessed++;

        // True Range for ATR
        double tr;
        if (_hasPrevClose)
        {
            tr = Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - _prevClose), Math.Abs(bar.Low - _prevClose)));
        }
        else
        {
            tr = bar.High - bar.Low;
            _hasPrevClose = true;
        }

        if (_atrCount < _atrPeriod)
        {
            _atrCount++;
            _atrSum += tr;
            if (_atrCount == _atrPeriod)
            {
                _atrRma = _atrSum / _atrPeriod;
            }
        }
        else
        {
            _atrRma = ((_atrRma * (_atrPeriod - 1)) + tr) / _atrPeriod;
        }

        // Range and Z-score (based on HA bar for robustness)
        double haRange = Math.Max(ha.High - ha.Low, 0.0);
        double zRange = _rangeStats.Count >= 2 ? _rangeStats.ZScore(haRange) : 0.0;
        _rangeStats.Add(haRange);

        // Gaps
        double gapCloseToOpen = _barsProcessed > 1 ? ha.Open - _prevClose : 0.0;
        double gapAbs = Math.Abs(gapCloseToOpen);

        // Breakouts using real H/L
        _hh.Add(bar.High);
        _ll.Add(bar.Low);
        double highest = _hh.Max;
        double lowest = _ll.Min;
        int isHighBreakout = !double.IsNaN(highest) && bar.High >= highest ? 1 : 0;
        int isLowBreakout = !double.IsNaN(lowest) && bar.Low <= lowest ? 1 : 0;

        // ATR-normalized features
        double safeDiv(double num, double den) => Math.Abs(den) < 1e-12 ? 0.0 : num / den;
        double rangeOverAtr = safeDiv(haRange, _atrRma);
        double gapOverAtr = safeDiv(gapAbs, _atrRma);

        // Wick-based asymmetry as potential spike indicator
        double upperWick = Math.Max(ha.High - Math.Max(ha.Open, ha.Close), 0.0);
        double lowerWick = Math.Max(Math.Min(ha.Open, ha.Close) - ha.Low, 0.0);
        double wickAsym = safeDiv(upperWick - lowerWick, Math.Max(haRange, 1e-12));

        int isSpikeByZ = zRange >= _spikeZThreshold ? 1 : 0;
        int isSpikeByAtr = rangeOverAtr >= 2.0 ? 1 : 0; // heuristic threshold

        _features["spike.ha_range"] = haRange;
        _features["spike.zscore_range"] = zRange;
        _features["spike.range_over_atr"] = double.IsFinite(rangeOverAtr) ? rangeOverAtr : 0.0;
        _features["spike.gap_abs"] = gapAbs;
        _features["spike.gap_over_atr"] = double.IsFinite(gapOverAtr) ? gapOverAtr : 0.0;
        _features[$"spike.breakout_h{_lookbackBreakout}"] = isHighBreakout;
        _features[$"spike.breakout_l{_lookbackBreakout}"] = isLowBreakout;
        _features["spike.wick_asym"] = wickAsym;
        _features["spike.is_spike_z"] = isSpikeByZ;
        _features["spike.is_spike_atr"] = isSpikeByAtr;

        _prevClose = bar.Close;
        _isWarm = _barsProcessed >= WarmupBars && _atrCount >= _atrPeriod;
    }

    public IReadOnlyDictionary<string, double> GetFeatures() => _features;
}