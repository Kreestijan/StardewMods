using Microsoft.Xna.Framework;

namespace CustomChestSize;

internal sealed class ChestMenuLayoutState
{
    public ChestMenuLayoutState(int overlayAnchorY, int chestPanelTop)
    {
        this.OverlayAnchorY = overlayAnchorY;
        this.ChestPanelTop = chestPanelTop;
    }

    public int OverlayAnchorY { get; }

    public int ChestPanelTop { get; }

    public Rectangle ChestPanelBounds { get; set; }

    public int UnlimitedStorageSearchAppliedXOffset { get; set; }

    public int UnlimitedStorageSearchAppliedYOffset { get; set; }

    public int UnlimitedStorageSearchAppliedLeftOffset { get; set; }

    public int UnlimitedStorageSearchAppliedRightOffset { get; set; }
}
