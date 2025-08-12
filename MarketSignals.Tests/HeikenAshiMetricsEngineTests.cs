using MarketSignals.Core;
using MarketSignals.Core.Features;

namespace MarketSignals.Tests;

public class HeikenAshiMetricsEngineTests
{
    [Fact]
    public void HeikenAshiMetrics_Computes_Core_Features()
    {
        var haCalc = new HeikinAshiCalculator();
        var engine = new HeikenAshiMetricsEngine(warmupBars: 3, dojiBodyRatioThreshold: 0.1, wideRangeZThreshold: 1.0);

        var bars = new List<OhlcvBar>
        {
            new(new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc), "EURUSD", 1.1000, 1.1050, 1.0950, 1.1020, 1000),
            new(new DateTime(2024,1,1,0,1,0,DateTimeKind.Utc), "EURUSD", 1.1020, 1.1080, 1.1000, 1.1060, 1200),
            new(new DateTime(2024,1,1,0,2,0,DateTimeKind.Utc), "EURUSD", 1.1060, 1.1090, 1.1010, 1.1025, 1100),
            new(new DateTime(2024,1,1,0,3,0,DateTimeKind.Utc), "EURUSD", 1.1025, 1.1030, 1.0980, 1.0990, 900),
        };

        foreach (var b in bars)
        {
            var ha = haCalc.Update(b);
            engine.Update(b, ha);
            var f = engine.GetFeatures();

            Assert.True(f.ContainsKey("ha.body"));
            Assert.True(f.ContainsKey("ha.range"));
            Assert.True(f.ContainsKey("ha.upper_wick"));
            Assert.True(f.ContainsKey("ha.lower_wick"));
            Assert.True(f.ContainsKey("ha.body_ratio"));
            Assert.True(f.ContainsKey("ha.zscore_range"));
            Assert.True(f.ContainsKey("ha.is_bullish"));
            Assert.True(f.ContainsKey("ha.is_doji"));
            Assert.True(f.ContainsKey("ha.is_wide_range"));
        }

        Assert.True(engine.IsWarm);
    }

    [Fact]
    public void FeatureCollector_Merges_Parts()
    {
        var rec = FeatureCollector.Collect(
            timestamp: new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc),
            symbol: "EURUSD",
            featureSetVersion: "v1",
            new Dictionary<string,double>{{"ha.body", 0.001}},
            new Dictionary<string,double>{{"ind.rsi14", 55.0}}
        );

        Assert.Equal("EURUSD", rec.Symbol);
        Assert.Equal("v1", rec.FeatureSetVersion);
        Assert.True(rec.Values.ContainsKey("ha.body"));
        Assert.True(rec.Values.ContainsKey("ind.rsi14"));
    }
}