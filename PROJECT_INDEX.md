# Project Index and Module Map (TCN ML - Spikes)

Last updated: {UTC}

## Directory and Files
- Indicators/IndicatorEngine.cs
- Logging/ChartSelectionLogger.cs
- MarketSignals.Core/RawData/OhlcvBar.cs
- MarketSignals.Core/RawData/InMemoryBarFeed.cs
- MarketSignals.Core/Metrics/OhlcvMetricsEngine.cs
- MarketSignals.Core/Metrics/HeikenAshiMetricsEngine.cs
- MarketSignals.Core/Hybrid/HybridRawFeatureEngine.cs
- MarketSignals.Core/Metrics/OptimalTCNSpikeEngine.cs

## Indicators/IndicatorEngine.cs
- Namespace: `Indicators`
- Modules / Classes:
  - `ISeries`: minimal time series interface (Count, indexer)
  - `ArraySeries`: IList<double> wrapper → `ISeries`
  - `IndicatorSeriesBundle`: external cTrader indicator series
    - RSI, MACD, MACDSignal, MACDHistogram
    - AO, MFI, WilliamsAD, VHF, VIDYA, CMO, ROC
    - BB_Middle, BB_Upper, BB_Lower, BB_Bandwidth
    - ATR, VolumeOsc, ElderForceIndex
    - VWAP, OBV, PVT
    - SuperTrendValue, SuperTrendDirection (-1/+1)
    - KVO, KVOSignal, KVOHistogram
    - WilliamsR, PriceROC
  - `IndicatorParams`: periods for derivatives/local ranges (indicator-native periods)
  - `Derivative`: Delta, Slope, AngleDeg, Acceleration
  - `IndicatorSnapshot`: all indicator values, derivatives, crosses (-1/0/+1), spreads, local ranges
  - `IndicatorEngine`:
    - `ComputeSnapshot(index, series, params)` → `IndicatorSnapshot`
    - `ComputeCore(index, series)` → current indicator values only
    - `ComputeDerivatives(getter, index, period)` → `Derivative`
    - `Cross(aGetter, bGetter, index)` → -1/0/+1
    - Special crosses: `MACD_Cross_Signal`, `RSI_Cross_MACD`, `KVO_Cross_Signal`
- Logic (raw, no normalization/Z/thresholds):
  - Derivatives per-indicator period: slope/angle/acceleration for RSI, MACD/Signal/Histogram, AO, CMO, ROC, BB_Bandwidth, WilliamsR, PriceROC
  - Crosses: MACD↔Signal, RSI↔MACD, KVO↔Signal, ROC↔PriceROC, WilliamsR↔RSI, VolumeOsc↔EFI, AO↔CMO/ROC, VIDYA↔VHF, BBW↔ATR
  - Spreads: MACD−Signal (signed/abs), RSI−{MACD,Signal,Histogram}, KVO−Signal, RSI−{WilliamsR,PriceROC}, MACD−PriceROC, CMO−ROC, VHF−VIDYA, VolumeOsc−EFI, ROC−PriceROC
  - Local ranges: max−min over the indicator period
- Integration:
  - `Logging.ChartSelectionLogger` can auto-include all metrics via `SetIndicatorEngine(...)`

## Logging/ChartSelectionLogger.cs
- Namespace: `Logging`
- Modules / Classes:
  - `ISelectionRenderer`: DrawStart, DrawEnd, RemoveAt, ClearAll (optional UI hooks)
  - `ChartSelectionLogger`:
    - Config:
      - `SetRenderer(ISelectionRenderer)` (optional)
      - `SetFeatureBuilder(FeatureBuilder)` (custom per-bar features)
      - `SetIndicatorEngine(IndicatorSeriesBundle, IndicatorParams)` (auto-inject all indicator metrics)
      - `SetMetadata(symbol, timeframe)`, `SetFileNameTemplate(...)`
      - `SetCultureInvariant(bool)` (CSV formatting)
      - `SetMissingValuePolicy(Zero|NaN)` (NaN recommended for ML)
      - `SetSchemaExport(bool)` (writes `.schema.json` next to CSV)
      - `SetHeaderOrder(IEnumerable<string>)` (stable header order)
    - Actions:
      - `OnMouseDown(barIndex, timestampUtc)` → start/end markers → `LogRange`
      - `ResetSelection()`
      - `LogRange(startIndex, endIndex, fileTag)` → CSV + `.schema.json` per zone
    - Internals:
      - Reflects all `IndicatorSnapshot` fields (incl. `Derivative` flatten) via `BuildIndicatorFeatures`
      - Fixed header per file; invariant culture; schema export with columns + metadata
