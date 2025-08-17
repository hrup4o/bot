using System;
using System.Collections.Generic;
using System.Linq;
using MarketSignals.Core.RawData;

namespace MarketSignals.Core.Metrics
{
    /// <summary>
    /// Конфигурация за Heiken-Ashi метрики, пригодени за ML (без Z-score/percentile/булеви прагове).
    /// Всички изчисления са „raw/чисти“ и past-only за статистики (без leakage).
    /// </summary>
    public sealed class HeikenAshiMetricsConfig
    {
        public int RangeMaShortWindow { get; init; } = 20;
        public int RangeMaLongWindow { get; init; } = 100;

        /// <summary>Прозорец за past-only статистики (mean/std/dev-from-mean) за HA Range и HA Parkinson.</summary>
        public int StatsWindow { get; init; } = 100;

        /// <summary>Къс и дълъг прозорец за OLS/Linear производни върху HA Close.</summary>
        public int OlsWindowShort { get; init; } = 20;
        public int OlsWindowLong { get; init; } = 50;

        /// <summary>Прозорец за MA на обема и съпътстващи метрики.</summary>
        public int VolumeMaWindow { get; init; } = 20;

        /// <summary>Размер N за структурни максимуми/минимуми (prev N).</summary>
        public int PrevNForBreak { get; init; } = 10;

        /// <summary>Lookback за swing high/low и време от последния swing.</summary>
        public int SwingLookback { get; init; } = 20;

        /// <summary>Множител за Bollinger ширина (абсолютна/отн.), базирана на HA Close std.</summary>
        public double BbStdMult { get; init; } = 2.0;

        /// <summary>Множител за Keltner proxy ширина (базиран на среден HA диапазон).</summary>
        public double KcMult { get; init; } = 1.5;

        /// <summary>ЕМА алфа за CLV/pressure индикатор (HA).</summary>
        public double PressureEmaAlpha { get; init; } = 2.0 / (20.0 + 1.0);

        /// <summary>ЕМА алфа за оценка на телесна сила (HA).</summary>
        public double TrendBodyEmaAlpha { get; init; } = 2.0 / (20.0 + 1.0);

        public void Validate()
        {
            if (RangeMaShortWindow <= 0) throw new ArgumentException("RangeMaShortWindow must be positive");
            if (RangeMaLongWindow <= RangeMaShortWindow) throw new ArgumentException("RangeMaLongWindow must be greater than RangeMaShortWindow");
            if (StatsWindow <= 1) throw new ArgumentException("StatsWindow must be greater than 1");
            if (OlsWindowShort <= 2) throw new ArgumentException("OlsWindowShort must be greater than 2");
            if (OlsWindowLong <= OlsWindowShort) throw new ArgumentException("OlsWindowLong must be greater than OlsWindowShort");
            if (VolumeMaWindow <= 0) throw new ArgumentException("VolumeMaWindow must be positive");
            if (PrevNForBreak <= 0) throw new ArgumentException("PrevNForBreak must be positive");
            if (SwingLookback <= 0) throw new ArgumentException("SwingLookback must be positive");
            if (BbStdMult <= 0) throw new ArgumentException("BbStdMult must be positive");
            if (KcMult <= 0) throw new ArgumentException("KcMult must be positive");
            if (PressureEmaAlpha <= 0 || PressureEmaAlpha >= 1) throw new ArgumentException("PressureEmaAlpha must be in (0,1)");
            if (TrendBodyEmaAlpha <= 0 || TrendBodyEmaAlpha >= 1) throw new ArgumentException("TrendBodyEmaAlpha must be in (0,1)");
        }
    }

    /// <summary>
    /// Чисти Heiken-Ashi метрики и производни (без Z-score/percentile/булеви флагове).
    /// Подходящи за ML: непрекъснати стойности, past-only статистики. Консумира се от логера/препроцесинга.
    /// </summary>
    public sealed class HeikenAshiMetrics
    {
        // HA OHLC
        public double HaOpen { get; init; }
        public double HaHigh { get; init; }
        public double HaLow { get; init; }
        public double HaClose { get; init; }

