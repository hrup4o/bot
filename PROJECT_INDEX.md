# Project Index and Module Map (TCN ML - Spikes)

Last updated: {UTC}

## Directory and Files
- Indicators/IndicatorEngine.cs
- Logging/ChartSelectionLogger.cs
- MarketSignals.Core/RawData/OhlcvBar.cs
- MarketSignals.Core/RawData/InMemoryBarFeed.cs
- MarketSignals.Core/Metrics/OhlcvMetricsEngine.cs
- MarketSignals.Core/Metrics/HeikenAshiMetricsEngine.cs

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

## Data Flow Summary
- Broker feed → `OhlcvBar` →
  - `OhlcvMetricsEngine.ComputeNext` → OHLC raw metrics
  - `HeikenAshiMetricsEngine.ComputeNext` → HA raw metrics
  - `IndicatorEngine.ComputeSnapshot` (given cTrader indicator series) → indicator metrics
- `ChartSelectionLogger.LogRange` writes combined rows (indicator + OHLC + HA) to CSV (+ `.schema.json`).
- Preprocessing (separate step): time split; compute normalization params on train; apply to val/test; optionally derive boolean flags (spike/impulse/divergence) there.