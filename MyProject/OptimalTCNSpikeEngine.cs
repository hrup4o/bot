using System;
using System.Collections.Generic;
using System.Linq;
using MarketSignals.Core.RawData;

namespace MarketSignals.Core.Metrics
{
    /// <summary>
    /// Configuration for optimal TCN features - maximum information density
    /// </summary>
    public sealed class OptimalTCNConfig
    {
        // Multi-scale windows for comprehensive temporal analysis
        public int[] VelocityWindows { get; init; } = { 1, 3, 5, 10, 20 };
        public int[] RatioWindows { get; init; } = { 3, 5, 10, 20, 50 };
        public int[] StatisticalWindows { get; init; } = { 10, 20, 50 };
        public int[] MomentumWindows { get; init; } = { 3, 5, 10, 20 };
        public int[] CorrelationWindows { get; init; } = { 5, 10, 20 };

        // Buffer sizes
        public int MaxBufferSize { get; init; } = 200;
        public int StructuralLookback { get; init; } = 100;

        // Feature density control
        public bool IncludeCrossCorrelations { get; init; } = true;
        public bool IncludeHigherMoments { get; init; } = true;
        public bool IncludeMultiScaleDecomposition { get; init; } = true;

        // Spike-oriented raw windows
        public int[] ER_Windows { get; init; } = new[] { 10, 20, 50 };
        public int[] RS_Windows { get; init; } = new[] { 20, 50, 100 };
        public int[] RV_Windows { get; init; } = new[] { 10, 20, 50 };
        public int[] VWAP_Windows { get; init; } = new[] { 10, 20, 50 };
        public int[] Drawdown_Windows { get; init; } = new[] { 20, 50, 100 };
        public int[] Entropy_Windows { get; init; } = new[] { 20, 50, 100 };
        public int[] FD_Windows { get; init; } = new[] { 32, 64 };
        public int[] SpectralWindowsN { get; init; } = new[] { 16, 32 };

        public void Validate()
        {
            if (VelocityWindows?.Length == 0) throw new ArgumentException("VelocityWindows cannot be empty");
            if (RatioWindows?.Length == 0) throw new ArgumentException("RatioWindows cannot be empty");
            if (StatisticalWindows?.Length == 0) throw new ArgumentException("StatisticalWindows cannot be empty");
            if (VelocityWindows?.Any(w => w < 1) == true)
                throw new ArgumentException("All velocity windows must be >= 1");
            if (MaxBufferSize < (RatioWindows?.Max() ?? 50) * 2)
                throw new ArgumentException("Buffer size too small for ratio windows");
            if (ER_Windows == null || ER_Windows.Length == 0) throw new ArgumentException("ER_Windows cannot be empty");
            if (RS_Windows == null || RS_Windows.Length == 0) throw new ArgumentException("RS_Windows cannot be empty");
            if (RV_Windows == null || RV_Windows.Length == 0) throw new ArgumentException("RV_Windows cannot be empty");
            if (VWAP_Windows == null || VWAP_Windows.Length == 0) throw new ArgumentException("VWAP_Windows cannot be empty");
            if (Drawdown_Windows == null || Drawdown_Windows.Length == 0) throw new ArgumentException("Drawdown_Windows cannot be empty");
            if (Entropy_Windows == null || Entropy_Windows.Length == 0) throw new ArgumentException("Entropy_Windows cannot be empty");
            if (SpectralWindowsN == null || SpectralWindowsN.Length == 0) throw new ArgumentException("SpectralWindowsN cannot be empty");
        }
    }

    /// <summary>
    /// Optimal features for TCN spike detection - maximum information, zero interpretation
    /// </summary>
    public sealed class OptimalTCNFeatures
    {
        // === TIER 1: Raw OHLCV Data ===
        public double Open { get; init; }
        public double High { get; init; }
        public double Low { get; init; }
        public double Close { get; init; }
        public double Volume { get; init; }

        // === TIER 2: Basic Mathematical Transformations ===
        public double Range { get; init; }                    // H-L
        public double Body { get; init; }                     // |C-O|
        public double UpperWick { get; init; }               // H-max(O,C)
        public double LowerWick { get; init; }               // min(O,C)-L
        public double BodyRatio { get; init; }               // Body/Range
        public double UpperWickRatio { get; init; }          // UpperWick/Range
        public double LowerWickRatio { get; init; }          // LowerWick/Range
        public double WickImbalance { get; init; }           // (UpperWick-LowerWick)/Range

        // === TIER 3: Multi-Scale Price Velocities ===
        public double PriceVelocity1 { get; init; }         // 1-period velocity
        public double PriceVelocity3 { get; init; }         // 3-period velocity
        public double PriceVelocity5 { get; init; }         // 5-period velocity
        public double PriceVelocity10 { get; init; }        // 10-period velocity
        public double PriceVelocity20 { get; init; }        // 20-period velocity

        // === TIER 4: Multi-Scale Acceleration ===
        public double PriceAcceleration3 { get; init; }     // Change in 3-period velocity
        public double PriceAcceleration5 { get; init; }     // Change in 5-period velocity
        public double PriceAcceleration10 { get; init; }    // Change in 10-period velocity

        // === TIER 5: Multi-Scale Ratios (Current vs Historical) ===
        public double RangeRatio3 { get; init; }            // Current range vs 3-period avg
        public double RangeRatio5 { get; init; }            // Current range vs 5-period avg
        public double RangeRatio10 { get; init; }           // Current range vs 10-period avg
        public double RangeRatio20 { get; init; }           // Current range vs 20-period avg
        public double RangeRatio50 { get; init; }           // Current range vs 50-period avg

        public double VolumeRatio3 { get; init; }           // Current volume vs 3-period avg
        public double VolumeRatio5 { get; init; }           // Current volume vs 5-period avg
        public double VolumeRatio10 { get; init; }          // Current volume vs 10-period avg
        public double VolumeRatio20 { get; init; }          // Current volume vs 20-period avg
        public double VolumeRatio50 { get; init; }          // Current volume vs 50-period avg

        // === TIER 6: Statistical Moments (Raw Numbers Only) ===
        public double PriceStdDev10 { get; init; }          // 10-period price std dev
        public double PriceStdDev20 { get; init; }          // 20-period price std dev
        public double PriceStdDev50 { get; init; }          // 50-period price std dev

        public double VolumeStdDev10 { get; init; }         // 10-period volume std dev
        public double VolumeStdDev20 { get; init; }         // 20-period volume std dev
        public double VolumeStdDev50 { get; init; }         // 50-period volume std dev

        public double RangeStdDev10 { get; init; }          // 10-period range std dev
        public double RangeStdDev20 { get; init; }          // 20-period range std dev
        public double RangeStdDev50 { get; init; }          // 50-period range std dev

        // === TIER 7: Higher Statistical Moments ===
        public double PriceSkewness10 { get; init; }        // 10-period price skewness
        public double PriceSkewness20 { get; init; }        // 20-period price skewness
        public double PriceKurtosis10 { get; init; }        // 10-period price kurtosis
        public double PriceKurtosis20 { get; init; }        // 20-period price kurtosis

        public double VolumeSkewness10 { get; init; }       // 10-period volume skewness
        public double VolumeSkewness20 { get; init; }       // 20-period volume skewness

        // === TIER 8: Cross-Correlations ===
        public double PriceVolumeCorr5 { get; init; }       // 5-period price-volume correlation
        public double PriceVolumeCorr10 { get; init; }      // 10-period price-volume correlation
        public double PriceVolumeCorr20 { get; init; }      // 20-period price-volume correlation

        public double RangeVolumeCorr5 { get; init; }       // 5-period range-volume correlation
        public double RangeVolumeCorr10 { get; init; }      // 10-period range-volume correlation
        public double RangeVolumeCorr20 { get; init; }      // 20-period range-volume correlation

        // === TIER 9: Multi-Scale Momentum ===
        public double PriceMomentum3 { get; init; }         // 3-period price momentum
        public double PriceMomentum5 { get; init; }         // 5-period price momentum
        public double PriceMomentum10 { get; init; }        // 10-period price momentum
        public double PriceMomentum20 { get; init; }        // 20-period price momentum

        public double VolumeMomentum3 { get; init; }        // 3-period volume momentum
        public double VolumeMomentum5 { get; init; }        // 5-period volume momentum
        public double VolumeMomentum10 { get; init; }       // 10-period volume momentum
        public double VolumeMomentum20 { get; init; }       // 20-period volume momentum