        // Геометрия на свещ (чисти)
        public double HaRange { get; init; }
        public double HaBody { get; init; }
        public double HaUpperWick { get; init; }
        public double HaLowerWick { get; init; }
        public double HaBodyToRange { get; init; }
        public double HaWickAsymmetry { get; init; }
        public double HaClosePosInRange { get; init; }

        // Непрекъснати pattern-скоринг метрики
        public double HaMarubozuScore { get; init; }
        public double HaDojiScore { get; init; }
        public double HaPinBarScore { get; init; }

        // Direction / Gaps
        public int HaDirection { get; init; } // -1 / 0 / +1
        public double HaGap { get; init; }       // HaOpen - PrevHaClose
        public double HaGapAbs { get; init; }
        public double HaGapPct { get; init; }

        // Реализационна волатилност (чисти формули върху HA)
        public double HaParkinsonRv { get; init; }
        public double HaGarmanKlassRv { get; init; }
        public double HaRogersSatchellRv { get; init; }

        // Rolling „чисти“ статистики и MA върху HA Range и Parkinson (past-only)
        public double? HaRangeMaShort { get; init; }
        public double? HaRangeMaLong { get; init; }
        public double? HaRangeMeanW { get; init; }
        public double? HaRangeStdW { get; init; }
        public double? HaRangeDevFromMeanW { get; init; }
        public double? HaParkMeanW { get; init; }
        public double? HaParkStdW { get; init; }
        public double? HaParkDevFromMeanW { get; init; }

        // OLS/Linear производни върху HA Close
        public double? HaOlsSlopeShort { get; init; }
        public double? HaOlsSlopeLong { get; init; }
        public double? HaOlsSlopeSEShort { get; init; }
        public double? HaOlsSlopeMSEShort { get; init; }
        public double? HaLinearSlopeShort { get; init; }
        public double? HaLinearSlopeLong { get; init; }
        public double? HaOlsAngleShortDeg { get; init; }
        public double? HaOlsAngleLongDeg { get; init; }
        public double? HaLinearAngleShortDeg { get; init; }
        public double? HaLinearAngleLongDeg { get; init; }
        public double? HaAccelerationShort { get; init; } // Δ OLS slope short
        public double? HaCurvature { get; init; }         // OLS short - OLS long

        // Trend proxy (чисти)
        public int HaColorRunLen { get; init; }
        public double? HaTrendStrength { get; init; } // runLen * EMA(BodyToRange)

        // Обем/pressure (чисти)
        public double HaClv { get; init; }
        public double? HaClvEmaShort { get; init; }
        public double? HaVolumeMaShort { get; init; }
        public double? HaVolumeRatio { get; init; }
        public double? HaPressureIndex { get; init; }
        public double HaBodyPressure { get; init; }
        public double HaWickPressure { get; init; }

        // Патърни/структура/ликвидност (чисти score/distance)
        public double? HaEngulfingScore { get; init; }
        public double? HaFvgUp { get; init; }
        public double? HaFvgDown { get; init; }
        public double? HaSweepUpScore { get; init; }
        public double? HaSweepDownScore { get; init; }

        // Дистанции/време (без булеви BOS)
        public double? HaDistToPrevHighN { get; init; }
        public double? HaDistToPrevLowN { get; init; }
        public double? HaDistToSwingHigh { get; init; }
        public double? HaDistToSwingLow { get; init; }
        public int? HaBarsSinceSwingHigh { get; init; }
        public int? HaBarsSinceSwingLow { get; init; }

        // Компресия/„squeeze“ (чисти)
        public double? HaBbWidthShort { get; init; }
        public double? HaKcWidthShortProxy { get; init; }
        public double? HaSqueezeProxy { get; init; }
        public double? HaCompressionProxy { get; init; }
    }

    /// <summary>
    /// Двигател за Heiken-Ashi метрики, пречистени за ML (без Z-score/percentile/флагове).
    /// - Вход: OhlcvBar (от MarketSignals.Core.RawData).
    /// - Изход: HeikenAshiMetrics (използва се от логера/препроцесинга).
    /// - Няма директна зависимост към индикаторите; може да се комбинира с IndicatorEngine в ChartSelectionLogger.
    /// </summary>
    public sealed class HeikenAshiMetricsEngine
    {
        private readonly HeikenAshiMetricsConfig _cfg;
        private const double Eps = 1e-12;

