using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public sealed class PreconditionEditorPanel
{
    private const int PanelWidth = 860;
    private const int PanelHeight = 560;
    private const int RowHeight = 42;
    private const int ButtonHeight = 36;

    private static readonly PreconditionType[] AddableTypes = Enum.GetValues<PreconditionType>();
    private static readonly string[] Seasons = { "Spring", "Summer", "Fall", "Winter" };
    private static readonly string[] Weathers = { "Sun", "rain", "snow", "wind", "storm" };

    private readonly EditorState state;
    private readonly Action close;
    private readonly string originalTriggersJson;
    private readonly bool originalIsDirty;
    private readonly List<(Rectangle Bounds, Action LeftClick, Action? RightClick)> buttons = new();
    private readonly Dictionary<string, BoundTextField> textFields = new();
    private bool addMenuOpen;
    private int selectedIndex;

    public PreconditionEditorPanel(EditorState state, Action close)
    {
        this.state = state;
        this.close = close;
        this.originalTriggersJson = JsonConvert.SerializeObject(state.Cutscene.Triggers);
        this.originalIsDirty = state.IsDirty;
        this.selectedIndex = state.Cutscene.Triggers.Count > 0 ? 0 : -1;
    }

    public bool HasSelectedTextField()
    {
        return this.textFields.Values.Any(field => field.Selected);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        this.buttons.Clear();
        spriteBatch.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.55f);

        Rectangle bounds = this.GetBounds();
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);

        int x = bounds.X + 28;
        int y = bounds.Y + 24;
        this.DrawLine(spriteBatch, "Event Triggers", x, y);
        this.DrawLine(spriteBatch, "All conditions must be met simultaneously for the event to fire.", x, y + 34, Color.DimGray);

        int listY = y + 78;
        int listWidth = bounds.Width - 56;
        if (this.state.Cutscene.Triggers.Count == 0)
        {
            this.DrawLine(spriteBatch, "No conditions. This event can fire unconditionally.", x, listY, Color.DimGray);
        }

        for (int i = 0; i < this.state.Cutscene.Triggers.Count; i++)
        {
            this.DrawConditionRow(spriteBatch, i, new Rectangle(x, listY + i * RowHeight, listWidth, RowHeight - 4));
        }

        int editorY = bounds.Y + 318;
        this.DrawSelectedEditor(spriteBatch, x, editorY, listWidth);

        Rectangle addButton = new(x, bounds.Bottom - 56, 220, ButtonHeight);
        Rectangle doneButton = new(bounds.Right - 248, bounds.Bottom - 56, 104, ButtonHeight);
        Rectangle cancelButton = new(bounds.Right - 132, bounds.Bottom - 56, 104, ButtonHeight);
        this.DrawButton(spriteBatch, addButton, "+ Add Condition", () => this.addMenuOpen = !this.addMenuOpen);
        this.DrawButton(spriteBatch, doneButton, "Done", this.SaveAndClose);
        this.DrawButton(spriteBatch, cancelButton, "Cancel", this.CancelAndClose);

        if (this.addMenuOpen)
        {
            this.DrawAddMenu(spriteBatch, addButton);
        }
    }

    public void Update()
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            field.Update();
        }
    }

    public void ReceiveLeftClick(int x, int y)
    {
        foreach ((Rectangle bounds, Action leftClick, _) in this.buttons)
        {
            if (bounds.Contains(x, y))
            {
                leftClick();
                return;
            }
        }

        this.DeselectTextFields();
        this.addMenuOpen = false;
    }

    public void ReceiveRightClick(int x, int y)
    {
        foreach ((Rectangle bounds, _, Action? rightClick) in this.buttons)
        {
            if (bounds.Contains(x, y))
            {
                rightClick?.Invoke();
                return;
            }
        }
    }

    public void ReceiveKeyPress(Keys key)
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            if (field.Selected)
            {
                field.ReceiveKeyPress(key);
                return;
            }
        }
    }

    public void Cancel()
    {
        this.CancelAndClose();
    }

    private void DrawConditionRow(SpriteBatch spriteBatch, int index, Rectangle bounds)
    {
        PreconditionData condition = this.state.Cutscene.Triggers[index];
        Color color = index == this.selectedIndex ? Color.LightGoldenrodYellow : Color.White;
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, color);

        string prefix = condition.Negated ? "! " : string.Empty;
        this.DrawLine(spriteBatch, $"{prefix}{condition.Type}: {this.GetSummary(condition)}", bounds.X + 12, bounds.Y + 8);
        this.buttons.Add((new Rectangle(bounds.X, bounds.Y, bounds.Width - 244, bounds.Height), () => this.selectedIndex = index, null));

        this.DrawButton(spriteBatch, new Rectangle(bounds.Right - 238, bounds.Y + 4, 86, ButtonHeight), "Neg", () => this.ToggleNegated(condition));
        this.DrawButton(spriteBatch, new Rectangle(bounds.Right - 144, bounds.Y + 4, 88, ButtonHeight), "Type", () => this.CycleType(condition, 1), () => this.CycleType(condition, -1));
        this.DrawButton(spriteBatch, new Rectangle(bounds.Right - 48, bounds.Y + 4, 42, ButtonHeight), "X", () => this.RemoveCondition(index));
    }

    private void DrawSelectedEditor(SpriteBatch spriteBatch, int x, int y, int width)
    {
        if (this.selectedIndex < 0 || this.selectedIndex >= this.state.Cutscene.Triggers.Count)
        {
            this.DrawLine(spriteBatch, "Select or add a condition to edit it.", x, y, Color.DimGray);
            return;
        }

        PreconditionData condition = this.state.Cutscene.Triggers[this.selectedIndex];
        IClickableMenu.drawTextureBox(spriteBatch, x, y, width, 126, Color.White);
        int contentX = x + 18;
        int contentY = y + 18;
        this.DrawLine(spriteBatch, $"Editing: {condition.Type}", contentX, contentY);

        switch (condition.Type)
        {
            case PreconditionType.Time:
                this.DrawLine(spriteBatch, "Start", contentX, contentY + RowHeight);
                this.DrawIntegerField(spriteBatch, $"{this.selectedIndex}.time.start", new Rectangle(contentX + 72, contentY + RowHeight - 8, 90, 40), () => condition.TimeStart ?? 600, value => condition.TimeStart = Math.Clamp(value, 600, 2600));
                this.DrawLine(spriteBatch, "End", contentX + 190, contentY + RowHeight);
                this.DrawIntegerField(spriteBatch, $"{this.selectedIndex}.time.end", new Rectangle(contentX + 248, contentY + RowHeight - 8, 90, 40), () => condition.TimeEnd ?? 2600, value => condition.TimeEnd = Math.Clamp(value, 600, 2600));
                break;

            case PreconditionType.Season:
                this.DrawButton(spriteBatch, new Rectangle(contentX, contentY + RowHeight, 140, ButtonHeight), condition.Season ?? "Spring", () => this.CycleString(value => condition.Season = value, condition.Season, Seasons, 1), () => this.CycleString(value => condition.Season = value, condition.Season, Seasons, -1));
                break;

            case PreconditionType.Weather:
                this.DrawButton(spriteBatch, new Rectangle(contentX, contentY + RowHeight, 140, ButtonHeight), condition.Weather ?? "Sun", () => this.CycleString(value => condition.Weather = value, condition.Weather, Weathers, 1), () => this.CycleString(value => condition.Weather = value, condition.Weather, Weathers, -1));
                break;

            case PreconditionType.Year:
                this.DrawLine(spriteBatch, "Min year", contentX, contentY + RowHeight);
                this.DrawIntegerField(spriteBatch, $"{this.selectedIndex}.year", new Rectangle(contentX + 112, contentY + RowHeight - 8, 90, 40), () => condition.MinYear ?? 1, value => condition.MinYear = Math.Max(1, value));
                break;

            case PreconditionType.DaysPlayed:
                this.DrawLine(spriteBatch, "Days played", contentX, contentY + RowHeight);
                this.DrawIntegerField(spriteBatch, $"{this.selectedIndex}.days", new Rectangle(contentX + 140, contentY + RowHeight - 8, 100, 40), () => condition.DaysPlayed ?? 1, value => condition.DaysPlayed = Math.Max(1, value));
                break;

            case PreconditionType.Friendship:
                this.DrawButton(spriteBatch, new Rectangle(contentX, contentY + RowHeight, 160, ButtonHeight), condition.NpcName ?? "Lewis", () => this.CycleNpc(condition, 1), () => this.CycleNpc(condition, -1));
                this.DrawLine(spriteBatch, "Hearts", contentX + 184, contentY + RowHeight);
                this.DrawIntegerField(spriteBatch, $"{this.selectedIndex}.friend.hearts", new Rectangle(contentX + 260, contentY + RowHeight - 8, 76, 40), () => condition.HeartLevel ?? 1, value => condition.HeartLevel = Math.Clamp(value, 1, 14));
                break;

            case PreconditionType.HasSeenEvent:
            case PreconditionType.HasMailFlag:
                this.DrawLine(spriteBatch, "ID", contentX, contentY + RowHeight);
                this.DrawStringField(spriteBatch, $"{this.selectedIndex}.flag", new Rectangle(contentX + 48, contentY + RowHeight - 8, 280, 40), () => condition.FlagOrEventId ?? string.Empty, value => condition.FlagOrEventId = value, 100);
                break;

            case PreconditionType.GameStateQuery:
                this.DrawLine(spriteBatch, "GSQ", contentX, contentY + RowHeight);
                this.DrawStringField(spriteBatch, $"{this.selectedIndex}.gsq", new Rectangle(contentX + 56, contentY + RowHeight - 8, Math.Max(280, width - 104), 40), () => condition.QueryString ?? string.Empty, value => condition.QueryString = value, 240);
                break;
        }
    }

    private void DrawAddMenu(SpriteBatch spriteBatch, Rectangle addButton)
    {
        int x = addButton.X;
        int y = addButton.Y - AddableTypes.Length * RowHeight;
        foreach (PreconditionType type in AddableTypes)
        {
            Rectangle bounds = new(x, y, 240, RowHeight);
            this.DrawButton(spriteBatch, bounds, type.ToString(), () => this.AddCondition(type));
            y += RowHeight;
        }
    }

    private void AddCondition(PreconditionType type)
    {
        this.state.Cutscene.Triggers.Add(CreateDefaultCondition(type));
        this.selectedIndex = this.state.Cutscene.Triggers.Count - 1;
        this.addMenuOpen = false;
        this.state.IsDirty = true;
    }

    private void RemoveCondition(int index)
    {
        if (index < 0 || index >= this.state.Cutscene.Triggers.Count)
        {
            return;
        }

        this.state.Cutscene.Triggers.RemoveAt(index);
        this.selectedIndex = this.state.Cutscene.Triggers.Count == 0
            ? -1
            : Math.Min(index, this.state.Cutscene.Triggers.Count - 1);
        this.state.IsDirty = true;
    }

    private void ToggleNegated(PreconditionData condition)
    {
        condition.Negated = !condition.Negated;
        this.state.IsDirty = true;
    }

    private void CycleType(PreconditionData condition, int direction)
    {
        int index = Array.IndexOf(AddableTypes, condition.Type);
        condition.Type = AddableTypes[WrapIndex(index + Math.Sign(direction), AddableTypes.Length)];
        ApplyDefaults(condition);
        this.state.IsDirty = true;
    }

    private void CycleNpc(PreconditionData condition, int direction)
    {
        ModEntry.Instance.RefreshKnownNpcs();
        if (ModEntry.KnownNpcNames.Count == 0)
        {
            return;
        }

        int index = Math.Max(0, ModEntry.KnownNpcNames.FindIndex(name => string.Equals(name, condition.NpcName, StringComparison.OrdinalIgnoreCase)));
        condition.NpcName = ModEntry.KnownNpcNames[WrapIndex(index + Math.Sign(direction), ModEntry.KnownNpcNames.Count)];
        this.state.IsDirty = true;
    }

    private void CycleString(Action<string> setValue, string? current, IReadOnlyList<string> values, int direction)
    {
        int index = Math.Max(0, values.ToList().FindIndex(value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase)));
        setValue(values[WrapIndex(index + Math.Sign(direction), values.Count)]);
        this.state.IsDirty = true;
    }

    private void SaveAndClose()
    {
        this.close();
    }

    private void CancelAndClose()
    {
        this.state.Cutscene.Triggers = JsonConvert.DeserializeObject<List<PreconditionData>>(this.originalTriggersJson) ?? new List<PreconditionData>();
        this.state.IsDirty = this.originalIsDirty;
        this.close();
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Action leftClick, Action? rightClick = null)
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
        this.buttons.Add((bounds, leftClick, rightClick));
    }

    private void DrawIntegerField(SpriteBatch spriteBatch, string key, Rectangle bounds, Func<int> getValue, Action<int> setValue)
    {
        this.DrawBoundField(
            spriteBatch,
            key,
            bounds,
            () => getValue().ToString(),
            value =>
            {
                if (int.TryParse(value, out int parsed))
                {
                    setValue(parsed);
                    this.state.IsDirty = true;
                }
            },
            numbersOnly: true
        );
    }

    private void DrawStringField(SpriteBatch spriteBatch, string key, Rectangle bounds, Func<string> getValue, Action<string> setValue, int textLimit)
    {
        this.DrawBoundField(
            spriteBatch,
            key,
            bounds,
            getValue,
            value =>
            {
                setValue(value);
                this.state.IsDirty = true;
            },
            numbersOnly: false,
            textLimit: textLimit
        );
    }

    private void DrawBoundField(SpriteBatch spriteBatch, string key, Rectangle bounds, Func<string> getValue, Action<string> setValue, bool numbersOnly, int textLimit = -1)
    {
        BoundTextField field = this.GetTextField(key, getValue, setValue, numbersOnly, textLimit);
        field.SetBounds(bounds);
        field.Draw(spriteBatch);
        this.buttons.Add((bounds, () => this.SelectTextField(field), null));
    }

    private BoundTextField GetTextField(string key, Func<string> getValue, Action<string> setValue, bool numbersOnly, int textLimit)
    {
        if (!this.textFields.TryGetValue(key, out BoundTextField? field))
        {
            field = new BoundTextField(getValue, setValue, numbersOnly, textLimit);
            this.textFields[key] = field;
        }

        return field;
    }

    private void SelectTextField(BoundTextField selectedField)
    {
        this.DeselectTextFields();
        selectedField.Select();
    }

    private void DeselectTextFields()
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            field.Selected = false;
        }
    }

    private void DrawLine(SpriteBatch spriteBatch, string text, int x, int y)
    {
        this.DrawLine(spriteBatch, text, x, y, Game1.textColor);
    }

    private void DrawLine(SpriteBatch spriteBatch, string text, int x, int y, Color color)
    {
        Utility.drawTextWithShadow(spriteBatch, text, Game1.smallFont, new Vector2(x, y), color);
    }

    private string GetSummary(PreconditionData condition)
    {
        return condition.Type switch
        {
            PreconditionType.Time => $"{condition.TimeStart ?? 600}-{condition.TimeEnd ?? 2600}",
            PreconditionType.Season => condition.Season ?? "Spring",
            PreconditionType.Weather => condition.Weather ?? "Sun",
            PreconditionType.Year => $"year {condition.MinYear ?? 1}+",
            PreconditionType.DaysPlayed => $"{condition.DaysPlayed ?? 1}+ days",
            PreconditionType.Friendship => $"{condition.NpcName ?? "Lewis"} {condition.HeartLevel ?? 1} hearts",
            PreconditionType.HasSeenEvent => condition.FlagOrEventId ?? "event id",
            PreconditionType.HasMailFlag => condition.FlagOrEventId ?? "mail flag",
            PreconditionType.GameStateQuery => condition.QueryString ?? "query",
            _ => string.Empty
        };
    }

    private static PreconditionData CreateDefaultCondition(PreconditionType type)
    {
        PreconditionData condition = new() { Type = type };
        ApplyDefaults(condition);
        return condition;
    }

    private static void ApplyDefaults(PreconditionData condition)
    {
        switch (condition.Type)
        {
            case PreconditionType.Time:
                condition.TimeStart ??= 600;
                condition.TimeEnd ??= 2600;
                break;

            case PreconditionType.Season:
                condition.Season ??= "Spring";
                break;

            case PreconditionType.Weather:
                condition.Weather ??= "Sun";
                break;

            case PreconditionType.Year:
                condition.MinYear ??= 1;
                break;

            case PreconditionType.DaysPlayed:
                condition.DaysPlayed ??= 1;
                break;

            case PreconditionType.Friendship:
                condition.NpcName ??= ModEntry.KnownNpcNames.FirstOrDefault() ?? "Lewis";
                condition.HeartLevel ??= 1;
                break;

            case PreconditionType.HasSeenEvent:
            case PreconditionType.HasMailFlag:
                condition.FlagOrEventId ??= string.Empty;
                break;

            case PreconditionType.GameStateQuery:
                condition.QueryString ??= string.Empty;
                break;
        }
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
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
}