- Integration:
  - Combines: indicator metrics (auto) + OHLC/HA metrics (via FeatureBuilder) into one CSV row per bar
  - New CSV per marked zone (templated name)

## MarketSignals.Core/RawData/OhlcvBar.cs
- Namespace: `MarketSignals.Core.RawData`
- Class: `OhlcvBar` (IEquatable)
  - Fields: `Symbol` (Trim+UpperInvariant), `Timeframe`, `TimestampUtc` (UTC), `Open/High/Low/Close` (>=0, OHLC relations enforced), `TickVolume` (>=0)
  - Metadata: `Source?`, `IsFinal`, `IsSanitized`
  - Ctors:
    - Strict validation ctor
    - Extended (with metadata)
    - `CreateSanitized(..., out wasSanitized)` → clamps Open/Close to [Low, High]
  - Equality/HashCode by (Symbol, Timeframe, TimestampUtc); `TimestampComparer`
- Integration:
  - Input for `OhlcvMetricsEngine` and `HeikenAshiMetricsEngine`
  - Used by `InMemoryBarFeed`

## MarketSignals.Core/RawData/InMemoryBarFeed.cs
- Namespace: `MarketSignals.Core.RawData`
- Interfaces / Classes:
  - `IBarFeed`: `GetHistory(...)`, `Subscribe(...)` (dummy)
  - `InMemoryBarFeed`:
    - `AddSeries(bars)`: global sort (TimestampUtc) + dedup (keep last)
    - `GetHistory(...)`: time filter; ascending order
    - `Subscribe(...)`: stub for tests/offline

## MarketSignals.Core/Metrics/OhlcvMetricsEngine.cs
- Namespace: `MarketSignals.Core.Metrics`
- Classes:
  - `OhlcvMetricsConfig`:
    - Windows: `RangeMaShort/Long`, `StatsWindow` (raw mean/std/dev), `OlsWindowShort/Long`, `VolumeMaWindow`, `PrevNForBreak`, `SwingLookback`
    - Params: `BbStdMult`, `KcMult`, `PressureEmaAlpha`
    - `Validate()`
  - `OhlcvMetrics` (raw, ML-ready; no Z/percentile/flags):
    - Geometry: `Range`, `Body`, `UpperWick`, `LowerWick`, `BodyToRange`, `WickAsymmetry`, `ClosePosInRange`
    - Direction & Gaps: `Direction` (-1/0/+1), `Gap`, `GapAbs`, `GapPct`
    - Volatilities: `ParkinsonRv`, `GarmanKlassRv`, `RogersSatchellRv`
    - Rolling stats: `RangeMaShort/Long`, `RangeMeanW/StdW/DevFromMeanW`, `ParkMeanW/StdW/DevFromMeanW`
    - Derivatives (Close): `OlsSlopeShort/Long`, `OlsSlopeSEShort/MSEShort`, `LinearSlopeShort/Long`, `OlsAngleShort/Long`, `LinearAngleShort/Long`, `AccelerationShort`
    - Volume/Pressure: `Clv`, `ClvEmaShort`, `VolumeMaShort`, `VolumeRatio`, `PressureIndex`, `BodyPressure`, `WickPressure`
    - Patterns/Structure scores: `EngulfingScore`, `FvgUp/Down`, `SweepUp/Down`
    - Distances/Time: `DistToPrevHighN/LowN`, `DistToSwingHigh/Low`, `BarsSinceSwingHigh/Low`
    - Compression: `BbWidthShort`, `KcWidthShortProxy`, `SqueezeProxy`, `CompressionProxy`
  - `OhlcvMetricsEngine`:
    - `Reset()`, `ComputeNext(OhlcvBar)` → `OhlcvMetrics`
    - Helpers: `PastOnlyMeanStd` (no leakage), `LinearSlope`, `OlsSlopeSeMse`, `IndexFromEndOf`, `EmaNext`, enqueue helpers
- Integration:
  - Feed with `OhlcvBar`; flatten metrics in FeatureBuilder → `ChartSelectionLogger`

