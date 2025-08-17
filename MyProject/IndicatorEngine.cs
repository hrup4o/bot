

using System;
using System.Collections.Generic;
using MarketSignals.Core.RawData;

namespace Indicators
{

    /// <summary>
    /// Минимален интерфейс за индикаторна серия (индексируеми стойности).
    /// </summary>
    public interface ISeries
    {
        int Count { get; }
        double this[int index] { get; }
    }

    /// <summary>
    /// Имплементация на ISeries върху IList<double>.
    /// </summary>
    public sealed class ArraySeries : ISeries
    {
        private readonly System.Collections.Generic.IList<double> _data;

        public ArraySeries(System.Collections.Generic.IList<double> data) { _data = data ?? throw new ArgumentNullException(nameof(data)); }
        public int Count { get { return _data.Count; } }
        public double this[int index] { get { return (index >= 0 && index < _data.Count) ? _data[index] : 0.0; } }
    }

    /// <summary>
    /// Пакет от индикаторни серии, подавани отвън (напр. от cTrader API).
    /// </summary>
    public sealed class IndicatorSeriesBundle
    {
        public ISeries? RSI { get; set; }
        public ISeries? MACD { get; set; }
        public ISeries? MACDSignal { get; set; }
        public ISeries? MACDHistogram { get; set; }

        public ISeries? AO { get; set; }
        public ISeries? MFI { get; set; }
        public ISeries? WilliamsAD { get; set; }
        public ISeries? VHF { get; set; }
        public ISeries? VIDYA { get; set; }
        public ISeries? CMO { get; set; }
        public ISeries? ROC { get; set; }

        public ISeries? BB_Middle { get; set; }
        public ISeries? BB_Upper { get; set; }
        public ISeries? BB_Lower { get; set; }
        public ISeries? BB_Bandwidth { get; set; }

        public ISeries? ATR { get; set; }
        public ISeries? VolumeOsc { get; set; }
        public ISeries? ElderForceIndex { get; set; }

        public ISeries? VWAP { get; set; }
        public ISeries? OBV { get; set; }
        public ISeries? PVT { get; set; }

        public ISeries? SuperTrendValue { get; set; }
        /// <summary>Направление на SuperTrend като числова серия (-1 или +1), предоставена от източника.</summary>
        public ISeries? SuperTrendDirection { get; set; }

        public ISeries? KVO { get; set; }
        public ISeries? KVOSignal { get; set; }
        public ISeries? KVOHistogram { get; set; }

        public ISeries? WilliamsR { get; set; }
        public ISeries? PriceROC { get; set; }
    }

    /// <summary>
    /// Параметри (периоди/множители) за производни и локални диапазони. Самите индикатори се подават отвън.
    /// </summary>
    public sealed class IndicatorParams
    {
        public int RsiPeriod { get; set; } = 14;
        public int MacdFast { get; set; } = 12;
        public int MacdSlow { get; set; } = 26;
        public int MacdSignal { get; set; } = 9;

        public int AoFast { get; set; } = 5;
        public int AoSlow { get; set; } = 34;

        public int CmoPeriod { get; set; } = 14;
        public int RocPeriod { get; set; } = 14;
        public int BbPeriod { get; set; } = 20;
        public int AtrPeriod { get; set; } = 14;
        public int VhfPeriod { get; set; } = 28;
        public int VidyaPeriod { get; set; } = 14;
        public int VolOscFast { get; set; } = 5;
        public int VolOscSlow { get; set; } = 14;

        public int KvoFastPeriod { get; set; } = 34;
        public int KvoSlowPeriod { get; set; } = 55;
        public int KvoSignalPeriod { get; set; } = 13;

        public int WilliamsRPeriod { get; set; } = 14;
        public int PriceRocPeriod { get; set; } = 14;

        /// <summary>Период за производни на серии без естествен период (VWAP/OBV/PVT/EFI).</summary>
        public int DerivativesDefaultPeriod { get; set; } = 14;
    }

    /// <summary>
    /// Производни върху индикаторна серия: първа разлика, линейна наклон/ъгъл и „ускорение“ (разлика на наклон).
    /// </summary>
    public struct Derivative
    {
        public double Delta;
        public double Slope;
        public double AngleDeg;
        public double Acceleration;
    }

