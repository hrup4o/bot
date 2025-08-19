# Project Index and Module Map (TCN ML - Spikes)

Last updated: 2025-08-16T00:00:00Z

## Directory Overview
- MarketSignals.Core/
  - Class1.cs
  - Features/
    - IFeatureEngine.cs
    - IndicatorEngine.cs
    - HeikenAshiMetricsEngine.cs
    - SpikeHeuristicsEngine.cs
    - FeatureCollector.cs
    - FeatureRecord.cs
  - TimeSeries/
    - RollingWindowStats.cs
    - RollingExtrema.cs
- Logging/
  - ChartSelectionLogger.cs
- MarketSignals.Tests/
  - IndicatorEngineTests.cs
  - HeikenAshiMetricsEngineTests.cs
  - SpikeHeuristicsEngineTests.cs
  - UnitTest1.cs

## Core Data Types (MarketSignals.Core)

### `OhlcvBar` (record struct)
- File: `MarketSignals.Core/Class1.cs`
- Fields: `Timestamp`, `Symbol`, `Open`, `High`, `Low`, `Close`, `Volume`
- Purpose: Canonical OHLCV bar for all engines.

### `HeikinAshiBar` (record struct) and `HeikinAshiCalculator`
- File: `MarketSignals.Core/Class1.cs`
- `HeikinAshiCalculator.Update(OhlcvBar)` → `HeikinAshiBar`
- HA formulae: `haClose=(O+H+L+C)/4`, `haOpen=avg(prevHaOpen, prevHaClose)` (seed with avg(O,C)), `haHigh=max(H,haOpen,haClose)`, `haLow=min(L,haOpen,haClose)`

## Time-Series Utilities (MarketSignals.Core/TimeSeries)

### `RollingWindowStats`
- Mean, variance, stddev over a fixed-size rolling window.
- Methods: `Add(x)`, `Reset()`, properties `Mean`, `Variance`, `StdDev`, helper `ZScore(x)`.

### `RollingWindowMax` / `RollingWindowMin`
- O(n) monotonic deque windows for rolling maxima/minima.
- Methods: `Add(value)`, `Reset()`, properties `Max`/`Min`.

## Feature Engines (MarketSignals.Core/Features)

Common interface for engines:

### `IFeatureEngine`
- Properties: `Name`, `WarmupBars`, `IsWarm`
- Methods: `Reset()`, `Update(in OhlcvBar bar, in HeikinAshiBar haBar)`, `GetFeatures()` → `IReadOnlyDictionary<string,double>`

### `IndicatorEngine`
- File: `MarketSignals.Core/Features/IndicatorEngine.cs`
- Purpose: Compute core indicator features from price only (no external platform dependency).
- Constructor parameters:
  - `rsiPeriod` (default 14), `atrPeriod` (14)
  - `emaFastPeriod` (12), `emaSlowPeriod` (26), `emaSignalPeriod` (9) for MACD
  - `bbPeriod` (20), `bbK` (2.0) for Bollinger Bands
- Warmup: `WarmupBars = max(rsi, atr, bb, emaSlow+emaSignal)`
- Update flow:
  - ATR (Wilder RMA) with TR using prev close
  - RSI (Wilder) with seeded averages
  - MACD (EMA fast/slow), Signal (EMA on MACD), Histogram
  - Bollinger mid/std/upper/lower/width and `%B`
- Feature keys (examples; exact include parameterization in names):
  - `ind.rsi{period}` → e.g., `ind.rsi14`
  - `ind.atr{period}` → e.g., `ind.atr14`
  - `ind.macd`, `ind.macd_signal`, `ind.macd_hist`
  - `ind.bb{period}_k{K}.mid`, `.upper`, `.lower`, `.width`, `.pctb` (e.g., `ind.bb20_k2.mid`)
- API: `GetFeatures()` returns the latest feature map; `IsWarm` indicates readiness.

