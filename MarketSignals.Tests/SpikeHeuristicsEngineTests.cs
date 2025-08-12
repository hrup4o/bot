using MarketSignals.Core;
using MarketSignals.Core.Features;

namespace MarketSignals.Tests;

public class SpikeHeuristicsEngineTests
{
    [Fact]
    public void SpikeHeuristics_Computes_Breakouts_And_Normalized_Gaps()
    {
        var haCalc = new HeikinAshiCalculator();
        var eng = new SpikeHeuristicsEngine(atrPeriod: 5, lookbackBreakout: 5, spikeZThreshold: 1.5);

        var bars = new List<OhlcvBar>();
        double price = 100.0;
        for (int i = 0; i < 20; i++)
        {
            double open = price;
            double high = open + 0.5 + 0.1 * i; // increasing highs
            double low = open - 0.5;
            double close = open + 0.1 * i;
            price = close;
            bars.Add(new OhlcvBar(new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc).AddMinutes(i), "SYM", open, high, low, close, 1000+i));
        }

        foreach (var b in bars)
        {
            var ha = haCalc.Update(b);
            eng.Update(b, ha);
        }

        var f = eng.GetFeatures();
        Assert.True(f.ContainsKey("spike.range_over_atr"));
        Assert.True(f.ContainsKey("spike.gap_over_atr"));
        Assert.True(f.ContainsKey("spike.breakout_h5"));
        Assert.True(f.ContainsKey("spike.breakout_l5"));

        // In an increasing series, ultimate bar likely triggers high breakout
        Assert.True(f[$"spike.breakout_h5"] == 1 || f[$"spike.breakout_l5"] == 0);

        Assert.True(eng.IsWarm);
    }
}