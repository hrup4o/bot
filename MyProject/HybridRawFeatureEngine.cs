using System;
using System.Collections.Generic;
using System.Linq;
using MarketSignals.Core.Metrics;
using MarketSignals.Core.RawData;

namespace MarketSignals.Core.Hybrid
{
    /// <summary>
    /// Configuration for HybridRawFeatureEngine: multi-scale raw features only (no thresholds, no normalization).
    /// </summary>
    public sealed class HybridRawFeatureConfig
    {
        public int[] VelocityWindows { get; init; } = new[] { 1, 3, 5, 10, 20 };
        public int[] AccelerationWindows { get; init; } = new[] { 3, 5, 10 };
        public int[] RatioWindows { get; init; } = new[] { 5, 10, 20, 50 };
        public int[] StdWindows { get; init; } = new[] { 10, 20, 50 };
        public int[] MomentWindows { get; init; } = new[] { 10, 20 };
        public int[] CorrWindows { get; init; } = new[] { 5, 10, 20 };

        // Additional windows for new features
        public int[] ER_Windows { get; init; } = new[] { 10, 20, 50 };          // Efficiency Ratio
        public int[] RS_Windows { get; init; } = new[] { 20, 50, 100 };         // R/S (Hurst proxy)
        public int[] RV_Windows { get; init; } = new[] { 10, 20, 50 };          // Realized variance family
        public int[] VWAP_Windows { get; init; } = new[] { 10, 20, 50 };        // Rolling VWAP
        public int[] Drawdown_Windows { get; init; } = new[] { 20, 50, 100 };   // Windowed DD/Run-up
        public int[] Entropy_Windows { get; init; } = new[] { 20, 50, 100 };    // Entropy of sign(returns)

        // Spectral windows N (DFT); keep small for performance
        public int[] SpectralWindowsN { get; init; } = new[] { 16, 32 };

        // Fractal dimension windows and Higuchi parameter
        public int[] FD_Windows { get; init; } = new[] { 32, 64 };
        public int HiguchiKMax { get; init; } = 6;

        /// <summary>Short MA window for Range and Volume (past-only) used by counters/ratios.</summary>
        public int ShortMAWindow { get; init; } = 20;

        /// <summary>Maximum buffer size (bars) to keep in memory.</summary>
        public int MaxBufferSize { get; init; } = 512;

        public void Validate()
        {
            if (VelocityWindows == null || VelocityWindows.Length == 0) throw new ArgumentException("VelocityWindows must not be empty");
            if (AccelerationWindows == null || AccelerationWindows.Length == 0) throw new ArgumentException("AccelerationWindows must not be empty");
            if (RatioWindows == null || RatioWindows.Length == 0) throw new ArgumentException("RatioWindows must not be empty");
            if (StdWindows == null || StdWindows.Length == 0) throw new ArgumentException("StdWindows must not be empty");
            if (MomentWindows == null || MomentWindows.Length == 0) throw new ArgumentException("MomentWindows must not be empty");
            if (CorrWindows == null || CorrWindows.Length == 0) throw new ArgumentException("CorrWindows must not be empty");
            if (ER_Windows == null || ER_Windows.Length == 0) throw new ArgumentException("ER_Windows must not be empty");
            if (RS_Windows == null || RS_Windows.Length == 0) throw new ArgumentException("RS_Windows must not be empty");
            if (RV_Windows == null || RV_Windows.Length == 0) throw new ArgumentException("RV_Windows must not be empty");
            if (VWAP_Windows == null || VWAP_Windows.Length == 0) throw new ArgumentException("VWAP_Windows must not be empty");
            if (Drawdown_Windows == null || Drawdown_Windows.Length == 0) throw new ArgumentException("Drawdown_Windows must not be empty");
            if (Entropy_Windows == null || Entropy_Windows.Length == 0) throw new ArgumentException("Entropy_Windows must not be empty");
            if (SpectralWindowsN == null || SpectralWindowsN.Length == 0) throw new ArgumentException("SpectralWindowsN must not be empty");
            if (FD_Windows == null || FD_Windows.Length == 0) throw new ArgumentException("FD_Windows must not be empty");
            if (HiguchiKMax < 2) throw new ArgumentException("HiguchiKMax must be >= 2");
            if (ShortMAWindow < 2) throw new ArgumentException("ShortMAWindow must be >= 2");

            int need = 2 * Math.Max(
                Math.Max(AccelerationWindows.Max(), RatioWindows.Max()),
                Math.Max(RS_Windows.Max(), Drawdown_Windows.Max())
            );
            if (MaxBufferSize < need) throw new ArgumentException("MaxBufferSize too small for the largest windowed computations");
        }
    }

    /// <summary>
    /// HybridRawFeatureEngine computes additional raw features (missing from OHLC/HA engines):
    /// multi-scale velocities/accelerations, ratios, higher moments, correlations, consecutive counters,
    /// divergences (price vs volume, HA vs regular), realized variance family, VWAP & deviations,
    /// spectral ratios, slope stability, liquidity ratios, drawdown/run-up, entropy of sign(returns), and time encodings.
    /// It uses only raw math; no thresholds, no normalization, no boolean flags.
    /// </summary>
    public sealed class HybridRawFeatureEngine
    {
        private readonly HybridRawFeatureConfig _cfg;
        private const double Eps = 1e-12;
        private const double Mu1 = 0.7978845608028654; // sqrt(2/pi), used in bipower variation normalization

