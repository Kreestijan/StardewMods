using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CommunityCenterPins;

internal sealed class BundlePinOverlay
{
    private const int HeaderHeight = 40;
    private const int Padding = 16;
    private const int CloseButtonSize = 24;
    private const int IconSlotSize = 52;
    private const int IconGap = 12;
    private const int ContentTopPadding = 6;
    private const int ContentBottomPadding = 12;
    private const int EmptyHeight = 36;
    private const float BaseItemDrawScale = 0.75f;
    private const int ItemVerticalLift = 8;

    public BundlePinOverlay(int bundleIndex, string bundleName, List<BundleRequirementLine> requirements, Vector2 position)
    {
        this.BundleIndex = bundleIndex;
        this.BundleName = bundleName;
        this.Requirements = requirements;
        this.Position = position;
        this.RemainingSlots = requirements.Count;
    }

    public int BundleIndex { get; }

    public string BundleName { get; private set; }

    public int RemainingSlots { get; private set; }

    public List<BundleRequirementLine> Requirements { get; private set; }

    public Vector2 Position { get; private set; }

    public Rectangle GetBounds(float scale)
    {
        int scaledPadding = Scale(Padding, scale);
        int scaledHeaderHeight = Scale(HeaderHeight, scale);
        int scaledCloseButtonSize = Scale(CloseButtonSize, scale);
        int scaledIconSlotSize = Scale(IconSlotSize, scale);
        int scaledIconGap = Scale(IconGap, scale);
        int titleWidth = (int)Math.Ceiling(Game1.smallFont.MeasureString(this.GetDisplayTitle()).X);
        int iconsWidth = this.Requirements.Count > 0
            ? this.Requirements.Count * scaledIconSlotSize + Math.Max(0, this.Requirements.Count - 1) * scaledIconGap
            : scaledIconSlotSize;
        int width = Math.Max(
            scaledCloseButtonSize + scaledPadding * 2 + 8,
            Math.Max(iconsWidth, titleWidth) + scaledPadding * 2
        );
        int height = scaledHeaderHeight
            + Scale(ContentTopPadding, scale)
            + (this.Requirements.Count > 0 ? scaledIconSlotSize : Scale(EmptyHeight, scale))
            + Scale(ContentBottomPadding, scale);

        return new Rectangle((int)this.Position.X, (int)this.Position.Y, width, height);
    }

    public Rectangle GetCloseButtonBounds(float scale)
    {
        Rectangle bounds = this.GetBounds(scale);
        int size = Scale(CloseButtonSize, scale);
        int inset = Scale(4, scale);
        return new Rectangle(bounds.Right - size - inset, bounds.Y + inset, size, size);
    }

    public bool Contains(Point point, float scale)
    {
        return this.GetBounds(scale).Contains(point);
    }

    public bool ContainsBody(Point point, float scale)
    {
        return this.Contains(point, scale) && !this.GetCloseButtonBounds(scale).Contains(point);
    }

    public void SetPosition(Vector2 position)
    {
        this.Position = position;
    }

    public void ClampToViewport()
    {
        this.ClampToViewport(1f);
    }

    public void ClampToViewport(float scale)
    {
        Rectangle bounds = this.GetBounds(scale);
        int maxX = Math.Max(0, Game1.uiViewport.Width - bounds.Width);
        int maxY = Math.Max(0, Game1.uiViewport.Height - bounds.Height);

        this.Position = new Vector2(
            Math.Clamp((int)this.Position.X, 0, maxX),
            Math.Clamp((int)this.Position.Y, 0, maxY)
        );
    }

    public void UpdateContent(BundleSnapshot snapshot)
    {
        this.BundleName = snapshot.BundleName;
        this.RemainingSlots = snapshot.RemainingSlots;
        this.Requirements = snapshot.Requirements;
    }

    public OverlayPinData ToSaveData()
    {
        return new OverlayPinData
        {
            BundleIndex = this.BundleIndex,
            BundleName = this.BundleName,
            RemainingSlots = this.RemainingSlots,
            X = (int)this.Position.X,
            Y = (int)this.Position.Y,
            Requirements = this.Requirements
                .Select(line => new RequirementSaveData
                {
                    Name = line.Name,
                    Required = line.Required,
                    QualifiedItemId = line.QualifiedItemId,
                    Quality = line.Quality,
                    PreservesId = line.PreservesId
                })
                .ToList()
        };
    }

