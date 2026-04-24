using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace SpouseWarp;

internal sealed class SpouseWarpWidget
{
    private static readonly Rectangle SpeechBubbleSourceRect = new(141, 465, 20, 24);

    private const int BasePortraitSize = 48;
    private const int BaseDecorationSlotSize = 36;
    private const int BaseDecorationDrawSize = 28;
    private const int BaseSosButtonSize = 32;
    private const int BaseWidgetPadding = 16;
    private const int BaseSlotGap = 4;
    private const int BaseRowGap = 6;
    private const int BaseColumnGap = 14;
    private const int BaseBorderPadding = 2;
    private const int BaseButtonGap = 8;
    private const int RowsPerColumn = 10;

    private readonly DecorationCatalog decorations;
    private readonly WarpTargetResolver targetResolver;
    private readonly ConfigManager configManager;
    private readonly Texture2D sosButtonTexture;

    public SpouseWarpWidget(DecorationCatalog decorations, WarpTargetResolver targetResolver, ConfigManager configManager, Texture2D sosButtonTexture)
    {
        this.decorations = decorations;
        this.targetResolver = targetResolver;
        this.configManager = configManager;
        this.sosButtonTexture = sosButtonTexture;
    }

    public void DrawRows(InventoryPage page, SpriteBatch spriteBatch)
    {
        WidgetLayout layout = this.BuildLayout(page);
        if (layout.SosButtonBounds.HasValue)
        {
            this.DrawSosButton(spriteBatch, layout.SosButtonBounds.Value);
        }

        foreach (WidgetRow row in layout.Rows)
        {
            this.DrawRow(spriteBatch, row);
        }

        if (this.configManager.Config.DebugHitboxes)
        {
            this.DrawDebugHitboxes(spriteBatch, layout);
        }
    }

    public void DrawTooltip(InventoryPage page, SpriteBatch spriteBatch)
    {
        WidgetLayout layout = this.BuildLayout(page);
        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);

        if (layout.SosButtonBounds.HasValue && layout.SosButtonBounds.Value.Contains(mouseX, mouseY))
        {
            IClickableMenu.drawHoverText(spriteBatch, "Emergency warp home", Game1.smallFont);
            return;
        }