        // Rolling buffers (append-only; trimmed to MaxBufferSize)
        private readonly List<double> _closes = new();
        private readonly List<double> _volumes = new();
        private readonly List<double> _ranges = new();
        private readonly List<double> _highs = new();
        private readonly List<double> _lows = new();

        // Consecutive counters
        private int _consUp = 0, _consDown = 0, _consHighVol = 0, _consTightRange = 0;

        // Bars processed
        private int _barsProcessed = 0;

        public HybridRawFeatureEngine(HybridRawFeatureConfig? config = null)
        {
            _cfg = config ?? new HybridRawFeatureConfig();
            _cfg.Validate();
        }

        /// <summary>Resets all internal buffers and counters.</summary>
        public void Reset()
        {
            _closes.Clear(); _volumes.Clear(); _ranges.Clear(); _highs.Clear(); _lows.Clear();
            _consUp = _consDown = _consHighVol = _consTightRange = 0;
            _barsProcessed = 0;
        }

        /// <summary>
        /// Computes hybrid raw features for the given bar. Optionally accepts already computed OHLC/HA metrics
        /// to enable HA-vs-Regular divergences and reuse (if desired). If null, engine falls back to its own buffers.
        /// </summary>
        public Dictionary<string, float> ComputeNext(OhlcvBar bar, OhlcvMetrics? ohlcv = null, HeikenAshiMetrics? ha = null)
        {
            _barsProcessed++;

            // Append to buffers
            double close = bar.Close;
            double vol = Math.Max(0.0, bar.TickVolume);
            double range = Math.Max(0.0, bar.High - bar.Low);

            _closes.Add(close);
            _volumes.Add(vol);
            _ranges.Add(range);
            _highs.Add(bar.High);
            _lows.Add(bar.Low);
            TrimToCapacity();

            // Update consecutive counters (past-only comparisons)
            if (_closes.Count >= 2)
            {
                double prevClose = _closes[^2];
                if (close > prevClose) { _consUp++; _consDown = 0; }
                else if (close < prevClose) { _consDown++; _consUp = 0; }
                else { /* unchanged */ }
            }

            // Past-only short MAs for counters
            double? rangeMaShortPast = SMA(_ranges, _cfg.ShortMAWindow, pastOnly: true);
            double? volMaShortPast = SMA(_volumes, _cfg.ShortMAWindow, pastOnly: true);

            if (rangeMaShortPast.HasValue && rangeMaShortPast.Value > Eps && range < rangeMaShortPast.Value) _consTightRange++;
            else if (rangeMaShortPast.HasValue) _consTightRange = 0;

            if (volMaShortPast.HasValue && vol > volMaShortPast.Value) _consHighVol++;
            else if (volMaShortPast.HasValue) _consHighVol = 0;

            // Build feature map
            var f = new Dictionary<string, float>(256, StringComparer.Ordinal);

            // Multi-scale velocities (ΔClose over W)
            foreach (int w in _cfg.VelocityWindows) f[$"MxPriceVel_{w}"] = (float)Velocity(w);

            // Multi-scale accelerations (needs 2W)
            foreach (int w in _cfg.AccelerationWindows) f[$"MxPriceAccel_{w}"] = (float)Acceleration(w);

            // Range/Volume ratios (current / past-only MA(W))
            foreach (int w in _cfg.RatioWindows)
            {
                double? rma = SMA(_ranges, w, pastOnly: true);
                double? vma = SMA(_volumes, w, pastOnly: true);
                double rr = (rma.HasValue && rma.Value > Eps) ? _ranges[^1] / rma.Value : 1.0;
                double vr = (vma.HasValue && vma.Value > Eps) ? _volumes[^1] / vma.Value : 1.0;
                f[$"MxRangeRatio_{w}"] = (float)rr;
                f[$"MxVolRatio_{w}"] = (float)vr;
            }

            // StdDev (past-only), Skewness, Kurtosis for closes
            foreach (int w in _cfg.StdWindows) f[$"MxPriceStd_{w}"] = (float)(StdDevPastOnly(_closes, w) ?? 0.0);
            foreach (int w in _cfg.MomentWindows)
            {
                var (sk, ku) = MomentsPastOnly(_closes, w);
                f[$"MxSkew_{w}"] = (float)sk;
                f[$"MxKurt_{w}"] = (float)ku;
            }

            // Price-Volume correlation (past-only) on ΔClose vs Volume
            foreach (int w in _cfg.CorrWindows) f[$"MxCorrPV_{w}"] = (float)CorrDeltaCloseVsVol(w);

            // Consecutive counters
            f["ConsUpBars"] = _consUp;
            f["ConsDownBars"] = _consDown;
            f["ConsHighVol"] = _consHighVol;
            f["ConsTightRanges"] = _consTightRange;

            // Price vs Volume momentum divergence (raw) for windows 5/10
            AddDivergencePV(f, 5);
            AddDivergencePV(f, 10);

            // Efficiency Ratio (Kaufman): ER_w = |Close_t − Close_{t−w}| / sum_{i=1..w} |Close_i − Close_{i−1}|
            foreach (int w in _cfg.ER_Windows) f[$"MxER_{w}"] = (float)EfficiencyRatio(w);

            // R/S statistic (Hurst proxy): H ≈ log(R/S) / log(n); we report both R_S and H
            foreach (int w in _cfg.RS_Windows)
            {
                var (rs, hurstProxy) = RS_HurstProxy(_closes, w);
                f[$"MxRS_{w}"] = (float)rs;
                f[$"MxHurstProxy_{w}"] = (float)hurstProxy;
            }

            // Realized variance family (using log returns)
            foreach (int w in _cfg.RV_Windows)
            {
                var (rv, bpv, rq, rRange) = RealizedFamily(w);
                f[$"MxRV_{w}"] = (float)rv;
                f[$"MxBPV_{w}"] = (float)bpv;
                f[$"MxRQ_{w}"] = (float)rq;
                f[$"MxRealRange_{w}"] = (float)rRange;
            }

            // Rolling VWAP and deviation (using typical price)
            foreach (int w in _cfg.VWAP_Windows)
            {
                var (vwap, dev) = VWAP_AndDeviation(w);
                f[$"MxVWAP_{w}"] = (float)vwap;
                f[$"MxDevVWAP_{w}"] = (float)dev;
            }

            // Auto-correlation of returns (lag 1..3) over StdWindows (use the smallest std window)
            int acfW = _cfg.StdWindows.Min();
            var (acf1, acf2, acf3) = ACFReturns(acfW);
            f["MxACF1"] = (float)acf1;
            f["MxACF2"] = (float)acf2;
            f["MxACF3"] = (float)acf3;

            // PACF1 (for lag1 PACF equals ACF1)
            f["MxPACF1"] = (float)acf1;

            // Sign imbalance (returns): avg sign and positive share
            {
                int w = _cfg.StdWindows.Min();
                var (imb, posShare) = SignImbalance(w);
                f["MxSignImb"] = (float)imb;
                f["MxPosShare"] = (float)posShare;
            }

            // Robust stats: Median, MAD, DevFromMedian for closes
            foreach (int w in _cfg.MomentWindows)
            {
                var (median, mad, devMed) = MedianMad(_closes, w);
                f[$"MxMed_{w}"] = (float)median;
                f[$"MxMAD_{w}"] = (float)mad;
                f[$"MxDevMed_{w}"] = (float)devMed;
            }

            // Spectral energy ratio (DFT) low/high for N=16/32
            foreach (int n in _cfg.SpectralWindowsN)
            {
                double lhr = SpectralLowHighEnergyRatio(n);
                f[$"MxSpecLHRatio_{n}"] = (float)lhr;
            }

            // Fractal Dimensions (Higuchi/Katz)
            foreach (int w in _cfg.FD_Windows)
            {
                double fdh = HiguchiFD(_closes, w, _cfg.HiguchiKMax);
                double fdk = KatzFD(_closes, w);
                f[$"MxFD_Higuchi_{w}"] = (float)fdh;
                f[$"MxFD_Katz_{w}"] = (float)fdk;
            }

            // Slope stability: std of linear slopes over last K subwindows (K=4) of size s = StdWindows.Min()
            {
                int s = _cfg.StdWindows.Min();
                double slopeStd = SlopeStability(s, segments: 4);
                f["MxSlopeStd"] = (float)slopeStd;
            }

            // Liquidity ratios: Range/Vol, Body/Vol; and window ratio vs mean
            {
                double body = Math.Abs(bar.Close - bar.Open);
                double rVol = _volumes[^1] > Eps ? _ranges[^1] / _volumes[^1] : 0.0;
                double bVol = _volumes[^1] > Eps ? body / _volumes[^1] : 0.0;
                f["MxLiq_RangeVol"] = (float)rVol;
                f["MxLiq_BodyVol"] = (float)bVol;

                int w = _cfg.RatioWindows.Min();
                double? meanRVol = MeanPastOnly(_ranges, _volumes, w, ratio: true);
                double? meanBVol = MeanPastOnlyBody(_closes, _volumes, w);
                double rvr = (meanRVol.HasValue && Math.Abs(meanRVol.Value) > Eps) ? rVol / meanRVol.Value : 1.0;
                double bvr = (meanBVol.HasValue && Math.Abs(meanBVol.Value) > Eps) ? bVol / meanBVol.Value : 1.0;
                f["MxLiq_RangeVolRatio"] = (float)rvr;
                f["MxLiq_BodyVolRatio"] = (float)bvr;
            }

            // Std ratio short/long
            {
                int sShort = _cfg.StdWindows.Min();
                int sLong = _cfg.StdWindows.Max();
                double? stdS = StdDevPastOnly(_closes, sShort);
                double? stdL = StdDevPastOnly(_closes, sLong);
                double stdRatio = (stdS.HasValue && stdL.HasValue && stdL.Value > Eps) ? stdS.Value / stdL.Value : 1.0;
                f["MxStdRatio_ShortLong"] = (float)stdRatio;
            }

            // Drawdown/Run-up windows
            foreach (int w in _cfg.Drawdown_Windows)
            {
                var (dd, ru) = DrawdownRunup(w);
                f[$"MxDD_{w}"] = (float)dd;
                f[$"MxRU_{w}"] = (float)ru;
            }

            // Entropy of sign(returns) windows
            foreach (int w in _cfg.Entropy_Windows) f[$"MxEntSign_{w}"] = (float)EntropySignReturns(w);

            // HA vs Regular extra differences if metrics provided
            if (ohlcv != null && ha != null)
            {
                // Angle differences (short)
                if (ohlcv.OlsAngleShortDeg.HasValue && ha.HaOlsAngleShortDeg.HasValue)
                    f["MxAngleDiff_OlsShort"] = (float)Math.Abs(ohlcv.OlsAngleShortDeg.Value - ha.HaOlsAngleShortDeg.Value);
                else f["MxAngleDiff_OlsShort"] = 0f;

                if (ohlcv.LinearAngleShortDeg.HasValue && ha.HaLinearAngleShortDeg.HasValue)
                    f["MxAngleDiff_LinShort"] = (float)Math.Abs(ohlcv.LinearAngleShortDeg.Value - ha.HaLinearAngleShortDeg.Value);
                else f["MxAngleDiff_LinShort"] = 0f;

                // Pressure diffs
                if (ohlcv.PressureIndex.HasValue && ha.HaPressureIndex.HasValue)
                    f["MxPressureDiff"] = (float)Math.Abs(ohlcv.PressureIndex.Value - ha.HaPressureIndex.Value);
                else f["MxPressureDiff"] = 0f;

                f["MxBodyPressureDiff"] = (float)Math.Abs(ohlcv.BodyPressure - ha.HaBodyPressure);
                f["MxWickPressureDiff"] = (float)Math.Abs(ohlcv.WickPressure - ha.HaWickPressure);

                // RunLen diff (use ConsUp/ConsDown as proxy vs HaColorRunLen)
                int runLenProxy = _consUp > 0 ? _consUp : (_consDown > 0 ? -_consDown : 0);
                f["MxRunLenDiff_Signed"] = (float)(ha.HaColorRunLen - runLenProxy);
                f["MxRunLenDiff_Abs"] = (float)Math.Abs(ha.HaColorRunLen - runLenProxy);
            }
            else
            {
                f["MxAngleDiff_OlsShort"] = 0f;
                f["MxAngleDiff_LinShort"] = 0f;
                f["MxPressureDiff"] = 0f;
                f["MxBodyPressureDiff"] = 0f;
                f["MxWickPressureDiff"] = 0f;
                f["MxRunLenDiff_Signed"] = 0f;
                f["MxRunLenDiff_Abs"] = 0f;
            }

            // Time encodings
            var (hSin, hCos, dSin, dCos, mSin, mCos) = TimeEncodings(bar.TimestampUtc);
            f["Time_HourSin"] = (float)hSin;
            f["Time_HourCos"] = (float)hCos;
            f["Time_DaySin"] = (float)dSin;
            f["Time_DayCos"] = (float)dCos;
            f["Time_MinSin"] = (float)mSin;
            f["Time_MinCos"] = (float)mCos;

            // Quality metrics
            var (featCompl, dataCons) = Quality(ohlcv, ha, f.Count);
            f["Meta_FeatureCompleteness"] = (float)featCompl;
            f["Meta_DataConsistency"] = (float)dataCons;
            f["Meta_BarsProcessed"] = _barsProcessed;

            return f;
        }

