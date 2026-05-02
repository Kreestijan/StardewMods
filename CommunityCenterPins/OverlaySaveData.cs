namespace CommunityCenterPins;

internal sealed class CommunityCenterPinsSaveData
{
    public List<OverlayPinData> Pins { get; set; } = new();
}

internal sealed class OverlayPinData
{
    public int BundleIndex { get; set; }

    public string BundleName { get; set; } = string.Empty;

    public int RemainingSlots { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public List<RequirementSaveData> Requirements { get; set; } = new();
}

internal sealed class RequirementSaveData
{
    public string Name { get; set; } = string.Empty;

    public int Required { get; set; }

    public string QualifiedItemId { get; set; } = string.Empty;

    public int Quality { get; set; }

    public string? PreservesId { get; set; }
}

internal sealed record BundleRequirementLine(
    string Name,
    int Required,
    string QualifiedItemId,
    int Quality,
    string? PreservesId
);

internal sealed record BundleSnapshot(int BundleIndex, string BundleName, int RemainingSlots, List<BundleRequirementLine> Requirements);
