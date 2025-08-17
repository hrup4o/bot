using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketSignals.Core.RawData
{
    /// <summary>
    /// Поддържани таймфреймове (в минути).
    /// </summary>
    public enum Timeframe
    {
        M15 = 15
    }

    /// <summary>Каноничен контейнер за един OHLCV бар с силни инварианти и идентичност.</summary>
    public sealed class OhlcvBar : IEquatable<OhlcvBar>
    {
        /// <summary>Име на символ (нормализирано UpperInvariant/Trim).</summary>
        public string Symbol { get; }
        /// <summary>Таймфрейм.</summary>
        public Timeframe Timeframe { get; }
        /// <summary>Време на затваряне на бара (UTC).</summary>
        public DateTime TimestampUtc { get; }
        /// <summary>Цена Open.</summary>
        public double Open { get; }
        /// <summary>Най-висока цена.</summary>
        public double High { get; }
        /// <summary>Най-ниска цена.</summary>
        public double Low { get; }
        /// <summary>Цена Close.</summary>
        public double Close { get; }
        /// <summary>Тик обем.</summary>
        public long TickVolume { get; }
        /// <summary>Произход/име на фийда (по избор).</summary>
        public string? Source { get; }
        /// <summary>Дали барът е финален (затворен) от фийда.</summary>
        public bool IsFinal { get; }
        /// <summary>Дали стойностите са били санитизирани при създаването.</summary>
        public bool IsSanitized { get; }

        /// <summary>Създава бар с твърда валидация на OHLCV (без санитизация).</summary>
        public OhlcvBar(string symbol, Timeframe timeframe, DateTime timestampUtc,
                        double open, double high, double low, double close, long tickVolume)
        {
            if (symbol is null) throw new ArgumentNullException(nameof(symbol));
            var normSymbol = symbol.Trim().ToUpperInvariant();
            if (normSymbol.Length == 0) throw new ArgumentException("Symbol cannot be empty/whitespace", nameof(symbol));
            Symbol = normSymbol;

            Timeframe = timeframe;
            TimestampUtc = DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);

            if (double.IsNaN(open) || double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
                throw new ArgumentException("OHLC values cannot be NaN");

            if (double.IsInfinity(open) || double.IsInfinity(high) || double.IsInfinity(low) || double.IsInfinity(close))
                throw new ArgumentException("OHLC values cannot be Infinity");

            if (open < 0 || high < 0 || low < 0 || close < 0)
                throw new ArgumentException("OHLC values cannot be negative");

            if (high < low)
                throw new ArgumentException("High cannot be less than Low");

            if (open < low || open > high)
                throw new ArgumentException("Open must be between Low and High");

            if (close < low || close > high)
                throw new ArgumentException("Close must be between Low and High");

            if (tickVolume < 0)
                throw new ArgumentException("TickVolume cannot be negative");

            Open = open;
            High = high;
            Low = low;
            Close = close;
            TickVolume = tickVolume;
            Source = null;
            IsFinal = true;
            IsSanitized = false;
        }

        /// <summary>Разширен конструктор с метаданни (произход/финалност/санитизация).</summary>
        public OhlcvBar(string symbol, Timeframe timeframe, DateTime timestampUtc,
                        double open, double high, double low, double close, long tickVolume,
                        string? source, bool isFinal = true, bool isSanitized = false)
            : this(symbol, timeframe, timestampUtc, open, high, low, close, tickVolume)
        {
            Source = source;
            IsFinal = isFinal;
            IsSanitized = isSanitized;
        }

        /// <summary>
        /// Фабрика с „sanitize“: клъмпва Open/Close в [Low, High], връща бар и флаг дали е имало корекции.
        /// </summary>
        public static OhlcvBar CreateSanitized(string symbol, Timeframe timeframe, DateTime timestampUtc,
                                               double open, double high, double low, double close, long tickVolume,
                                               string? source, bool isFinal, out bool wasSanitized)
        {
            if (double.IsNaN(open) || double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
                throw new ArgumentException("OHLC values cannot be NaN");
            if (double.IsInfinity(open) || double.IsInfinity(high) || double.IsInfinity(low) || double.IsInfinity(close))
                throw new ArgumentException("OHLC values cannot be Infinity");
            if (open < 0 || high < 0 || low < 0 || close < 0)
                throw new ArgumentException("OHLC values cannot be negative");
            if (high < low)
                throw new ArgumentException("High cannot be less than Low");

            wasSanitized = false;
            double o = open, c = close;
            if (o < low) { o = low; wasSanitized = true; }
            if (o > high) { o = high; wasSanitized = true; }
            if (c < low) { c = low; wasSanitized = true; }
            if (c > high) { c = high; wasSanitized = true; }

            return new OhlcvBar(symbol, timeframe, timestampUtc, o, high, low, c, tickVolume, source, isFinal, wasSanitized);
        }

        /// <summary>Equality по (Symbol, Timeframe, TimestampUtc).</summary>
        public bool Equals(OhlcvBar? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Timeframe == other.Timeframe
                && TimestampUtc == other.TimestampUtc
                && string.Equals(Symbol, other.Symbol, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => Equals(obj as OhlcvBar);
        public override int GetHashCode() => HashCode.Combine(Symbol, Timeframe, TimestampUtc);
        public static bool operator ==(OhlcvBar? a, OhlcvBar? b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(OhlcvBar? a, OhlcvBar? b) => !(a == b);

        /// <summary>Сортиране по време на бара (UTC).</summary>
        public static IComparer<OhlcvBar> TimestampComparer { get; } = new TimestampComparerImpl();
        private sealed class TimestampComparerImpl : IComparer<OhlcvBar>
        {
            public int Compare(OhlcvBar? x, OhlcvBar? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;
                return x.TimestampUtc.CompareTo(y.TimestampUtc);
            }
        }
    }

    /// <summary>
    /// Интерфейс за източник на OHLCV барове: история и абонамент.
    /// </summary>
    public interface IBarFeed
    {
        IEnumerable<OhlcvBar> GetHistory(string symbol, Timeframe timeframe, DateTime startUtcInclusive, DateTime endUtcInclusive);
        IDisposable Subscribe(string symbol, Timeframe timeframe, Action<OhlcvBar> onBarClosed);
    }

    /// <summary>
    /// In-memory имплементация за тестове и разработки.
    /// </summary>
    public sealed class InMemoryBarFeed : IBarFeed
    {
        private readonly Dictionary<(string Symbol, Timeframe Tf), List<OhlcvBar>> _series = new();

        /// <summary>
        /// Добавя серия(и) барове; гарантира сортиране по време и дедупликация по TimestampUtc (запазва последния).
        /// </summary>
        public void AddSeries(IEnumerable<OhlcvBar> bars)
        {
            foreach (var group in bars.GroupBy(b => (b.Symbol, b.Timeframe)))
            {
                if (!_series.TryGetValue(group.Key, out var list))
                {
                    list = new List<OhlcvBar>();
                    _series[group.Key] = list;
                }

                list.AddRange(group);
                list.Sort(OhlcvBar.TimestampComparer);

                var dedup = list
                    .GroupBy(b => b.TimestampUtc)
                    .Select(g => g.Last())
                    .OrderBy(b => b.TimestampUtc)
                    .ToList();

                list.Clear();
                list.AddRange(dedup);
            }
        }

        public IEnumerable<OhlcvBar> GetHistory(string symbol, Timeframe timeframe, DateTime startUtcInclusive, DateTime endUtcInclusive)
        {
            if (!_series.TryGetValue((symbol.Trim().ToUpperInvariant(), timeframe), out var list)) return Enumerable.Empty<OhlcvBar>();
            var start = startUtcInclusive.ToUniversalTime();
            var end = endUtcInclusive.ToUniversalTime();
            return list.Where(b => b.TimestampUtc >= start && b.TimestampUtc <= end);
        }

        public IDisposable Subscribe(string symbol, Timeframe timeframe, Action<OhlcvBar> onBarClosed)
        {
            return new DummyUnsubscriber();
        }

        private sealed class DummyUnsubscriber : IDisposable { public void Dispose() { } }
    }
}

namespace MarketSignals.Core.Metrics
{
    using MarketSignals.Core.RawData;

    /// <summary>
    /// Конфигурация за изчисляване на метрички върху OHLCV свещи.
    /// </summary>
    public sealed class OhlcvMetricsConfig
    {
        public int RangeMaShortWindow { get; init; } = 20;
        public int RangeMaLongWindow { get; init; } = 100;
        public int PrevNForBreak { get; init; } = 10;
        public int SwingLookback { get; init; } = 20;

        public void Validate()
        {
            if (RangeMaShortWindow <= 0)
                throw new ArgumentException("RangeMaShortWindow must be positive");
            if (RangeMaLongWindow <= RangeMaShortWindow)
                throw new ArgumentException("RangeMaLongWindow must be greater than RangeMaShortWindow");
            if (PrevNForBreak <= 0)
                throw new ArgumentException("PrevNForBreak must be positive");
            if (SwingLookback <= 0)
                throw new ArgumentException("SwingLookback must be positive");
        }
    }

    /// <summary>
    /// Резултати от изчисленията върху свещта и ролинг прозорците. Имената са агностични към конфиг стойностите.
    /// </summary>
    public sealed class OhlcvMetrics
    {
        public double Range { get; init; }
        public double Body { get; init; }
        public double UpperWick { get; init; }
        public double LowerWick { get; init; }
        public double BodyToRange { get; init; }
        public double WickAsymmetry { get; init; }
        public double ClosePosInRange { get; init; }

        public double MarubozuScore { get; init; }
        public double DojiScore { get; init; }

        /// <summary>Посока на свещта: -1 / 0 / +1.</summary>
        public int Direction { get; init; }

        /// <summary>Подписан гъп спрямо предходния close: Open - PrevClose.</summary>
        public double Gap { get; init; }
        /// <summary>Абсолютен гъп |Open - PrevClose|.</summary>
        public double GapAbs { get; init; }
        /// <summary>Подписан гъп в проценти спрямо предходния close.</summary>
        public double GapPct { get; init; }

        /// <summary>Плъзгаща средна на диапазона по късия прозорец.</summary>
        public double? RangeMaShort { get; init; }
        /// <summary>Плъзгаща средна на диапазона по дългия прозорец.</summary>
        public double? RangeMaLong { get; init; }




        /// <summary>Нормализирана дистанция до максимум от последните N бара.</summary>
        public double? DistToPrevHighN { get; init; }
        /// <summary>Нормализирана дистанция до минимум от последните N бара.</summary>
        public double? DistToPrevLowN { get; init; }

        /// <summary>Брой барове от последен swing high в рамките на прозореца (SwingLookback).</summary>
        public int? BarsSinceSwingHigh { get; init; }
        /// <summary>Брой барове от последен swing low в рамките на прозореца (SwingLookback).</summary>
        public int? BarsSinceSwingLow { get; init; }

        /// <summary>Достатъчно ли са обработените барове за всички ролинг метрики.</summary>
        public bool IsRollingReady { get; init; }
    }

    /// <summary>
    /// Двигател за изчисляване на свещни и ролинг метрики върху поток от барове.
    /// </summary>
    public sealed class OhlcvMetricsEngine
    {
        private readonly OhlcvMetricsConfig _config;
        private const double Epsilon = 1e-12;

        private bool _hasPrev;
        private double _prevClose;
        private double _prevHigh;
        private double _prevLow;

        private readonly Queue<double> _rangeW20 = new();
        private readonly Queue<double> _rangeW100 = new();
        private double _sumRangeW20;
        private double _sumRangeW100;

        private readonly Queue<double> _highsPrevN = new();
        private readonly Queue<double> _lowsPrevN = new();

        private readonly Queue<double> _highsSwing = new();
        private readonly Queue<double> _lowsSwing = new();

        private int _barsProcessed;

        public OhlcvMetricsEngine(OhlcvMetricsConfig? config = null)
        {
            _config = config ?? new OhlcvMetricsConfig();
            _config.Validate();
            Reset();
        }

        /// <summary>Нулира вътрешното състояние и прозорците.</summary>
        public void Reset()
        {
            _hasPrev = false;
            _prevClose = 0;
            _prevHigh = 0;
            _prevLow = 0;

            _rangeW20.Clear();
            _rangeW100.Clear();
            _sumRangeW20 = 0;
            _sumRangeW100 = 0;

            _highsPrevN.Clear();
            _lowsPrevN.Clear();

            _highsSwing.Clear();
            _lowsSwing.Clear();

            _barsProcessed = 0;
        }

        /// <summary>
        /// Обработва следващ бар и връща метриките за него.
        /// </summary>
        public OhlcvMetrics ComputeNext(OhlcvBar bar)
        {
            if (bar == null)
                throw new ArgumentNullException(nameof(bar));

            _barsProcessed++;

            double o = bar.Open;
            double h = bar.High;
            double l = bar.Low;
            double c = bar.Close;

            double range = Math.Max(h - l, 0.0);
            double body = Math.Abs(c - o);
            double upperW = Math.Max(h - Math.Max(o, c), 0.0);
            double lowerW = Math.Max(Math.Min(o, c) - l, 0.0);
            double denom = Math.Max(range, Epsilon);

            double bodyToRange = body / denom;
            double wickAsymmetry = (upperW - lowerW) / denom;
            double closePosInRange = (c - l) / denom;

            double marubozuScore = 1.0 - ((upperW + lowerW) / denom);
            double dojiScore = 1.0 - bodyToRange;

            int direction = c > o ? 1 : (c < o ? -1 : 0);

            // Gap: подписан, абсолютен и процент
            double gapSigned = _hasPrev ? (o - _prevClose) : 0.0;
            double gapAbs = Math.Abs(gapSigned);
            double gapPct = (_hasPrev && Math.Abs(_prevClose) > Epsilon) ? (gapSigned / _prevClose) : 0.0;

            UpdateFixedWindow(_rangeW20, ref _sumRangeW20, range, _config.RangeMaShortWindow);
            UpdateFixedWindow(_rangeW100, ref _sumRangeW100, range, _config.RangeMaLongWindow);

            double? distToPrevHighN = null;
            double? distToPrevLowN = null;

            if (_highsPrevN.Count >= _config.PrevNForBreak && _lowsPrevN.Count >= _config.PrevNForBreak)
            {
                double prevNHigh = MaxQueue(_highsPrevN);
                double prevNLow = MinQueue(_lowsPrevN);

                distToPrevHighN = Math.Abs(c) > Epsilon ? (prevNHigh - c) / Math.Abs(c) : 0.0;
                distToPrevLowN = Math.Abs(c) > Epsilon ? (c - prevNLow) / Math.Abs(c) : 0.0;
            }

            int? barsSinceSwingHigh = null;
            int? barsSinceSwingLow = null;

            if (_highsSwing.Count > 0)
                barsSinceSwingHigh = IndexFromEndOfMax(_highsSwing);
            if (_lowsSwing.Count > 0)
                barsSinceSwingLow = IndexFromEndOfMin(_lowsSwing);

            double? maShort = _rangeW20.Count == _config.RangeMaShortWindow ? _sumRangeW20 / _rangeW20.Count : (double?)null;
            double? maLong = _rangeW100.Count == _config.RangeMaLongWindow ? _sumRangeW100 / _rangeW100.Count : (double?)null;

            int required = Math.Max(_config.RangeMaLongWindow, _config.PrevNForBreak);
            required = Math.Max(required, _config.SwingLookback);
            bool isRollingReady = _barsProcessed >= required;

            EnqueueWithLimit(_highsPrevN, h, _config.PrevNForBreak);
            EnqueueWithLimit(_lowsPrevN, l, _config.PrevNForBreak);

            EnqueueWithLimit(_highsSwing, h, _config.SwingLookback);
            EnqueueWithLimit(_lowsSwing, l, _config.SwingLookback);

            _prevClose = c;
            _prevHigh = h;
            _prevLow = l;
            _hasPrev = true;

            return new OhlcvMetrics
            {
                Range = range,
                Body = body,
                UpperWick = upperW,
                LowerWick = lowerW,
                BodyToRange = bodyToRange,
                WickAsymmetry = wickAsymmetry,
                ClosePosInRange = closePosInRange,

                MarubozuScore = marubozuScore,
                DojiScore = dojiScore,

                Direction = direction,
                Gap = gapSigned,
                GapAbs = gapAbs,
                GapPct = gapPct,

                RangeMaShort = maShort,
                RangeMaLong = maLong,
                DistToPrevHighN = distToPrevHighN,
                DistToPrevLowN = distToPrevLowN,

                BarsSinceSwingHigh = barsSinceSwingHigh,
                BarsSinceSwingLow = barsSinceSwingLow,

                IsRollingReady = isRollingReady
            };
        }

        private static void EnqueueWithLimit(Queue<double> q, double value, int limit)
        {
            q.Enqueue(value);
            while (q.Count > limit) q.Dequeue();
        }

        private static double MaxQueue(Queue<double> q)
        {
            double max = double.NegativeInfinity;
            foreach (var v in q) if (v > max) max = v;
            return max;
        }

        private static double MinQueue(Queue<double> q)
        {
            double min = double.PositiveInfinity;
            foreach (var v in q) if (v < min) min = v;
            return min;
        }

        private static int IndexFromEndOfMax(Queue<double> q)
        {
            int idx = -1;
            double max = double.NegativeInfinity;
            int i = 0;
            foreach (var v in q)
            {
                if (v >= max) { max = v; idx = i; }
                i++;
            }
            int fromEnd = (q.Count - 1) - idx;
            return Math.Max(0, fromEnd);
        }

        private static int IndexFromEndOfMin(Queue<double> q)
        {
            int idx = -1;
            double min = double.PositiveInfinity;
            int i = 0;
            foreach (var v in q)
            {
                if (v <= min) { min = v; idx = i; }
                i++;
            }
            int fromEnd = (q.Count - 1) - idx;
            return Math.Max(0, fromEnd);
        }

        private void UpdateFixedWindow(Queue<double> window, ref double sum, double value, int capacity)
        {
            window.Enqueue(value);
            sum += value;
            if (window.Count > capacity)
            {
                var removed = window.Dequeue();
                sum -= removed;
            }
        }


    }
}
