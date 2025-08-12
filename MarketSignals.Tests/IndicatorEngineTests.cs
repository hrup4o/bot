using MarketSignals.Core;
using MarketSignals.Core.Features;

namespace MarketSignals.Tests;

public class IndicatorEngineTests
{
    [Fact]
    public void IndicatorEngine_Computes_RSI_ATR_MACD_BBands()
    {
        var haCalc = new HeikinAshiCalculator();
        var eng = new IndicatorEngine(rsiPeriod: 5, atrPeriod: 5, emaFastPeriod: 3, emaSlowPeriod: 6, emaSignalPeriod: 3, bbPeriod: 5, bbK: 2.0);

        // Generate a small trending series
        var bars = new List<OhlcvBar>();
        double price = 100.0;
        var rand = new Random(42);
        for (int i = 0; i < 40; i++)
        {
            double open = price;
            double high = open + rand.NextDouble();
            double low = open - rand.NextDouble();
            price = open + (rand.NextDouble() - 0.4); // slight upward drift
            double close = price;
            bars.Add(new OhlcvBar(new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc).AddMinutes(i), "SYNTH", open, Math.Max(high, close), Math.Min(low, close), close, 1000+i));
        }

        foreach (var b in bars)
        {
            var ha = haCalc.Update(b);
            eng.Update(b, ha);
        }

        var f = eng.GetFeatures();
        Assert.True(f.ContainsKey("ind.rsi5"));
        Assert.True(f.ContainsKey("ind.atr5"));
        Assert.True(f.ContainsKey("ind.macd"));
        Assert.True(f.ContainsKey("ind.macd_signal"));
        Assert.True(f.ContainsKey("ind.macd_hist"));

        // RSI must be within [0,100]
        Assert.InRange(f["ind.rsi5"], 0.0, 100.0);

        // Bollinger keys
        Assert.Contains(f, kv => kv.Key.Contains(".mid"));
        Assert.Contains(f, kv => kv.Key.Contains(".upper"));
        Assert.Contains(f, kv => kv.Key.Contains(".lower"));
        Assert.Contains(f, kv => kv.Key.Contains(".width"));
        Assert.Contains(f, kv => kv.Key.Contains(".pctb"));

        Assert.True(eng.IsWarm);
    }
}