        // === TIER 10: Momentum Divergence ===
        public double MomentumDivergence3 { get; init; }    // Price vs Volume momentum divergence (3p)
        public double MomentumDivergence5 { get; init; }    // Price vs Volume momentum divergence (5p)
        public double MomentumDivergence10 { get; init; }   // Price vs Volume momentum divergence (10p)

        // === TIER 11: Structural Relationships ===
        public double DistanceToHigh20 { get; init; }       // Distance to 20-period high (raw)
        public double DistanceToLow20 { get; init; }        // Distance to 20-period low (raw)
        public double DistanceToHigh50 { get; init; }       // Distance to 50-period high (raw)
        public double DistanceToLow50 { get; init; }        // Distance to 50-period low (raw)

        public double DistanceToHigh20Pct { get; init; }    // Distance to 20-period high (%)
        public double DistanceToLow20Pct { get; init; }     // Distance to 20-period low (%)
        public double DistanceToHigh50Pct { get; init; }    // Distance to 50-period high (%)
        public double DistanceToLow50Pct { get; init; }     // Distance to 50-period low (%)

        // === TIER 12: Simple Moving Average Relationships ===
        public double DistanceToSMA5 { get; init; }         // Raw distance to 5-period SMA
        public double DistanceToSMA10 { get; init; }        // Raw distance to 10-period SMA
        public double DistanceToSMA20 { get; init; }        // Raw distance to 20-period SMA
        public double DistanceToSMA50 { get; init; }        // Raw distance to 50-period SMA

        public double DistanceToSMA5Pct { get; init; }      // % distance to 5-period SMA
        public double DistanceToSMA10Pct { get; init; }     // % distance to 10-period SMA
        public double DistanceToSMA20Pct { get; init; }     // % distance to 20-period SMA
        public double DistanceToSMA50Pct { get; init; }     // % distance to 50-period SMA

        // === TIER 13: Consecutive Patterns (Counts Only) ===
        public int ConsecutiveUpBars { get; init; }         // Count of consecutive up closes
        public int ConsecutiveDownBars { get; init; }       // Count of consecutive down closes
        public int ConsecutiveUpBodies { get; init; }       // Count of consecutive up bodies
        public int ConsecutiveDownBodies { get; init; }     // Count of consecutive down bodies
        public int ConsecutiveHighVolume { get; init; }     // Count of consecutive high volume bars
        public int ConsecutiveLowVolume { get; init; }      // Count of consecutive low volume bars
        public int ConsecutiveWideRanges { get; init; }     // Count of consecutive wide range bars
        public int ConsecutiveTightRanges { get; init; }    // Count of consecutive tight range bars

        // === TIER 14: Time Context (Cyclical Encoding) ===
        public double HourSin { get; init; }                // sin(2π * hour / 24)
        public double HourCos { get; init; }                // cos(2π * hour / 24)
        public double DayOfWeekSin { get; init; }           // sin(2π * dayOfWeek / 7)
        public double DayOfWeekCos { get; init; }           // cos(2π * dayOfWeek / 7)
        public double MinuteSin { get; init; }              // sin(2π * minute / 60)
        public double MinuteCos { get; init; }              // cos(2π * minute / 60)

        // === TIER 15: Order Flow Proxies ===
        public double VolumePerPoint { get; init; }         // Volume / |Close - Open|
        public double VolumePerRange { get; init; }         // Volume / Range
        public double PriceEfficiency { get; init; }        // |Close - Open| / Range
        public double BuyPressureProxy { get; init; }       // Based on wick analysis
        public double SellPressureProxy { get; init; }      // Based on wick analysis

        // === TIER 16: Multi-Scale Volatility ===
        public double VolatilityRatio5 { get; init; }       // Current volatility vs 5-period avg
        public double VolatilityRatio10 { get; init; }      // Current volatility vs 10-period avg
        public double VolatilityRatio20 { get; init; }      // Current volatility vs 20-period avg
        public double VolatilityRatio50 { get; init; }      // Current volatility vs 50-period avg

        public double VolatilityMomentum5 { get; init; }    // 5-period volatility momentum
        public double VolatilityMomentum10 { get; init; }   // 10-period volatility momentum
        public double VolatilityMomentum20 { get; init; }   // 20-period volatility momentum

        // === TIER 17: Fractal only (unique) ===
        public double FractalDim32 { get; init; }           // Fractal dimension proxy (32-period)
        public double FractalDim64 { get; init; }           // Fractal dimension proxy (64-period)

        // === TIER 18: Additional Spike Raw Features ===
        public double ER10 { get; init; }
        public double ER20 { get; init; }
        public double ER50 { get; init; }
        public double RS20 { get; init; }
        public double RS50 { get; init; }
        public double RS100 { get; init; }
        public double RV10 { get; init; }
        public double RV20 { get; init; }
        public double RV50 { get; init; }
        public double BPV10 { get; init; }
        public double BPV20 { get; init; }
        public double BPV50 { get; init; }
        public double RQ10 { get; init; }
        public double RQ20 { get; init; }
        public double RQ50 { get; init; }
        public double RealRange10 { get; init; }
        public double RealRange20 { get; init; }
        public double RealRange50 { get; init; }
        public double VWAP10 { get; init; }
        public double VWAP20 { get; init; }
        public double VWAP50 { get; init; }
        public double DevVWAP10 { get; init; }
        public double DevVWAP20 { get; init; }
        public double DevVWAP50 { get; init; }

        // === TIER 19: ACF/PACF and Sign Features ===
        public double ACF1 { get; init; }
        public double ACF2 { get; init; }
        public double ACF3 { get; init; }
        public double PACF1 { get; init; }
        public double SignImbalance20 { get; init; }
        public double SignImbalance50 { get; init; }
        public double SignImbalance100 { get; init; }
        public double PosShare20 { get; init; }
        public double PosShare50 { get; init; }
        public double PosShare100 { get; init; }
        public double Median10 { get; init; }
        public double Median20 { get; init; }
        public double Median50 { get; init; }
        public double MAD10 { get; init; }
        public double MAD20 { get; init; }
        public double MAD50 { get; init; }
        public double DevMedian10 { get; init; }
        public double DevMedian20 { get; init; }
        public double DevMedian50 { get; init; }
        public double SpecLHRatio16 { get; init; }
        public double SpecLHRatio32 { get; init; }

        // === TIER 20: Liquidity and Drawdown Features ===
        public double SlopeStabilityShort { get; init; }
        public double RangePerVolume { get; init; }
        public double BodyPerVolume { get; init; }
        public double RangePerVolumeRatio20 { get; init; }
        public double RangePerVolumeRatio50 { get; init; }
        public double BodyPerVolumeRatio20 { get; init; }
        public double BodyPerVolumeRatio50 { get; init; }
        public double MaxDrawdown20 { get; init; }
        public double MaxDrawdown50 { get; init; }
        public double MaxDrawdown100 { get; init; }
        public double MaxRunup20 { get; init; }
        public double MaxRunup50 { get; init; }
        public double MaxRunup100 { get; init; }
        public double EntropySign20 { get; init; }
        public double EntropySign50 { get; init; }
        public double EntropySign100 { get; init; }

        // === Meta Information ===
        public bool IsReady { get; init; }                  // Are all features valid?
        public int BarsProcessed { get; init; }             // How many bars processed
    }

    /// <summary>
    /// Optimal TCN Spike Engine - Maximum information density, zero interpretation
    /// Pure mathematical measurements for ML discovery
    /// </summary>
    public sealed class OptimalTCNSpikeEngine
    {
        private readonly OptimalTCNConfig _config;
        private const double Eps = 1e-12;

        // Efficient circular buffers for all data
        private readonly CircularBuffer<double> _opens;
        private readonly CircularBuffer<double> _highs;
        private readonly CircularBuffer<double> _lows;
        private readonly CircularBuffer<double> _closes;
        private readonly CircularBuffer<double> _volumes;
        private readonly CircularBuffer<double> _ranges;
        private readonly CircularBuffer<double> _bodies;
        private readonly CircularBuffer<double> _velocities;

        private int _barsProcessed = 0;

        public OptimalTCNSpikeEngine(OptimalTCNConfig? config = null)
        {
            _config = config ?? new OptimalTCNConfig();
            _config.Validate();

            int bufferSize = _config.MaxBufferSize;
            _opens = new CircularBuffer<double>(bufferSize);
            _highs = new CircularBuffer<double>(bufferSize);
            _lows = new CircularBuffer<double>(bufferSize);
            _closes = new CircularBuffer<double>(bufferSize);
            _volumes = new CircularBuffer<double>(bufferSize);
            _ranges = new CircularBuffer<double>(bufferSize);
            _bodies = new CircularBuffer<double>(bufferSize);
            _velocities = new CircularBuffer<double>(bufferSize);
        }

