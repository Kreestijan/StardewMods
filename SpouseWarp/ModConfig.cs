namespace SpouseWarp;

internal sealed class ModConfig
{
    public bool RequiresMarriage { get; set; } = true;

    public bool IgnoreLocationUnlocks { get; set; }

    public bool EnableSosButton { get; set; }

    public bool DebugHitboxes { get; set; }

    public int WidgetScalePercent { get; set; } = 100;

    public int WarpCostGold { get; set; } = 500;

    public int CooldownSeconds { get; set; } = 5;

    public int WidgetOffsetX { get; set; } = 52;

    public int WidgetOffsetY { get; set; }

    public Dictionary<string, bool> ShowNPCs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Decorations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