    /// <summary>
    /// Компактен snapshot с индикаторни стойности, производни, кросове и спредове (без Z/нормализации/прагове).
    /// </summary>
    public sealed class IndicatorSnapshot
    {
        // Основни стойности
        public double RSI, MACD, MACDSignal, MACDHistogram;
        public double AO, MFI, WilliamsAD, VHF, VIDYA, CMO, ROC;
        public double BB_Middle, BB_Upper, BB_Lower, BB_Bandwidth;
        public double ATR, VolumeOsc, ElderForceIndex;
        public double VWAP, OBV, PVT;
        public double SuperTrendValue, SuperTrendDirection;
        public double KVO, KVOSignal, KVOHistogram;
        public double WilliamsR, PriceROC;

        // Производни (delta/slope/angle/acceleration)
        public Derivative RSI_D;
        public Derivative MACD_D, MACDSignal_D, MACDHistogram_D;
        public Derivative AO_D, CMO_D, ROC_D, BBW_D, WilliamsR_D, PriceROC_D;
        public Derivative KVO_D, KVOSignal_D, KVOHistogram_D;
        public Derivative VHF_D, VIDYA_D;
        public Derivative ATR_D, VolumeOsc_D, ElderForceIndex_D;
        public Derivative VWAP_D, OBV_D, PVT_D;

        // Допълнителни производни
        public Derivative MFI_D, WilliamsAD_D;
        public Derivative BB_Middle_D, BB_Upper_D, BB_Lower_D;
        public Derivative SuperTrendValue_D;

        // Дирекшън (sign of slope)
        public int RSI_Direction, MACD_Direction, MACDSignal_Direction, MACDHistogram_Direction;
        public int AO_Direction, CMO_Direction, ROC_Direction, BBW_Direction;
        public int WilliamsR_Direction, PriceROC_Direction;
        public int KVO_Direction, KVOSignal_Direction, KVOHistogram_Direction;
        public int MFI_Direction, WilliamsAD_Direction;
        public int BB_Middle_Direction, BB_Upper_Direction, BB_Lower_Direction;
        public int SuperTrendValue_Direction;

        // Кросове (-1/0/+1)
        public int MACD_Cross_Signal;
        public int RSI_Cross_MACD;
        public int KVO_Cross_Signal;
        public int ROC_Cross_PriceROC;
        public int WilliamsR_Cross_RSI;
        public int VolumeOsc_Cross_EFI;

        // Допълнителни кросове
        public int AO_Cross_CMO;
        public int AO_Cross_ROC;
        public int CMO_Cross_RSI;
        public int VIDYA_Cross_VHF;
        public int BB_Bandwidth_Cross_ATR;

        // Спредове/разлики
        public double MACD_LineSpread_Signed;
        public double MACD_LineSpread_Abs;
        public double RSI_MacdDistance;
        public double RSI_SignalDistance;
        public double RSI_HistogramDistance;

        public double KVO_Spread_Signed;
        public double KVO_Spread_Abs;

        public double RSI_WilliamsR_Distance;
        public double RSI_PriceROC_Distance;
        public double MACD_PriceROC_Distance;
        public double CMO_ROC_Distance;
        public double VHF_VIDYA_Distance;
        public double VolumeOsc_EFI_Distance;

        public double ROC_PriceROC_Distance;
        public double BB_WidthAbs;

        // Локални диапазони (max-min за периода)
        public double RSI_LocalRange;
        public double MACD_LocalRange, MACDSignal_LocalRange, MACDHistogram_LocalRange;
        public double AO_LocalRange, CMO_LocalRange, ROC_LocalRange, BBW_LocalRange;
        public double WilliamsR_LocalRange, PriceROC_LocalRange;
        public double KVO_LocalRange, KVOSignal_LocalRange, KVOHistogram_LocalRange;
        public double VHF_LocalRange, VIDYA_LocalRange;
        public double ATR_LocalRange, VolumeOsc_LocalRange, ElderForceIndex_LocalRange;
        public double VWAP_LocalRange, OBV_LocalRange, PVT_LocalRange;

