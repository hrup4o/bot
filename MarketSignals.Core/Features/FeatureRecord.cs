namespace MarketSignals.Core.Features;

public sealed class FeatureRecord
{
    public DateTime Timestamp { get; }
    public string Symbol { get; }
    public string FeatureSetVersion { get; }
    public IReadOnlyDictionary<string, double> Values => _values;

    private readonly Dictionary<string, double> _values;

    public FeatureRecord(DateTime timestamp, string symbol, string featureSetVersion, IDictionary<string, double> values)
    {
        Timestamp = timestamp;
        Symbol = symbol;
        FeatureSetVersion = featureSetVersion;
        _values = new Dictionary<string, double>(values);
    }

    public bool TryGet(string key, out double value) => _values.TryGetValue(key, out value);
}