## MarketSignals.Core/Metrics/HeikenAshiMetricsEngine.cs
- Namespace: `MarketSignals.Core.Metrics`
- Classes:
  - `HeikenAshiMetricsConfig`: analogous windows/params for HA
  - `HeikenAshiMetrics` (raw; `Ha...` prefix):
    - HA OHLC; geometry; scores (Marubozu/Doji/Pin); Direction & Gaps
    - RV: `HaParkinsonRv`, `HaGarmanKlassRv`, `HaRogersSatchellRv`
    - Rolling stats: `HaRangeMaShort/Long`, `HaRangeMeanW/StdW/DevFromMeanW`, `HaParkMeanW/StdW/DevFromMeanW`
    - Derivatives (HaClose): `HaOlsSlopeShort/Long`, `HaLinearSlopeShort/Long`, `HaOlsAngleShort/Long`, `HaLinearAngleShort/Long`, `HaAccelerationShort`, `HaCurvature`
    - Trend proxy: `HaColorRunLen`, `HaTrendStrength`
    - Volume/Pressure: `HaClv`, `HaClvEmaShort`, `HaVolumeMaShort`, `HaVolumeRatio`, `HaPressureIndex`, `HaBodyPressure`, `HaWickPressure`
    - Patterns/Structure scores: `HaEngulfingScore`, `HaFvgUp/Down` (uses internal `_prevHaHigh/_prevHaLow`), `HaSweepUp/Down`
    - Distances/Time: `HaDistToPrevHighN/LowN`, `HaDistToSwingHigh/Low`, `HaBarsSinceSwingHigh/Low`
    - Compression: `HaBbWidthShort`, `HaKcWidthShortProxy`, `HaSqueezeProxy`, `HaCompressionProxy`
  - `HeikenAshiMetricsEngine`:
    - `Reset()`, `ComputeNext(OhlcvBar)` → `HeikenAshiMetrics`
    - FVG uses internal state `_prevHaHigh/_prevHaLow`
    - Helpers identical to OHLC engine (past-only stats, derivatives, EMA)
- Integration:
  - Feed with `OhlcvBar`; flatten metrics in FeatureBuilder → `ChartSelectionLogger`

## MarketSignals.Core/Hybrid/HybridRawFeatureEngine.cs
- Namespace: `MarketSignals.Core.Hybrid`
- Purpose: Additional raw features not covered by other engines; no thresholds/normalization.
- Config:
  - `VelocityWindows`, `AccelerationWindows`, `RatioWindows`, `StdWindows`, `MomentWindows`, `CorrWindows`
  - `ER_Windows`, `RS_Windows`, `RV_Windows`, `VWAP_Windows`, `Drawdown_Windows`, `Entropy_Windows`
  - `SpectralWindowsN`, `FD_Windows`, `HiguchiKMax`
- Public API / Classes:
  - `HybridRawFeatureConfig` (above)
  - `HybridRawFeatureEngine`
    - `Reset()`
    - `ComputeNext(OhlcvBar bar, OhlcvMetrics? ohlc = null, HeikenAshiMetrics? ha = null)` → flat feature map/DTO
- Feature groups (raw; examples of keys):
  - Price dynamics: velocity ΔClose_w, acceleration Δ(velocity)_w
  - Ratios: `Range/MA_Range_w`, `Volume/MA_Volume_w`
  - Statistics: `Std(Close)_w`, `Skew(Close)_w`, `Kurt(Close)_w`
  - Correlations: `Corr(ΔClose, Volume)_w`, lagged corr (optional), ACF1/2/3, PACF1
  - Robust: `Median_w(Close)`, `MAD_w(Close)`, `DevFromMedian_w`
  - Spectral: `SpecLHRatio_N` for N ∈ {16,32}
  - Slope stability: `Std(LinearSlope over 4 subwindows)`
  - Liquidity: `Range/Volume`, `Body/Volume`, and their windowed ratios vs avg
  - Realized family: `RV_w`, `BPV_w`, `Quarticity_w`, `RealizedRange_w`
  - VWAP: `VWAP_w`, `DevVWAP_w`
  - Extremes: `MaxDrawdown_w`, `MaxRunup_w`
  - Entropy: Shannon entropy of sign(returns)_w
  - Fractal dimension: `FD_Higuchi_w`, `FD_Katz_w` (short windows)
- Helpers (typical): `HiguchiFD`, `KatzFD`, `ComputeRV/BPV/Quarticity`, `ComputeVWAP`, `ComputeSpectralLHRatio`, `ComputeSlopeStability`

## MarketSignals.Core/Metrics/OptimalTCNSpikeEngine.cs
- Namespace: `MarketSignals.Core.Metrics`
- Purpose: Spike-focused raw feature set for TCN; zero interpretation.
- Config:
  - Windows: `VelocityWindows`, `RatioWindows`, `StatisticalWindows`, `MomentumWindows`, `CorrelationWindows`
  - Spike windows: `ER_Windows`, `RS_Windows`, `RV_Windows`, `VWAP_Windows`, `Drawdown_Windows`, `Entropy_Windows`, `FD_Windows`, `SpectralWindowsN`