        // Локални диапазони (добавени серии)
        public double MFI_LocalRange, WilliamsAD_LocalRange;
        public double BB_Middle_LocalRange, BB_Upper_LocalRange, BB_Lower_LocalRange;
        public double SuperTrendValue_LocalRange;
    }

    /// <summary>
    /// Двигател за изчисляване на чисти метрики върху индикаторни серии (подадени отвън).
    /// Не изчислява самите индикатори; без прагове/булеви/Z-score/нормализации.
    /// </summary>
    public static class IndicatorEngine
    {
        public static IndicatorSnapshot ComputeSnapshot(int index, IndicatorSeriesBundle s, IndicatorParams p)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (p == null) throw new ArgumentNullException(nameof(p));

            var snap = new IndicatorSnapshot();

            // Основни стойности (safe getters)
            snap.RSI = Get(s.RSI, index);
            snap.MACD = Get(s.MACD, index);
            snap.MACDSignal = Get(s.MACDSignal, index);
            snap.MACDHistogram = Get(s.MACDHistogram, index);

            snap.AO = Get(s.AO, index);
            snap.MFI = Get(s.MFI, index);
            snap.WilliamsAD = Get(s.WilliamsAD, index);
            snap.VHF = Get(s.VHF, index);
            snap.VIDYA = Get(s.VIDYA, index);
            snap.CMO = Get(s.CMO, index);
            snap.ROC = Get(s.ROC, index);

            snap.BB_Middle = Get(s.BB_Middle, index);
            snap.BB_Upper = Get(s.BB_Upper, index);
            snap.BB_Lower = Get(s.BB_Lower, index);
            snap.BB_Bandwidth = Get(s.BB_Bandwidth, index);

            snap.ATR = Get(s.ATR, index);
            snap.VolumeOsc = Get(s.VolumeOsc, index);
            snap.ElderForceIndex = Get(s.ElderForceIndex, index);

            snap.VWAP = Get(s.VWAP, index);
            snap.OBV = Get(s.OBV, index);
            snap.PVT = Get(s.PVT, index);

            snap.SuperTrendValue = Get(s.SuperTrendValue, index);
            snap.SuperTrendDirection = Get(s.SuperTrendDirection, index);

            snap.KVO = Get(s.KVO, index);
            snap.KVOSignal = Get(s.KVOSignal, index);
            snap.KVOHistogram = Get(s.KVOHistogram, index);

            snap.WilliamsR = Get(s.WilliamsR, index);
            snap.PriceROC = Get(s.PriceROC, index);

            // Ширина на лентите (ненормализирана)
            snap.BB_WidthAbs = snap.BB_Upper - snap.BB_Lower;

            // Производни (период = период на индикатора)
            snap.RSI_D = Derive(s.RSI, index, p.RsiPeriod);
            snap.MACD_D = Derive(s.MACD, index, p.MacdSignal);
            snap.MACDSignal_D = Derive(s.MACDSignal, index, p.MacdSignal);
            snap.MACDHistogram_D = Derive(s.MACDHistogram, index, p.MacdSignal);

            snap.AO_D = Derive(s.AO, index, p.AoSlow);
            snap.CMO_D = Derive(s.CMO, index, p.CmoPeriod);
            snap.ROC_D = Derive(s.ROC, index, p.RocPeriod);
            snap.BBW_D = Derive(s.BB_Bandwidth, index, p.BbPeriod);
            snap.WilliamsR_D = Derive(s.WilliamsR, index, p.WilliamsRPeriod);
            snap.PriceROC_D = Derive(s.PriceROC, index, p.PriceRocPeriod);

            snap.KVO_D = Derive(s.KVO, index, p.KvoSignalPeriod);
            snap.KVOSignal_D = Derive(s.KVOSignal, index, p.KvoSignalPeriod);
            snap.KVOHistogram_D = Derive(s.KVOHistogram, index, p.KvoSignalPeriod);

            snap.VHF_D = Derive(s.VHF, index, p.VhfPeriod);
            snap.VIDYA_D = Derive(s.VIDYA, index, p.VidyaPeriod);

            snap.ATR_D = Derive(s.ATR, index, p.AtrPeriod);
            snap.VolumeOsc_D = Derive(s.VolumeOsc, index, p.VolOscSlow);
            snap.ElderForceIndex_D = Derive(s.ElderForceIndex, index, p.DerivativesDefaultPeriod);