        public void Reset()
        {
            _opens.Clear();
            _highs.Clear();
            _lows.Clear();
            _closes.Clear();
            _volumes.Clear();
            _ranges.Clear();
            _bodies.Clear();
            _velocities.Clear();
            _barsProcessed = 0;
        }

        /// <summary>
        /// Compute next set of optimal TCN features - pure measurements only
        /// </summary>
        public OptimalTCNFeatures ComputeNext(OhlcvBar bar)
        {
            _barsProcessed++;

            // === Update all buffers ===
            double range = Math.Max(bar.High - bar.Low, 0.0);
            double body = Math.Abs(bar.Close - bar.Open);

            _opens.Add(bar.Open);
            _highs.Add(bar.High);
            _lows.Add(bar.Low);
            _closes.Add(bar.Close);
            _volumes.Add(bar.TickVolume);
            _ranges.Add(range);
            _bodies.Add(body);

            // Calculate and store velocity
            if (_closes.Count >= 2)
            {
                double velocity = CalculateVelocity(_closes, 1);
                _velocities.Add(velocity);
            }

            // === TIER 1: Raw OHLCV ===
            double open = bar.Open;
            double high = bar.High;
            double low = bar.Low;
            double close = bar.Close;
            double volume = bar.TickVolume;

            // === TIER 2: Basic transformations ===
            double upperWick = high - Math.Max(open, close);
            double lowerWick = Math.Min(open, close) - low;
            double bodyRatio = range > Eps ? body / range : 0.0;
            double upperWickRatio = range > Eps ? upperWick / range : 0.0;
            double lowerWickRatio = range > Eps ? lowerWick / range : 0.0;
            double wickImbalance = upperWickRatio - lowerWickRatio;

            // === TIER 3-4: Multi-scale velocities and acceleration ===
            var velocities = new double[5];
            if (_config.VelocityWindows.Length >= 1) velocities[0] = CalculateVelocity(_closes, _config.VelocityWindows[0]);
            if (_config.VelocityWindows.Length >= 2) velocities[1] = CalculateVelocity(_closes, _config.VelocityWindows[1]);
            if (_config.VelocityWindows.Length >= 3) velocities[2] = CalculateVelocity(_closes, _config.VelocityWindows[2]);
            if (_config.VelocityWindows.Length >= 4) velocities[3] = CalculateVelocity(_closes, _config.VelocityWindows[3]);
            if (_config.VelocityWindows.Length >= 5) velocities[4] = CalculateVelocity(_closes, _config.VelocityWindows[4]);

            var accelerations = new double[3];
            accelerations[0] = CalculateAcceleration(3);
            accelerations[1] = CalculateAcceleration(5);
            accelerations[2] = CalculateAcceleration(10);

            // === TIER 5: Multi-scale ratios ===
            var rangeRatios = new double[5];
            var volumeRatios = new double[5];
            for (int i = 0; i < Math.Min(5, _config.RatioWindows.Length); i++)
            {
                rangeRatios[i] = CalculateRatio(_ranges, _config.RatioWindows[i]);
                volumeRatios[i] = CalculateRatio(_volumes, _config.RatioWindows[i]);
            }

            // === TIER 6-7: Statistical moments ===
            var priceStdDevs = new double[3];
            var volumeStdDevs = new double[3];
            var rangeStdDevs = new double[3];
            for (int i = 0; i < Math.Min(3, _config.StatisticalWindows.Length); i++)
            {
                priceStdDevs[i] = CalculateStdDev(_closes, _config.StatisticalWindows[i]);
                volumeStdDevs[i] = CalculateStdDev(_volumes, _config.StatisticalWindows[i]);
                rangeStdDevs[i] = CalculateStdDev(_ranges, _config.StatisticalWindows[i]);
            }

            var priceSkewness = new double[2];
            var priceKurtosis = new double[2];
            var volumeSkewness = new double[2];
            if (_config.IncludeHigherMoments)
            {
                priceSkewness[0] = CalculateSkewness(_closes, 10);
                priceSkewness[1] = CalculateSkewness(_closes, 20);
                priceKurtosis[0] = CalculateKurtosis(_closes, 10);
                priceKurtosis[1] = CalculateKurtosis(_closes, 20);
                volumeSkewness[0] = CalculateSkewness(_volumes, 10);
                volumeSkewness[1] = CalculateSkewness(_volumes, 20);
            }

            // === TIER 8: Cross-correlations ===
            var priceVolumeCorrs = new double[3];
            var rangeVolumeCorrs = new double[3];
            if (_config.IncludeCrossCorrelations)
            {
                for (int i = 0; i < Math.Min(3, _config.CorrelationWindows.Length); i++)
                {
                    priceVolumeCorrs[i] = CalculateCorrelation(_closes, _volumes, _config.CorrelationWindows[i]);
                    rangeVolumeCorrs[i] = CalculateCorrelation(_ranges, _volumes, _config.CorrelationWindows[i]);
                }
            }

            // === TIER 9-10: Momentum and divergence ===
            var priceMomentums = new double[4];
            var volumeMomentums = new double[4];
            for (int i = 0; i < Math.Min(4, _config.MomentumWindows.Length); i++)
            {
                priceMomentums[i] = CalculateMomentum(_closes, _config.MomentumWindows[i]);
                volumeMomentums[i] = CalculateMomentum(_volumes, _config.MomentumWindows[i]);
            }

            var momentumDivergences = new double[3];
            momentumDivergences[0] = CalculateMomentum(_closes, 3) - CalculateMomentum(_volumes, 3);
            momentumDivergences[1] = CalculateMomentum(_closes, 5) - CalculateMomentum(_volumes, 5);
            momentumDivergences[2] = CalculateMomentum(_closes, 10) - CalculateMomentum(_volumes, 10);

            // === TIER 11-12: Structural relationships ===
            var (distHigh20, distLow20) = CalculateDistanceToLevels(20);
            var (distHigh50, distLow50) = CalculateDistanceToLevels(50);
            var (distHigh20Pct, distLow20Pct) = (distHigh20 / Math.Max(Math.Abs(close), Eps),
                                                  distLow20 / Math.Max(Math.Abs(close), Eps));
            var (distHigh50Pct, distLow50Pct) = (distHigh50 / Math.Max(Math.Abs(close), Eps),
                                                  distLow50 / Math.Max(Math.Abs(close), Eps));

            var smaDistances = new double[4];
            var smaDistancesPct = new double[4];
            var smaWindows = new[] { 5, 10, 20, 50 };
            for (int i = 0; i < 4; i++)
            {
                double smaValue = CalculateSMA(_closes, smaWindows[i]);
                smaDistances[i] = close - smaValue;
                smaDistancesPct[i] = smaDistances[i] / Math.Max(Math.Abs(close), Eps);
            }

            // === TIER 13: Consecutive patterns ===
            var consecutives = CalculateAllConsecutivePatterns();

            // === TIER 14: Time context (cyclical encoding) ===
            var timeFeatures = CalculateTimeFeatures(bar.TimestampUtc);

            // === TIER 15: Order flow proxies ===
            var orderFlowFeatures = CalculateOrderFlowProxies(bar, range, body);

            // === TIER 16: Multi-scale volatility ===
            var volatilityRatios = new double[4];
            var volatilityMomentums = new double[3];
            var volRatioWindows = new[] { 5, 10, 20, 50 };
            var volMomWindows = new[] { 5, 10, 20 };
            for (int i = 0; i < 4; i++)
            {
                volatilityRatios[i] = CalculateVolatilityRatio(volRatioWindows[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                volatilityMomentums[i] = CalculateVolatilityMomentum(volMomWindows[i]);
            }

            // === TIER 17: Fractal dimensions only ===

            var fdWins = _config.FD_Windows ?? Array.Empty<int>();
            var fractalDims = new double[Math.Min(2, fdWins.Length)];
            for (int i = 0; i < fractalDims.Length; i++)
            {
                fractalDims[i] = CalculateFractalDimension(fdWins[i]);
            }

            // === SPIKE-ORIENTED RAW CALCULATIONS ===

            // Returns for ACF/PACF and realized family
            var ret = GetReturns(_closes, Math.Min(_closes.Count - 1, _config.StatisticalWindows.Last()));

            // Efficiency Ratio (per window)
            double er10 = ComputeER(_closes, 10);
            double er20 = ComputeER(_closes, 20);
            double er50 = ComputeER(_closes, 50);

            // R/S (Hurst proxy)
            double rs20 = ComputeRS(_closes, 20);
            double rs50 = ComputeRS(_closes, 50);
            double rs100 = ComputeRS(_closes, 100);

            // Realized family (using returns)
            double rv10 = ComputeRV(ret, 10);
            double rv20 = ComputeRV(ret, 20);
            double rv50 = ComputeRV(ret, 50);
            double bpv10 = ComputeBPV(ret, 10);
            double bpv20 = ComputeBPV(ret, 20);
            double bpv50 = ComputeBPV(ret, 50);
            double rq10 = ComputeQuarticity(ret, 10);
            double rq20 = ComputeQuarticity(ret, 20);
            double rq50 = ComputeQuarticity(ret, 50);
            double rr10 = ComputeRealizedRange(_highs, _lows, 10);
            double rr20 = ComputeRealizedRange(_highs, _lows, 20);
            double rr50 = ComputeRealizedRange(_highs, _lows, 50);

            // Rolling VWAP and deviation
            double vwap10 = ComputeVWAP(_closes, _volumes, 10);
            double vwap20 = ComputeVWAP(_closes, _volumes, 20);
            double vwap50 = ComputeVWAP(_closes, _volumes, 50);
            double devVWAP10 = _closes.Count > 0 ? _closes[_closes.Count - 1] - vwap10 : 0.0;
            double devVWAP20 = _closes.Count > 0 ? _closes[_closes.Count - 1] - vwap20 : 0.0;
            double devVWAP50 = _closes.Count > 0 ? _closes[_closes.Count - 1] - vwap50 : 0.0;

            // ACF lag1/2/3 and PACF1 (for returns)
            double acf1 = ComputeACF(ret, 1);
            double acf2 = ComputeACF(ret, 2);
            double acf3 = ComputeACF(ret, 3);
            double pacf1 = acf1; // PACF1 == ACF1

            // Sign imbalance and positive share
            double si20 = ComputeSignImbalance(ret, 20, out double ps20);
            double si50 = ComputeSignImbalance(ret, 50, out double ps50);
            double si100 = ComputeSignImbalance(ret, 100, out double ps100);

            // Robust stats: median, MAD, deviation from median
            (double med10, double mad10, double dmed10) = ComputeRobust(_closes, 10);
            (double med20, double mad20, double dmed20) = ComputeRobust(_closes, 20);
            (double med50, double mad50, double dmed50) = ComputeRobust(_closes, 50);

            // Spectral low/high energy ratio (DFT)
            double spec16 = ComputeSpectralLHRatio(_closes, 16);
            double spec32 = ComputeSpectralLHRatio(_closes, 32);

            // Slope stability: std of linear slopes across four sub-windows of the shortest Statistical window
            int shortW = _config.StatisticalWindows.Min();
            double slopeStab = ComputeSlopeStability(_closes, shortW);

            // Liquidity ratios
            double rangePerVol = volume > Eps ? range / volume : 0.0;
            double bodyPerVol = volume > Eps ? body / volume : 0.0;
            double rangePerVolRatio20 = ComputeRatioSeries(_ranges, _volumes, 20);
            double rangePerVolRatio50 = ComputeRatioSeries(_ranges, _volumes, 50);
            double bodyPerVolRatio20 = ComputeRatioSeries(_bodies, _volumes, 20);
            double bodyPerVolRatio50 = ComputeRatioSeries(_bodies, _volumes, 50);

            // Drawdown / Run-up (windowed, based on Close)
            double dd20 = ComputeMaxDrawdown(_closes, 20);
            double dd50 = ComputeMaxDrawdown(_closes, 50);
            double dd100 = ComputeMaxDrawdown(_closes, 100);
            double ru20 = ComputeMaxRunup(_closes, 20);
            double ru50 = ComputeMaxRunup(_closes, 50);
            double ru100 = ComputeMaxRunup(_closes, 100);

            // Entropy of sign of returns
            double ent20 = ComputeSignEntropy(ret, 20);
            double ent50 = ComputeSignEntropy(ret, 50);
            double ent100 = ComputeSignEntropy(ret, 100);

            bool isReady = _barsProcessed >= _config.StatisticalWindows.Max();

            return new OptimalTCNFeatures
            {
                // TIER 1: Raw OHLCV
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,

                // TIER 2: Basic transformations
                Range = range,
                Body = body,
                UpperWick = upperWick,
                LowerWick = lowerWick,
                BodyRatio = bodyRatio,
                UpperWickRatio = upperWickRatio,
                LowerWickRatio = lowerWickRatio,
                WickImbalance = wickImbalance,

                // TIER 3: Multi-scale velocities
                PriceVelocity1 = velocities.Length > 0 ? velocities[0] : 0.0,
                PriceVelocity3 = velocities.Length > 1 ? velocities[1] : 0.0,
                PriceVelocity5 = velocities.Length > 2 ? velocities[2] : 0.0,
                PriceVelocity10 = velocities.Length > 3 ? velocities[3] : 0.0,
                PriceVelocity20 = velocities.Length > 4 ? velocities[4] : 0.0,

                // TIER 4: Acceleration
                PriceAcceleration3 = accelerations.Length > 0 ? accelerations[0] : 0.0,
                PriceAcceleration5 = accelerations.Length > 1 ? accelerations[1] : 0.0,
                PriceAcceleration10 = accelerations.Length > 2 ? accelerations[2] : 0.0,

                // TIER 5: Multi-scale ratios
                RangeRatio3 = rangeRatios.Length > 0 ? rangeRatios[0] : 1.0,
                RangeRatio5 = rangeRatios.Length > 1 ? rangeRatios[1] : 1.0,
                RangeRatio10 = rangeRatios.Length > 2 ? rangeRatios[2] : 1.0,
                RangeRatio20 = rangeRatios.Length > 3 ? rangeRatios[3] : 1.0,
                RangeRatio50 = rangeRatios.Length > 4 ? rangeRatios[4] : 1.0,

                VolumeRatio3 = volumeRatios.Length > 0 ? volumeRatios[0] : 1.0,
                VolumeRatio5 = volumeRatios.Length > 1 ? volumeRatios[1] : 1.0,
                VolumeRatio10 = volumeRatios.Length > 2 ? volumeRatios[2] : 1.0,
                VolumeRatio20 = volumeRatios.Length > 3 ? volumeRatios[3] : 1.0,
                VolumeRatio50 = volumeRatios.Length > 4 ? volumeRatios[4] : 1.0,

                // TIER 6: Statistical moments
                PriceStdDev10 = priceStdDevs.Length > 0 ? priceStdDevs[0] : 0.0,
                PriceStdDev20 = priceStdDevs.Length > 1 ? priceStdDevs[1] : 0.0,
                PriceStdDev50 = priceStdDevs.Length > 2 ? priceStdDevs[2] : 0.0,

                VolumeStdDev10 = volumeStdDevs.Length > 0 ? volumeStdDevs[0] : 0.0,
                VolumeStdDev20 = volumeStdDevs.Length > 1 ? volumeStdDevs[1] : 0.0,
                VolumeStdDev50 = volumeStdDevs.Length > 2 ? volumeStdDevs[2] : 0.0,

                RangeStdDev10 = rangeStdDevs.Length > 0 ? rangeStdDevs[0] : 0.0,
                RangeStdDev20 = rangeStdDevs.Length > 1 ? rangeStdDevs[1] : 0.0,
                RangeStdDev50 = rangeStdDevs.Length > 2 ? rangeStdDevs[2] : 0.0,

                // TIER 7: Higher moments
                PriceSkewness10 = priceSkewness.Length > 0 ? priceSkewness[0] : 0.0,
                PriceSkewness20 = priceSkewness.Length > 1 ? priceSkewness[1] : 0.0,
                PriceKurtosis10 = priceKurtosis.Length > 0 ? priceKurtosis[0] : 0.0,
                PriceKurtosis20 = priceKurtosis.Length > 1 ? priceKurtosis[1] : 0.0,

                VolumeSkewness10 = volumeSkewness.Length > 0 ? volumeSkewness[0] : 0.0,
                VolumeSkewness20 = volumeSkewness.Length > 1 ? volumeSkewness[1] : 0.0,

                // TIER 8: Cross-correlations
                PriceVolumeCorr5 = priceVolumeCorrs.Length > 0 ? priceVolumeCorrs[0] : 0.0,
                PriceVolumeCorr10 = priceVolumeCorrs.Length > 1 ? priceVolumeCorrs[1] : 0.0,
                PriceVolumeCorr20 = priceVolumeCorrs.Length > 2 ? priceVolumeCorrs[2] : 0.0,

                RangeVolumeCorr5 = rangeVolumeCorrs.Length > 0 ? rangeVolumeCorrs[0] : 0.0,
                RangeVolumeCorr10 = rangeVolumeCorrs.Length > 1 ? rangeVolumeCorrs[1] : 0.0,
                RangeVolumeCorr20 = rangeVolumeCorrs.Length > 2 ? rangeVolumeCorrs[2] : 0.0,

                // TIER 9: Multi-scale momentum
                PriceMomentum3 = priceMomentums.Length > 0 ? priceMomentums[0] : 0.0,
                PriceMomentum5 = priceMomentums.Length > 1 ? priceMomentums[1] : 0.0,
                PriceMomentum10 = priceMomentums.Length > 2 ? priceMomentums[2] : 0.0,
                PriceMomentum20 = priceMomentums.Length > 3 ? priceMomentums[3] : 0.0,

                VolumeMomentum3 = volumeMomentums.Length > 0 ? volumeMomentums[0] : 0.0,
                VolumeMomentum5 = volumeMomentums.Length > 1 ? volumeMomentums[1] : 0.0,
                VolumeMomentum10 = volumeMomentums.Length > 2 ? volumeMomentums[2] : 0.0,
                VolumeMomentum20 = volumeMomentums.Length > 3 ? volumeMomentums[3] : 0.0,

                // TIER 10: Momentum divergence
                MomentumDivergence3 = momentumDivergences.Length > 0 ? momentumDivergences[0] : 0.0,
                MomentumDivergence5 = momentumDivergences.Length > 1 ? momentumDivergences[1] : 0.0,
                MomentumDivergence10 = momentumDivergences.Length > 2 ? momentumDivergences[2] : 0.0,

                // TIER 11: Structural relationships
                DistanceToHigh20 = distHigh20,
                DistanceToLow20 = distLow20,
                DistanceToHigh50 = distHigh50,
                DistanceToLow50 = distLow50,
                DistanceToHigh20Pct = distHigh20Pct,
                DistanceToLow20Pct = distLow20Pct,
                DistanceToHigh50Pct = distHigh50Pct,
                DistanceToLow50Pct = distLow50Pct,

                // TIER 12: SMA relationships
                DistanceToSMA5 = smaDistances.Length > 0 ? smaDistances[0] : 0.0,
                DistanceToSMA10 = smaDistances.Length > 1 ? smaDistances[1] : 0.0,
                DistanceToSMA20 = smaDistances.Length > 2 ? smaDistances[2] : 0.0,
                DistanceToSMA50 = smaDistances.Length > 3 ? smaDistances[3] : 0.0,
                DistanceToSMA5Pct = smaDistancesPct.Length > 0 ? smaDistancesPct[0] : 0.0,
                DistanceToSMA10Pct = smaDistancesPct.Length > 1 ? smaDistancesPct[1] : 0.0,
                DistanceToSMA20Pct = smaDistancesPct.Length > 2 ? smaDistancesPct[2] : 0.0,
                DistanceToSMA50Pct = smaDistancesPct.Length > 3 ? smaDistancesPct[3] : 0.0,

                // TIER 13: Consecutive patterns
                ConsecutiveUpBars = consecutives.upBars,
                ConsecutiveDownBars = consecutives.downBars,
                ConsecutiveUpBodies = consecutives.upBodies,
                ConsecutiveDownBodies = consecutives.downBodies,
                ConsecutiveHighVolume = consecutives.highVolume,
                ConsecutiveLowVolume = consecutives.lowVolume,
                ConsecutiveWideRanges = consecutives.wideRanges,
                ConsecutiveTightRanges = consecutives.tightRanges,

                // TIER 14: Time context
                HourSin = timeFeatures.hourSin,
                HourCos = timeFeatures.hourCos,
                DayOfWeekSin = timeFeatures.dayOfWeekSin,
                DayOfWeekCos = timeFeatures.dayOfWeekCos,
                MinuteSin = timeFeatures.minuteSin,
                MinuteCos = timeFeatures.minuteCos,

                // TIER 15: Order flow proxies
                VolumePerPoint = orderFlowFeatures.volumePerPoint,
                VolumePerRange = orderFlowFeatures.volumePerRange,
                PriceEfficiency = orderFlowFeatures.priceEfficiency,
                BuyPressureProxy = orderFlowFeatures.buyPressure,
                SellPressureProxy = orderFlowFeatures.sellPressure,

                // TIER 16: Multi-scale volatility
                VolatilityRatio5 = volatilityRatios.Length > 0 ? volatilityRatios[0] : 1.0,
                VolatilityRatio10 = volatilityRatios.Length > 1 ? volatilityRatios[1] : 1.0,
                VolatilityRatio20 = volatilityRatios.Length > 2 ? volatilityRatios[2] : 1.0,
                VolatilityRatio50 = volatilityRatios.Length > 3 ? volatilityRatios[3] : 1.0,

                VolatilityMomentum5 = volatilityMomentums.Length > 0 ? volatilityMomentums[0] : 0.0,
                VolatilityMomentum10 = volatilityMomentums.Length > 1 ? volatilityMomentums[1] : 0.0,
                VolatilityMomentum20 = volatilityMomentums.Length > 2 ? volatilityMomentums[2] : 0.0,

                // TIER 17: Fractal only (unique)
                FractalDim32 = fractalDims.Length > 0 ? fractalDims[0] : 1.0,
                FractalDim64 = fractalDims.Length > 1 ? fractalDims[1] : 1.0,

                // TIER 18: Additional Spike Raw Features
                ER10 = er10,
                ER20 = er20,
                ER50 = er50,
                RS20 = rs20,
                RS50 = rs50,
                RS100 = rs100,
                RV10 = rv10,
                RV20 = rv20,
                RV50 = rv50,
                BPV10 = bpv10,
                BPV20 = bpv20,
                BPV50 = bpv50,
                RQ10 = rq10,
                RQ20 = rq20,
                RQ50 = rq50,
                RealRange10 = rr10,
                RealRange20 = rr20,
                RealRange50 = rr50,
                VWAP10 = vwap10,
                VWAP20 = vwap20,
                VWAP50 = vwap50,
                DevVWAP10 = devVWAP10,
                DevVWAP20 = devVWAP20,
                DevVWAP50 = devVWAP50,

                // TIER 19: ACF/PACF and Sign Features
                ACF1 = acf1,
                ACF2 = acf2,
                ACF3 = acf3,
                PACF1 = pacf1,
                SignImbalance20 = si20,
                SignImbalance50 = si50,
                SignImbalance100 = si100,
                PosShare20 = ps20,
                PosShare50 = ps50,
                PosShare100 = ps100,
                Median10 = med10,
                Median20 = med20,
                Median50 = med50,
                MAD10 = mad10,
                MAD20 = mad20,
                MAD50 = mad50,
                DevMedian10 = dmed10,
                DevMedian20 = dmed20,
                DevMedian50 = dmed50,
                SpecLHRatio16 = spec16,
                SpecLHRatio32 = spec32,

                // TIER 20: Liquidity and Drawdown Features
                SlopeStabilityShort = slopeStab,
                RangePerVolume = rangePerVol,
                BodyPerVolume = bodyPerVol,
                RangePerVolumeRatio20 = rangePerVolRatio20,
                RangePerVolumeRatio50 = rangePerVolRatio50,
                BodyPerVolumeRatio20 = bodyPerVolRatio20,
                BodyPerVolumeRatio50 = bodyPerVolRatio50,
                MaxRunup20 = ru20,
                MaxRunup50 = ru50,
                MaxRunup100 = ru100,
                EntropySign20 = ent20,
                EntropySign50 = ent50,
                EntropySign100 = ent100,

                // Meta information
                IsReady = isReady,
                BarsProcessed = _barsProcessed
            };
        }

        // ===============================================================
        // HELPER METHODS - Pure mathematical calculations
        // ===============================================================

        private double CalculateVelocity(CircularBuffer<double> values, int period)
        {
            if (values.Count < period + 1) return 0.0;
            double current = values[values.Count - 1];
            double previous = values[values.Count - period - 1];
            return Math.Abs(previous) > Eps ? (current - previous) / Math.Abs(previous) : 0.0;
        }

        private double CalculateAcceleration(int period)
        {
            if (_velocities.Count < period * 2) return 0.0;
            double currentVel = _velocities.TakeLast(period).Average();
            double previousVel = _velocities.Skip(_velocities.Count - period * 2).Take(period).Average();
            return currentVel - previousVel;
        }

        private double CalculateRatio(CircularBuffer<double> values, int window)
        {
            if (values.Count < window + 1) return 1.0;
            double current = values[values.Count - 1];
            double sum = 0.0;
            int count = Math.Min(window, values.Count - 1);
            for (int i = values.Count - 1 - count; i < values.Count - 1; i++)
            {
                sum += values[i];
            }
            double average = count > 0 ? sum / count : current;
            return Math.Abs(average) > Eps ? current / average : 1.0;
        }

        private double CalculateStdDev(CircularBuffer<double> values, int window)
        {
            if (values.Count < window) return 0.0;

            // Calculate mean
            double sum = 0.0;
            int count = Math.Min(window, values.Count);
            int startIdx = values.Count - count;

            for (int i = startIdx; i < values.Count; i++)
            {
                sum += values[i];
            }
            double mean = sum / count;

            // Calculate variance
            double variance = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                double diff = values[i] - mean;
                variance += diff * diff;
            }
            variance /= count;

            return Math.Sqrt(variance);
        }

        private double CalculateSkewness(CircularBuffer<double> values, int window)
        {
            if (values.Count < window) return 0.0;

            int count = Math.Min(window, values.Count);
            int startIdx = values.Count - count;

            // Calculate mean
            double sum = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                sum += values[i];
            }
            double mean = sum / count;

            // Calculate standard deviation
            double variance = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                double diff = values[i] - mean;
                variance += diff * diff;
            }
            variance /= count;
            double stdDev = Math.Sqrt(variance);