        // Предишни HA стойности / състояние
        private bool _hasPrev;
        private double _prevHaOpen, _prevHaClose, _prevHaHigh, _prevHaLow;
        private int _barsProcessed;
        private int _haRunLen; // последователност от еднакъв цвят свещи

        // Windows за HA Range (MA)
        private readonly Queue<double> _haRangeWShort = new();
        private double _sumHaRangeWShort;
        private readonly Queue<double> _haRangeWLong = new();
        private double _sumHaRangeWLong;

        // Past-only статистики за HA Range и HA Parkinson
        private readonly Queue<double> _haRangeStatsW = new();
        private double _sumHaRangeStats, _sumHaRangeStatsSq;
        private readonly Queue<double> _haParkStatsW = new();
        private double _sumHaParkStats, _sumHaParkStatsSq;

        // HA Close за OLS/Linear
        private readonly Queue<double> _haCloseWShort = new();
        private double _sumHaCloseShort, _sumHaCloseShortSq;
        private readonly Queue<double> _haCloseWLong = new();
        private double _sumHaCloseLong, _sumHaCloseLongSq;
        private double? _prevHaOlsSlopeShort;

        // Volume и EMA-та
        private readonly Queue<long> _haVolWShort = new();
        private long _sumHaVolShort;
        private double? _haClvEmaShort;
        private double? _haBodyToRangeEmaShort;

        // Structure windows
        private readonly Queue<double> _haHighsPrevN = new();
        private readonly Queue<double> _haLowsPrevN = new();
        private readonly Queue<double> _haHighsSwing = new();
        private readonly Queue<double> _haLowsSwing = new();

        /// <summary>Създава нов двигател с конфигурация. Валидира конфигурацията.</summary>
        public HeikenAshiMetricsEngine(HeikenAshiMetricsConfig? cfg = null)
        {
            _cfg = cfg ?? new HeikenAshiMetricsConfig();
            _cfg.Validate();
            Reset();
        }

        /// <summary>
        /// Нулира вътрешното състояние и прозорците. Извикай при смяна на символ/таймфрейм или при clean start.
        /// </summary>
        public void Reset()
        {
            _hasPrev = false;
            _prevHaOpen = _prevHaClose = _prevHaHigh = _prevHaLow = 0.0;
            _barsProcessed = 0;
            _haRunLen = 0;

            _haRangeWShort.Clear(); _sumHaRangeWShort = 0;
            _haRangeWLong.Clear(); _sumHaRangeWLong = 0;

            _haRangeStatsW.Clear(); _sumHaRangeStats = 0; _sumHaRangeStatsSq = 0;
            _haParkStatsW.Clear(); _sumHaParkStats = 0; _sumHaParkStatsSq = 0;

            _haCloseWShort.Clear(); _sumHaCloseShort = 0; _sumHaCloseShortSq = 0;
            _haCloseWLong.Clear(); _sumHaCloseLong = 0; _sumHaCloseLongSq = 0;
            _prevHaOlsSlopeShort = null;

            _haVolWShort.Clear(); _sumHaVolShort = 0;
            _haClvEmaShort = null;
            _haBodyToRangeEmaShort = null;

            _haHighsPrevN.Clear(); _haLowsPrevN.Clear();
            _haHighsSwing.Clear(); _haLowsSwing.Clear();
        }