            snap.VWAP_D = Derive(s.VWAP, index, p.DerivativesDefaultPeriod);
            snap.OBV_D = Derive(s.OBV, index, p.DerivativesDefaultPeriod);
            snap.PVT_D = Derive(s.PVT, index, p.DerivativesDefaultPeriod);

            // Допълнителни производни
            snap.MFI_D = Derive(s.MFI, index, p.DerivativesDefaultPeriod);
            snap.WilliamsAD_D = Derive(s.WilliamsAD, index, p.DerivativesDefaultPeriod);
            snap.BB_Middle_D = Derive(s.BB_Middle, index, p.BbPeriod);
            snap.BB_Upper_D = Derive(s.BB_Upper, index, p.BbPeriod);
            snap.BB_Lower_D = Derive(s.BB_Lower, index, p.BbPeriod);
            snap.SuperTrendValue_D = Derive(s.SuperTrendValue, index, p.DerivativesDefaultPeriod);

            // Дирекшън = sign(slope)
            snap.RSI_Direction = Sign(snap.RSI_D.Slope);
            snap.MACD_Direction = Sign(snap.MACD_D.Slope);
            snap.MACDSignal_Direction = Sign(snap.MACDSignal_D.Slope);
            snap.MACDHistogram_Direction = Sign(snap.MACDHistogram_D.Slope);

            snap.AO_Direction = Sign(snap.AO_D.Slope);
            snap.CMO_Direction = Sign(snap.CMO_D.Slope);
            snap.ROC_Direction = Sign(snap.ROC_D.Slope);
            snap.BBW_Direction = Sign(snap.BBW_D.Slope);

            snap.WilliamsR_Direction = Sign(snap.WilliamsR_D.Slope);
            snap.PriceROC_Direction = Sign(snap.PriceROC_D.Slope);

            snap.KVO_Direction = Sign(snap.KVO_D.Slope);
            snap.KVOSignal_Direction = Sign(snap.KVOSignal_D.Slope);
            snap.KVOHistogram_Direction = Sign(snap.KVOHistogram_D.Slope);

            snap.MFI_Direction = Sign(snap.MFI_D.Slope);
            snap.WilliamsAD_Direction = Sign(snap.WilliamsAD_D.Slope);
            snap.BB_Middle_Direction = Sign(snap.BB_Middle_D.Slope);
            snap.BB_Upper_Direction = Sign(snap.BB_Upper_D.Slope);
            snap.BB_Lower_Direction = Sign(snap.BB_Lower_D.Slope);
            snap.SuperTrendValue_Direction = Sign(snap.SuperTrendValue_D.Slope);

            // Кросове
            snap.MACD_Cross_Signal = Cross(s.MACD, s.MACDSignal, index);
            snap.RSI_Cross_MACD = Cross(s.RSI, s.MACD, index);
            snap.KVO_Cross_Signal = Cross(s.KVO, s.KVOSignal, index);

            snap.ROC_Cross_PriceROC = Cross(s.ROC, s.PriceROC, index);
            snap.WilliamsR_Cross_RSI = Cross(s.WilliamsR, s.RSI, index);
            snap.VolumeOsc_Cross_EFI = Cross(s.VolumeOsc, s.ElderForceIndex, index);

            // Допълнителни кросове
            snap.AO_Cross_CMO = Cross(s.AO, s.CMO, index);
            snap.AO_Cross_ROC = Cross(s.AO, s.ROC, index);
            snap.CMO_Cross_RSI = Cross(s.CMO, s.RSI, index);
            snap.VIDYA_Cross_VHF = Cross(s.VIDYA, s.VHF, index);
            snap.BB_Bandwidth_Cross_ATR = Cross(s.BB_Bandwidth, s.ATR, index);

            // Спредове/разлики
            snap.MACD_LineSpread_Signed = snap.MACD - snap.MACDSignal;
            snap.MACD_LineSpread_Abs = Math.Abs(snap.MACD_LineSpread_Signed);

            snap.RSI_MacdDistance = snap.RSI - snap.MACD;
            snap.RSI_SignalDistance = snap.RSI - snap.MACDSignal;
            snap.RSI_HistogramDistance = snap.RSI - snap.MACDHistogram;