        // ===== internal helpers =====

        private void TrimToCapacity()
        {
            int cap = _cfg.MaxBufferSize;
            if (_closes.Count > cap) _closes.RemoveRange(0, _closes.Count - cap);
            if (_volumes.Count > cap) _volumes.RemoveRange(0, _volumes.Count - cap);
            if (_ranges.Count > cap) _ranges.RemoveRange(0, _ranges.Count - cap);
            if (_highs.Count > cap) _highs.RemoveRange(0, _highs.Count - cap);
            if (_lows.Count > cap) _lows.RemoveRange(0, _lows.Count - cap);
        }

        private double Velocity(int w)
        {
            if (_closes.Count <= w) return 0.0;
            return _closes[^1] - _closes[^1 - w];
        }

        private double Acceleration(int w)
        {
            if (_closes.Count <= 2 * w) return 0.0;
            double velNow = _closes[^1] - _closes[^1 - w];
            double velPrev = _closes[^1 - w] - _closes[^1 - 2 * w];
            return velNow - velPrev;
        }

        private static double? SMA(List<double> data, int w, bool pastOnly)
        {
            if (w <= 0 || data.Count < (pastOnly ? w + 1 : w)) return null;
            int end = pastOnly ? data.Count - 1 : data.Count;
            int start = end - w;
            double sum = 0.0;
            for (int i = start; i < end; i++) sum += data[i];
            return sum / w;
        }