    public bool TryGetRequirementAtPoint(Point point, float scale, out BundleRequirementLine? requirement)
    {
        requirement = null;
        for (int i = 0; i < this.Requirements.Count; i++)
        {
            if (!this.GetRequirementBounds(i, scale).Contains(point))
            {
                continue;
            }

            requirement = this.Requirements[i];
            return true;
        }

        return false;
    }

    public void Draw(SpriteBatch spriteBatch, BundleInfoResolver bundleResolver, float scale)
    {
        Rectangle bounds = this.GetBounds(scale);
        Rectangle closeBounds = this.GetCloseButtonBounds(scale);
        int scaledPadding = Scale(Padding, scale);
        int scaledHeaderHeight = Scale(HeaderHeight, scale);
        int scaledIconSlotSize = Scale(IconSlotSize, scale);
        int scaledIconGap = Scale(IconGap, scale);
        int scaledContentTopPadding = Scale(ContentTopPadding, scale);
        float itemDrawScale = BaseItemDrawScale * scale;
        int itemDrawSize = Math.Max(1, (int)Math.Round(64f * itemDrawScale));

        IClickableMenu.drawTextureBox(
            spriteBatch,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            Color.White
        );

        Utility.drawTextWithShadow(
            spriteBatch,
            "x",
            Game1.smallFont,
            new Vector2(closeBounds.X + Scale(7, scale), closeBounds.Y + Scale(1, scale)),
            Color.Red
        );

        Utility.drawTextWithShadow(
            spriteBatch,
            this.GetDisplayTitle(),
            Game1.smallFont,
            new Vector2(bounds.X + (bounds.Width - Game1.smallFont.MeasureString(this.GetDisplayTitle()).X) / 2f, bounds.Y + Scale(10, scale)),
            Game1.textColor
        );

        if (this.Requirements.Count == 0)
        {
            return;
        }

        int totalIconsWidth = this.Requirements.Count * scaledIconSlotSize + Math.Max(0, this.Requirements.Count - 1) * scaledIconGap;
        int iconX = bounds.X + Math.Max(scaledPadding, (bounds.Width - totalIconsWidth) / 2);
        int iconY = bounds.Y + scaledHeaderHeight + scaledContentTopPadding;

        foreach (BundleRequirementLine line in this.Requirements)
        {
            if (bundleResolver.TryCreateItem(line, out Item? item) && item is not null)
            {
                int itemX = iconX + Math.Max(0, (scaledIconSlotSize - itemDrawSize) / 2);
                int itemY = iconY + Math.Max(0, (scaledIconSlotSize - itemDrawSize) / 2) - Scale(ItemVerticalLift, scale);
                item.drawInMenu(spriteBatch, new Vector2(itemX, itemY), itemDrawScale);
            }
            iconX += scaledIconSlotSize + scaledIconGap;
        }
    }

    public void DrawDebugHitboxes(SpriteBatch spriteBatch, float scale)
    {
        this.DrawDebugRectangle(spriteBatch, this.GetBounds(scale), Color.LimeGreen * 0.3f);
        this.DrawDebugRectangle(spriteBatch, this.GetCloseButtonBounds(scale), Color.Red * 0.45f);

        for (int i = 0; i < this.Requirements.Count; i++)
        {
            this.DrawDebugRectangle(spriteBatch, this.GetRequirementBounds(i, scale), Color.DeepSkyBlue * 0.35f);
        }
    }

    private Rectangle GetRequirementBounds(int index, float scale)
    {
        Rectangle bounds = this.GetBounds(scale);
        int scaledPadding = Scale(Padding, scale);
        int scaledHeaderHeight = Scale(HeaderHeight, scale);
        int scaledIconSlotSize = Scale(IconSlotSize, scale);
        int scaledIconGap = Scale(IconGap, scale);
        int scaledContentTopPadding = Scale(ContentTopPadding, scale);
        int totalIconsWidth = this.Requirements.Count * scaledIconSlotSize + Math.Max(0, this.Requirements.Count - 1) * scaledIconGap;
        int iconX = bounds.X + Math.Max(scaledPadding, (bounds.Width - totalIconsWidth) / 2) + index * (scaledIconSlotSize + scaledIconGap);

        return new Rectangle(
            iconX,
            bounds.Y + scaledHeaderHeight + scaledContentTopPadding,
            scaledIconSlotSize,
            scaledIconSlotSize
        );
    }

    private void DrawDebugRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        spriteBatch.Draw(Game1.staminaRect, bounds, color);
    }

    private static int Scale(int value, float scale)
    {
        return Math.Max(1, (int)Math.Round(value * scale));
    }

    private string GetDisplayTitle()
    {
        return $"{this.BundleName} ({this.RemainingSlots}S)";
    }
}
