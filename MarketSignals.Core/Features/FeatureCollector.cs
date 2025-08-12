namespace MarketSignals.Core.Features;

public static class FeatureCollector
{
    public static FeatureRecord Collect(
        DateTime timestamp,
        string symbol,
        string featureSetVersion,
        params IReadOnlyDictionary<string, double>[] parts)
    {
        var merged = new Dictionary<string, double>(capacity: parts.Sum(p => p.Count));
        foreach (var p in parts)
        {
            foreach (var kv in p)
            {
                // Last write wins if duplicate keys appear; enforce namespacing to avoid it
                merged[kv.Key] = kv.Value;
            }
        }
        return new FeatureRecord(timestamp, symbol, featureSetVersion, merged);
    }
}