namespace MarketSignals.Core.Features;

public interface IFeatureEngine
{
    string Name { get; }

    int WarmupBars { get; }

    bool IsWarm { get; }

    void Reset();

    void Update(in OhlcvBar bar, in HeikinAshiBar haBar);

    IReadOnlyDictionary<string, double> GetFeatures();
}