            snap.KVO_Spread_Signed = snap.KVO - snap.KVOSignal;
            snap.KVO_Spread_Abs = Math.Abs(snap.KVO_Spread_Signed);

            snap.RSI_WilliamsR_Distance = snap.RSI - snap.WilliamsR;
            snap.RSI_PriceROC_Distance = snap.RSI - snap.PriceROC;
            snap.MACD_PriceROC_Distance = snap.MACD - snap.PriceROC;
            snap.CMO_ROC_Distance = snap.CMO - snap.ROC;
            snap.VHF_VIDYA_Distance = snap.VHF - snap.VIDYA;
            snap.VolumeOsc_EFI_Distance = snap.VolumeOsc - snap.ElderForceIndex;

            snap.ROC_PriceROC_Distance = snap.ROC - snap.PriceROC;

            // Локални диапазони
            snap.RSI_LocalRange = LocalRange(s.RSI, index, p.RsiPeriod);
            snap.MACD_LocalRange = LocalRange(s.MACD, index, p.MacdSignal);
            snap.MACDSignal_LocalRange = LocalRange(s.MACDSignal, index, p.MacdSignal);
            snap.MACDHistogram_LocalRange = LocalRange(s.MACDHistogram, index, p.MacdSignal);

            snap.AO_LocalRange = LocalRange(s.AO, index, p.AoSlow);
            snap.CMO_LocalRange = LocalRange(s.CMO, index, p.CmoPeriod);
            snap.ROC_LocalRange = LocalRange(s.ROC, index, p.RocPeriod);
            snap.BBW_LocalRange = LocalRange(s.BB_Bandwidth, index, p.BbPeriod);

            snap.WilliamsR_LocalRange = LocalRange(s.WilliamsR, index, p.WilliamsRPeriod);
            snap.PriceROC_LocalRange = LocalRange(s.PriceROC, index, p.PriceRocPeriod);

            snap.KVO_LocalRange = LocalRange(s.KVO, index, p.KvoSignalPeriod);
            snap.KVOSignal_LocalRange = LocalRange(s.KVOSignal, index, p.KvoSignalPeriod);
            snap.KVOHistogram_LocalRange = LocalRange(s.KVOHistogram, index, p.KvoSignalPeriod);

            snap.VHF_LocalRange = LocalRange(s.VHF, index, p.VhfPeriod);
            snap.VIDYA_LocalRange = LocalRange(s.VIDYA, index, p.VidyaPeriod);

            snap.ATR_LocalRange = LocalRange(s.ATR, index, p.AtrPeriod);
            snap.VolumeOsc_LocalRange = LocalRange(s.VolumeOsc, index, p.VolOscSlow);
            snap.ElderForceIndex_LocalRange = LocalRange(s.ElderForceIndex, index, p.DerivativesDefaultPeriod);

            snap.VWAP_LocalRange = LocalRange(s.VWAP, index, p.DerivativesDefaultPeriod);
            snap.OBV_LocalRange = LocalRange(s.OBV, index, p.DerivativesDefaultPeriod);
            snap.PVT_LocalRange = LocalRange(s.PVT, index, p.DerivativesDefaultPeriod);

            // Допълнителни локални диапазони
            snap.MFI_LocalRange = LocalRange(s.MFI, index, p.DerivativesDefaultPeriod);
            snap.WilliamsAD_LocalRange = LocalRange(s.WilliamsAD, index, p.DerivativesDefaultPeriod);
            snap.BB_Middle_LocalRange = LocalRange(s.BB_Middle, index, p.BbPeriod);
            snap.BB_Upper_LocalRange = LocalRange(s.BB_Upper, index, p.BbPeriod);
            snap.BB_Lower_LocalRange = LocalRange(s.BB_Lower, index, p.BbPeriod);
            snap.SuperTrendValue_LocalRange = LocalRange(s.SuperTrendValue, index, p.DerivativesDefaultPeriod);

            return snap;
        }