        private static double? StdDevPastOnly(List<double> data, int w)
        {
            if (w <= 1 || data.Count < w + 1) return null;
            int end = data.Count - 1;
            int start = end - w + 1;
            double mean = 0.0, sumSq = 0.0;
            for (int i = start; i <= end; i++) mean += data[i];
            mean /= w;
            for (int i = start; i <= end; i++) { double d = data[i] - mean; sumSq += d * d; }
            double var = Math.Max(sumSq / w, 0.0);
            return Math.Sqrt(var);
        }

        private static (double skew, double kurt) MomentsPastOnly(List<double> data, int w)
        {
            if (w <= 2 || data.Count < w + 1) return (0.0, 0.0);
            int end = data.Count - 1;
            int start = end - w + 1;

            double mean = 0.0;
            for (int i = start; i <= end; i++) mean += data[i];
            mean /= w;

            double m2 = 0.0, m3 = 0.0, m4 = 0.0;
            for (int i = start; i <= end; i++)
            {
                double d = data[i] - mean;
                double d2 = d * d;
                m2 += d2;
                m3 += d2 * d;
                m4 += d2 * d2;
            }
            m2 /= w; m3 /= w; m4 /= w;

            double std = Math.Sqrt(Math.Max(m2, 0.0));
            double skew = (std > Eps) ? (m3 / (std * std * std)) : 0.0;
            double kurt = (std > Eps) ? (m4 / (std * std * std * std)) : 0.0;
            return (skew, kurt);
        }

