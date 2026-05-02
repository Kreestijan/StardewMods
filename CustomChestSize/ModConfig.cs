namespace CustomChestSize;

internal sealed class ModConfig
{
    public int RegularChestColumns { get; set; } = 17;

    public int RegularChestRows { get; set; } = 7;

    public int BigChestColumns { get; set; } = 24;

    public int BigChestRows { get; set; } = 12;

    public int StoneChestColumns { get; set; } = 17;

    public int StoneChestRows { get; set; } = 7;

    public int BigStoneChestColumns { get; set; } = 24;

    public int BigStoneChestRows { get; set; } = 12;

    public int FridgeColumns { get; set; } = 24;

    public int FridgeRows { get; set; } = 12;

    public int MiniFridgeColumns { get; set; } = 24;

    public int MiniFridgeRows { get; set; } = 12;

    public int JunimoChestColumns { get; set; } = 17;

    public int JunimoChestRows { get; set; } = 7;

    public int AutoGrabberColumns { get; set; } = 24;

    public int AutoGrabberRows { get; set; } = 12;

    public int ChestBackgroundHeightOffset { get; set; }

    public int InventoryPanelGapOffset { get; set; } = -12;

    public int InventoryBackgroundTopOffset { get; set; } = 28;

    public int InventoryBackgroundBottomOffset { get; set; }

    public int ChestsAnywhereWidgetXOffset { get; set; }

    public int ChestsAnywhereWidgetYOffset { get; set; } = 28;

    public int ConvenientChestsXOffset { get; set; } = -304;

    public int ConvenientChestsYOffset { get; set; }

    public int CategorizeChestsXOffset { get; set; } = -40;

    public int CategorizeChestsYOffset { get; set; } = -72;

    public int ColorPickerXOffset { get; set; } = 328;

    public int ColorPickerYOffset { get; set; } = 48;

    public int RemoteFridgeStorageXOffset { get; set; } = 64;

    public int RemoteFridgeStorageYOffset { get; set; } = -72;

    public int UnlimitedStorageSearchXOffset { get; set; } = 200;

    public int UnlimitedStorageSearchYOffset { get; set; } = -16;

    public int UnlimitedStorageSearchLeftOffset { get; set; } = -112;

    public int UnlimitedStorageSearchRightOffset { get; set; } = -160;

    public bool DebugLogEnabled { get; set; } = false;

    public bool TintChestUI { get; set; } = false;

    public int TintChestUIOpacity { get; set; } = 75;

    public int TintChestUIPaddingLeft { get; set; } = -40;

    public int TintChestUIPaddingRight { get; set; } = -40;

    public int TintChestUIPaddingTop { get; set; } = -56;

    public int TintChestUIPaddingBottom { get; set; } = -24;
}
