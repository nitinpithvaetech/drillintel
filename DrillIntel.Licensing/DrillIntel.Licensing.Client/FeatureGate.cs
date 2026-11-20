namespace DrillIntel.Licensing.Client;

public enum PremiumFeature
{
    NewReportGeneration,
    PremiumCharts
}

/// <summary>
/// UI-layer allow-list for premium features. Safety alarms, data ingestion, and read/export
/// are intentionally not represented here and must not depend on this type.
/// </summary>
public sealed class FeatureGate
{
    private readonly IReadOnlySet<PremiumFeature> _enabledFeatures;

    public FeatureGate(IEnumerable<PremiumFeature> enabledFeatures) =>
        _enabledFeatures = new HashSet<PremiumFeature>(enabledFeatures ?? throw new ArgumentNullException(nameof(enabledFeatures)));

    public bool IsEnabled(PremiumFeature feature) =>
        Enum.IsDefined(feature) && _enabledFeatures.Contains(feature);
}