        /// <summary>
        /// Обработва следващ OHLCV бар, конвертира към HA и връща Heiken-Ashi метриките (чисти).
        /// </summary>
        /// <param name="bar">OhlcvBar от MarketSignals.Core.RawData (времева решетка/символ/таймфрейм).</param>
        public HeikenAshiMetrics ComputeNext(OhlcvBar bar)
        {
            _barsProcessed++;

            double o = bar.Open, h = bar.High, l = bar.Low, c = bar.Close;

            // HA OHLC
            double haClose = (o + h + l + c) / 4.0;
            double haOpen = _hasPrev ? (_prevHaOpen + _prevHaClose) / 2.0 : (o + c) / 2.0;
            double haHigh = Math.Max(h, Math.Max(haOpen, haClose));
            double haLow = Math.Min(l, Math.Min(haOpen, haClose));

            // Геометрия
            double haRange = Math.Max(haHigh - haLow, 0.0);
            double haBody = Math.Abs(haClose - haOpen);
            double haUpper = Math.Max(haHigh - Math.Max(haOpen, haClose), 0.0);
            double haLower = Math.Max(Math.Min(haOpen, haClose) - haLow, 0.0);
            double denom = Math.Max(haRange, Eps);

            double haBodyToRange = haBody / denom;
            double haWickAsym = (haUpper - haLower) / denom;
            double haClosePosInRange = (haClose - haLow) / denom;

            double haMarubozuScore = 1.0 - ((haUpper + haLower) / denom);
            double haDojiScore = 1.0 - haBodyToRange;
            double haPinBarScore = Math.Max(haUpper, haLower) / denom - haBodyToRange;

            int haDirection = haClose > haOpen ? 1 : (haClose < haOpen ? -1 : 0);

            // Gap (подписан/абсолютен/процентен) спрямо предишен HA close
            double haGap = _hasPrev ? (haOpen - _prevHaClose) : 0.0;
            double haGapAbs = Math.Abs(haGap);
            double haGapPct = (_hasPrev && Math.Abs(_prevHaClose) > Eps) ? (haGap / _prevHaClose) : 0.0;

            // RV върху HA
            double haPark = (haHigh > haLow + Eps) ? Math.Pow(Math.Log(haHigh / haLow), 2) / (4.0 * Math.Log(2.0)) : 0.0;
            double haGK = (haHigh > haLow + Eps && haOpen > Eps && haClose > Eps)
                ? (0.5 * Math.Pow(Math.Log(haHigh / haLow), 2) - (2.0 * Math.Log(2.0) - 1.0) * Math.Pow(Math.Log(haClose / haOpen), 2))
                : 0.0;
            double haRS = (haOpen > Eps && haClose > Eps)
                ? (Math.Log(haHigh / haClose) * Math.Log(haHigh / haOpen) + Math.Log(haLow / haClose) * Math.Log(haLow / haOpen))
                : 0.0;

            // HA Range MA
            EnqueueFixed(_haRangeWShort, ref _sumHaRangeWShort, haRange, _cfg.RangeMaShortWindow);
            EnqueueFixed(_haRangeWLong, ref _sumHaRangeWLong, haRange, _cfg.RangeMaLongWindow);
            double? haRangeMaShort = _haRangeWShort.Count == _cfg.RangeMaShortWindow ? _sumHaRangeWShort / _haRangeWShort.Count : (double?)null;
            double? haRangeMaLong = _haRangeWLong.Count == _cfg.RangeMaLongWindow ? _sumHaRangeWLong / _haRangeWLong.Count : (double?)null;

            // Past-only stats за HA Range и HA Parkinson (без leakage)
            PastOnlyMeanStd(_haRangeStatsW, ref _sumHaRangeStats, ref _sumHaRangeStatsSq, haRange, _cfg.StatsWindow,
                            out double? haRangeMeanW, out double? haRangeStdW, out double? haRangeDevFromMeanW);
            PastOnlyMeanStd(_haParkStatsW, ref _sumHaParkStats, ref _sumHaParkStatsSq, haPark, _cfg.StatsWindow,
                            out double? haParkMeanW, out double? haParkStdW, out double? haParkDevFromMeanW);

            // OLS/Linear върху HA Close
            EnqueueClose(_haCloseWShort, ref _sumHaCloseShort, ref _sumHaCloseShortSq, haClose, _cfg.OlsWindowShort);
            EnqueueClose(_haCloseWLong, ref _sumHaCloseLong, ref _sumHaCloseLongSq, haClose, _cfg.OlsWindowLong);

            double? haLinearSlopeShort = _haCloseWShort.Count == _cfg.OlsWindowShort ? LinearSlope(_haCloseWShort) : (double?)null;
            double? haLinearSlopeLong = _haCloseWLong.Count == _cfg.OlsWindowLong ? LinearSlope(_haCloseWLong) : (double?)null;

            (double? haSlopeShort, double? haSeShort, double? haMseShort) =
                _haCloseWShort.Count == _cfg.OlsWindowShort ? OlsSlopeSeMse(_haCloseWShort) : (null, null, null);
            (double? haSlopeLong, _, _) =
                _haCloseWLong.Count == _cfg.OlsWindowLong ? OlsSlopeSeMse(_haCloseWLong) : (null, null, null);

            double? haOlsAngleShort = haSlopeShort.HasValue ? Math.Atan(haSlopeShort.Value) * 180.0 / Math.PI : (double?)null;
            double? haOlsAngleLong = haSlopeLong.HasValue ? Math.Atan(haSlopeLong.Value) * 180.0 / Math.PI : (double?)null;
            double? haLinAngleShort = haLinearSlopeShort.HasValue ? Math.Atan(haLinearSlopeShort.Value) * 180.0 / Math.PI : (double?)null;
            double? haLinAngleLong = haLinearSlopeLong.HasValue ? Math.Atan(haLinearSlopeLong.Value) * 180.0 / Math.PI : (double?)null;

            double? haAccelShort = (haSlopeShort.HasValue && _prevHaOlsSlopeShort.HasValue) ? (haSlopeShort.Value - _prevHaOlsSlopeShort.Value) : (double?)null;
            double? haCurv = (haSlopeShort.HasValue && haSlopeLong.HasValue) ? (haSlopeShort.Value - haSlopeLong.Value) : (double?)null;
            _prevHaOlsSlopeShort = haSlopeShort;

            // Volume/Pressure (HA)
            EnqueueVol(_haVolWShort, ref _sumHaVolShort, bar.TickVolume, _cfg.VolumeMaWindow);
            double? haVolMaShort = _haVolWShort.Count == _cfg.VolumeMaWindow ? (double)_sumHaVolShort / _haVolWShort.Count : (double?)null;
            double haClv = haRange > Eps ? (2.0 * haClose - haHigh - haLow) / (haHigh - haLow) : 0.0;
            _haClvEmaShort = EmaNext(_haClvEmaShort, haClv, _cfg.PressureEmaAlpha);
            double? haVolRatio = haVolMaShort.HasValue && haVolMaShort.Value > Eps ? bar.TickVolume / haVolMaShort.Value : (double?)null;
            double? haPressure = (_haClvEmaShort.HasValue && haVolRatio.HasValue) ? _haClvEmaShort.Value * haVolRatio.Value : (double?)null;
            double haBodyPressure = haDirection * haBodyToRange;
            double haWickPressure = (haLower - haUpper) / denom;

            // Trend proxy: run-length и EMA на телесна сила
            if (!_hasPrev) _haRunLen = haDirection == 0 ? 0 : 1;
            else
            {
                int prevDir = Math.Sign(_prevHaClose - _prevHaOpen);
                _haRunLen = haDirection == 0 ? 0 : (prevDir == haDirection ? _haRunLen + 1 : 1);
            }
            _haBodyToRangeEmaShort = EmaNext(_haBodyToRangeEmaShort, haBodyToRange, _cfg.TrendBodyEmaAlpha);
            double? haTrendStrength = _haBodyToRangeEmaShort.HasValue ? _haRunLen * _haBodyToRangeEmaShort.Value : (double?)null;

            // Pattern scores: Engulfing/FVG (без булеви флагове)
            double? haEngulfScore = null;
            double? haFvgUp = null, haFvgDown = null;
            if (_hasPrev)
            {
                double prevBody = Math.Abs(_prevHaClose - _prevHaOpen);
                if (prevBody > Eps)
                {
                    double prevBodyMin = Math.Min(_prevHaOpen, _prevHaClose);
                    double prevBodyMax = Math.Max(_prevHaOpen, _prevHaClose);
                    double currBodyMin = Math.Min(haOpen, haClose);
                    double currBodyMax = Math.Max(haOpen, haClose);
                    double cover = Math.Max(0.0, Math.Min(currBodyMax, prevBodyMax) - Math.Max(currBodyMin, prevBodyMin));
                    haEngulfScore = Math.Max(0.0, (haBody - prevBody) / prevBody) + (cover / prevBody);
                }
                // ВРЪЗКА КЪМ ВЪТРЕШНИЯ STATE: ползвай директно _prevHaHigh/_prevHaLow
                haFvgUp = Math.Max(0.0, Math.Min(haOpen, haClose) - _prevHaHigh);
                haFvgDown = Math.Max(0.0, _prevHaLow - Math.Max(haOpen, haClose));
            }

            // Sweeps (чисти) + дистанции/време (без BOS флагове)
            double? haSweepUp = null, haSweepDown = null;
            double? haDistPrevHighN = null, haDistPrevLowN = null;

            if (_haHighsPrevN.Count >= _cfg.PrevNForBreak && _haLowsPrevN.Count >= _cfg.PrevNForBreak)
            {
                double prevNHigh = _haHighsPrevN.Max();
                double prevNLow = _haLowsPrevN.Min();

                if (haHigh > prevNHigh && haClose <= prevNHigh) haSweepUp = (haHigh - prevNHigh) / denom;
                if (haLow < prevNLow && haClose >= prevNLow) haSweepDown = (prevNLow - haLow) / denom;

                haDistPrevHighN = (prevNHigh - haClose) / Math.Max(Math.Abs(haClose), Eps);
                haDistPrevLowN = (haClose - prevNLow) / Math.Max(Math.Abs(haClose), Eps);
            }

            int? haBarsSinceSwingHigh = null, haBarsSinceSwingLow = null;
            double? haDistSwingHigh = null, haDistSwingLow = null;

            if (_haHighsSwing.Count > 0)
            {
                haBarsSinceSwingHigh = IndexFromEndOf(_haHighsSwing, findMax: true);
                haBarsSinceSwingLow = IndexFromEndOf(_haLowsSwing, findMax: false);

                double swingHigh = _haHighsSwing.Max();
                double swingLow = _haLowsSwing.Min();
                haDistSwingHigh = (swingHigh - haClose) / Math.Max(Math.Abs(haClose), Eps);
                haDistSwingLow = (haClose - swingLow) / Math.Max(Math.Abs(haClose), Eps);
            }

            // Compression / Squeeze (HA Close std, HA Range MA)
            double? haSmaShort = _haCloseWShort.Count == _cfg.OlsWindowShort ? _sumHaCloseShort / _haCloseWShort.Count : (double?)null;
            double? haVarShort = null;
            if (_haCloseWShort.Count == _cfg.OlsWindowShort)
            {
                double mean = _sumHaCloseShort / _haCloseWShort.Count;
                haVarShort = Math.Max(_sumHaCloseShortSq / _haCloseWShort.Count - mean * mean, 0.0);
            }
            double? haStdShort = haVarShort.HasValue ? Math.Sqrt(haVarShort.Value) : (double?)null;

            double? haBbWidthShort = null;
            if (haStdShort.HasValue)
            {
                double widthAbs = 2.0 * _cfg.BbStdMult * haStdShort.Value;
                haBbWidthShort = (haSmaShort.HasValue && Math.Abs(haSmaShort.Value) > Eps) ? widthAbs / Math.Abs(haSmaShort.Value) : widthAbs;
            }

            double? haKcWidthShort = haRangeMaShort.HasValue ? 2.0 * _cfg.KcMult * haRangeMaShort.Value : (double?)null;
            double? haSqueezeProxy = (haBbWidthShort.HasValue && haKcWidthShort.HasValue && haKcWidthShort.Value > Eps)
                ? haBbWidthShort.Value / haKcWidthShort.Value
                : (double?)null;

            double? haCompressionProxy = (haRangeMaShort.HasValue && haRangeMaLong.HasValue && haRangeMaLong.Value > Eps)
                ? haRangeMaShort.Value / haRangeMaLong.Value
                : (double?)null;

            // Обнови structure windows СЛЕД изчисленията
            EnqueueLimit(_haHighsPrevN, haHigh, _cfg.PrevNForBreak);
            EnqueueLimit(_haLowsPrevN, haLow, _cfg.PrevNForBreak);
            EnqueueLimit(_haHighsSwing, haHigh, _cfg.SwingLookback);
            EnqueueLimit(_haLowsSwing, haLow, _cfg.SwingLookback);

            // Обнови предишна HA свещ
            _prevHaOpen = haOpen; _prevHaClose = haClose; _prevHaHigh = haHigh; _prevHaLow = haLow; _hasPrev = true;

            return new HeikenAshiMetrics
            {
                // HA OHLC
                HaOpen = haOpen,
                HaHigh = haHigh,
                HaLow = haLow,
                HaClose = haClose,

                // Геометрия
                HaRange = haRange,
                HaBody = haBody,
                HaUpperWick = haUpper,
                HaLowerWick = haLower,
                HaBodyToRange = haBodyToRange,
                HaWickAsymmetry = haWickAsym,
                HaClosePosInRange = haClosePosInRange,

                // Scores
                HaMarubozuScore = haMarubozuScore,
                HaDojiScore = haDojiScore,
                HaPinBarScore = haPinBarScore,

                // Direction & Gaps
                HaDirection = haDirection,
                HaGap = haGap,
                HaGapAbs = haGapAbs,
                HaGapPct = haGapPct,

                // RV
                HaParkinsonRv = haPark,
                HaGarmanKlassRv = haGK,
                HaRogersSatchellRv = haRS,

                // Rolling stats/MA (past-only)
                HaRangeMaShort = haRangeMaShort,
                HaRangeMaLong = haRangeMaLong,
                HaRangeMeanW = haRangeMeanW,
                HaRangeStdW = haRangeStdW,
                HaRangeDevFromMeanW = haRangeDevFromMeanW,
                HaParkMeanW = haParkMeanW,
                HaParkStdW = haParkStdW,
                HaParkDevFromMeanW = haParkDevFromMeanW,

                // Производни
                HaOlsSlopeShort = haSlopeShort,
                HaOlsSlopeLong = haSlopeLong,
                HaOlsSlopeSEShort = haSeShort,
                HaOlsSlopeMSEShort = haMseShort,
                HaLinearSlopeShort = haLinearSlopeShort,
                HaLinearSlopeLong = haLinearSlopeLong,
                HaOlsAngleShortDeg = haOlsAngleShort,
                HaOlsAngleLongDeg = haOlsAngleLong,
                HaLinearAngleShortDeg = haLinAngleShort,
                HaLinearAngleLongDeg = haLinAngleLong,
                HaAccelerationShort = haAccelShort,
                HaCurvature = haCurv,

                // Trend proxy
                HaColorRunLen = _haRunLen,
                HaTrendStrength = haTrendStrength,

                // Volume/Pressure
                HaClv = haClv,
                HaClvEmaShort = _haClvEmaShort,
                HaVolumeMaShort = haVolMaShort,
                HaVolumeRatio = haVolRatio,
                HaPressureIndex = haPressure,
                HaBodyPressure = haBodyPressure,
                HaWickPressure = haWickPressure,

                // Patterns/Structure
                HaEngulfingScore = haEngulfScore,
                HaFvgUp = haFvgUp,
                HaFvgDown = haFvgDown,
                HaSweepUpScore = haSweepUp,
                HaSweepDownScore = haSweepDown,

                HaDistToPrevHighN = haDistPrevHighN,
                HaDistToPrevLowN = haDistPrevLowN,
                HaDistToSwingHigh = haDistSwingHigh,
                HaDistToSwingLow = haDistSwingLow,
                HaBarsSinceSwingHigh = haBarsSinceSwingHigh,
                HaBarsSinceSwingLow = haBarsSinceSwingLow,

                // Compression
                HaBbWidthShort = haBbWidthShort,
                HaKcWidthShortProxy = haKcWidthShort,
                HaSqueezeProxy = haSqueezeProxy,
                HaCompressionProxy = haCompressionProxy
            };
        }

