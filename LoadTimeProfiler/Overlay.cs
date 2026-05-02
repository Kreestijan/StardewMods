using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace LoadTimeProfiler;

public sealed class Overlay
{
    private const int PanelWidth = 420;
    private const int OuterPadding = 16;
    private const int InnerPadding = 12;
    private const int RowHeight = 26;
    private const int HeaderHeight = 26;
    private const int TabHeight = 28;
    private const int FooterHeight = 28;

    private readonly Func<ModConfig> getConfig;
    private readonly RuntimeProfiler runtimeProfiler;

    public Overlay(Func<ModConfig> getConfig, RuntimeProfiler runtimeProfiler)
    {
        this.getConfig = getConfig;
        this.runtimeProfiler = runtimeProfiler;
    }

    public bool IsOpen { get; private set; }

    public RuntimeProfiler.ProfileCategory ActiveCategory { get; private set; } = RuntimeProfiler.ProfileCategory.Draw;

    public void Toggle()
    {
        this.IsOpen = !this.IsOpen;
    }

    public void Close()
    {
        this.IsOpen = false;
    }

    public void SwitchTab()
    {
        this.ActiveCategory = this.ActiveCategory == RuntimeProfiler.ProfileCategory.Draw
            ? RuntimeProfiler.ProfileCategory.Update
            : RuntimeProfiler.ProfileCategory.Draw;
    }

