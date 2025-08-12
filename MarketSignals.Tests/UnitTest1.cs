using MarketSignals.Core;

namespace MarketSignals.Tests;

public class HeikinAshiCalculatorTests
{
    [Fact]
    public void HeikinAshi_Produces_Deterministic_Sequence()
    {
        var calc = new HeikinAshiCalculator();
        var bars = new List<OhlcvBar>
        {
            new(new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc), "EURUSD", 1.1000, 1.1050, 1.0950, 1.1020, 1000),
            new(new DateTime(2024,1,1,0,1,0,DateTimeKind.Utc), "EURUSD", 1.1020, 1.1080, 1.1000, 1.1060, 1200),
            new(new DateTime(2024,1,1,0,2,0,DateTimeKind.Utc), "EURUSD", 1.1060, 1.1090, 1.1010, 1.1025, 1100),
            new(new DateTime(2024,1,1,0,3,0,DateTimeKind.Utc), "EURUSD", 1.1025, 1.1030, 1.0980, 1.0990, 900),
        };

        var ha = new List<HeikinAshiBar>();
        foreach (var b in bars)
        {
            ha.Add(calc.Update(b));
        }

        Assert.Equal(bars.Count, ha.Count);

        // First bar checks
        var ha0 = ha[0];
        var ha0Close = (bars[0].Open + bars[0].High + bars[0].Low + bars[0].Close) / 4.0;
        var ha0Open = (bars[0].Open + bars[0].Close) / 2.0; // first init rule
        Assert.Equal(ha0Close, ha0.Close, 8);
        Assert.Equal(ha0Open, ha0.Open, 8);
        Assert.Equal(Math.Max(bars[0].High, Math.Max(ha0Open, ha0Close)), ha0.High, 8);
        Assert.Equal(Math.Min(bars[0].Low, Math.Min(ha0Open, ha0Close)), ha0.Low, 8);

        // Second bar open should be avg of previous HA open/close
        var ha1 = ha[1];
        var expectedHa1Open = (ha0.Open + ha0.Close) / 2.0;
        Assert.Equal(expectedHa1Open, ha1.Open, 8);

        // Monotonic extremes bounds check
        foreach (var i in Enumerable.Range(0, ha.Count))
        {
            var b = bars[i];
            var h = ha[i];
            Assert.True(h.High >= h.Open && h.High >= h.Close && h.High >= b.High - 1e-12);
            Assert.True(h.Low <= h.Open && h.Low <= h.Close && h.Low <= b.Low + 1e-12);
        }
    }
}