        private double CorrDeltaCloseVsVol(int w)
        {
            if (w <= 1 || _closes.Count < w + 1) return 0.0;
            int end = _closes.Count - 1;
            int start = end - w;

            double[] dClose = new double[w];
            double[] vol = new double[w];
            for (int i = 0; i < w; i++)
            {
                int idx = start + i;
                dClose[i] = _closes[idx + 1] - _closes[idx];
                vol[i] = _volumes[idx + 1];
            }
            return Pearson(dClose, vol);
        }

        private static double Pearson(double[] x, double[] y)
        {
            int n = x.Length;
            if (n == 0 || y.Length != n) return 0.0;
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double xi = x[i], yi = y[i];
                sx += xi; sy += yi;
                sxx += xi * xi; syy += yi * yi; sxy += xi * yi;
            }
            double num = n * sxy - sx * sy;
            double den = Math.Sqrt(Math.Max(n * sxx - sx * sx, 0.0) * Math.Max(n * syy - sy * sy, 0.0));
            if (den < Eps) return 0.0;
            return Math.Max(-1.0, Math.Min(1.0, num / den));
        }

        private void AddDivergencePV(Dictionary<string, float> f, int w)
        {
            if (_closes.Count <= w || _volumes.Count <= w)
            {
                f[$"Div_PriceVol_{w}"] = 0f;
                return;
            }
            double priceMom = _closes[^1] - _closes[^1 - w];
            double volMom = _volumes[^1] - _volumes[^1 - w];
            double priceNorm = Math.Abs(_closes[^1 - w]) > Eps ? priceMom / Math.Abs(_closes[^1 - w]) : 0.0;
            double volNorm = Math.Abs(_volumes[^1 - w]) > Eps ? volMom / Math.Abs(_volumes[^1 - w]) : 0.0;
            f[$"Div_PriceVol_{w}"] = (float)(priceNorm - volNorm);
        }

        private static (double hourSin, double hourCos, double daySin, double dayCos, double minuteSin, double minuteCos) TimeEncodings(DateTime tsUtc)
        {
            var t = tsUtc.ToUniversalTime();
            double hourSin = Math.Sin(2 * Math.PI * t.Hour / 24.0);
            double hourCos = Math.Cos(2 * Math.PI * t.Hour / 24.0);
            double daySin = Math.Sin(2 * Math.PI * (int)t.DayOfWeek / 7.0);
            double dayCos = Math.Cos(2 * Math.PI * (int)t.DayOfWeek / 7.0);
            double minuteSin = Math.Sin(2 * Math.PI * t.Minute / 60.0);
            double minuteCos = Math.Cos(2 * Math.PI * t.Minute / 60.0);
            return (hourSin, hourCos, daySin, dayCos, minuteSin, minuteCos);
        }

        // Efficiency Ratio (Kaufman)
        private double EfficiencyRatio(int w)
        {
            if (_closes.Count <= w) return 0.0;
            double change = Math.Abs(_closes[^1] - _closes[^1 - w]);
            double noise = 0.0;
            for (int i = _closes.Count - w; i < _closes.Count; i++)
                noise += Math.Abs(_closes[i] - _closes[i - 1]);
            if (noise < Eps) return 0.0;
            return Math.Min(1.0, change / noise);
        }