    public void HandleLeftClick(int mouseX, int mouseY)
    {
        OverlayLayout layout = this.GetLayout();
        if (layout.DrawTabBounds.Contains(mouseX, mouseY))
        {
            this.ActiveCategory = RuntimeProfiler.ProfileCategory.Draw;
        }
        else if (layout.UpdateTabBounds.Contains(mouseX, mouseY))
        {
            this.ActiveCategory = RuntimeProfiler.ProfileCategory.Update;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.ShouldRender())
        {
            return;
        }

        RuntimeProfiler.OverlaySnapshot snapshot = this.runtimeProfiler.LatestSnapshot;
        OverlayLayout layout = this.GetLayout();

        spriteBatch.Draw(Game1.staminaRect, layout.Bounds, Color.Black * 0.75f);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(layout.Bounds.X, layout.Bounds.Y, layout.Bounds.Width, 2), Color.White * 0.2f);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(layout.Bounds.X, layout.Bounds.Bottom - 2, layout.Bounds.Width, 2), Color.White * 0.2f);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(layout.Bounds.X, layout.Bounds.Y, 2, layout.Bounds.Height), Color.White * 0.2f);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(layout.Bounds.Right - 2, layout.Bounds.Y, 2, layout.Bounds.Height), Color.White * 0.2f);

        Vector2 textPosition = new(layout.Bounds.X + InnerPadding, layout.Bounds.Y + 8);
        spriteBatch.DrawString(Game1.smallFont, "Load Time Profiler", textPosition, Color.White);

        textPosition.Y += HeaderHeight;
        string header = $"FPS: {snapshot.Fps,5:0.0}  |  Frame: {snapshot.FrameTimeMs,5:0.0}ms";
        spriteBatch.DrawString(Game1.smallFont, header, textPosition, Color.Gainsboro);

        DrawTab(spriteBatch, layout.DrawTabBounds, "Draw", this.ActiveCategory == RuntimeProfiler.ProfileCategory.Draw);
        DrawTab(spriteBatch, layout.UpdateTabBounds, "Update", this.ActiveCategory == RuntimeProfiler.ProfileCategory.Update);

        int dividerY = layout.Bounds.Y + HeaderHeight + TabHeight + 18;
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(layout.Bounds.X + InnerPadding, dividerY, layout.Bounds.Width - (InnerPadding * 2), 2), Color.White * 0.15f);

        IReadOnlyList<RuntimeProfiler.OverlayRow> rows = this.ActiveCategory == RuntimeProfiler.ProfileCategory.Draw
            ? snapshot.DrawRows
            : snapshot.UpdateRows;

        int rowStartY = dividerY + 12;
        for (int index = 0; index < rows.Count; index++)
        {
            RuntimeProfiler.OverlayRow row = rows[index];
            float rowY = rowStartY + (index * RowHeight);
            Color rowColor = GetRowColor(row.AverageMs);
            string displayName = row.Name.Contains('.') ? row.Name[(row.Name.IndexOf('.') + 1)..] : row.Name;
            string leftText = $"{index + 1,2}. {displayName}";
            string rightText = $"{row.AverageMs:0.00}ms";

            spriteBatch.DrawString(Game1.smallFont, leftText, new Vector2(layout.Bounds.X + InnerPadding, rowY), rowColor);

            Vector2 rightSize = Game1.smallFont.MeasureString(rightText);
            spriteBatch.DrawString(
                Game1.smallFont,
                rightText,
                new Vector2(layout.Bounds.Right - InnerPadding - rightSize.X, rowY),
                rowColor
            );
        }

        int footerDividerY = layout.Bounds.Bottom - FooterHeight - 10;
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(layout.Bounds.X + InnerPadding, footerDividerY, layout.Bounds.Width - (InnerPadding * 2), 2), Color.White * 0.15f);

        double modTax = this.ActiveCategory == RuntimeProfiler.ProfileCategory.Draw ? snapshot.DrawTaxMs : snapshot.UpdateTaxMs;
        double percentage = snapshot.FrameTimeMs <= 0.0001 ? 0 : (modTax / snapshot.FrameTimeMs) * 100d;
        string footer = $"Overhead: {modTax:0.00}ms / {snapshot.FrameTimeMs:0.0}ms ({percentage:0.0}%)";
        spriteBatch.DrawString(
            Game1.smallFont,
            footer,
            new Vector2(layout.Bounds.X + InnerPadding, footerDividerY + 8),
            Color.Gainsboro
        );
    }

    private static void DrawTab(SpriteBatch spriteBatch, Rectangle bounds, string label, bool active)
    {
        Color background = active ? new Color(70, 95, 120, 220) : new Color(40, 40, 40, 180);
        Color textColor = active ? Color.White : Color.Silver;

        spriteBatch.Draw(Game1.staminaRect, bounds, background);

        Vector2 size = Game1.smallFont.MeasureString(label);
        Vector2 position = new(
            bounds.X + ((bounds.Width - size.X) / 2f),
            bounds.Y + ((bounds.Height - size.Y) / 2f)
        );
        spriteBatch.DrawString(Game1.smallFont, label, position, textColor);
    }

    private bool ShouldRender()
    {
        return this.IsOpen
            && Context.IsWorldReady
            && Game1.currentLocation is not null
            && Game1.gameMode == 3;
    }

    private OverlayLayout GetLayout()
    {
        int topN = this.getConfig().OverlayTopN;
        int height = 130 + (topN * RowHeight);
        int x = Game1.uiViewport.Width - PanelWidth - OuterPadding;
        int y = OuterPadding;
        Rectangle bounds = new(x, y, PanelWidth, height);

        Rectangle drawTabBounds = new(bounds.X + InnerPadding, bounds.Y + HeaderHeight + 28, 84, TabHeight);
        Rectangle updateTabBounds = new(drawTabBounds.Right + 10, drawTabBounds.Y, 96, TabHeight);

        return new OverlayLayout(bounds, drawTabBounds, updateTabBounds);
    }

    private static Color GetRowColor(double averageMs)
    {
        if (averageMs < 1)
        {
            return Color.LightGreen;
        }

        if (averageMs <= 5)
        {
            return Color.Gold;
        }

        return Color.IndianRed;
    }

    private sealed record OverlayLayout(Rectangle Bounds, Rectangle DrawTabBounds, Rectangle UpdateTabBounds);
}
