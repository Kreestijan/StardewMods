namespace CustomChestSize;

internal sealed class ModConfig
{
    public int RegularChestColumns { get; set; } = 16;

    public int RegularChestRows { get; set; } = 7;

    public int BigChestColumns { get; set; } = 24;

    public int BigChestRows { get; set; } = 12;

    public int ChestBackgroundHeightOffset { get; set; }

    public int InventoryPanelGapOffset { get; set; } = -12;

    public int InventoryBackgroundTopOffset { get; set; } = 28;

    public int InventoryBackgroundBottomOffset { get; set; }

    public int ChestsAnywhereWidgetXOffset { get; set; }

    public int ChestsAnywhereWidgetYOffset { get; set; } = 28;

    public int ColorPickerXOffset { get; set; } = 328;

    public int ColorPickerYOffset { get; set; } = 48;

    public int UnlimitedStorageSearchXOffset { get; set; } = 200;

    public int UnlimitedStorageSearchYOffset { get; set; } = -16;

    public int UnlimitedStorageSearchLeftOffset { get; set; } = -112;

    public int UnlimitedStorageSearchRightOffset { get; set; } = -160;
}