        // R/S (Hurst proxy): R/S statistic and H ≈ log(R/S)/log(n)
        private static (double rs, double hurstProxy) RS_HurstProxy(List<double> series, int w)
        {
            if (series.Count < w + 1) return (0.0, 0.0);
            int end = series.Count - 1;
            int start = end - w + 1;

            // Use returns or differences for stationarity
            double[] x = new double[w];
            for (int i = 0; i < w; i++) x[i] = series[start + i] - ((i == 0) ? series[start] : series[start + i - 1]);

            double mean = x.Average();
            double[] y = new double[w];
            for (int i = 0; i < w; i++) y[i] = x[i] - mean;

            // Cumulative dev series
            double[] z = new double[w];
            double c = 0.0;
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int i = 0; i < w; i++)
            {
                c += y[i];
                z[i] = c;
                if (c < min) min = c;
                if (c > max) max = c;
            }
            double R = max - min;
            double S = Std(y);
            double rs = (S > Eps) ? (R / S) : 0.0;
            double hurst = (rs > Eps && w > 1) ? Math.Log(rs) / Math.Log(w) : 0.0;
            return (rs, hurst);
        }

        // Realized Variance family (using log returns)
        private (double rv, double bpv, double rq, double realizedRangeProxy) RealizedFamily(int w)
        {
            if (_closes.Count <= w) return (0.0, 0.0, 0.0, 0.0);
            var r = new double[w];
            for (int i = 0; i < w; i++)
            {
                int idx = _closes.Count - w + i;
                double prev = _closes[idx - 1];
                double cur = _closes[idx];
                double lr = (prev > Eps && cur > Eps) ? Math.Log(cur / prev) : 0.0;
                r[i] = lr;
            }
            double rv = r.Sum(v => v * v);

            double bpv = 0.0;
            for (int i = 1; i < w; i++) bpv += Math.Abs(r[i]) * Math.Abs(r[i - 1]);
            bpv /= (Mu1 * Mu1); // μ1^{-2} sum |r_i||r_{i-1}|

            double rq = (w > 0) ? (w / 3.0) * r.Sum(v => v * v * v * v) : 0.0; // Realized quarticity

            // Realized range proxy: sum (High-Low)^2
            double rr = 0.0;
            for (int i = 0; i < w; i++)
            {
                int idx = _ranges.Count - w + i;
                double rr_i = _ranges[idx];
                rr += rr_i * rr_i;
            }
            return (rv, bpv, rq, rr);
        }

        // VWAP and Deviation (typical price; window w)
        private (double vwap, double dev) VWAP_AndDeviation(int w)
        {
            if (_closes.Count < w) return (0.0, 0.0);
            int end = _closes.Count - 1;
            int start = end - w + 1;

            double sumPV = 0.0, sumV = 0.0;
            for (int i = start; i <= end; i++)
            {
                double tp = (_highs[i] + _lows[i] + _closes[i]) / 3.0;
                double v = _volumes[i];
                sumPV += tp * v;
                sumV += v;
            }
            double vwap = sumV > Eps ? sumPV / sumV : _closes[end];
            double dev = _closes[end] - vwap;
            return (vwap, dev);
        }

        // Auto-correlation of returns for lag1..3 over window w (past-only, uses last w+1 closes)
        private (double acf1, double acf2, double acf3) ACFReturns(int w)
        {
            if (_closes.Count < w + 2) return (0.0, 0.0, 0.0);
            // Build returns r_t = Close_t − Close_{t−1} for last w+1 points → w returns (past-only)
            int end = _closes.Count - 2;
            int start = end - w + 1;
            double[] r = new double[w];
            for (int i = 0; i < w; i++)
            {
                int idx = start + i;
                r[i] = _closes[idx + 1] - _closes[idx];
            }
            double acf1 = AutoCorr(r, 1);
            double acf2 = AutoCorr(r, 2);
            double acf3 = AutoCorr(r, 3);
            return (acf1, acf2, acf3);
        }

        private static double AutoCorr(double[] r, int lag)
        {
            int n = r.Length;
            if (lag <= 0 || lag >= n) return 0.0;
            double mean = r.Average();
            double num = 0.0, den = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = r[i] - mean;
                den += d * d;
            }
            for (int i = lag; i < n; i++)
            {
                num += (r[i] - mean) * (r[i - lag] - mean);
            }
            if (den < Eps) return 0.0;
            return num / den;
        }

        // Sign imbalance: average sign of returns (ignore zeros) + positive share
        private (double imb, double posShare) SignImbalance(int w)
        {
            if (_closes.Count < w + 1) return (0.0, 0.0);
            int end = _closes.Count - 1;
            int start = end - w + 1;
            int pos = 0, neg = 0;
            for (int i = start; i <= end; i++)
            {
                double r = _closes[i] - _closes[i - 1];
                if (r > 0) pos++;
                else if (r < 0) neg++;
            }
            int tot = pos + neg;
            double imb = tot > 0 ? (double)(pos - neg) / tot : 0.0;
            double ppos = tot > 0 ? (double)pos / tot : 0.0;
            return (imb, ppos);
        }

        // Median/MAD for closes and deviation from median (past-only)
        private static (double median, double mad, double devFromMedian) MedianMad(List<double> data, int w)
        {
            if (data.Count < w + 1) return (0.0, 0.0, 0.0);
            int end = data.Count - 1;
            int start = end - w + 1;
            var window = data.GetRange(start, w);
            window.Sort();
            double median = (w % 2 == 1) ? window[w / 2] : 0.5 * (window[w / 2 - 1] + window[w / 2]);
            var devs = new List<double>(w);
            for (int i = 0; i < w; i++) devs.Add(Math.Abs(window[i] - median));
            devs.Sort();
            double mad = (w % 2 == 1) ? devs[w / 2] : 0.5 * (devs[w / 2 - 1] + devs[w / 2]);
            double dev = data[end] - median;
            return (median, mad, dev);
        }

        // Spectral low/high energy ratio for last N closes (DFT, naive O(N^2) but N is small)
        private double SpectralLowHighEnergyRatio(int N)
        {
            if (_closes.Count < N) return 0.0;
            int start = _closes.Count - N;
            double[] x = new double[N];
            for (int i = 0; i < N; i++) x[i] = _closes[start + i];

            // detrend/mean-remove for stability
            double mean = x.Average();
            for (int i = 0; i < N; i++) x[i] -= mean;

            // DFT magnitudes (0..N-1)
            double[] mag = new double[N];
            for (int k = 0; k < N; k++)
            {
                double re = 0.0, im = 0.0;
                for (int n = 0; n < N; n++)
                {
                    double phi = -2.0 * Math.PI * k * n / N;
                    re += x[n] * Math.Cos(phi);
                    im += x[n] * Math.Sin(phi);
                }
                mag[k] = re * re + im * im; // power
            }

            // Low vs high frequency energy (skip k=0 DC component from ratio)
            int split = N / 4; // low band size
            double low = 0.0, high = 0.0;
            for (int k = 1; k < split; k++) low += mag[k];
            for (int k = split; k < N / 2; k++) high += mag[k]; // Nyquist symmetry, use half-spectrum
            double total = low + high;
            return total > Eps ? low / total : 0.0;
        }

        // Slope stability: std of linear slopes over last K segments of size s
        private double SlopeStability(int s, int segments)
        {
            int need = s * segments;
            if (_closes.Count < need) return 0.0;
            double[] slopes = new double[segments];
            int end = _closes.Count;
            for (int k = 0; k < segments; k++)
            {
                int segEnd = end - k * s;
                int segStart = segEnd - s;
                if (segStart < 0) return 0.0;
                slopes[k] = LinearSlopeSegment(_closes, segStart, s);
            }
            return Std(slopes);
        }

        private static double LinearSlopeSegment(List<double> y, int start, int len)
        {
            if (len < 2 || start < 0 || start + len > y.Count) return 0.0;
            int n = len;
            double sx = (n - 1) * n / 2.0;
            double sxx = (n - 1) * n * (2 * n - 1) / 6.0;
            double sy = 0.0, sxy = 0.0;
            for (int i = 0; i < n; i++)
            {
                double xi = i;
                double yi = y[start + i];
                sy += yi;
                sxy += xi * yi;
            }
            double denom = n * sxx - sx * sx;
            return denom > Eps ? (n * sxy - sx * sy) / denom : 0.0;
        }

        // Mean of (range/volume) over w, past-only
        private static double? MeanPastOnly(List<double> ranges, List<double> volumes, int w, bool ratio)
        {
            if (ranges.Count < w + 1 || volumes.Count < w + 1) return null;
            int end = ranges.Count - 1;
            int start = end - w + 1;
            double sum = 0.0; int cnt = 0;
            for (int i = start; i <= end; i++)
            {
                if (volumes[i] > Eps)
                {
                    double rv = ratio ? (ranges[i] / volumes[i]) : ranges[i];
                    sum += rv; cnt++;
                }
            }
            return cnt > 0 ? sum / cnt : (double?)null;
        }

        // Mean of (body/volume) over w, past-only, using closes list for body via close-open proxy (approx)
        private static double? MeanPastOnlyBody(List<double> closes, List<double> volumes, int w)
        {
            if (closes.Count < w + 2 || volumes.Count < w + 2) return null;
            int end = closes.Count - 1;
            int start = end - w + 1;
            double sum = 0.0; int cnt = 0;
            // Approximate body as |ΔClose|
            for (int i = start; i <= end; i++)
            {
                double body = Math.Abs(closes[i] - closes[i - 1]);
                if (volumes[i] > Eps)
                {
                    sum += body / volumes[i];
                    cnt++;
                }
            }
            return cnt > 0 ? sum / cnt : (double?)null;
        }

        private (double dd, double ru) DrawdownRunup(int w)
        {
            if (_closes.Count < w) return (0.0, 0.0);
            int end = _closes.Count - 1;
            int start = end - w + 1;
            double maxPeak = _closes[start];
            double minTrough = _closes[start];
            double maxDD = 0.0;
            double maxRU = 0.0;

            for (int i = start + 1; i <= end; i++)
            {
                double c = _closes[i];
                if (c > maxPeak) maxPeak = c;
                if (c < minTrough) minTrough = c;

                if (maxPeak > Eps)
                {
                    double dd = (maxPeak - c) / maxPeak;
                    if (dd > maxDD) maxDD = dd;
                }
                if (minTrough > Eps)
                {
                    double ru = (c - minTrough) / minTrough;
                    if (ru > maxRU) maxRU = ru;
                }
            }
            return (maxDD, maxRU);
        }

        private double EntropySignReturns(int w)
        {
            if (_closes.Count < w + 1) return 0.0;
            int end = _closes.Count - 1;
            int start = end - w + 1;
            int pos = 0, neg = 0;
            for (int i = start; i <= end; i++)
            {
                double r = _closes[i] - _closes[i - 1];
                if (r > 0) pos++;
                else if (r < 0) neg++;
            }
            int tot = pos + neg;
            if (tot == 0) return 0.0;
            double ppos = (double)pos / tot;
            double pneg = (double)neg / tot;
            double H = 0.0;
            if (ppos > 0) H -= ppos * Math.Log(ppos);
            if (pneg > 0) H -= pneg * Math.Log(pneg);
            // Normalize by ln(2) → [0,1] for two outcomes
            return H / Math.Log(2.0);
        }

        private static double Std(IEnumerable<double> seq)
        {
            var a = seq.ToArray();
            if (a.Length == 0) return 0.0;
            double mean = a.Average();
            double sumSq = 0.0;
            for (int i = 0; i < a.Length; i++) { double d = a[i] - mean; sumSq += d * d; }
            return Math.Sqrt(Math.Max(sumSq / a.Length, 0.0));
        }

        private static double Std(double[] a)
        {
            if (a.Length == 0) return 0.0;
            double mean = a.Average();
            double sumSq = 0.0;
            for (int i = 0; i < a.Length; i++) { double d = a[i] - mean; sumSq += d * d; }
            return Math.Sqrt(Math.Max(sumSq / a.Length, 0.0));
        }

        private (double featureCompleteness, double dataConsistency) Quality(OhlcvMetrics? o, HeikenAshiMetrics? h, int currentFeatureCount)
        {
            // Approximate feature completeness: ratio of produced keys vs intended (heuristic)
            int intended =
                _cfg.VelocityWindows.Length +
                _cfg.AccelerationWindows.Length +
                2 * _cfg.RatioWindows.Length +
                _cfg.StdWindows.Length +
                2 * _cfg.MomentWindows.Length +
                _cfg.CorrWindows.Length +
                4 + // consecutive
                2 + // PV divergences
                (_cfg.ER_Windows.Length) + // ER
                2 * _cfg.RS_Windows.Length + // RS + Hurst
                4 * _cfg.RV_Windows.Length + // RV family
                2 * _cfg.VWAP_Windows.Length + // VWAP & Dev
                1 +  // ACF pack
                2 +  // sign imbalance
                3 * _cfg.MomentWindows.Length + // median/mad/dev
                _cfg.SpectralWindowsN.Length + // spectral
                2 * _cfg.FD_Windows.Length + // fractal dimensions (Higuchi + Katz)
                1 +  // slope stability
                4 +  // liquidity ratios
                2 * _cfg.Drawdown_Windows.Length +
                _cfg.Entropy_Windows.Length +
                6 +  // HA vs Reg extras
                6 +  // time encodings + meta bars processed, feature completeness, data consistency
                0;

            double featureCompleteness = intended > 0 ? Math.Min(1.0, (double)currentFeatureCount / intended) : 1.0;

            // DataConsistency: compare OHLC vs HA direction/volatility/pressure if available
            if (o == null || h == null) return (featureCompleteness, 1.0);

            double score = 1.0;
            int comps = 0;

            // Direction consistency
            if (o.Direction != 0 && h.HaDirection != 0)
            {
                bool same = Math.Sign(o.Direction) == Math.Sign(h.HaDirection);
                score += same ? 1.0 : 0.0;
                comps++;
            }

            // Volatility consistency using short range MA if available
            if (o.RangeMaShort.HasValue && h.HaRangeMaShort.HasValue && o.RangeMaShort.Value > Eps && h.HaRangeMaShort.Value > Eps)
            {
                double ratio = o.RangeMaShort.Value / h.HaRangeMaShort.Value;
                double vcons = 1.0 - Math.Min(1.0, Math.Abs(Math.Log(ratio)) / 2.0);
                score += Math.Max(0.0, vcons);
                comps++;
            }

            // Pressure consistency if available
            if (o.PressureIndex.HasValue && h.HaPressureIndex.HasValue)
            {
                double diff = Math.Abs(o.PressureIndex.Value - h.HaPressureIndex.Value);
                double pcons = 1.0 - Math.Min(1.0, diff / 2.0);
                score += pcons;
                comps++;
            }

            double dataConsistency = comps > 0 ? score / (comps + 1) : 1.0;
            return (featureCompleteness, dataConsistency);
        }

        /// <summary>
        /// Higuchi Fractal Dimension on last w samples, with k = 1..kMax. Returns D ∈ [1,2] typically for time series.
        /// </summary>
        private static double HiguchiFD(List<double> series, int w, int kMax)
        {
            if (w < 2 || series.Count < w) return 0.0;
            int end = series.Count - 1;
            int start = end - w + 1;
            double[] x = new double[w];
            for (int i = 0; i < w; i++) x[i] = series[start + i];

            int K = Math.Max(2, kMax);
            var logk = new List<double>(K);
            var logL = new List<double>(K);

            for (int k = 1; k <= K; k++)
            {
                double LkSum = 0.0;
                int mkCount = 0;
                for (int m = 0; m < k; m++)
                {
                    int num = (w - 1 - m) / k;
                    if (num <= 0) continue;
                    double sum = 0.0;
                    for (int j = 1; j <= num; j++)
                    {
                        int idx1 = m + j * k;
                        int idx0 = m + (j - 1) * k;
                        sum += Math.Abs(x[idx1] - x[idx0]);
                    }
                    double norm = (double)(w - 1) / (num * k);
                    double Lm = (sum * norm);
                    LkSum += Lm;
                    mkCount++;
                }
                if (mkCount > 0)
                {
                    double Lk = LkSum / mkCount;
                    logk.Add(Math.Log(1.0 / k));
                    logL.Add(Math.Log(Lk + Eps));
                }
            }

            if (logk.Count < 2) return 0.0;
            double slope = LinearSlopeXY(logk.ToArray(), logL.ToArray());
            return -slope; // D ≈ -slope
        }

        /// <summary>
        /// Katz Fractal Dimension on last w samples.
        /// FD = ln(n) / (ln(n) + ln(d/L)), where n = w, L = total curve length, d = max distance from first point.
        /// </summary>
        private static double KatzFD(List<double> series, int w)
        {
            if (w < 2 || series.Count < w) return 0.0;
            int end = series.Count - 1;
            int start = end - w + 1;
            double L = 0.0;
            double x0 = series[start];
            double d = 0.0;
            for (int i = start + 1; i <= end; i++)
            {
                double xi = series[i];
                double xim1 = series[i - 1];
                L += Math.Abs(xi - xim1);
                d = Math.Max(d, Math.Abs(xi - x0));
            }
            int n = w;
            if (L <= Eps || d <= Eps) return 1.0;
            double num = Math.Log(n);
            double den = num + Math.Log(d / L);
            if (Math.Abs(den) < Eps) return 1.0;
            return num / den;
        }

        /// <summary>Linear regression slope for y vs x arrays.</summary>
        private static double LinearSlopeXY(double[] x, double[] y)
        {
            int n = x.Length;
            if (n != y.Length || n < 2) return 0.0;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                sx += x[i]; sy += y[i];
                sxx += x[i] * x[i]; sxy += x[i] * y[i];
            }
            double num = n * sxy - sx * sy;
            double den = n * sxx - sx * sx;
            return Math.Abs(den) < Eps ? 0.0 : num / den;
        }
    }
}
