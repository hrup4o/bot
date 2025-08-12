namespace MarketSignals.Core.Features;

using MarketSignals.Core.TimeSeries;

public sealed class IndicatorEngine : IFeatureEngine
{
    private readonly int _rsiPeriod;
    private readonly int _atrPeriod;
    private readonly int _emaFastPeriod;
    private readonly int _emaSlowPeriod;
    private readonly int _emaSignalPeriod;
    private readonly int _bbPeriod;
    private readonly double _bbK;

    private readonly Dictionary<string, double> _features = new();

    // State for ATR (Wilder)
    private bool _hasPrevClose;
    private double _prevClose;
    private int _atrCount;
    private double _atrSum; // for seeding
    private double _atrRma; // Wilder's smoothed ATR

    // State for RSI (Wilder)
    private int _rsiCount;
    private double _rsiGainSum;
    private double _rsiLossSum;
    private double _rsiAvgGain;
    private double _rsiAvgLoss;

    // State for MACD (EMA)
    private bool _emaInitialized;
    private double _emaFast;
    private double _emaSlow;
    private bool _signalInitialized;
    private double _emaSignal;

    // State for Bollinger (rolling window)
    private readonly RollingWindowStats _bbWindow;

    private int _barsProcessed;
    private bool _isWarm;

    public string Name => "ind";

    public int WarmupBars { get; }

    public bool IsWarm => _isWarm;

    public IndicatorEngine(
        int rsiPeriod = 14,
        int atrPeriod = 14,
        int emaFastPeriod = 12,
        int emaSlowPeriod = 26,
        int emaSignalPeriod = 9,
        int bbPeriod = 20,
        double bbK = 2.0)
    {
        if (rsiPeriod < 2) throw new ArgumentOutOfRangeException(nameof(rsiPeriod));
        if (atrPeriod < 1) throw new ArgumentOutOfRangeException(nameof(atrPeriod));
        if (emaFastPeriod < 1 || emaSlowPeriod <= emaFastPeriod) throw new ArgumentOutOfRangeException(nameof(emaSlowPeriod));
        if (emaSignalPeriod < 1) throw new ArgumentOutOfRangeException(nameof(emaSignalPeriod));
        if (bbPeriod < 2) throw new ArgumentOutOfRangeException(nameof(bbPeriod));
        if (bbK <= 0) throw new ArgumentOutOfRangeException(nameof(bbK));

        _rsiPeriod = rsiPeriod;
        _atrPeriod = atrPeriod;
        _emaFastPeriod = emaFastPeriod;
        _emaSlowPeriod = emaSlowPeriod;
        _emaSignalPeriod = emaSignalPeriod;
        _bbPeriod = bbPeriod;
        _bbK = bbK;

        _bbWindow = new RollingWindowStats(bbPeriod);

        WarmupBars = Math.Max(Math.Max(_rsiPeriod, _atrPeriod), Math.Max(_bbPeriod, _emaSlowPeriod + _emaSignalPeriod));
    }

    public void Reset()
    {
        _features.Clear();

        _hasPrevClose = false;
        _prevClose = 0.0;
        _atrCount = 0;
        _atrSum = 0.0;
        _atrRma = 0.0;

        _rsiCount = 0;
        _rsiGainSum = 0.0;
        _rsiLossSum = 0.0;
        _rsiAvgGain = 0.0;
        _rsiAvgLoss = 0.0;

        _emaInitialized = false;
        _emaFast = 0.0;
        _emaSlow = 0.0;
        _signalInitialized = false;
        _emaSignal = 0.0;

        _bbWindow.Reset();

        _barsProcessed = 0;
        _isWarm = false;
    }