- Public API / Classes:
  - `OptimalTCNConfig` (above)
  - `OptimalTCNFeatures` (flat DTO; properties by tiers below)
  - `OptimalTCNSpikeEngine`
    - `Reset()`
    - `ComputeNext(OhlcvBar bar)` → `OptimalTCNFeatures`
- Features by tier (raw):
  - TIER 1–2 (OHLCV, geometry): `Open/High/Low/Close/Volume`, `Range/Body/UpperWick/LowerWick/BodyRatio/UpperWickRatio/LowerWickRatio/WickImbalance`
  - TIER 3–4 (dynamics): `PriceVelocity{1,3,5,10,20}`, `PriceAcceleration{3,5,10}`
  - TIER 5 (ratios): `RangeRatio{3,5,10,20,50}`, `VolumeRatio{3,5,10,20,50}`
  - TIER 6–7 (moments): `PriceStdDev{10,20,50}`, `VolumeStdDev{10,20,50}`, `RangeStdDev{10,20,50}`, `PriceSkewness{10,20}`, `PriceKurtosis{10,20}`, `VolumeSkewness{10,20}`
  - TIER 8 (cross-corr): `PriceVolumeCorr{5,10,20}`, `RangeVolumeCorr{5,10,20}`
  - TIER 9–10 (momentum): `PriceMomentum{3,5,10,20}`, `VolumeMomentum{3,5,10,20}`, `MomentumDivergence{3,5,10}`
  - TIER 11–12 (structure/SMA): distances to highs/lows `{20,50}` (abs/%), `DistanceToSMA{5,10,20,50}` (abs/%)
  - TIER 13 (runs): `ConsecutiveUp/DownBars`, `ConsecutiveUp/DownBodies`, `ConsecutiveHigh/LowVolume`, `ConsecutiveWide/TightRanges`
  - TIER 14 (time): `HourSin/Cos`, `DayOfWeekSin/Cos`, `MinuteSin/Cos`
  - TIER 15 (order-flow proxies): `VolumePerPoint`, `VolumePerRange`, `PriceEfficiency`, `BuyPressureProxy`, `SellPressureProxy`
  - TIER 16 (volatility): `VolatilityRatio{5,10,20,50}` (past-only), `VolatilityMomentum{5,10,20}`
  - TIER 17 (fractal): `FractalDim{32,64}`
  - TIER 18 (realized/VWAP/etc.): `ER{10,20,50}`, `RS{20,50,100}`, `RV{10,20,50}`, `BPV{10,20,50}`, `RQ{10,20,50}`, `RealRange{10,20,50}`, `VWAP{10,20,50}`, `DevVWAP{10,20,50}`
  - TIER 19 (ACF/robust/spectral/sign): `ACF{1,2,3}`, `PACF1`, `SignImbalance{20,50,100}`, `PosShare{20,50,100}`, `Median{10,20,50}`, `MAD{10,20,50}`, `DevMedian{10,20,50}`, `SpecLHRatio{16,32}`
  - TIER 20 (stability/liquidity/DD/RU/entropy): `SlopeStabilityShort`, `RangePerVolume`, `BodyPerVolume`, `RangePerVolumeRatio{20,50}`, `BodyPerVolumeRatio{20,50}`, `MaxDrawdown{20,50,100}`, `MaxRunup{20,50,100}`, `EntropySign{20,50,100}`
- Helpers (selected): `ComputeER/RS/RV/BPV/RQ/RealizedRange/VWAP/ACF/SignImbalance/Robust/SpectralLHRatio/SlopeStability/RatioSeries/MaxDD/MaxRU/SignEntropy`
- Notes:
  - Past-only where applicable; no thresholds/normalization; .NET 6

## Data Flow Summary
- Broker feed → `OhlcvBar` →
  - `OhlcvMetricsEngine.ComputeNext` → OHLC raw metrics
  - `HeikenAshiMetricsEngine.ComputeNext` → HA raw metrics
  - `IndicatorEngine.ComputeSnapshot` (given cTrader indicator series) → indicator metrics
  - `HybridRawFeatureEngine.ComputeNext` → additional raw features
  - `OptimalTCNSpikeEngine.ComputeNext` → spike-focused raw features
- `ChartSelectionLogger.LogRange` writes combined rows (indicator + OHLC + HA + hybrid/spike) to CSV (+ `.schema.json`).
- Preprocessing (separate step): time split; compute normalization params on train; apply to val/test; optional boolean feature derivation (spike/impulse/divergence) post-train.