### `HeikenAshiMetricsEngine`
- File: `MarketSignals.Core/Features/HeikenAshiMetricsEngine.cs`
- Purpose: Compute HA-derived candle geometry and simple statistics.
- Constructor params: `warmupBars=50`, `dojiBodyRatioThreshold=0.1`, `wideRangeZThreshold=1.5`
- Update flow per HA bar:
  - Geometry: body, range, upper/lower wick; ratios to range
  - Z-Score of range using rolling stats (computed past-only before enqueue)
  - Pressure proxies: buy/sell/net from body sign/magnitude
  - Flags: `is_bullish`, `is_doji` (via `dojiBodyRatioThreshold`), `is_wide_range` (via z-score threshold)
- Feature keys:
  - `ha.body`, `ha.range`, `ha.upper_wick`, `ha.lower_wick`
  - `ha.body_ratio`, `ha.upper_wick_ratio`, `ha.lower_wick_ratio`
  - `ha.zscore_range`
  - `ha.buy_pressure`, `ha.sell_pressure`, `ha.net_pressure`
  - `ha.is_bullish`, `ha.is_doji`, `ha.is_wide_range`

### `SpikeHeuristicsEngine`
- File: `MarketSignals.Core/Features/SpikeHeuristicsEngine.cs`
- Purpose: Heuristic spike-related features combining ATR, HA range z-scores, gaps, and breakouts.
- Constructor params: `atrPeriod=14`, `lookbackBreakout=20`, `spikeZThreshold=2.0`
- Update flow:
  - ATR (Wilder) from real OHLC and prev close
  - HA range and its rolling z-score
  - Gaps: `gap_close_to_open` (HA open − prev real close)
  - Rolling highest high/lowest low (real OHLC) for breakout flags
  - ATR-normalized ratios and wick asymmetry
  - Heuristic flags for spike by Z and ATR
- Feature keys:
  - `spike.ha_range`, `spike.zscore_range`
  - `spike.range_over_atr`, `spike.gap_abs`, `spike.gap_over_atr`
  - `spike.breakout_h{N}`, `spike.breakout_l{N}` (N = `lookbackBreakout`)
  - `spike.wick_asym`
  - `spike.is_spike_z`, `spike.is_spike_atr`

### Feature composition helpers
- `FeatureCollector.Collect(timestamp, symbol, version, params IReadOnlyDictionary<string,double>[] parts)` merges multiple engine outputs into a `FeatureRecord` (last write wins). Enforce namespacing in keys to avoid collisions.
- `FeatureRecord`: DTO with `Timestamp`, `Symbol`, `FeatureSetVersion`, and `Values` map.

## Logging and Export (Logging/ChartSelectionLogger.cs)

### `ISelectionRenderer`
- Methods: `DrawStart(index, timestampUtc)`, `DrawEnd(index, timestampUtc)`, `RemoveAt(index)`, `ClearAll()`
- UI-agnostic; optional.

### `ChartSelectionLogger`
- Purpose: Manage chart range selections (mouse clicks by index) and export combined features to CSV with companion JSON schema.
- Configuration:
  - `SetRenderer(ISelectionRenderer)` (optional visual markers)
  - `SetFeatureBuilder(FeatureBuilder builder)` to add custom per-bar features
  - `RegisterFeatureProvider(string group, Func<int, IDictionary<string,float>> provider)` for additional feature sources
  - `SetOhlcvBarAccessor(Func<int,OhlcvBar>)` to access bars by index
  - CSV/Schema: `SetHeaderOrder(IEnumerable<string>)`, `SetCultureInvariant(bool)`, `SetMissingValuePolicy(Zero|NaN)`, `SetSchemaExport(bool)`
  - File metadata: `SetMetadata(symbol, timeframe)`, `SetFileNameTemplate(template)`; placeholders `{symbol}`, `{timeframe}`, `{start}`, `{end}`, `{date}`
- Actions:
  - `OnMouseDown(barIndex, timestampUtc)`: first click marks start; second click marks end and triggers `LogRange`
  - `ResetSelection()` clears markers and pending start
  - `LogRange(startIndex, endIndex, fileTag?)` builds rows by aggregating configured providers and writes CSV; emits `.schema.json` next to CSV on first write
