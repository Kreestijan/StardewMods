using CutsceneMaker.Importer;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public sealed class EventPickerPanel
{
    private const int PanelWidth = 1180;
    private const int PanelHeight = 560;
    private const int RowHeight = 46;
    private const int ButtonHeight = 36;

    private readonly Action close;
    private readonly Action<CutsceneData> importCutscene;
    private readonly List<(Rectangle Bounds, Action Click)> buttons = new();
    private readonly BoundTextField pathField;
    private readonly List<CutsceneData> importedCutscenes = new();
    private string contentJsonPath = string.Empty;
    private string statusMessage = "Enter a Content Patcher content.json path.";
    private Color statusColor = Color.DimGray;
    private int selectedIndex = -1;

    public EventPickerPanel(Action close, Action<CutsceneData> importCutscene, string initialPath)
    {
        this.close = close;
        this.importCutscene = importCutscene;
        this.contentJsonPath = string.IsNullOrWhiteSpace(initialPath)
            ? ModEntry.Instance.ModsDirectoryPath
            : initialPath;
        this.pathField = new BoundTextField(
            () => this.contentJsonPath,
            value => this.contentJsonPath = value.Trim('"').Trim(),
            numbersOnly: false,
            textLimit: 260
        );
    }

    public bool HasSelectedTextField()
    {
        return this.pathField.Selected;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        this.buttons.Clear();
        spriteBatch.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.55f);

        Rectangle bounds = this.GetBounds();
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);

        int x = bounds.X + 28;
        int y = bounds.Y + 24;
        this.DrawLine(spriteBatch, "Import Content Patcher Event", x, y, Game1.textColor);
        this.DrawLine(spriteBatch, "content.json path", x, y + 52, Game1.textColor);

        Rectangle pathBounds = new(x, y + 82, bounds.Width - 168, 44);
        this.pathField.SetBounds(pathBounds);
        this.pathField.Draw(spriteBatch);
        this.buttons.Add((pathBounds, this.pathField.Select));

        this.DrawButton(spriteBatch, new Rectangle(bounds.Right - 140, y + 86, 112, ButtonHeight), "Load", this.LoadEvents);
        this.DrawLine(spriteBatch, this.statusMessage, x, y + 144, this.statusColor);

        int listY = y + 188;
        int listWidth = bounds.Width - 56;
        if (this.importedCutscenes.Count > 0)
        {
            this.DrawLine(spriteBatch, "Select an event to import:", x, listY - 34, Game1.textColor);
        }

        for (int i = 0; i < this.importedCutscenes.Count; i++)
        {
            this.DrawEventRow(spriteBatch, i, new Rectangle(x, listY + i * RowHeight, listWidth, RowHeight - 4));
        }

        Rectangle openButton = new(bounds.Right - 216, bounds.Bottom - 56, 88, ButtonHeight);
        Rectangle cancelButton = new(bounds.Right - 120, bounds.Bottom - 56, 92, ButtonHeight);
        this.DrawButton(spriteBatch, openButton, "Open", this.OpenSelected);
        this.DrawButton(spriteBatch, cancelButton, "Cancel", this.close);
    }

    public void Update()
    {
        this.pathField.Update();
    }

    public void ReceiveLeftClick(int x, int y)
    {
        foreach ((Rectangle bounds, Action click) in this.buttons)
        {
            if (bounds.Contains(x, y))
            {
                click();
                return;
            }
        }

        this.pathField.Selected = false;
    }

    public void ReceiveKeyPress(Keys key)
    {
        if (this.pathField.Selected)
        {
            this.pathField.ReceiveKeyPress(key);
            return;
        }

        if (key == Keys.Escape)
        {
            this.close();
        }
    }

    private void LoadEvents()
    {
        this.pathField.Selected = false;
        this.importedCutscenes.Clear();
        this.selectedIndex = -1;

        if (string.IsNullOrWhiteSpace(this.contentJsonPath))
        {
            this.SetStatus("Path is required.", Color.Red);
            return;
        }

        string importPath = this.ResolveImportPath();
        if (!File.Exists(importPath))
        {
            this.SetStatus("File does not exist.", Color.Red);
            return;
        }

        try
        {
            List<CutsceneData> imported = ContentPackImporter.Import(importPath);
            if (imported.Count == 0)
            {
                this.SetStatus("No Data/Events entries found in that file.", Color.Red);
                return;
            }

            this.importedCutscenes.AddRange(imported);
            this.selectedIndex = 0;
            this.SetStatus(imported.Count == 1 ? "Found 1 event." : $"Found {imported.Count} events.", Color.DarkGreen);
        }
        catch (Exception ex)
        {
            this.SetStatus("Import failed. See SMAPI log for details.", Color.Red);
            ModEntry.Instance.Monitor.Log($"Cutscene Maker import failed: {ex}", StardewModdingAPI.LogLevel.Error);
        }
    }

    private string ResolveImportPath()
    {
        if (File.Exists(this.contentJsonPath))
        {
            return this.contentJsonPath;
        }

        if (Directory.Exists(this.contentJsonPath))
        {
            return Path.Combine(this.contentJsonPath, "content.json");
        }

        return this.contentJsonPath;
    }

    private void OpenSelected()
    {
        if (this.selectedIndex < 0 || this.selectedIndex >= this.importedCutscenes.Count)
        {
            this.SetStatus("Select an event first.", Color.Red);
            return;
        }

        this.importCutscene(this.importedCutscenes[this.selectedIndex]);
        this.close();
    }

    private void DrawEventRow(SpriteBatch spriteBatch, int index, Rectangle bounds)
    {
        CutsceneData cutscene = this.importedCutscenes[index];
        Color color = index == this.selectedIndex ? Color.LightGoldenrodYellow : Color.White;
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, color);

        string label = $"{cutscene.UniqueId} | {cutscene.LocationName} | {cutscene.Triggers.Count} trigger(s)";
        if (label.Length > 82)
        {
            label = label[..79] + "...";
        }

        this.DrawLine(spriteBatch, label, bounds.X + 12, bounds.Y + 9, Game1.textColor);
        this.buttons.Add((bounds, () => this.selectedIndex = index));
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Action click)
    {
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        Vector2 labelSize = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(
            spriteBatch,
            label,
            Game1.smallFont,
            new Vector2(bounds.Center.X - labelSize.X / 2f, bounds.Center.Y - labelSize.Y / 2f),
            Game1.textColor
        );
        this.buttons.Add((bounds, click));
    }

    private void DrawLine(SpriteBatch spriteBatch, string text, int x, int y, Color color)
    {
        Utility.drawTextWithShadow(spriteBatch, text, Game1.smallFont, new Vector2(x, y), color);
    }

    private void SetStatus(string message, Color color)
    {
        this.statusMessage = message;
        this.statusColor = color;
    }

    private Rectangle GetBounds()
    {
        int width = Math.Min(PanelWidth, Math.Max(760, Game1.uiViewport.Width - 96));
        return new Rectangle(
            Game1.uiViewport.Width / 2 - width / 2,
            Game1.uiViewport.Height / 2 - PanelHeight / 2,
            width,
            PanelHeight
        );
    }
}