        public static System.Collections.Generic.IDictionary<string, double> ComputeCore(int index, IndicatorSeriesBundle s)
        {
            var d = new System.Collections.Generic.Dictionary<string, double>(64, StringComparer.Ordinal);
            d["RSI"] = Get(s.RSI, index);
            d["MACD"] = Get(s.MACD, index);
            d["MACDSignal"] = Get(s.MACDSignal, index);
            d["MACDHistogram"] = Get(s.MACDHistogram, index);
            d["AO"] = Get(s.AO, index);
            d["MFI"] = Get(s.MFI, index);
            d["WilliamsAD"] = Get(s.WilliamsAD, index);
            d["VHF"] = Get(s.VHF, index);
            d["VIDYA"] = Get(s.VIDYA, index);
            d["CMO"] = Get(s.CMO, index);
            d["ROC"] = Get(s.ROC, index);
            d["BB_Middle"] = Get(s.BB_Middle, index);
            d["BB_Upper"] = Get(s.BB_Upper, index);
            d["BB_Lower"] = Get(s.BB_Lower, index);
            d["BB_Bandwidth"] = Get(s.BB_Bandwidth, index);
            d["ATR"] = Get(s.ATR, index);
            d["VolumeOsc"] = Get(s.VolumeOsc, index);
            d["ElderForceIndex"] = Get(s.ElderForceIndex, index);
            d["VWAP"] = Get(s.VWAP, index);
            d["OBV"] = Get(s.OBV, index);
            d["PVT"] = Get(s.PVT, index);
            d["SuperTrendValue"] = Get(s.SuperTrendValue, index);
            d["SuperTrendDirection"] = Get(s.SuperTrendDirection, index);
            d["KVO"] = Get(s.KVO, index);
            d["KVOSignal"] = Get(s.KVOSignal, index);
            d["KVOHistogram"] = Get(s.KVOHistogram, index);
            d["WilliamsR"] = Get(s.WilliamsR, index);
            d["PriceROC"] = Get(s.PriceROC, index);
            return d;
        }

        public static Derivative ComputeDerivatives(Func<int, double> seriesGetter, int index, int period)
        {
            return Derive(new FuncSeries(seriesGetter, index + 1), index, period);
        }

        public static int Cross(Func<int, double> aGetter, Func<int, double> bGetter, int index)
        {
            return Cross(new FuncSeries(aGetter, index + 1), new FuncSeries(bGetter, index + 1), index);
        }

        public static int MACD_Cross_Signal(IndicatorSeriesBundle s, int index) => Cross(s.MACD, s.MACDSignal, index);
        public static int RSI_Cross_MACD(IndicatorSeriesBundle s, int index) => Cross(s.RSI, s.MACD, index);
        public static int KVO_Cross_Signal(IndicatorSeriesBundle s, int index) => Cross(s.KVO, s.KVOSignal, index);

        private static double Get(ISeries s, int index)
        {
            if (s == null || index < 0 || index >= s.Count) return 0.0;
            double v = s[index];
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0.0;
            return v;
        }

        private static Derivative Derive(ISeries s, int index, int period)
        {
            var d = new Derivative();
            if (s == null || index <= 0) return d;

            double prev = Get(s, index - 1);
            double cur = Get(s, index);
            d.Delta = cur - prev;

            int n = Math.Max(2, period);
            d.Slope = LinearSlope(s, index, n);
            d.AngleDeg = Math.Atan(d.Slope) * 180.0 / Math.PI;

            double slopePrev = LinearSlope(s, index - 1, n);
            d.Acceleration = d.Slope - slopePrev;

            return d;
        }

        private static double LinearSlope(ISeries s, int index, int n)
        {
            if (s == null || index < 0) return 0.0;
            int end = Math.Min(index, s.Count - 1);
            int start = Math.Max(0, end - n + 1);
            int len = end - start + 1;
            if (len < 2) return 0.0;

            double sumX = 0.0, sumY = 0.0, sumXY = 0.0, sumX2 = 0.0;
            for (int i = 0; i < len; i++)
            {
                double x = i + 1;
                double y = Get(s, start + i);
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }
            double num = len * sumXY - sumX * sumY;
            double den = len * sumX2 - sumX * sumX;
            if (Math.Abs(den) < 1e-12) return 0.0;
            return num / den;
        }

