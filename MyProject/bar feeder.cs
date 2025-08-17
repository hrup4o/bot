// cTrader indicator adapter that wires broker bars to your engines and ChartSelectionLogger
// Drop this into cTrader as an Indicator. It does not contain thresholds/normalizations.
// It logs either a selected range via parameters or every bar close if enabled.

using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;
using Logging;
using MarketSignals.Core.Hybrid;
using MarketSignals.Core.Metrics;
using MarketSignals.Core.RawData;
using IndicatorLib = Indicators; // alias to your custom Indicators namespace (IndicatorEngine, ArraySeries, etc.)

namespace cAlgo
{
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FileSystem)]
    public class TcnSpikeLoggerIndicator : Indicator
    {
        // Logging
        [Parameter("Log directory", DefaultValue = "C:/Users/Public/MyCTraderLogs", Group = "Logging")]
        public string LogDirectory { get; set; }

        [Parameter("Log invariant culture", DefaultValue = true, Group = "Logging")]
        public bool LogInvariant { get; set; }

        [Parameter("Missing as NaN (else 0)", DefaultValue = true, Group = "Logging")]
        public bool MissingAsNaN { get; set; }

        [Parameter("Write .schema.json", DefaultValue = true, Group = "Logging")]
        public bool WriteSchema { get; set; }

        [Parameter("Auto log each bar close", DefaultValue = false, Group = "Logging")]
        public bool AutoLogOnClose { get; set; }

        [Parameter("Manual Log Range Start", DefaultValue = 0, Group = "Manual Log")]
        public int ManualStart { get; set; }

        [Parameter("Manual Log Range End", DefaultValue = 0, Group = "Manual Log")]
        public int ManualEnd { get; set; }

        [Parameter("Trigger Manual Log", DefaultValue = false, Group = "Manual Log")]
        public bool TriggerManualLog { get; set; }

        // Engines toggles
        [Parameter("Use OHLC Engine", DefaultValue = true, Group = "Engines")]
        public bool UseOhlc { get; set; }

        [Parameter("Use HeikenAshi Engine", DefaultValue = true, Group = "Engines")]
        public bool UseHa { get; set; }

        [Parameter("Use Spike Engine", DefaultValue = true, Group = "Engines")]
        public bool UseSpike { get; set; }

        [Parameter("Use Hybrid Engine", DefaultValue = true, Group = "Engines")]
        public bool UseHybrid { get; set; }

        [Parameter("Use Indicator Engine (cTrader series)", DefaultValue = false, Group = "Engines")]
        public bool UseIndicatorEngine { get; set; }

        // cTrader built-in indicators (minimal set; expand as needed)
        private RelativeStrengthIndex _rsi;
        private MacdCrossOver _macd;
        private AverageTrueRange _atr;
        private BollingerBands _bb;
        private AwesomeOscillator _ao;
        private MoneyFlowIndex _mfi;
        private WilliamsPctR _willr;
        private RateOfChange _roc;
        private ForceIndex _efi;

        // State for adapter-computed series
        private double _obv = 0.0;
        private double _pvt = 0.0;
        private double _emaVolFast = 0.0;
        private double _emaVolSlow = 0.0;
        private int _volOscFast = 5;
        private int _volOscSlow = 14;

        // Local lists behind ArraySeries in bundle (so we can write values)
        private List<double> _rsiL, _macdL, _macdSigL, _macdHistL;
        private List<double> _atrL, _bbMidL, _bbUpL, _bbLoL, _bbBwL;
        private List<double> _aoL, _mfiL, _willrL, _rocL, _efiL;
        private List<double> _obvL, _pvtL, _volOscL, _vwapL;

        // Extras config
        private IndicatorExtrasConfig _extrasCfg;

        // Storage
        private readonly List<OhlcvBar> _bars = new List<OhlcvBar>(4096);

        // Engines
        private OhlcvMetricsEngine _ohlcEngine;
        private HeikenAshiMetricsEngine _haEngine;
        private OptimalTCNSpikeEngine _spikeEngine;
        private HybridRawFeatureEngine _hybridEngine;

        // Indicator Engine wiring
        private IndicatorLib.IndicatorSeriesBundle _indBundle;
        private IndicatorLib.IndicatorParams _indParams;

        // Logger
        private ChartSelectionLogger _logger;

        protected override void Initialize()
        {
            // Initialize built-in indicators if we will use IndicatorEngine
            if (UseIndicatorEngine)
            {
                _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);
                _macd = Indicators.MacdCrossOver(Bars.ClosePrices, 12, 26, 9);
                _atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
                _bb = Indicators.BollingerBands(Bars.ClosePrices, 20, 2.0, MovingAverageType.Simple);
                _ao = Indicators.AwesomeOscillator(Bars);
                _mfi = Indicators.MoneyFlowIndex(Bars, 14);
                _willr = Indicators.WilliamsPctR(Bars, 14);
                _roc = Indicators.RateOfChange(Bars.ClosePrices, 14);
                _efi = Indicators.ForceIndex(Bars, 13, MovingAverageType.Exponential);

                // Allocate lists and bind them into bundle
                _rsiL = NewList(Bars.Count);
                _macdL = NewList(Bars.Count);
                _macdSigL = NewList(Bars.Count);
                _macdHistL = NewList(Bars.Count);

                _atrL = NewList(Bars.Count);
                _bbMidL = NewList(Bars.Count);
                _bbUpL = NewList(Bars.Count);
                _bbLoL = NewList(Bars.Count);
                _bbBwL = NewList(Bars.Count);

                _aoL = NewList(Bars.Count);
                _mfiL = NewList(Bars.Count);
                _willrL = NewList(Bars.Count);
                _rocL = NewList(Bars.Count);
                _efiL = NewList(Bars.Count);

                _obvL = NewList(Bars.Count);
                _pvtL = NewList(Bars.Count);
                _volOscL = NewList(Bars.Count);
                _vwapL = NewList(Bars.Count);

                _indBundle = new IndicatorLib.IndicatorSeriesBundle
                {
                    RSI = new IndicatorLib.ArraySeries(_rsiL),
                    MACD = new IndicatorLib.ArraySeries(_macdL),
                    MACDSignal = new IndicatorLib.ArraySeries(_macdSigL),
                    MACDHistogram = new IndicatorLib.ArraySeries(_macdHistL),

                    ATR = new IndicatorLib.ArraySeries(_atrL),
                    BB_Middle = new IndicatorLib.ArraySeries(_bbMidL),
                    BB_Upper = new IndicatorLib.ArraySeries(_bbUpL),
                    BB_Lower = new IndicatorLib.ArraySeries(_bbLoL),
                    BB_Bandwidth = new IndicatorLib.ArraySeries(_bbBwL),

                    AO = new IndicatorLib.ArraySeries(_aoL),
                    MFI = new IndicatorLib.ArraySeries(_mfiL),
                    WilliamsR = new IndicatorLib.ArraySeries(_willrL),
                    PriceROC = new IndicatorLib.ArraySeries(_rocL),
                    ElderForceIndex = new IndicatorLib.ArraySeries(_efiL),

                    OBV = new IndicatorLib.ArraySeries(_obvL),
                    PVT = new IndicatorLib.ArraySeries(_pvtL),
                    VolumeOsc = new IndicatorLib.ArraySeries(_volOscL),
                    VWAP = new IndicatorLib.ArraySeries(_vwapL)
                };

                _indParams = new IndicatorLib.IndicatorParams
                {
                    // Use standard periods for derivatives if needed (these are examples)
                    RSIPeriod = 14,
                    MACDFast = 12,
                    MACDSlow = 26,
                    MACDSignal = 9,
                    ATRPeriod = 14,
                    BBPeriod = 20
                };

                // extras config + register extras to logger
                _extrasCfg = new IndicatorExtrasConfig();
                // Register meta provider (Index/Timestamp)
                _logger.RegisterFeatureProvider("meta", i =>
                {
                    var f = new Dictionary<string, float>(4, StringComparer.Ordinal);
                    f["Index"] = i;
                    f["Timestamp"] = (float)_bars[i].TimestampUtc.Ticks; // Use Ticks for numeric value
                    f["Symbol"] = SymbolName.GetHashCode(); // Numeric representation
                    f["Timeframe"] = (float)TimeFrame.Minutes;
                    return f;
                });

                _logger.RegisterFeatureProvider("indicator_extras", i =>
                {
                    var d = Indicators.IndicatorEngine.ComputeExtrasFromBarsAsDict(i, _bars, _extrasCfg, _indBundle);
                    var f = new Dictionary<string, float>(d.Count, StringComparer.Ordinal);
                    foreach (var kv in d) f[kv.Key] = (float)kv.Value;
                    return f;
                });

                // Set column order for CSV output
                var headerOrder = new List<string>
                {
                    // Meta
                    "Index", "Timestamp", "Symbol", "Timeframe",

                    // OHLC (OhlcvMetrics)
                    "Range", "Body", "UpperWick", "LowerWick", "BodyToRange", "WickAsymmetry", "ClosePosInRange",
                    "MarubozuScore", "DojiScore",
                    "Direction", "Gap", "GapAbs", "GapPct",
                    "RangeMaShort", "RangeMaLong",
                    "DistToPrevHighN", "DistToPrevLowN", "BarsSinceSwingHigh", "BarsSinceSwingLow",
                    "ParkinsonRv", "GarmanKlassRv", "RogersSatchellRv",

                    // Heiken-Ashi (HeikenAshiMetrics)
                    "HaOpen", "HaHigh", "HaLow", "HaClose",
                    "HaRange", "HaBody", "HaUpperWick", "HaLowerWick",
                    "HaBodyToRange", "HaWickAsymmetry", "HaClosePosInRange",
                    "HaMarubozuScore", "HaDojiScore", "HaPinBarScore",
                    "HaDirection", "HaGap", "HaGapAbs", "HaGapPct",
                    "HaParkinsonRv", "HaGarmanKlassRv", "HaRogersSatchellRv",
                    "HaRangeMaShort", "HaRangeMaLong",
                    "HaRangeMeanW", "HaRangeStdW", "HaRangeDevFromMeanW",
                    "HaParkMeanW", "HaParkStdW", "HaParkDevFromMeanW",
                    "HaOlsSlopeShort", "HaOlsSlopeLong", "HaOlsSlopeSEShort", "HaOlsSlopeMSEShort",
                    "HaLinearSlopeShort", "HaLinearSlopeLong",
                    "HaOlsAngleShortDeg", "HaOlsAngleLongDeg", "HaLinearAngleShortDeg", "HaLinearAngleLongDeg",
                    "HaAccelerationShort", "HaCurvature",
                    "HaColorRunLen", "HaTrendStrength",
                    "HaClv", "HaClvEmaShort", "HaVolumeMaShort", "HaVolumeRatio",
                    "HaPressureIndex", "HaBodyPressure", "HaWickPressure",
                    "HaEngulfingScore", "HaFvgUp", "HaFvgDown", "HaSweepUpScore", "HaSweepDownScore",
                    "HaDistToPrevHighN", "HaDistToPrevLowN", "HaDistToSwingHigh", "HaDistToSwingLow",
                    "HaBarsSinceSwingHigh", "HaBarsSinceSwingLow",
                    "HaBbWidthShort", "HaKcWidthShortProxy", "HaSqueezeProxy", "HaCompressionProxy",

                    // Indicator Extras (IndicatorEngine)
                    "VIDYA", "VHF", "SuperTrendValue", "SuperTrendDirection",
                    "KVO", "KVOSignal", "KVOHistogram", "WilliamsAD", "VWAP",

                    // Indicators Core (raw)
                    "RSI", "MACD", "MACDSignal", "MACDHistogram",
                    "AO", "MFI", "CMO", "ROC",
                    "BB_Middle", "BB_Upper", "BB_Lower", "BB_Bandwidth",
                    "ATR", "VolumeOsc", "ElderForceIndex",
                    "OBV", "PVT", "WilliamsR", "PriceROC",

                    // Indicator derivatives (Delta/Slope/AngleDeg/Acceleration/Direction)
                    "RSI_Delta", "RSI_Slope", "RSI_AngleDeg", "RSI_Acceleration", "RSI_Direction",
                    "MACD_Delta", "MACD_Slope", "MACD_AngleDeg", "MACD_Acceleration", "MACD_Direction",
                    "MACDSignal_Delta", "MACDSignal_Slope", "MACDSignal_AngleDeg", "MACDSignal_Acceleration", "MACDSignal_Direction",
                    "MACDHistogram_Delta", "MACDHistogram_Slope", "MACDHistogram_AngleDeg", "MACDHistogram_Acceleration", "MACDHistogram_Direction",
                    "AO_Delta", "AO_Slope", "AO_AngleDeg", "AO_Acceleration", "AO_Direction",
                    "MFI_Delta", "MFI_Slope", "MFI_AngleDeg", "MFI_Acceleration", "MFI_Direction",
                    "CMO_Delta", "CMO_Slope", "CMO_AngleDeg", "CMO_Acceleration", "CMO_Direction",
                    "ROC_Delta", "ROC_Slope", "ROC_AngleDeg", "ROC_Acceleration", "ROC_Direction",
                    "ATR_Delta", "ATR_Slope", "ATR_AngleDeg", "ATR_Acceleration", "ATR_Direction",
                    "VolumeOsc_Delta", "VolumeOsc_Slope", "VolumeOsc_AngleDeg", "VolumeOsc_Acceleration", "VolumeOsc_Direction",
                    "ElderForceIndex_Delta", "ElderForceIndex_Slope", "ElderForceIndex_AngleDeg", "ElderForceIndex_Acceleration", "ElderForceIndex_Direction",
                    "OBV_Delta", "OBV_Slope", "OBV_AngleDeg", "OBV_Acceleration", "OBV_Direction",
                    "PVT_Delta", "PVT_Slope", "PVT_AngleDeg", "PVT_Acceleration", "PVT_Direction",
                    "WilliamsR_Delta", "WilliamsR_Slope", "WilliamsR_AngleDeg", "WilliamsR_Acceleration", "WilliamsR_Direction",
                    "PriceROC_Delta", "PriceROC_Slope", "PriceROC_AngleDeg", "PriceROC_Acceleration", "PriceROC_Direction",
                    "VIDYA_Delta", "VIDYA_Slope", "VIDYA_AngleDeg", "VIDYA_Acceleration", "VIDYA_Direction",
                    "VHF_Delta", "VHF_Slope", "VHF_AngleDeg", "VHF_Acceleration", "VHF_Direction",
                    "SuperTrendValue_Delta", "SuperTrendValue_Slope", "SuperTrendValue_AngleDeg", "SuperTrendValue_Acceleration", "SuperTrendValue_Direction",
                    "KVO_Delta", "KVO_Slope", "KVO_AngleDeg", "KVO_Acceleration", "KVO_Direction",
                    "KVOSignal_Delta", "KVOSignal_Slope", "KVOSignal_AngleDeg", "KVOSignal_Acceleration", "KVOSignal_Direction",
                    "WilliamsAD_Delta", "WilliamsAD_Slope", "WilliamsAD_AngleDeg", "WilliamsAD_Acceleration", "WilliamsAD_Direction",
                    "VWAP_Delta", "VWAP_Slope", "VWAP_AngleDeg", "VWAP_Acceleration", "VWAP_Direction",
                    "BB_Middle_Delta", "BB_Middle_Slope", "BB_Middle_AngleDeg", "BB_Middle_Acceleration", "BB_Middle_Direction",
                    "BB_Upper_Delta", "BB_Upper_Slope", "BB_Upper_AngleDeg", "BB_Upper_Acceleration", "BB_Upper_Direction",
                    "BB_Lower_Delta", "BB_Lower_Slope", "BB_Lower_AngleDeg", "BB_Lower_Acceleration", "BB_Lower_Direction",

                    // Indicator pairs: spreads/distances
                    "MACD_Minus_Signal",
                    "RSI_Minus_MFI", "RSI_Minus_CMO", "MFI_Minus_CMO", "RSI_Minus_MACDSignal",
                    "VIDYA_Minus_BB_Middle",

                    // Indicator crosses (-1/0/+1)
                    "MACD_Cross_Signal", "KVO_Cross_Signal",
                    "RSI_Cross_MFI", "RSI_Cross_CMO", "MFI_Cross_CMO",
                    "OBV_Cross_PVT", "VIDYA_Cross_BB_Middle",

                    // OptimalTCNSpikeEngine - TIER 1: Raw OHLCV
                    "Open", "High", "Low", "Close", "Volume",

                    // TIER 2: Basic transformations
                    "Range_Tier2", "Body_Tier2", "UpperWick_Tier2", "LowerWick_Tier2",
                    "BodyRatio", "UpperWickRatio", "LowerWickRatio", "WickImbalance",

                    // TIER 3: Multi-Scale Price Velocities
                    "PriceVelocity1", "PriceVelocity3", "PriceVelocity5", "PriceVelocity10", "PriceVelocity20",

                    // TIER 4: Multi-Scale Acceleration
                    "PriceAcceleration3", "PriceAcceleration5", "PriceAcceleration10",

                    // TIER 5: Multi-Scale Ratios
                    "RangeRatio3", "RangeRatio5", "RangeRatio10", "RangeRatio20", "RangeRatio50",
                    "VolumeRatio3", "VolumeRatio5", "VolumeRatio10", "VolumeRatio20", "VolumeRatio50",

                    // TIER 6: Statistical Moments
                    "PriceStdDev10", "PriceStdDev20", "PriceStdDev50",
                    "VolumeStdDev10", "VolumeStdDev20", "VolumeStdDev50",
                    "RangeStdDev10", "RangeStdDev20", "RangeStdDev50",

                    // TIER 7: Higher Statistical Moments
                    "PriceSkewness10", "PriceSkewness20",
                    "PriceKurtosis10", "PriceKurtosis20",
                    "VolumeSkewness10", "VolumeSkewness20",

                    // TIER 8: Cross-Correlations
                    "PriceVolumeCorr5", "PriceVolumeCorr10", "PriceVolumeCorr20",
                    "RangeVolumeCorr5", "RangeVolumeCorr10", "RangeVolumeCorr20",

                    // TIER 9: Multi-Scale Momentum
                    "PriceMomentum3", "PriceMomentum5", "PriceMomentum10", "PriceMomentum20",
                    "VolumeMomentum3", "VolumeMomentum5", "VolumeMomentum10", "VolumeMomentum20",

                    // TIER 10: Momentum Divergence
                    "MomentumDivergence3", "MomentumDivergence5", "MomentumDivergence10",

                    // TIER 11: Structural Relationships
                    "DistanceToHigh20", "DistanceToLow20", "DistanceToHigh50", "DistanceToLow50",
                    "DistanceToHigh20Pct", "DistanceToLow20Pct", "DistanceToHigh50Pct", "DistanceToLow50Pct",

                    // TIER 12: Simple Moving Average Relationships
                    "DistanceToSMA5", "DistanceToSMA10", "DistanceToSMA20", "DistanceToSMA50",
                    "DistanceToSMA5Pct", "DistanceToSMA10Pct", "DistanceToSMA20Pct", "DistanceToSMA50Pct",

                    // TIER 13: Consecutive Patterns
                    "ConsecutiveUpBars", "ConsecutiveDownBars",
                    "ConsecutiveUpBodies", "ConsecutiveDownBodies",
                    "ConsecutiveHighVolume", "ConsecutiveLowVolume",
                    "ConsecutiveWideRanges", "ConsecutiveTightRanges",

                    // TIER 14: Time Context
                    "HourSin", "HourCos", "DayOfWeekSin", "DayOfWeekCos", "MinuteSin", "MinuteCos",

                    // TIER 15: Order Flow Proxies
                    "VolumePerPoint", "VolumePerRange", "PriceEfficiency",
                    "BuyPressureProxy", "SellPressureProxy",

                    // TIER 16: Multi-Scale Volatility
                    "VolatilityRatio5", "VolatilityRatio10", "VolatilityRatio20", "VolatilityRatio50",
                    "VolatilityMomentum5", "VolatilityMomentum10", "VolatilityMomentum20",

                    // TIER 17: Fractal Dimensions
                    "FractalDim32", "FractalDim64",

                    // TIER 18: Additional Spike Raw Features
                    "ER10", "ER20", "ER50",
                    "RS20", "RS50", "RS100",
                    "RV10", "RV20", "RV50",
                    "BPV10", "BPV20", "BPV50",
                    "RQ10", "RQ20", "RQ50",
                    "RealRange10", "RealRange20", "RealRange50",
                    "VWAP10", "VWAP20", "VWAP50",
                    "DevVWAP10", "DevVWAP20", "DevVWAP50",

                    // TIER 19: ACF/PACF and Sign Features
                    "ACF1", "ACF2", "ACF3", "PACF1",
                    "SignImbalance20", "SignImbalance50", "SignImbalance100",
                    "PosShare20", "PosShare50", "PosShare100",
                    "Median10", "MAD10", "DevMedian10",
                    "Median20", "MAD20", "DevMedian20",
                    "Median50", "MAD50", "DevMedian50",
                    "SpecLHRatio16", "SpecLHRatio32",

                    // TIER 20: Liquidity and Drawdown Features
                    "SlopeStabilityShort",
                    "RangePerVolume", "BodyPerVolume",
                    "RangePerVolumeRatio20", "RangePerVolumeRatio50",
                    "BodyPerVolumeRatio20", "BodyPerVolumeRatio50",
                    "MaxDrawdown20", "MaxDrawdown50", "MaxDrawdown100",
                    "MaxRunup20", "MaxRunup50", "MaxRunup100",
                    "EntropySign20", "EntropySign50", "EntropySign100",

                    // HybridRawFeatureEngine (Mx prefix)
                    "MxPriceVel_1", "MxPriceVel_3", "MxPriceVel_5", "MxPriceVel_10", "MxPriceVel_20",
                    "MxPriceAccel_3", "MxPriceAccel_5", "MxPriceAccel_10",
                    "MxRangeRatio_5", "MxRangeRatio_10", "MxRangeRatio_20", "MxRangeRatio_50",
                    "MxVolRatio_5", "MxVolRatio_10", "MxVolRatio_20", "MxVolRatio_50",
                    "MxPriceStd_10", "MxPriceStd_20", "MxPriceStd_50",
                    "MxSkew_10", "MxSkew_20", "MxKurt_10", "MxKurt_20",
                    "MxCorrPV_5", "MxCorrPV_10", "MxCorrPV_20",
                    "ConsUpBars", "ConsDownBars", "ConsHighVol", "ConsTightRanges",
                    "Div_PriceVol_5", "Div_PriceVol_10",
                    "MxER_10", "MxER_20", "MxER_50",
                    "MxRS_20", "MxRS_50", "MxRS_100", "MxHurstProxy_20", "MxHurstProxy_50", "MxHurstProxy_100",
                    "MxRV_10", "MxRV_20", "MxRV_50", "MxBPV_10", "MxBPV_20", "MxBPV_50",
                    "MxRQ_10", "MxRQ_20", "MxRQ_50", "MxRealRange_10", "MxRealRange_20", "MxRealRange_50",
                    "MxVWAP_10", "MxVWAP_20", "MxVWAP_50", "MxDevVWAP_10", "MxDevVWAP_20", "MxDevVWAP_50",
                    "MxACF1", "MxACF2", "MxACF3", "MxPACF1",
                    "MxSignImb", "MxPosShare",
                    "MxMed_10", "MxMed_20", "MxMAD_10", "MxMAD_20", "MxDevMed_10", "MxDevMed_20",
                    "MxSpecLHRatio_16", "MxSpecLHRatio_32",
                    "MxFD_Higuchi_32", "MxFD_Higuchi_64", "MxFD_Katz_32", "MxFD_Katz_64",
                    "MxSlopeStd",
                    "MxLiq_RangeVol", "MxLiq_BodyVol", "MxLiq_RangeVolRatio", "MxLiq_BodyVolRatio",
                    "MxStdRatio_ShortLong",
                    "MxDD_20", "MxDD_50", "MxDD_100", "MxRU_20", "MxRU_50", "MxRU_100",
                    "MxEntSign_20", "MxEntSign_50", "MxEntSign_100",
                    "MxAngleDiff_OlsShort", "MxAngleDiff_LinShort", "MxPressureDiff",
                    "MxBodyPressureDiff", "MxWickPressureDiff", "MxRunLenDiff_Signed", "MxRunLenDiff_Abs",
                    "Time_HourSin", "Time_HourCos", "Time_DaySin", "Time_DayCos", "Time_MinSin", "Time_MinCos",
                    "Meta_FeatureCompleteness", "Meta_DataConsistency", "Meta_BarsProcessed",

                    // Engine meta (optional)
                    "IsReady", "BarsProcessed"
                };
                _logger.SetHeaderOrder(headerOrder);
            }

            // Engines
            if (UseOhlc) _ohlcEngine = new OhlcvMetricsEngine(new OhlcvMetricsConfig());
            if (UseHa) _haEngine = new HeikenAshiMetricsEngine(new HeikenAshiMetricsConfig());
            if (UseSpike) _spikeEngine = new OptimalTCNSpikeEngine(new OptimalTCNConfig());
            if (UseHybrid) _hybridEngine = new HybridRawFeatureEngine(new HybridRawFeatureConfig());

            // Logger
            _logger = new ChartSelectionLogger(LogDirectory)
                .SetMetadata(SymbolName, TimeFrame.ToString())
                .SetCultureInvariant(LogInvariant)
                .SetMissingValuePolicy(MissingAsNaN ? ChartSelectionLogger.MissingValuePolicy.NaN : ChartSelectionLogger.MissingValuePolicy.Zero)
                .SetSchemaExport(WriteSchema)
                .SetOhlcvBarAccessor(i => _bars[i]);

            if (UseOhlc) _logger.SetOhlcvEngine(_ohlcEngine);
            if (UseHa) _logger.SetHeikenAshiEngine(_haEngine);
            if (UseSpike) _logger.SetSpikeEngine(_spikeEngine);
            if (UseHybrid) _logger.SetHybridEngine(_hybridEngine);

            if (UseIndicatorEngine && _indBundle != null && _indParams != null)
            {
                _logger.SetIndicatorEngine(_indBundle, _indParams);
            }

            // Optional: add a lightweight renderer to draw selection markers (vertical lines)
            _logger.SetRenderer(new CTraderSelectionRenderer(this));
        }

        public override void Calculate(int index)
        {
            // Ensure capacity
            if (_bars.Count <= index)
            {
                for (int i = _bars.Count; i <= index; i++)
                {
                    _bars.Add(CreateBar(i));
                }
            }
            else
            {
                _bars[index] = CreateBar(index);
            }

            // Update indicator bundle arrays for this index if in use
            if (UseIndicatorEngine && _indBundle != null)
            {
                // Resize if Bars grew
                EnsureSeriesCapacity(index + 1);

                // Write built-in indicators to lists/bundle
                _rsiL[index] = _rsi?.Result[index] ?? double.NaN;
                _macdL[index] = _macd?.MACD[index] ?? double.NaN;
                _macdSigL[index] = _macd?.Signal[index] ?? double.NaN;
                _macdHistL[index] = _macd?.Histogram[index] ?? double.NaN;

                _atrL[index] = _atr?.Result[index] ?? double.NaN;
                _bbMidL[index] = _bb?.Main[index] ?? double.NaN;
                _bbUpL[index] = _bb?.Top[index] ?? double.NaN;
                _bbLoL[index] = _bb?.Bottom[index] ?? double.NaN;
                _bbBwL[index] = (_bb != null) ? (_bb.Top[index] - _bb.Bottom[index]) : double.NaN;

                // Additional built-ins
                _aoL[index] = _ao?.Result[index] ?? double.NaN;
                _mfiL[index] = _mfi?.Result[index] ?? double.NaN;
                _willrL[index] = _willr?.Result[index] ?? double.NaN;
                _rocL[index] = _roc?.Result[index] ?? double.NaN;
                _efiL[index] = _efi?.Result[index] ?? double.NaN;

                // Adapter-computed OBV/PVT/VolumeOsc/VWAP
                if (index > 0)
                {
                    // OBV
                    if (Bars.ClosePrices[index] > Bars.ClosePrices[index - 1]) _obv += Bars.TickVolumes[index];
                    else if (Bars.ClosePrices[index] < Bars.ClosePrices[index - 1]) _obv -= Bars.TickVolumes[index];
                    _obvL[index] = _obv;

                    // PVT
                    if (Math.Abs(Bars.ClosePrices[index - 1]) > 1e-12)
                        _pvt += Bars.TickVolumes[index] * (Bars.ClosePrices[index] - Bars.ClosePrices[index - 1]) / Bars.ClosePrices[index - 1];
                    _pvtL[index] = _pvt;

                    // VolumeOsc (EMA fast - EMA slow on volumes)
                    double alphaF = 2.0 / (_volOscFast + 1.0);
                    double alphaS = 2.0 / (_volOscSlow + 1.0);
                    if (index == 1) { _emaVolFast = Bars.TickVolumes[0]; _emaVolSlow = Bars.TickVolumes[0]; }
                    _emaVolFast = alphaF * Bars.TickVolumes[index] + (1 - alphaF) * _emaVolFast;
                    _emaVolSlow = alphaS * Bars.TickVolumes[index] + (1 - alphaS) * _emaVolSlow;
                    _volOscL[index] = _emaVolFast - _emaVolSlow;
                }
                else
                {
                    _obvL[index] = 0.0;
                    _pvtL[index] = 0.0;
                    _emaVolFast = Bars.TickVolumes[index];
                    _emaVolSlow = Bars.TickVolumes[index];
                    _volOscL[index] = 0.0;
                }

                // VWAP rolling 20
                int w = Math.Min(20, index + 1);
                double pv = 0.0, vv = 0.0;
                for (int j = index - w + 1; j <= index; j++)
                {
                    double tp = (Bars.HighPrices[j] + Bars.LowPrices[j] + Bars.ClosePrices[j]) / 3.0;
                    pv += tp * Bars.TickVolumes[j];
                    vv += Bars.TickVolumes[j];
                }
                _vwapL[index] = vv > 0 ? pv / vv : Bars.ClosePrices[index];
            }

            // Optionally log each bar close as a single-row CSV append
            if (AutoLogOnClose && IsLastBar)
            {
                _logger.LogRange(index, index, "live");
            }

            // Manual one-shot logging via parameters
            if (TriggerManualLog)
            {
                int start = Math.Max(0, Math.Min(ManualStart, index));
                int end = Math.Max(start, Math.Min(ManualEnd <= 0 ? index : ManualEnd, index));
                _logger.LogRange(start, end, "manual");
                // reset switch to avoid repeat
                TriggerManualLog = false;
            }
        }

        private OhlcvBar CreateBar(int i)
        {
            // Map cTrader bar to OhlcvBar (UTC)
            return new OhlcvBar(
                SymbolName,
                Timeframe: MapTimeframe(TimeFrame),
                timestampUtc: Bars.OpenTimes[i].ToUniversalTime(),
                open: Bars.OpenPrices[i],
                high: Bars.HighPrices[i],
                low: Bars.LowPrices[i],
                close: Bars.ClosePrices[i],
                tickVolume: Bars.TickVolumes[i]
            );
        }

        private static Timeframe MapTimeframe(TimeFrame tf)
        {
            // Minimal map; extend as needed
            switch (tf)
            {
                case TimeFrame.Minute15: return Timeframe.M15;
                default: return Timeframe.M15;
            }
        }

        private void EnsureSeriesCapacity(int needed)
        {
            void Ensure(List<double> l)
            {
                if (l.Count < needed)
                {
                    int add = needed - l.Count;
                    for (int i = 0; i < add; i++) l.Add(double.NaN);
                }
            }
            Ensure(_rsiL); Ensure(_macdL); Ensure(_macdSigL); Ensure(_macdHistL);
            Ensure(_atrL); Ensure(_bbMidL); Ensure(_bbUpL); Ensure(_bbLoL); Ensure(_bbBwL);
            Ensure(_aoL); Ensure(_mfiL); Ensure(_willrL); Ensure(_rocL); Ensure(_efiL);
            Ensure(_obvL); Ensure(_pvtL); Ensure(_volOscL); Ensure(_vwapL);
        }

        // Simple renderer that draws vertical lines as selection markers
        private sealed class CTraderSelectionRenderer : ISelectionRenderer
        {
            private readonly TcnSpikeLoggerIndicator _owner;

            public CTraderSelectionRenderer(TcnSpikeLoggerIndicator owner) { _owner = owner; }

            public void DrawStart(int index, DateTime timestampUtc)
            {
                DrawLine($"sel_start_{index}", index, Colors.Lime);
            }

            public void DrawEnd(int index, DateTime timestampUtc)
            {
                DrawLine($"sel_end_{index}", index, Colors.Orange);
            }

            public void RemoveAt(int index)
            {
                _owner.Chart.RemoveObject($"sel_start_{index}");
                _owner.Chart.RemoveObject($"sel_end_{index}");
            }

            public void ClearAll()
            {
                // naive clear (remove many possible names)
                for (int i = 0; i < _owner.Bars.Count; i++)
                {
                    RemoveAt(i);
                }
            }

            private void DrawLine(string name, int index, Color color)
            {
                if (index < 0 || index >= _owner.Bars.Count) return;
                var time = _owner.Bars.OpenTimes[index];
                _owner.Chart.DrawVerticalLine(name, time, color, 1, LineStyle.Solid);
            }
        }

        // Helper local function
        private List<double> NewList(int n)
        {
            var l = new List<double>(n);
            for (int i = 0; i < n; i++) l.Add(double.NaN);
            return l;
        }

        // >>> ADD: simple fallback formulas if built-ins not created
        private double AwesomeOscillatorValue(int index)
        {
            // AO = SMA(Median,5) - SMA(Median,34)
            double SMA(int p)
            {
                int start = Math.Max(0, index - p + 1);
                int count = index - start + 1;
                if (count <= 0) return double.NaN;
                double sum = 0.0;
                for (int i = start; i <= index; i++)
                {
                    double mid = (Bars.HighPrices[i] + Bars.LowPrices[i]) * 0.5;
                    sum += mid;
                }
                return sum / count;
            }
            return SMA(5) - SMA(34);
        }

        private double MoneyFlowIndexValue(int index, int period)
        {
            if (index == 0) return double.NaN;
            int start = Math.Max(1, index - period + 1);
            double pos = 0.0, neg = 0.0;
            for (int i = start; i <= index; i++)
            {
                double tp = (Bars.HighPrices[i] + Bars.LowPrices[i] + Bars.ClosePrices[i]) / 3.0;
                double prev = (Bars.HighPrices[i - 1] + Bars.LowPrices[i - 1] + Bars.ClosePrices[i - 1]) / 3.0;
                double mf = tp * Bars.TickVolumes[i];
                if (tp > prev) pos += mf; else if (tp < prev) neg += mf;
            }
            if (neg <= 1e-12) return 100.0;
            double mfr = pos / neg;
            return 100.0 - 100.0 / (1.0 + mfr);
        }

        private double WilliamsRValue(int index, int period)
        {
            int start = Math.Max(0, index - period + 1);
            double hh = double.NegativeInfinity, ll = double.PositiveInfinity;
            for (int i = start; i <= index; i++) { if (Bars.HighPrices[i] > hh) hh = Bars.HighPrices[i]; if (Bars.LowPrices[i] < ll) ll = Bars.LowPrices[i]; }
            return (hh - ll) > 1e-12 ? -100.0 * (hh - Bars.ClosePrices[index]) / (hh - ll) : 0.0;
        }

        private double RocValue(int index, int period)
        {
            if (index < period) return double.NaN;
            double prev = Bars.ClosePrices[index - period];
            return Math.Abs(prev) > 1e-12 ? (Bars.ClosePrices[index] - prev) / Math.Abs(prev) : 0.0;
        }

        private double EfiValue(int index, int period)
        {
            if (index == 0) return 0.0;
            return (Bars.ClosePrices[index] - Bars.ClosePrices[index - 1]) * Bars.TickVolumes[index];
        }
    }
}