    public void Update(in OhlcvBar bar, in HeikinAshiBar _)
    {
        _barsProcessed++;
        double close = bar.Close;

        // ATR (Wilder)
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
                _atrRma = _atrSum / _atrPeriod; // seed
            }
        }
        else
        {
            // Wilder smoothing: ATR_t = (ATR_{t-1}*(n-1) + TR_t) / n
            _atrRma = ((_atrRma * (_atrPeriod - 1)) + tr) / _atrPeriod;
        }

        // RSI (Wilder)
        if (_barsProcessed > 1)
        {
            double change = close - _prevClose;
            double gain = Math.Max(change, 0.0);
            double loss = Math.Max(-change, 0.0);

            if (_rsiCount < _rsiPeriod)
            {
                _rsiCount++;
                _rsiGainSum += gain;
                _rsiLossSum += loss;
                if (_rsiCount == _rsiPeriod)
                {
                    _rsiAvgGain = _rsiGainSum / _rsiPeriod;
                    _rsiAvgLoss = _rsiLossSum / _rsiPeriod;
                }
            }
            else
            {
                _rsiAvgGain = ((_rsiAvgGain * (_rsiPeriod - 1)) + gain) / _rsiPeriod;
                _rsiAvgLoss = ((_rsiAvgLoss * (_rsiPeriod - 1)) + loss) / _rsiPeriod;
            }
        }

        // MACD (EMA)
        double alphaFast = 2.0 / (_emaFastPeriod + 1);
        double alphaSlow = 2.0 / (_emaSlowPeriod + 1);
        if (!_emaInitialized)
        {
            _emaFast = close;
            _emaSlow = close;
            _emaInitialized = true;
        }
        else
        {
            _emaFast = alphaFast * close + (1.0 - alphaFast) * _emaFast;
            _emaSlow = alphaSlow * close + (1.0 - alphaSlow) * _emaSlow;
        }
        double macd = _emaFast - _emaSlow;

        double alphaSignal = 2.0 / (_emaSignalPeriod + 1);
        if (!_signalInitialized)
        {
            _emaSignal = macd;
            _signalInitialized = true;
        }
        else
        {
            _emaSignal = alphaSignal * macd + (1.0 - alphaSignal) * _emaSignal;
        }
        double macdHist = macd - _emaSignal;

        // Bollinger
        _bbWindow.Add(close);
        double bbMid = _bbWindow.Mean;
        double bbStd = _bbWindow.StdDev;
        double bbUpper = bbMid + _bbK * bbStd;
        double bbLower = bbMid - _bbK * bbStd;
        double bbWidth = bbUpper - bbLower;
        double pctB = Math.Abs(bbWidth) < 1e-12 ? 0.0 : (close - bbLower) / bbWidth;

        // Expose features
        _features[$"ind.rsi{_rsiPeriod}"] = ComputeRsi();
        _features[$"ind.atr{_atrPeriod}"] = _atrCount < _atrPeriod ? 0.0 : _atrRma;
        _features["ind.macd"] = macd;
        _features["ind.macd_signal"] = _emaSignal;
        _features["ind.macd_hist"] = macdHist;
        _features[$"ind.bb{_bbPeriod}_k{TrimDouble(_bbK)}.mid"] = bbMid;
        _features[$"ind.bb{_bbPeriod}_k{TrimDouble(_bbK)}.upper"] = bbUpper;
        _features[$"ind.bb{_bbPeriod}_k{TrimDouble(_bbK)}.lower"] = bbLower;
        _features[$"ind.bb{_bbPeriod}_k{TrimDouble(_bbK)}.width"] = bbWidth;
        _features[$"ind.bb{_bbPeriod}_k{TrimDouble(_bbK)}.pctb"] = pctB;

        _prevClose = close;
        _isWarm = _barsProcessed >= WarmupBars && _atrCount >= _atrPeriod && _rsiCount >= _rsiPeriod && _bbWindow.Count >= _bbPeriod;
    }

    public IReadOnlyDictionary<string, double> GetFeatures() => _features;

    private double ComputeRsi()
    {
        if (_rsiCount < _rsiPeriod)
        {
            return 50.0; // neutral during seeding
        }
        if (_rsiAvgLoss <= 1e-12)
        {
            return 100.0;
        }
        double rs = _rsiAvgGain / _rsiAvgLoss;
        double rsi = 100.0 - (100.0 / (1.0 + rs));
        if (double.IsNaN(rsi) || double.IsInfinity(rsi)) return 50.0;
        return Math.Min(100.0, Math.Max(0.0, rsi));
    }

    private static string TrimDouble(double value)
    {
        var s = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (s.Contains('.'))
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }
        return s;
    }
}