        private static int Cross(ISeries a, ISeries b, int index)
        {
            if (a == null || b == null || index <= 0) return 0;
            double a0 = Get(a, index - 1);
            double b0 = Get(b, index - 1);
            double a1 = Get(a, index);
            double b1 = Get(b, index);
            if (a0 <= b0 && a1 > b1) return +1;
            if (a0 >= b0 && a1 < b1) return -1;
            return 0;
        }

        private static int Sign(double v)
        {
            if (v > 0) return +1;
            if (v < 0) return -1;
            return 0;
        }

        private static double LocalRange(ISeries s, int index, int period)
        {
            if (s == null || index < 0) return 0.0;
            int end = Math.Min(index, s.Count - 1);
            int start = Math.Max(0, end - Math.Max(1, period) + 1);
            if (end < start) return 0.0;
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int i = start; i <= end; i++)
            {
                double v = Get(s, i);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (double.IsInfinity(min) || double.IsInfinity(max)) return 0.0;
            return max - min;
        }

        private sealed class FuncSeries : ISeries
        {
            private readonly Func<int, double> _getter;
            private readonly int _count;
            public FuncSeries(Func<int, double> getter, int count) { _getter = getter; _count = Math.Max(0, count); }
            public int Count { get { return _count; } }
            public double this[int index] { get { return (index >= 0 && index < _count) ? Safe(index) : 0.0; } }
            private double Safe(int i)
            {
                try
                {
                    double v = _getter(i);
                    if (double.IsNaN(v) || double.IsInfinity(v)) return 0.0;
                    return v;
                }
                catch { return 0.0; }
            }
        }
    }
}

    /// <summary>
    /// Параметри за вътрешно изчисление на „допълнителни" индикатори върху барове,
    /// когато не са налични като стандартни cTrader серии.
    /// </summary>
    public sealed class IndicatorExtrasConfig
    {
        public int VidyaPeriod { get; init; } = 14;
        public double VidyaK { get; init; } = 0.2;
        public int VhfPeriod { get; init; } = 28;
        public int SuperTrendPeriod { get; init; } = 10;
        public double SuperTrendMult { get; init; } = 3.0;
        public int KvoFast { get; init; } = 34;
        public int KvoSlow { get; init; } = 55;
        public int KvoSignal { get; init; } = 13;
        public int VwapWindow { get; init; } = 20;
    }

    public static partial class IndicatorEngine
    {
        private const double ExtrasEps = 1e-12;

        /// <summary>
        /// Изчислява „допълнителни" индикатори върху баровете (VIDYA, VHF, SuperTrend, KVO(+Signal/Hist), WilliamsAD, VWAP).
        /// Може да ползва и вече налични стандартни серии (std) като ATR за SuperTrend. Връща плосък речник.
        /// </summary>
        public static IDictionary<string, double> ComputeExtrasFromBarsAsDict(
            int index,
            IReadOnlyList<OhlcvBar> bars,
            IndicatorExtrasConfig cfg,
            IndicatorSeriesBundle std = null)
        {
            var r = new Dictionary<string, double>(32, StringComparer.Ordinal);
            if (bars == null || bars.Count == 0 || index <= 0) return r;
            index = Math.Min(index, bars.Count - 1);

            double Close(int i) => bars[i].Close;
            double High(int i) => bars[i].High;
            double Low(int i) => bars[i].Low;
            long Vol(int i) => bars[i].TickVolume;
            double Typ(int i) => (High(i) + Low(i) + Close(i)) / 3.0;

            // Helpers
            double EMA(int period, Func<int, double> src)
            {
                double alpha = 2.0 / (period + 1.0);
                double ema = src(0);
                for (int i = 1; i <= index; i++) ema = alpha * src(i) + (1.0 - alpha) * ema;
                return ema;
            }
            double SMA(int period, Func<int, double> src)
            {
                int start = Math.Max(0, index - period + 1);
                int count = index - start + 1;
                if (count <= 0) return 0.0;
                double sum = 0.0;
                for (int i = start; i <= index; i++) sum += src(i);
                return sum / count;
            }

            // 1) VIDYA (adaptive EMA via CMO)
            {
                double vidya = Close(0);
                for (int i = 1; i <= index; i++)
                {
                    int p = Math.Min(cfg.VidyaPeriod, i);
                    double up = 0.0, dn = 0.0;
                    for (int k = i - p + 1; k <= i; k++)
                    {
                        double ch = Close(k) - Close(k - 1);
                        if (ch > 0) up += ch; else dn -= ch;
                    }
                    double cmo = (up + dn) > ExtrasEps ? (up - dn) / (up + dn) : 0.0;
                    double alpha = cfg.VidyaK * Math.Abs(cmo);
                    vidya = vidya + alpha * (Close(i) - vidya);
                }
                r["VIDYA"] = vidya;
            }

            // 2) VHF (Vertical Horizontal Filter)
            if (index >= cfg.VhfPeriod)
            {
                int start = index - cfg.VhfPeriod + 1;
                double hh = double.NegativeInfinity, ll = double.PositiveInfinity, sum = 0.0;
                for (int i = start; i <= index; i++)
                {
                    if (High(i) > hh) hh = High(i);
                    if (Low(i) < ll) ll = Low(i);
                    if (i > start) sum += Math.Abs(Close(i) - Close(i - 1));
                }
                r["VHF"] = sum > ExtrasEps ? (hh - ll) / sum : 0.0;
            }

            // 3) SuperTrend (използва ATR от std ако има; иначе fallback SMA TR)
            {
                double atr = 0.0;
                if (std?.ATR != null && index < std.ATR.Count) atr = Get(std.ATR, index);
                if (atr <= 0.0)
                {
                    int p = Math.Max(1, Math.Min(cfg.SuperTrendPeriod, index));
                    int s = p;
                    double sumTr = 0.0;
                    for (int i = index - s + 1; i <= index; i++)
                    {
                        double prevClose = (i > 0 ? Close(i - 1) : Close(i));
                        double tr = Math.Max(High(i) - Low(i), Math.Max(Math.Abs(High(i) - prevClose), Math.Abs(Low(i) - prevClose)));
                        sumTr += tr;
                    }
                    atr = sumTr / Math.Max(1, s);
                }

                double hl2 = (High(index) + Low(index)) / 2.0;
                double upperBand = hl2 + cfg.SuperTrendMult * atr;
                double lowerBand = hl2 - cfg.SuperTrendMult * atr;

                int dir = Close(index) >= upperBand ? +1 : (Close(index) <= lowerBand ? -1 : 0);
                double stValue = (dir >= 0) ? lowerBand : upperBand;

                r["SuperTrendValue"] = stValue;
                r["SuperTrendDirection"] = dir;
            }

            // 4) KVO (Klinger Volume Oscillator) + сигнал/хистограма
            {
                double VFsrc(int i)
                {
                    double dm = Typ(i) - Typ(i - 1);
                    int trend = dm >= 0 ? +1 : -1;
                    return trend * Vol(i) * Math.Abs(High(i) - Low(i));
                }
                double emaVFfast = EMA(cfg.KvoFast, i => (i == 0 ? 0.0 : VFsrc(i)));
                double emaVFslow = EMA(cfg.KvoSlow, i => (i == 0 ? 0.0 : VFsrc(i)));
                double kvo = emaVFfast - emaVFslow;
                double kvoSignal = EMA(cfg.KvoSignal, _ => kvo);
                r["KVO"] = kvo;
                r["KVOSignal"] = kvoSignal;
                r["KVOHistogram"] = kvo - kvoSignal;
            }

            // 5) Williams Accumulation/Distribution (ADL)
            {
                double adl = 0.0;
                for (int i = 0; i <= index; i++)
                {
                    double range = (High(i) - Low(i));
                    double clv = range > ExtrasEps ? ((Close(i) - Low(i)) - (High(i) - Close(i))) / range : 0.0;
                    adl += clv * Vol(i);
                }
                r["WilliamsAD"] = adl;
            }

            // 6) VWAP (rolling по прозорец)
            {
                int w = cfg.VwapWindow;
                int start = Math.Max(0, index - w + 1);
                double pv = 0.0, v = 0.0;
                for (int i = start; i <= index; i++) { pv += Typ(i) * Vol(i); v += Vol(i); }
                r["VWAP"] = v > ExtrasEps ? pv / v : Close(index);
            }

            return r;
        }
    }