        // ===== Helpers =====

        private static void EnqueueFixed(Queue<double> q, ref double sum, double value, int cap)
        {
            q.Enqueue(value);
            sum += value;
            if (q.Count > cap)
            {
                var r = q.Dequeue();
                sum -= r;
            }
        }

        private static void EnqueueLimit(Queue<double> q, double v, int limit)
        {
            q.Enqueue(v);
            while (q.Count > limit) q.Dequeue();
        }

        private static void EnqueueClose(Queue<double> q, ref double sum, ref double sumSq, double v, int cap)
        {
            q.Enqueue(v);
            sum += v;
            sumSq += v * v;
            if (q.Count > cap)
            {
                var r = q.Dequeue();
                sum -= r;
                sumSq -= r * r;
            }
        }

        private static void EnqueueVol(Queue<long> q, ref long sum, long v, int cap)
        {
            q.Enqueue(v);
            sum += v;
            if (q.Count > cap)
            {
                var r = q.Dequeue();
                sum -= r;
            }
        }

        /// <summary>
        /// Past-only средно/стд/отклонение от средното за текущата стойност v (без leakage).
        /// Първо изчислява спрямо миналото, после enqueue-ва v.
        /// </summary>
        private static void PastOnlyMeanStd(Queue<double> w, ref double sum, ref double sumSq, double v, int cap,
                                            out double? mean, out double? std, out double? devFromMean)
        {
            if (w.Count == cap)
            {
                mean = sum / cap;
                double var = Math.Max(sumSq / cap - mean.Value * mean.Value, 0.0);
                std = Math.Sqrt(var);
                devFromMean = v - mean.Value;
            }
            else
            {
                mean = null; std = null; devFromMean = null;
            }

            w.Enqueue(v);
            sum += v; sumSq += v * v;
            if (w.Count > cap)
            {
                var r = w.Dequeue();
                sum -= r; sumSq -= r * r;
            }
        }