- Header/schema:
  - If `SetHeaderOrder(...)` is not specified, header columns are sorted lexicographically from first row keys
  - Schema includes: file name, creation time (UTC), symbol/timeframe, culture/missing policy, file template, flags for which engine integrations are active, names of extra providers, and ordered columns
- Integration patterns in current repo:
  - Engines in `MarketSignals.Core/Features` implement `IFeatureEngine`; integrate via `RegisterFeatureProvider` like: `logger.RegisterFeatureProvider("ind", i => ToFloatMap(indicatorEngine.GetFeatures()))`
  - The file also contains placeholders for integrations with non-existent types (e.g., `IndicatorSeriesBundle`, `OhlcvMetricsEngine`, `OptimalTCNSpikeEngine`). Use `RegisterFeatureProvider` for current engines, or adapt types when those modules are added.

## Tests (MarketSignals.Tests)

### `IndicatorEngineTests`
- Validates presence and basic ranges for RSI/ATR/MACD/Bollinger features and `IsWarm` behavior.

### `HeikenAshiMetricsEngineTests`
- Validates HA metrics presence and z-score/flags behavior against generated data.

### `SpikeHeuristicsEngineTests`
- Validates spike features, including ATR, z-score, gaps, breakout flags.

### `UnitTest1`
- Additional basic sanity checks.

## Data Flow and Real-time Integration

- Feed real-time or historical bars as `OhlcvBar`.
- Derive `HeikinAshiBar` using `HeikinAshiCalculator.Update(bar)`.
- For each bar, update feature engines:
  - `indicatorEngine.Update(bar, haBar)`
  - `haMetricsEngine.Update(bar, haBar)`
  - `spikeHeuristicsEngine.Update(bar, haBar)`
- Merge features with `FeatureCollector` or via `ChartSelectionLogger` providers.
- `ChartSelectionLogger` writes CSV per selected range; schema JSON ensures reproducibility.

## Key Metric Groups (Summary)

- Indicator (price-only): RSI (Wilder), ATR (Wilder RMA), MACD (EMA fast/slow, signal, histogram), Bollinger (mid/std/upper/lower/width/%B)
- Heiken-Ashi geometry: body/range/wicks, ratios, HA range z-score, pressure proxies, simple flags
- Spike heuristics: HA range, range z-score, gaps, ATR-normalized magnitudes, HH/LL breakout flags, wick asymmetry, spike flags

## Module Interactions

- Engines implement `IFeatureEngine` and are stateless DTO producers with internal rolling state; they do not write files.
- `ChartSelectionLogger` orchestrates feature collection and export; it is UI-agnostic and file-system focused.
- Tests exercise engines independently; there is no platform adapter in this repository.

## Known Gaps / Next Steps

- cTrader adapter/robot is not present. To integrate live indicators from a platform, create an adapter that:
  - Builds `OhlcvBar` from platform bars, updates `HeikinAshiCalculator`, then calls `Update` on engines
  - Bridges platform indicator series if needed, or rely on current self-contained `IndicatorEngine`
  - Wires `ChartSelectionLogger` with `RegisterFeatureProvider` and a stable header list when schema stabilizes
- The logger contains integration stubs for modules not yet in this repository (`IndicatorSeriesBundle`, `OhlcvMetricsEngine`, `OptimalTCNSpikeEngine`, `HybridRawFeatureEngine`). Replace or implement these as new modules or adapt to the current `IFeatureEngine` pattern.
- If the project must enforce “raw-only” features (no z-scores/flags/thresholds), refactor `HeikenAshiMetricsEngine` and `SpikeHeuristicsEngine` to remove boolean flags and z-score outputs, or provide parallel raw-only variants.

## Solution and Targets
- Solution: `MarketSignals.sln`
- Projects:
  - `MarketSignals.Core` → TargetFramework: `net6.0`
  - `MarketSignals.Tests` → TargetFramework: `net6.0`; references `MarketSignals.Core`; packages: xUnit, coverlet, Microsoft.NET.Test.Sdk