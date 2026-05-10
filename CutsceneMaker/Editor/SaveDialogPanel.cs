using CutsceneMaker.Compiler;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public sealed class SaveDialogPanel
{
    private const int PanelWidth = 620;
    private const int PanelHeight = 260;
    private const int ButtonHeight = 36;

    private readonly EditorState state;
    private readonly string modsPath;
    private readonly Action close;
    private readonly Action<string> saved;
    private readonly List<(Rectangle Bounds, Action Click)> buttons = new();
    private readonly BoundTextField nameField;
    private string currentName;
    private string statusMessage = string.Empty;
    private Color statusColor = Color.DimGray;

    public SaveDialogPanel(EditorState state, string modsPath, Action close, Action<string> saved)
    {
        this.state = state;
        this.modsPath = modsPath;
        this.close = close;
        this.saved = saved;
        this.currentName = state.Cutscene.CutsceneName;
        this.nameField = new BoundTextField(
            () => this.currentName,
            value => this.currentName = SanitizeName(value),
            numbersOnly: false,
            textLimit: 64
        );
    }

    public bool HasSelectedTextField()
    {
        return this.nameField.Selected;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        this.buttons.Clear();
        spriteBatch.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.55f);

        Rectangle bounds = this.GetBounds();
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);

        int x = bounds.X + 28;
        int y = bounds.Y + 24;
        this.DrawLine(spriteBatch, "Save Cutscene", x, y, Game1.textColor);
        this.DrawLine(spriteBatch, "Cutscene Name", x, y + 52, Game1.textColor);

        Rectangle fieldBounds = new(x + 170, y + 42, bounds.Width - 220, 44);
        this.nameField.SetBounds(fieldBounds);
        this.nameField.Draw(spriteBatch);
        this.buttons.Add((fieldBounds, this.nameField.Select));

        string validation = this.GetValidationMessage();
        if (!string.IsNullOrWhiteSpace(validation))
        {
            this.DrawLine(spriteBatch, validation, x, y + 102, Color.Red);
        }
        else if (this.TargetDirectoryExists())
        {
            this.DrawLine(spriteBatch, "This name already exists and will be overwritten.", x, y + 102, Color.DarkGoldenrod);
        }
        else if (!string.IsNullOrWhiteSpace(this.statusMessage))
        {
            this.DrawLine(spriteBatch, this.statusMessage, x, y + 102, this.statusColor);
        }

        if (!string.Equals(this.currentName, this.state.Cutscene.CutsceneName, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(this.state.Cutscene.CutsceneName))
        {
            this.DrawLine(spriteBatch, "Renaming does not change the event ID.", x, y + 132, Color.DarkGoldenrod);
        }

        Rectangle saveButton = new(bounds.Right - 216, bounds.Bottom - 56, 88, ButtonHeight);
        Rectangle cancelButton = new(bounds.Right - 120, bounds.Bottom - 56, 92, ButtonHeight);
        this.DrawButton(spriteBatch, saveButton, "Save", this.TrySave);
        this.DrawButton(spriteBatch, cancelButton, "Cancel", this.close);
    }

    public void Update()
    {
        this.nameField.Update();
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

        this.nameField.Selected = false;
    }

    public void ReceiveKeyPress(Keys key)
    {
        if (this.nameField.Selected)
        {
            this.nameField.ReceiveKeyPress(key);
            return;
        }

        if (key == Keys.Escape)
        {
            this.close();
        }
    }

    private void TrySave()
    {
        this.nameField.Selected = false;

        string validation = this.GetValidationMessage();
        if (!string.IsNullOrWhiteSpace(validation))
        {
            this.statusMessage = validation;
            this.statusColor = Color.Red;
            return;
        }

        try
        {
            this.state.Cutscene.CutsceneName = this.currentName.Trim();
            List<string> validationErrors = CutsceneValidator.Validate(this.state.Cutscene, ModEntry.Instance.CommandCatalog, ModEntry.Instance.PreconditionCatalog, forPreview: false);
            if (validationErrors.Count > 0)
            {
                this.statusMessage = validationErrors[0];
                this.statusColor = Color.Red;
                return;
            }

            ContentPackWriter.Write(this.state.Cutscene, this.modsPath);
            this.state.IsDirty = false;
            string outputDirectory = Path.Combine(this.modsPath, "[CP] " + this.state.Cutscene.CutsceneName);
            this.state.LastSavedContentJsonPath = Path.Combine(outputDirectory, "content.json");
            ModEntry.Instance.Monitor.Log($"Cutscene Maker saved '{this.state.Cutscene.CutsceneName}' to {outputDirectory}.", StardewModdingAPI.LogLevel.Info);
            this.saved($"Saved to {outputDirectory}");
            this.close();
        }
        catch (Exception ex)
        {
            this.statusMessage = "Save failed. See SMAPI log for details.";
            this.statusColor = Color.Red;
            ModEntry.Instance.Monitor.Log($"Cutscene Maker save failed: {ex}", StardewModdingAPI.LogLevel.Error);
        }
    }

    private string GetValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(this.currentName))
        {
            return "Name is required.";
        }

        return string.Empty;
    }

    private bool TargetDirectoryExists()
    {
        if (string.IsNullOrWhiteSpace(this.currentName))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(this.modsPath, "[CP] " + this.currentName.Trim()));
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

    private Rectangle GetBounds()
    {
        return new Rectangle(
            Game1.uiViewport.Width / 2 - PanelWidth / 2,
            Game1.uiViewport.Height / 2 - PanelHeight / 2,
            PanelWidth,
            PanelHeight
        );
    }

    private static string SanitizeName(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int length = 0;

        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_')
            {
                buffer[length++] = ch;
            }
        }

        return new string(buffer[..length]);
    }
}