        WidgetRow? hoveredRow = layout.Rows.FirstOrDefault(row => row.PortraitBounds.Contains(mouseX, mouseY));
        if (hoveredRow is not null)
        {
            IClickableMenu.drawHoverText(spriteBatch, $"Warp to {hoveredRow.Target.DisplayName}", Game1.smallFont);
        }
    }

    public bool TryGetClickedPortrait(int mouseX, int mouseY, out WarpTarget? target)
    {
        if (Game1.activeClickableMenu is not GameMenu gameMenu
            || gameMenu.currentTab != GameMenu.inventoryTab
            || gameMenu.pages[gameMenu.currentTab] is not InventoryPage page)
        {
            target = null;
            return false;
        }

        foreach (WidgetRow row in this.BuildLayout(page).Rows)
        {
            if (row.PortraitBounds.Contains(mouseX, mouseY))
            {
                target = row.Target;
                return true;
            }
        }

        target = null;
        return false;
    }

    public bool TryCycleDecoration(int mouseX, int mouseY)
    {
        if (Game1.activeClickableMenu is not GameMenu gameMenu
            || gameMenu.currentTab != GameMenu.inventoryTab
            || gameMenu.pages[gameMenu.currentTab] is not InventoryPage page)
        {
            return false;
        }

        foreach (WidgetRow row in this.BuildLayout(page).Rows)
        {
            if (!row.DecorationBounds.Contains(mouseX, mouseY))
            {
                continue;
            }

            string nextId = this.decorations.GetNext(this.GetSelectedDecoration(row.Target));
            this.configManager.Config.Decorations[row.Target.Key] = nextId;
            this.configManager.Save();
            Game1.playSound("shwip");
            return true;
        }

        return false;
    }

    public bool IsSosButtonClicked(int mouseX, int mouseY)
    {
        if (!this.configManager.Config.EnableSosButton
            || Game1.activeClickableMenu is not GameMenu gameMenu
            || gameMenu.currentTab != GameMenu.inventoryTab
            || gameMenu.pages[gameMenu.currentTab] is not InventoryPage page)
        {
            return false;
        }

        Rectangle? bounds = this.BuildLayout(page).SosButtonBounds;
        return bounds.HasValue && bounds.Value.Contains(mouseX, mouseY);
    }

    private void DrawRow(SpriteBatch spriteBatch, WidgetRow row)
    {
        int borderPadding = row.Metrics.BorderPadding;
        float alpha = row.Target.IsSelectable
            ? ((row.Target.Kind == WarpTargetKind.Player && !row.Target.IsOnline) ? 0.45f : 1f)
            : 0.45f;

        IClickableMenu.drawTextureBox(
            spriteBatch,
            row.PortraitBounds.X - borderPadding,
            row.PortraitBounds.Y - borderPadding,
            row.PortraitBounds.Width + borderPadding * 2,
            row.PortraitBounds.Height + borderPadding * 2,
            Color.White * 0.85f
        );
        this.DrawDecorationFrame(spriteBatch, row.DecorationBounds);

        this.DrawPortrait(spriteBatch, row.Target, row.PortraitBounds, alpha);
        this.DrawDecoration(spriteBatch, row.Target, row.DecorationBounds, row.Metrics.DecorationDrawSize);
    }

    private void DrawSosButton(SpriteBatch spriteBatch, Rectangle bounds)
    {
        spriteBatch.Draw(this.sosButtonTexture, bounds, Color.White);
    }

    private void DrawPortrait(SpriteBatch spriteBatch, WarpTarget target, Rectangle bounds, float alpha)
    {
        if (target.Kind == WarpTargetKind.Player)
        {
            Farmer farmer = target.Farmer!;
            bool oldIsDrawingForUi = FarmerRenderer.isDrawingForUI;
            FarmerRenderer.isDrawingForUI = true;
            try
            {
                farmer.FarmerRenderer.drawMiniPortrat(
                    spriteBatch,
                    new Vector2(bounds.X, bounds.Y + this.GetPortraitVerticalOffset(bounds.Height)),
                    1f,
                    bounds.Width / 16f,
                    farmer.FacingDirection,
                    farmer,
                    alpha
                );
            }
            finally
            {
                FarmerRenderer.isDrawingForUI = oldIsDrawingForUi;
            }

            return;
        }

        NPC npc = target.Npc!;
        spriteBatch.Draw(
            npc.Portrait,
            bounds,
            new Rectangle(0, 0, 64, 64),
            Color.White * alpha
        );
    }

    private void DrawDecoration(SpriteBatch spriteBatch, WarpTarget target, Rectangle bounds, int decorationDrawSize)
    {
        Texture2D? texture = this.decorations.GetTexture(this.GetSelectedDecoration(target));
        if (texture is null)
        {
            return;
        }

        Rectangle destination = new(
            bounds.X + (bounds.Width - decorationDrawSize) / 2,
            bounds.Y + (bounds.Height - decorationDrawSize) / 2,
            decorationDrawSize,
            decorationDrawSize
        );

        spriteBatch.Draw(texture, destination, Color.White);
    }

    private void DrawDecorationFrame(SpriteBatch spriteBatch, Rectangle bounds)
    {
        spriteBatch.Draw(Game1.mouseCursors, bounds, SpeechBubbleSourceRect, Color.White);
    }

    private void DrawDebugHitboxes(SpriteBatch spriteBatch, WidgetLayout layout)
    {
        if (layout.SosButtonBounds.HasValue)
        {
            this.DrawDebugRectangle(spriteBatch, layout.SosButtonBounds.Value, Color.Gold);
        }

        foreach (WidgetRow row in layout.Rows)
        {
            this.DrawDebugRectangle(spriteBatch, row.PortraitBounds, row.Target.IsSelectable ? Color.LimeGreen : Color.OrangeRed);
            this.DrawDebugRectangle(spriteBatch, row.DecorationBounds, Color.DeepSkyBlue);
        }
    }

    private void DrawDebugRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        Color fillColor = color * 0.2f;
        Color borderColor = color * 0.9f;

        spriteBatch.Draw(Game1.staminaRect, bounds, fillColor);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), borderColor);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), borderColor);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), borderColor);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), borderColor);
    }

    private string? GetSelectedDecoration(WarpTarget target)
    {
        return this.configManager.Config.Decorations.TryGetValue(target.Key, out string? value)
            ? value
            : null;
    }

    private WidgetLayout BuildLayout(InventoryPage page)
    {
        List<WarpTarget> targets = this.targetResolver.BuildTargets(this.configManager.Config);
        List<WidgetRow> rows = new(targets.Count);
        WidgetMetrics metrics = this.GetMetrics();

        int rightEdge = page.organizeButton.bounds.Right;
        int top = page.organizeButton.bounds.Top;

        if (page.trashCan is not null)
        {
            rightEdge = Math.Max(rightEdge, page.trashCan.bounds.Right);
            top = Math.Min(top, page.trashCan.bounds.Top);
        }

        if (page.junimoNoteIcon is not null)
        {
            rightEdge = Math.Max(rightEdge, page.junimoNoteIcon.bounds.Right);
            top = Math.Min(top, page.junimoNoteIcon.bounds.Top);
        }

        int startX = rightEdge + metrics.WidgetPadding + this.configManager.Config.WidgetOffsetX;
        int startY = top + this.configManager.Config.WidgetOffsetY;
        int columnCount = Math.Max(1, (int)Math.Ceiling(targets.Count / (double)RowsPerColumn));
        int contentWidth = metrics.PortraitSize + metrics.SlotGap + metrics.DecorationSlotSize;
        int totalWidth = contentWidth + (columnCount - 1) * (contentWidth + metrics.ColumnGap);
        Rectangle? sosButtonBounds = null;

        if (this.configManager.Config.EnableSosButton)
        {
            int buttonWidth = metrics.SosButtonSize;
            int buttonHeight = metrics.SosButtonSize;
            int buttonX = startX + Math.Max(0, (totalWidth - buttonWidth) / 2);
            int buttonY = targets.Count > 0
                ? startY - buttonHeight - metrics.ButtonGap
                : startY;
            sosButtonBounds = new Rectangle(buttonX, buttonY, buttonWidth, buttonHeight);
        }

        if (targets.Count == 0)
        {
            return new WidgetLayout(rows, sosButtonBounds);
        }

        int rowHeight = Math.Max(metrics.PortraitSize, metrics.DecorationSlotSize) + metrics.RowGap;
        int columnWidth = metrics.PortraitSize + metrics.SlotGap + metrics.DecorationSlotSize + metrics.ColumnGap;

        for (int index = 0; index < targets.Count; index++)
        {
            int column = index / RowsPerColumn;
            int row = index % RowsPerColumn;
            int x = startX + column * columnWidth;
            int y = startY + row * rowHeight;

            rows.Add(new WidgetRow(
                targets[index],
                new Rectangle(x, y, metrics.PortraitSize, metrics.PortraitSize),
                new Rectangle(x + metrics.PortraitSize + metrics.SlotGap, y + (metrics.PortraitSize - metrics.DecorationSlotSize) / 2, metrics.DecorationSlotSize, metrics.DecorationSlotSize),
                metrics
            ));
        }

        return new WidgetLayout(rows, sosButtonBounds);
    }

    private WidgetMetrics GetMetrics()
    {
        int scalePercent = this.configManager.Config.WidgetScalePercent;
        return new WidgetMetrics(
            this.ScaleDimension(BasePortraitSize, scalePercent),
            this.ScaleDimension(BaseDecorationSlotSize, scalePercent),
            this.ScaleDimension(BaseDecorationDrawSize, scalePercent),
            this.ScaleDimension(BaseSosButtonSize, scalePercent),
            this.ScaleDimension(BaseWidgetPadding, scalePercent),
            this.ScaleDimension(BaseSlotGap, scalePercent),
            this.ScaleDimension(BaseRowGap, scalePercent),
            this.ScaleDimension(BaseColumnGap, scalePercent),
            Math.Max(1, this.ScaleDimension(BaseBorderPadding, scalePercent)),
            this.ScaleDimension(BaseButtonGap, scalePercent)
        );
    }

    private int ScaleDimension(int baseValue, int scalePercent)
    {
        return Math.Max(1, (int)Math.Round(baseValue * scalePercent / 100f));
    }

    private int GetPortraitVerticalOffset(int portraitSize)
    {
        return Math.Max(0, (int)Math.Round(portraitSize / 24f));
    }

    private sealed record WidgetRow(WarpTarget Target, Rectangle PortraitBounds, Rectangle DecorationBounds, WidgetMetrics Metrics);

    private sealed record WidgetLayout(List<WidgetRow> Rows, Rectangle? SosButtonBounds);

    private readonly record struct WidgetMetrics(
        int PortraitSize,
        int DecorationSlotSize,
        int DecorationDrawSize,
        int SosButtonSize,
        int WidgetPadding,
        int SlotGap,
        int RowGap,
        int ColumnGap,
        int BorderPadding,
        int ButtonGap
    );
}
