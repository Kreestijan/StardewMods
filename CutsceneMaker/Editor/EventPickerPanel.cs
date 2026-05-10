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
    private sealed class ImportCandidate
    {
        public CutsceneData Cutscene { get; init; } = CutsceneData.CreateBlank();

        public string SourcePath { get; init; } = string.Empty;
    }

    private const int PanelWidth = 1180;
    private const int PanelHeight = 560;
    private const int RowHeight = 46;
    private const int ButtonHeight = 36;
    private const int MaxScanDepth = 8;
    private const int WheelRows = 3;

    private readonly Action close;
    private readonly Action<CutsceneData> importCutscene;
    private readonly List<(Rectangle Bounds, Action Click)> buttons = new();
    private readonly List<(Rectangle Bounds, Action Click)> folderButtons = new();
    private readonly List<(Rectangle Bounds, string Path)> folderRows = new();
    private readonly BoundTextField pathField;
    private readonly List<ImportCandidate> importedCutscenes = new();
    private string contentJsonPath = string.Empty;
    private string folderBrowserPath = string.Empty;
    private string statusMessage = "Enter a Content Patcher content.json path.";
    private Color statusColor = Color.DimGray;
    private int selectedIndex = -1;
    private int scanDepth = 2;
    private int listScrollIndex;
    private int folderScrollIndex;
    private bool folderBrowserOpen;

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

        Rectangle browseButton = new(bounds.Right - 260, y + 86, 112, ButtonHeight);
        Rectangle loadButton = new(bounds.Right - 140, y + 86, 112, ButtonHeight);
        Rectangle pathBounds = new(x, y + 82, Math.Max(180, browseButton.X - x - 12), 44);
        this.pathField.SetBounds(pathBounds);
        this.pathField.Draw(spriteBatch);

        this.DrawButton(spriteBatch, loadButton, "Load", this.LoadEvents);
        this.DrawButton(spriteBatch, browseButton, "Browse", this.OpenFolderBrowser);
        this.buttons.Add((pathBounds, this.pathField.Select));
        this.DrawButton(spriteBatch, new Rectangle(bounds.Right - 300, y + 136, 72, ButtonHeight), "Depth -", () => this.SetScanDepth(this.scanDepth - 1));
        this.DrawLine(spriteBatch, $"Depth: {this.scanDepth}", bounds.Right - 220, y + 143, Game1.textColor);
        this.DrawButton(spriteBatch, new Rectangle(bounds.Right - 120, y + 136, 92, ButtonHeight), "Depth +", () => this.SetScanDepth(this.scanDepth + 1));
        this.DrawLine(spriteBatch, this.statusMessage, x, y + 144, this.statusColor);

        int listY = y + 188;
        int listWidth = bounds.Width - 56;
        if (this.importedCutscenes.Count > 0)
        {
            this.DrawLine(spriteBatch, "Select an event to import:", x, listY - 34, Game1.textColor);
        }

        int visibleRows = this.GetVisibleRows(bounds, listY);
        this.listScrollIndex = Math.Clamp(this.listScrollIndex, 0, Math.Max(0, this.importedCutscenes.Count - visibleRows));
        for (int visibleIndex = 0; visibleIndex < visibleRows; visibleIndex++)
        {
            int eventIndex = this.listScrollIndex + visibleIndex;
            if (eventIndex >= this.importedCutscenes.Count)
            {
                break;
            }

            this.DrawEventRow(spriteBatch, eventIndex, new Rectangle(x, listY + visibleIndex * RowHeight, listWidth, RowHeight - 4));
        }

        Rectangle openButton = new(bounds.Right - 216, bounds.Bottom - 56, 88, ButtonHeight);
        Rectangle cancelButton = new(bounds.Right - 120, bounds.Bottom - 56, 92, ButtonHeight);
        this.DrawButton(spriteBatch, openButton, "Open", this.OpenSelected);
        this.DrawButton(spriteBatch, cancelButton, "Cancel", this.close);

        if (this.folderBrowserOpen)
        {
            this.DrawFolderBrowser(spriteBatch, bounds);
        }
    }

    public void Update()
    {
        this.pathField.Update();
    }

    public void ReceiveLeftClick(int x, int y)
    {
        if (this.folderBrowserOpen)
        {
            this.ReceiveFolderBrowserClick(x, y);
            return;
        }

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
        if (this.folderBrowserOpen)
        {
            if (key == Keys.Escape)
            {
                this.folderBrowserOpen = false;
            }

            return;
        }

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

    public void ReceiveScrollWheelAction(int direction)
    {
        if (this.folderBrowserOpen)
        {
            this.ScrollFolderBrowser(direction);
            return;
        }

        if (this.importedCutscenes.Count == 0)
        {
            return;
        }

        Rectangle bounds = this.GetBounds();
        int listY = bounds.Y + 24 + 188;
        int visibleRows = this.GetVisibleRows(bounds, listY);
        int delta = direction > 0 ? -WheelRows : WheelRows;
        this.listScrollIndex = Math.Clamp(this.listScrollIndex + delta, 0, Math.Max(0, this.importedCutscenes.Count - visibleRows));
    }

    private void LoadEvents()
    {
        this.pathField.Selected = false;
        this.importedCutscenes.Clear();
        this.selectedIndex = -1;
        this.listScrollIndex = 0;

        if (string.IsNullOrWhiteSpace(this.contentJsonPath))
        {
            this.SetStatus("Path is required.", Color.Red);
            return;
        }

        List<string> importPaths = this.ResolveImportPaths();
        if (importPaths.Count == 0)
        {
            this.SetStatus("No content.json files found at that path/depth.", Color.Red);
            return;
        }

        try
        {
            int importedCount = 0;
            int skippedCount = 0;
            foreach (string importPath in importPaths)
            {
                if (!ContentPackImporter.TryImport(importPath, out List<CutsceneData> imported, out string error))
                {
                    skippedCount++;
                    ModEntry.Instance.Monitor.Log($"Cutscene Maker skipped import file '{importPath}': {error}", StardewModdingAPI.LogLevel.Trace);
                    continue;
                }

                this.importedCutscenes.AddRange(imported.Select(cutscene => new ImportCandidate
                {
                    Cutscene = cutscene,
                    SourcePath = importPath
                }));
                importedCount += imported.Count;
            }

            if (importedCount == 0)
            {
                this.SetStatus("No Data/Events entries found in matched files.", Color.Red);
                return;
            }

            this.selectedIndex = 0;
            string skippedSuffix = skippedCount > 0 ? $" Skipped {skippedCount} non-CP file(s)." : string.Empty;
            this.SetStatus(importedCount == 1 ? $"Found 1 event in {importPaths.Count} file(s).{skippedSuffix}" : $"Found {importedCount} events in {importPaths.Count} file(s).{skippedSuffix}", Color.DarkGreen);
        }
        catch (Exception ex)
        {
            this.SetStatus("Import failed. See SMAPI log for details.", Color.Red);
            ModEntry.Instance.Monitor.Log($"Cutscene Maker import failed: {ex}", StardewModdingAPI.LogLevel.Error);
        }
    }

    private List<string> ResolveImportPaths()
    {
        if (File.Exists(this.contentJsonPath))
        {
            return new List<string> { this.contentJsonPath };
        }

        if (Directory.Exists(this.contentJsonPath))
        {
            return FindContentJsonFiles(this.contentJsonPath, this.scanDepth).ToList();
        }

        return new List<string>();
    }

    private void OpenSelected()
    {
        if (this.selectedIndex < 0 || this.selectedIndex >= this.importedCutscenes.Count)
        {
            this.SetStatus("Select an event first.", Color.Red);
            return;
        }

        this.importCutscene(this.importedCutscenes[this.selectedIndex].Cutscene);
        this.close();
    }

    private void DrawEventRow(SpriteBatch spriteBatch, int index, Rectangle bounds)
    {
        ImportCandidate candidate = this.importedCutscenes[index];
        CutsceneData cutscene = candidate.Cutscene;
        Color color = index == this.selectedIndex ? Color.LightGoldenrodYellow : Color.White;
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, color);

        string sourceName = Path.GetFileName(Path.GetDirectoryName(candidate.SourcePath) ?? candidate.SourcePath);
        string label = $"{cutscene.UniqueId} | {cutscene.LocationName} | {sourceName} | {cutscene.Triggers.Count} trigger(s)";
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

    private void DrawFolderBrowser(SpriteBatch spriteBatch, Rectangle parentBounds)
    {
        this.folderButtons.Clear();
        this.folderRows.Clear();

        Rectangle bounds = new(
            parentBounds.Center.X - Math.Min(900, parentBounds.Width - 96) / 2,
            parentBounds.Center.Y - Math.Min(430, parentBounds.Height - 72) / 2,
            Math.Min(900, parentBounds.Width - 96),
            Math.Min(430, parentBounds.Height - 72)
        );

        spriteBatch.Draw(Game1.fadeToBlackRect, bounds, Color.Black * 0.18f);
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);

        int x = bounds.X + 24;
        int y = bounds.Y + 22;
        this.DrawLine(spriteBatch, "Select Folder To Scan", x, y, Game1.textColor);
        this.DrawLine(spriteBatch, TrimText(this.folderBrowserPath, 96), x, y + 42, Color.DimGray);

        this.DrawFolderButton(spriteBatch, new Rectangle(bounds.Right - 280, y + 4, 72, ButtonHeight), "Up", this.MoveFolderBrowserUp);
        this.DrawFolderButton(spriteBatch, new Rectangle(bounds.Right - 200, y + 4, 84, ButtonHeight), "Select", this.SelectFolderBrowserPath);
        this.DrawFolderButton(spriteBatch, new Rectangle(bounds.Right - 108, y + 4, 84, ButtonHeight), "Cancel", () => this.folderBrowserOpen = false);

        List<string> directories = GetChildDirectories(this.folderBrowserPath);
        int listY = y + 86;
        int visibleRows = Math.Max(1, (bounds.Bottom - 24 - listY) / RowHeight);
        this.folderScrollIndex = Math.Clamp(this.folderScrollIndex, 0, Math.Max(0, directories.Count - visibleRows));

        if (directories.Count == 0)
        {
            this.DrawLine(spriteBatch, "No subfolders.", x, listY + 8, Color.DimGray);
            return;
        }

        for (int visibleIndex = 0; visibleIndex < visibleRows; visibleIndex++)
        {
            int folderIndex = this.folderScrollIndex + visibleIndex;
            if (folderIndex >= directories.Count)
            {
                break;
            }

            string folderPath = directories[folderIndex];
            Rectangle row = new(x, listY + visibleIndex * RowHeight, bounds.Width - 48, RowHeight - 4);
            IClickableMenu.drawTextureBox(spriteBatch, row.X, row.Y, row.Width, row.Height, Color.White);
            this.DrawLine(spriteBatch, Path.GetFileName(folderPath), row.X + 12, row.Y + 9, Game1.textColor);
            this.folderRows.Add((row, folderPath));
        }
    }

    private void DrawFolderButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Action click)
    {
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        Vector2 labelSize = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(spriteBatch, label, Game1.smallFont, new Vector2(bounds.Center.X - labelSize.X / 2f, bounds.Center.Y - labelSize.Y / 2f), Game1.textColor);
        this.folderButtons.Add((bounds, click));
    }

    private void ReceiveFolderBrowserClick(int x, int y)
    {
        foreach ((Rectangle bounds, Action click) in this.folderButtons)
        {
            if (bounds.Contains(x, y))
            {
                click();
                return;
            }
        }

        foreach ((Rectangle bounds, string path) in this.folderRows)
        {
            if (bounds.Contains(x, y))
            {
                this.folderBrowserPath = path;
                this.folderScrollIndex = 0;
                return;
            }
        }
    }

    private void OpenFolderBrowser()
    {
        this.pathField.Selected = false;
        this.folderBrowserPath = this.GetInitialFolderBrowserPath();
        this.folderScrollIndex = 0;
        this.folderBrowserOpen = true;
    }

    private void SelectFolderBrowserPath()
    {
        if (!Directory.Exists(this.folderBrowserPath))
        {
            return;
        }

        this.contentJsonPath = this.folderBrowserPath;
        this.folderBrowserOpen = false;
        this.SetStatus("Folder selected. Click Load to scan.", Color.DarkGreen);
    }

    private void MoveFolderBrowserUp()
    {
        DirectoryInfo? parent = Directory.GetParent(this.folderBrowserPath);
        if (parent is null)
        {
            return;
        }

        this.folderBrowserPath = parent.FullName;
        this.folderScrollIndex = 0;
    }

    private void ScrollFolderBrowser(int direction)
    {
        List<string> directories = GetChildDirectories(this.folderBrowserPath);
        Rectangle bounds = this.GetBounds();
        int modalHeight = Math.Min(430, bounds.Height - 72);
        int listY = bounds.Center.Y - modalHeight / 2 + 22 + 86;
        int visibleRows = Math.Max(1, (bounds.Center.Y + modalHeight / 2 - 24 - listY) / RowHeight);
        int delta = direction > 0 ? -WheelRows : WheelRows;
        this.folderScrollIndex = Math.Clamp(this.folderScrollIndex + delta, 0, Math.Max(0, directories.Count - visibleRows));
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

    private void SetScanDepth(int depth)
    {
        this.scanDepth = Math.Clamp(depth, 0, MaxScanDepth);
    }

    private string GetInitialFolderBrowserPath()
    {
        if (File.Exists(this.contentJsonPath))
        {
            return Path.GetDirectoryName(this.contentJsonPath) ?? ModEntry.Instance.ModsDirectoryPath;
        }

        if (Directory.Exists(this.contentJsonPath))
        {
            return this.contentJsonPath;
        }

        return Directory.Exists(ModEntry.Instance.ModsDirectoryPath)
            ? ModEntry.Instance.ModsDirectoryPath
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private int GetVisibleRows(Rectangle panelBounds, int listY)
    {
        return Math.Max(1, (panelBounds.Bottom - 72 - listY) / RowHeight);
    }

    private static List<string> GetChildDirectories(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.EnumerateDirectories(path).OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
        }
        catch (IOException)
        {
            return new List<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    private static string TrimText(string text, int maxLength)
    {
        return text.Length <= maxLength
            ? text
            : "..." + text[^Math.Max(0, maxLength - 3)..];
    }

    private static IEnumerable<string> FindContentJsonFiles(string rootPath, int maxDepth)
    {
        Queue<(string Path, int Depth)> queue = new();
        queue.Enqueue((rootPath, 0));

        while (queue.Count > 0)
        {
            (string currentPath, int depth) = queue.Dequeue();
            string contentPath = Path.Combine(currentPath, "content.json");
            if (File.Exists(contentPath))
            {
                yield return contentPath;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string directory in directories)
            {
                queue.Enqueue((directory, depth + 1));
            }
        }
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