            if (stdDev <= Eps) return 0.0;

            // Calculate skewness
            double skewness = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                double standardized = (values[i] - mean) / stdDev;
                skewness += standardized * standardized * standardized;
            }
            skewness /= count;

            return skewness;
        }

        private double CalculateKurtosis(CircularBuffer<double> values, int window)
        {
            if (values.Count < window) return 0.0;

            int count = Math.Min(window, values.Count);
            int startIdx = values.Count - count;

            // Calculate mean
            double sum = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                sum += values[i];
            }
            double mean = sum / count;

            // Calculate standard deviation
            double variance = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                double diff = values[i] - mean;
                variance += diff * diff;
            }
            variance /= count;
            double stdDev = Math.Sqrt(variance);

            // Fix: return excess kurtosis (0) when std is ~0, not 3.0
            if (stdDev <= Eps) return 0.0;

            // Calculate kurtosis
            double kurtosis = 0.0;
            for (int i = startIdx; i < values.Count; i++)
            {
                double standardized = (values[i] - mean) / stdDev;
                kurtosis += standardized * standardized * standardized * standardized;
            }
            kurtosis /= count;

            return kurtosis - 3.0; // Excess kurtosis
        }

        private double CalculateCorrelation(CircularBuffer<double> x, CircularBuffer<double> y, int window)
        {
            int count = Math.Min(window, Math.Min(x.Count, y.Count));
            if (count <= 0) return 0.0;

            int xStart = x.Count - count;
            int yStart = y.Count - count;

            double xSum = 0.0, ySum = 0.0;
            for (int k = 0; k < count; k++)
            {
                xSum += x[xStart + k];
                ySum += y[yStart + k];
            }
            double xMean = xSum / count;
            double yMean = ySum / count;

            double num = 0.0, xSumSq = 0.0, ySumSq = 0.0;
            for (int k = 0; k < count; k++)
            {
                double xd = x[xStart + k] - xMean;
                double yd = y[yStart + k] - yMean;
                num += xd * yd;
                xSumSq += xd * xd;
                ySumSq += yd * yd;
            }
            double den = Math.Sqrt(xSumSq * ySumSq);
            return den > Eps ? num / den : 0.0;
        }

        private double CalculateMomentum(CircularBuffer<double> values, int period)
        {
            if (values.Count < period + 1) return 0.0;
            double current = values[values.Count - 1];
            double previous = values[values.Count - period - 1];
            return Math.Abs(previous) > Eps ? (current - previous) / Math.Abs(previous) : 0.0;
        }

        private (double distanceToHigh, double distanceToLow) CalculateDistanceToLevels(int period)
        {
            if (_highs.Count < period || _lows.Count < period || _closes.Count == 0) return (0.0, 0.0);

            double currentPrice = _closes[_closes.Count - 1];

            // Find max and min in the last 'period' bars
            int end = _highs.Count - 1;
            int start = end - period + 1;
            double periodHigh = double.NegativeInfinity;
            double periodLow = double.PositiveInfinity;

            for (int i = start; i <= end; i++)
            {
                if (_highs[i] > periodHigh) periodHigh = _highs[i];
                if (_lows[i] < periodLow) periodLow = _lows[i];
            }

            return (Math.Abs(currentPrice - periodHigh), Math.Abs(currentPrice - periodLow));
        }

        private double CalculateSMA(CircularBuffer<double> values, int period)
        {
            if (values.Count < period) return 0.0;

            double sum = 0.0;
            int count = Math.Min(period, values.Count);
            int startIdx = values.Count - count;

            for (int i = startIdx; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum / count;
        }

        private (int upBars, int downBars, int upBodies, int downBodies,
                 int highVolume, int lowVolume, int wideRanges, int tightRanges) CalculateAllConsecutivePatterns()
        {
            if (_closes.Count < 2) return (0, 0, 0, 0, 0, 0, 0, 0);

            // Consecutive up/down bars (close to close)
            int upBars = 0, downBars = 0;
            for (int i = _closes.Count - 1; i > 0; i--)
            {
                if (_closes[i] > _closes[i - 1]) upBars++;
                else break;
            }
            for (int i = _closes.Count - 1; i > 0; i--)
            {
                if (_closes[i] < _closes[i - 1]) downBars++;
                else break;
            }

            // Consecutive up/down bodies (open to close)
            int upBodies = 0, downBodies = 0;
            if (_opens.Count >= _closes.Count)
            {
                for (int i = _closes.Count - 1; i >= 0; i--)
                {
                    if (_closes[i] > _opens[i]) upBodies++;
                    else break;
                }
                for (int i = _closes.Count - 1; i >= 0; i--)
                {
                    if (_closes[i] < _opens[i]) downBodies++;
                    else break;
                }
            }

            // Consecutive high/low volume
            int highVolume = 0, lowVolume = 0;
            if (_volumes.Count >= 20)
            {
                double avgVolume = CalculateSMA(_volumes, 20);
                for (int i = _volumes.Count - 1; i >= 0; i--)
                {
                    if (_volumes[i] > avgVolume) highVolume++;
                    else break;
                }
                for (int i = _volumes.Count - 1; i >= 0; i--)
                {
                    if (_volumes[i] < avgVolume) lowVolume++;
                    else break;
                }
            }

            // Consecutive wide/tight ranges
            int wideRanges = 0, tightRanges = 0;
            if (_ranges.Count >= 20)
            {
                double avgRange = CalculateSMA(_ranges, 20);
                for (int i = _ranges.Count - 1; i >= 0; i--)
                {
                    if (_ranges[i] > avgRange) wideRanges++;
                    else break;
                }
                for (int i = _ranges.Count - 1; i >= 0; i--)
                {
                    if (_ranges[i] < avgRange) tightRanges++;
                    else break;
                }
            }

            return (upBars, downBars, upBodies, downBodies, highVolume, lowVolume, wideRanges, tightRanges);
        }

        private (double hourSin, double hourCos, double dayOfWeekSin, double dayOfWeekCos,
                 double minuteSin, double minuteCos) CalculateTimeFeatures(DateTime timestamp)
        {
            // Cyclical encoding for time features
            double hourSin = Math.Sin(2 * Math.PI * timestamp.Hour / 24.0);
            double hourCos = Math.Cos(2 * Math.PI * timestamp.Hour / 24.0);
            double dayOfWeekSin = Math.Sin(2 * Math.PI * (int)timestamp.DayOfWeek / 7.0);
            double dayOfWeekCos = Math.Cos(2 * Math.PI * (int)timestamp.DayOfWeek / 7.0);
            double minuteSin = Math.Sin(2 * Math.PI * timestamp.Minute / 60.0);
            double minuteCos = Math.Cos(2 * Math.PI * timestamp.Minute / 60.0);

            return (hourSin, hourCos, dayOfWeekSin, dayOfWeekCos, minuteSin, minuteCos);
        }

        private (double volumePerPoint, double volumePerRange, double priceEfficiency,
                 double buyPressure, double sellPressure) CalculateOrderFlowProxies(OhlcvBar bar, double range, double body)
        {
            // Volume per price point moved
            double priceMove = Math.Abs(bar.Close - bar.Open);
            double volumePerPoint = priceMove > Eps ? bar.TickVolume / priceMove : 0.0;

            // Volume per range
            double volumePerRange = range > Eps ? bar.TickVolume / range : 0.0;

            // Price efficiency (how much of the range was directional movement)
            double priceEfficiency = range > Eps ? priceMove / range : 0.0;

            // Buy/Sell pressure proxies based on wick analysis
            double upperWick = bar.High - Math.Max(bar.Open, bar.Close);
            double lowerWick = Math.Min(bar.Open, bar.Close) - bar.Low;

            // More upper wick = selling pressure, more lower wick = buying pressure
            double totalWick = upperWick + lowerWick;
            double buyPressure = totalWick > Eps ? lowerWick / totalWick : 0.5;
            double sellPressure = totalWick > Eps ? upperWick / totalWick : 0.5;

            return (volumePerPoint, volumePerRange, priceEfficiency, buyPressure, sellPressure);
        }

        private double CalculateVolatilityRatio(int window)
        {
            if (_ranges.Count < window + 1) return 1.0;

            double currentVolatility = _ranges[_ranges.Count - 1];

            // Past-only avg: последните `window` преди текущия бар
            double sum = 0.0;
            int start = _ranges.Count - 1 - window;
            for (int i = start; i < start + window; i++)
            {
                sum += _ranges[i];
            }
            double avgVolatility = sum / window;

            return avgVolatility > Eps ? currentVolatility / avgVolatility : 1.0;
        }

        private double CalculateVolatilityMomentum(int period)
        {
            if (_ranges.Count < period * 2) return 0.0;

            // Calculate recent average
            double recentSum = 0.0;
            for (int i = _ranges.Count - period; i < _ranges.Count; i++)
            {
                recentSum += _ranges[i];
            }
            double recentAvg = recentSum / period;

            // Calculate older average
            double olderSum = 0.0;
            int olderStart = _ranges.Count - period * 2;
            for (int i = olderStart; i < olderStart + period; i++)
            {
                olderSum += _ranges[i];
            }
            double olderAvg = olderSum / period;

            return Math.Abs(olderAvg) > Eps ? (recentAvg - olderAvg) / olderAvg : 0.0;
        }

        // === SPIKE-ORIENTED FEATURES METHODS ===

        private double CalculateFractalDimension(int window)
        {
            if (_closes.Count < window) return 1.0;
            var series = new double[window];
            for (int i = 0; i < window; i++)
                series[i] = _closes[_closes.Count - window + i];

            // Simplified Higuchi FD approximation
            double totalLength = 0.0;
            for (int i = 1; i < window; i++)
                totalLength += Math.Abs(series[i] - series[i - 1]);

            double directDistance = Math.Abs(series[window - 1] - series[0]);
            return directDistance > Eps ? 1.0 + Math.Log(totalLength / directDistance) / Math.Log(window) : 1.0;
        }

        // === HELPER METHODS FOR SPIKE-ORIENTED FEATURES ===

        private double[] GetReturns(CircularBuffer<double> closes, int maxLen)
        {
            int n = Math.Min(maxLen, closes.Count);
            if (n < 2) return Array.Empty<double>();
            var ret = new double[n - 1];
            int start = closes.Count - n;
            for (int i = 1; i < n; i++)
            {
                double prev = closes[start + i - 1];
                double curr = closes[start + i];
                ret[i - 1] = Math.Abs(prev) > Eps ? (curr - prev) / Math.Abs(prev) : 0.0;
            }
            return ret;
        }

        private double ComputeER(CircularBuffer<double> closes, int window)
        {
            if (closes.Count <= window) return 0.0;
            double change = Math.Abs(closes[closes.Count - 1] - closes[closes.Count - 1 - window]);
            double noise = 0.0;
            for (int i = closes.Count - window; i < closes.Count; i++)
                noise += Math.Abs(closes[i] - closes[i - 1]);
            return noise > Eps ? Math.Min(1.0, change / noise) : 0.0;
        }

        private double ComputeRS(CircularBuffer<double> closes, int window)
        {
            if (closes.Count < window + 1) return 0.0;
            var returns = new double[window];
            for (int i = 0; i < window; i++)
            {
                int idx = closes.Count - window + i;
                returns[i] = closes[idx] - closes[idx - 1];
            }
            double mean = returns.Average();
            double[] cumDev = new double[window];
            double cumSum = 0.0;
            for (int i = 0; i < window; i++)
            {
                cumSum += returns[i] - mean;
                cumDev[i] = cumSum;
            }
            double range = cumDev.Max() - cumDev.Min();
            double stdDev = Math.Sqrt(returns.Sum(r => (r - mean) * (r - mean)) / window);
            return stdDev > Eps ? range / stdDev : 0.0;
        }

        private double ComputeRV(double[] returns, int window)
        {
            if (returns.Length < window) return 0.0;
            double rv = 0.0;
            int start = returns.Length - window;
            for (int i = start; i < returns.Length; i++)
                rv += returns[i] * returns[i];
            return rv;
        }

        private double ComputeBPV(double[] returns, int window)
        {
            if (returns.Length < window || window < 2) return 0.0;
            double bpv = 0.0;
            int start = returns.Length - window;
            for (int i = start + 1; i < returns.Length; i++)
                bpv += Math.Abs(returns[i]) * Math.Abs(returns[i - 1]);
            return bpv / (0.7978845608028654 * 0.7978845608028654); // μ1^{-2}
        }

        private double ComputeQuarticity(double[] returns, int window)
        {
            if (returns.Length < window) return 0.0;
            double rq = 0.0;
            int start = returns.Length - window;
            for (int i = start; i < returns.Length; i++)
            {
                double r = returns[i];
                rq += r * r * r * r;
            }
            return (window / 3.0) * rq;
        }

        private double ComputeRealizedRange(CircularBuffer<double> highs, CircularBuffer<double> lows, int window)
        {
            if (highs.Count < window || lows.Count < window) return 0.0;
            double rr = 0.0;
            int start = highs.Count - window;
            for (int i = start; i < highs.Count; i++)
            {
                double range = highs[i] - lows[i];
                rr += range * range;
            }
            return rr;
        }

        private double ComputeVWAP(CircularBuffer<double> closes, CircularBuffer<double> volumes, int window)
        {
            if (closes.Count < window || volumes.Count < window) return 0.0;
            double sumPV = 0.0, sumV = 0.0;
            int start = closes.Count - window;
            for (int i = start; i < closes.Count; i++)
            {
                sumPV += closes[i] * volumes[i];
                sumV += volumes[i];
            }
            return sumV > Eps ? sumPV / sumV : 0.0;
        }

        private double ComputeACF(double[] returns, int lag)
        {
            if (returns.Length <= lag) return 0.0;
            double mean = returns.Average();
            double num = 0.0, den = 0.0;
            for (int i = 0; i < returns.Length; i++)
            {
                double d = returns[i] - mean;
                den += d * d;
            }
            for (int i = lag; i < returns.Length; i++)
            {
                num += (returns[i] - mean) * (returns[i - lag] - mean);
            }
            return den > Eps ? num / den : 0.0;
        }

        private double ComputeSignImbalance(double[] returns, int window, out double posShare)
        {
            posShare = 0.0;
            if (returns.Length < window) return 0.0;
            int pos = 0, neg = 0;
            int start = returns.Length - window;
            for (int i = start; i < returns.Length; i++)
            {
                if (returns[i] > 0) pos++;
                else if (returns[i] < 0) neg++;
            }
            int total = pos + neg;
            if (total == 0) return 0.0;
            posShare = (double)pos / total;
            return (double)(pos - neg) / total;
        }

        private (double median, double mad, double devFromMedian) ComputeRobust(CircularBuffer<double> values, int window)
        {
            if (values.Count < window) return (0.0, 0.0, 0.0);
            var data = new double[window];
            int start = values.Count - window;
            for (int i = 0; i < window; i++)
                data[i] = values[start + i];
            Array.Sort(data);
            double median = window % 2 == 1 ? data[window / 2] : (data[window / 2 - 1] + data[window / 2]) / 2.0;
            var devs = new double[window];
            for (int i = 0; i < window; i++)
                devs[i] = Math.Abs(data[i] - median);
            Array.Sort(devs);
            double mad = window % 2 == 1 ? devs[window / 2] : (devs[window / 2 - 1] + devs[window / 2]) / 2.0;
            double current = values.Count > 0 ? values[values.Count - 1] : 0.0;
            return (median, mad, current - median);
        }

        private double ComputeSpectralLHRatio(CircularBuffer<double> closes, int N)
        {
            if (closes.Count < N) return 0.0;
            var x = new double[N];
            int start = closes.Count - N;
            for (int i = 0; i < N; i++) x[i] = closes[start + i];
            double mean = x.Average();
            for (int i = 0; i < N; i++) x[i] -= mean;
            var mag = new double[N];
            for (int k = 0; k < N; k++)
            {
                double re = 0.0, im = 0.0;
                for (int n = 0; n < N; n++)
                {
                    double phi = -2.0 * Math.PI * k * n / N;
                    re += x[n] * Math.Cos(phi);
                    im += x[n] * Math.Sin(phi);
                }
                mag[k] = re * re + im * im;
            }
            int split = N / 4;
            double low = 0.0, high = 0.0;
            for (int k = 1; k < split; k++) low += mag[k];
            for (int k = split; k < N / 2; k++) high += mag[k];
            double total = low + high;
            return total > Eps ? low / total : 0.0;
        }

        private double ComputeSlopeStability(CircularBuffer<double> closes, int window)
        {
            if (closes.Count < window || window < 8) return 0.0;
            int segSize = window / 4;
            var slopes = new double[4];
            for (int seg = 0; seg < 4; seg++)
            {
                int segStart = closes.Count - window + seg * segSize;
                slopes[seg] = ComputeLinearSlope(closes, segStart, segSize);
            }
            double mean = slopes.Average();
            double variance = slopes.Sum(s => (s - mean) * (s - mean)) / 4.0;
            return Math.Sqrt(variance);
        }

        private double ComputeLinearSlope(CircularBuffer<double> values, int start, int length)
        {
            if (length < 2 || start < 0 || start + length > values.Count) return 0.0;
            double sx = (length - 1) * length / 2.0;
            double sxx = (length - 1) * length * (2 * length - 1) / 6.0;
            double sy = 0.0, sxy = 0.0;
            for (int i = 0; i < length; i++)
            {
                sy += values[start + i];
                sxy += i * values[start + i];
            }
            double denom = length * sxx - sx * sx;
            return denom > Eps ? (length * sxy - sx * sy) / denom : 0.0;
        }

        private double ComputeRatioSeries(CircularBuffer<double> numerator, CircularBuffer<double> denominator, int window)
        {
            if (numerator.Count < window || denominator.Count < window) return 0.0;
            double sum = 0.0;
            int count = 0;
            int start = numerator.Count - window;
            for (int i = start; i < numerator.Count; i++)
            {
                if (Math.Abs(denominator[i]) > Eps)
                {
                    sum += numerator[i] / denominator[i];
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.0;
        }

        private double ComputeMaxDrawdown(CircularBuffer<double> closes, int window)
        {
            if (closes.Count < window) return 0.0;
            double maxDD = 0.0;
            double peak = closes[closes.Count - window];
            int start = closes.Count - window + 1;
            for (int i = start; i < closes.Count; i++)
            {
                if (closes[i] > peak) peak = closes[i];
                double dd = peak > Eps ? (peak - closes[i]) / peak : 0.0;
                if (dd > maxDD) maxDD = dd;
            }
            return maxDD;
        }

        private double ComputeMaxRunup(CircularBuffer<double> closes, int window)
        {
            if (closes.Count < window) return 0.0;
            double maxRU = 0.0;
            double trough = closes[closes.Count - window];
            int start = closes.Count - window + 1;
            for (int i = start; i < closes.Count; i++)
            {
                if (closes[i] < trough) trough = closes[i];
                double ru = trough > Eps ? (closes[i] - trough) / trough : 0.0;
                if (ru > maxRU) maxRU = ru;
            }
            return maxRU;
        }

        private double ComputeSignEntropy(double[] returns, int window)
        {
            if (returns.Length < window) return 0.0;
            int pos = 0, neg = 0;
            int start = returns.Length - window;
            for (int i = start; i < returns.Length; i++)
            {
                if (returns[i] > 0) pos++;
                else if (returns[i] < 0) neg++;
            }
            int total = pos + neg;
            if (total == 0) return 0.0;
            double pPos = (double)pos / total;
            double pNeg = (double)neg / total;
            double entropy = 0.0;
            if (pPos > 0) entropy -= pPos * Math.Log(pPos);
            if (pNeg > 0) entropy -= pNeg * Math.Log(pNeg);
            return entropy / Math.Log(2.0);
        }
    }

    /// <summary>
    /// Efficient circular buffer implementation for real-time processing
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly int _capacity;
        private int _start = 0;
        private int _count = 0;

        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
        }

        public int Count => _count;
        public int Capacity => _capacity;

        public void Add(T item)
        {
            _buffer[(_start + _count) % _capacity] = item;
            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                _start = (_start + 1) % _capacity;
            }
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[(_start + index) % _capacity];
            }
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }

        public IEnumerable<T> TakeLast(int count)
        {
            int actualCount = Math.Min(count, _count);
            for (int i = _count - actualCount; i < _count; i++)
            {
                yield return this[i];
            }
        }

        public IEnumerable<T> Skip(int count)
        {
            for (int i = Math.Max(0, count); i < _count; i++)
            {
                yield return this[i];
            }
        }

        public IEnumerable<T> Take(int count)
        {
            int actualCount = Math.Min(count, _count);
            for (int i = 0; i < actualCount; i++)
            {
                yield return this[i];
            }
        }

        public double Average()
        {
            if (_count == 0) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < _count; i++)
            {
                if (this[i] is double d) sum += d;
            }
            return sum / _count;
        }

        public T Max()
        {
            if (_count == 0) return default(T);
            T max = this[0];
            for (int i = 1; i < _count; i++)
            {
                if (Comparer<T>.Default.Compare(this[i], max) > 0)
                    max = this[i];
            }
            return max;
        }

        public T Min()
        {
            if (_count == 0) return default(T);
            T min = this[0];
            for (int i = 1; i < _count; i++)
            {
                if (Comparer<T>.Default.Compare(this[i], min) < 0)
                    min = this[i];
            }
            return min;
        }
    }

    /// <summary>
    /// Usage example and performance comparison
    /// </summary>
    public static class OptimalTCNUsageExample
    {
        public static void ExampleUsage()
        {
            // Configuration for different trading styles
            var scalperConfig = new OptimalTCNConfig
            {
                VelocityWindows = new[] { 1, 2, 3, 5, 10 },    // Fast reactions
                RatioWindows = new[] { 3, 5, 10, 20 },         // Short-term focus
                StatisticalWindows = new[] { 10, 20 },         // Quick statistics
                IncludeHigherMoments = false                   // Keep it simple
            };

            var swingConfig = new OptimalTCNConfig
            {
                VelocityWindows = new[] { 5, 10, 20, 50 },     // Slower reactions
                RatioWindows = new[] { 10, 20, 50, 100 },      // Longer-term context
                StatisticalWindows = new[] { 20, 50, 100 },    // Rich statistics
                IncludeHigherMoments = true                    // Full feature set
            };

            var engine = new OptimalTCNSpikeEngine(scalperConfig);

            // Process bars in real-time
            foreach (var bar in GetMarketData())
            {
                var features = engine.ComputeNext(bar);

                if (features.IsReady)
                {
                    // Feed to your TCN model
                    var prediction = YourTCNModel.Predict(features);

                    // Make trading decisions based on raw ML output
                    if (prediction.SpikeProbability > 0.7)
                    {
                        // Take position
                    }
                }
            }
        }

        private static IEnumerable<OhlcvBar> GetMarketData()
        {
            // Your data source implementation
            throw new NotImplementedException("Implement your data source here");
        }

        private static class YourTCNModel
        {
            public static dynamic Predict(OptimalTCNFeatures features)
            {
                // Your TCN model implementation
                throw new NotImplementedException("Implement your TCN model here");
            }
        }
    }

    /// <summary>
    /// Performance optimizations and validation
    /// </summary>
    public static class OptimalTCNValidator
    {
        public static void ValidateFeatures(OptimalTCNFeatures features)
        {
            var properties = typeof(OptimalTCNFeatures).GetProperties()
                .Where(p => p.PropertyType == typeof(double))
                .ToList();

            foreach (var prop in properties)
            {
                var value = (double)prop.GetValue(features)!;

                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new InvalidOperationException($"Invalid feature value: {prop.Name} = {value}");
                }
            }
        }

        public static void LogFeatureStatistics(OptimalTCNFeatures features)
        {
            // Implementation for feature drift detection and monitoring
            // Log to your monitoring system for production use
        }
    }