        /// <summary>Линеен наклон от регресия върху последователност 0..n-1.</summary>
        private static double LinearSlope(Queue<double> y)
        {
            int n = y.Count;
            if (n < 2) return 0.0;

            double sx = (n - 1) * n / 2.0;
            double sxx = (n - 1) * n * (2 * n - 1) / 6.0;

            double sy = 0.0, sxy = 0.0;
            int i = 0;
            foreach (var v in y)
            {
                sy += v;
                sxy += i * v;
                i++;
            }
            double denom = n * sxx - sx * sx;
            return denom > Eps ? (n * sxy - sx * sy) / denom : 0.0;
        }

        /// <summary>OLS наклон + стандартна грешка и MSE върху текущата опашка (0..n-1).</summary>
        private static (double slope, double se, double mse) OlsSlopeSeMse(Queue<double> y)
        {
            int n = y.Count;
            double sx = (n - 1) * n / 2.0;
            double sxx = (n - 1) * n * (2 * n - 1) / 6.0;

            double sy = 0.0, sxy = 0.0;
            int i = 0;
            foreach (var v in y)
            {
                sy += v;
                sxy += i * v;
                i++;
            }
            double denom = n * sxx - sx * sx;
            double slope = denom > Eps ? (n * sxy - sx * sy) / denom : 0.0;
            double intercept = (sy - slope * sx) / n;

            double sse = 0.0;
            i = 0;
            foreach (var v in y)
            {
                double yhat = intercept + slope * i;
                double e = v - yhat;
                sse += e * e;
                i++;
            }
            double mse = (n > 2) ? sse / (n - 2) : 0.0;
            double sxxCentered = sxx - sx * sx / n;
            double se = (sxxCentered > Eps && n > 2) ? Math.Sqrt(mse / sxxCentered) : 0.0;
            return (slope, se, mse);
        }

        /// <summary>Връща баровете от края до последния max/min (0 ако последният е текущият).</summary>
        private static int IndexFromEndOf(Queue<double> q, bool findMax)
        {
            int idxBest = -1, i = 0;
            double best = findMax ? double.NegativeInfinity : double.PositiveInfinity;
            foreach (var v in q)
            {
                bool better = findMax ? v >= best : v <= best;
                if (better) { best = v; idxBest = i; }
                i++;
            }
            return Math.Max(0, (q.Count - 1) - idxBest);
        }

        private static double? EmaNext(double? prev, double value, double alpha)
        {
            return prev.HasValue ? (alpha * value + (1 - alpha) * prev.Value) : value;
        }
    }
}
