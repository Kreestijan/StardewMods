using CutsceneMaker.Commands;
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
    private const int PanelWidth = 920;
    private const int PanelHeight = 600;
    private const int RowHeight = 42;
    private const int ButtonHeight = 36;

    private readonly EditorState state;
    private readonly EventPreconditionCatalog catalog;
    private readonly Action close;
    private readonly string originalTriggersJson;
    private readonly bool originalIsDirty;
    private readonly List<(Rectangle Bounds, Action LeftClick, Action? RightClick)> buttons = new();
    private readonly Dictionary<string, BoundTextField> textFields = new();
    private bool addMenuOpen;
    private int selectedIndex;

    public PreconditionEditorPanel(EditorState state, EventPreconditionCatalog catalog, Action close)
    {
        this.state = state;
        this.catalog = catalog;
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
        EventPreconditionBlock condition = this.state.Cutscene.Triggers[index];
        Color color = index == this.selectedIndex ? Color.LightGoldenrodYellow : Color.White;
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, color);

        string prefix = condition.Negated ? "! " : string.Empty;
        this.DrawLine(spriteBatch, $"{prefix}{condition.DisplayName}: {this.GetSummary(condition)}", bounds.X + 12, bounds.Y + 8);
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

        EventPreconditionBlock condition = this.state.Cutscene.Triggers[this.selectedIndex];
        IClickableMenu.drawTextureBox(spriteBatch, x, y, width, 150, Color.White);
        int contentX = x + 18;
        int contentY = y + 18;
        this.DrawLine(spriteBatch, $"Editing: {condition.DisplayName}", contentX, contentY);

        if (!this.catalog.TryGetById(condition.PreconditionId, out EventPreconditionDefinition? definition))
        {
            this.DrawLine(spriteBatch, "Raw", contentX, contentY + RowHeight);
            this.DrawStringField(spriteBatch, $"{this.selectedIndex}.raw", new Rectangle(contentX + 64, contentY + RowHeight - 8, Math.Max(300, width - 120), 40), () => condition.Verb, value => condition.Verb = value, 300);
            return;
        }

        int row = 1;
        foreach (EventCommandParameter parameter in definition.Parameters)
        {
            int currentY = contentY + RowHeight * row;
            this.DrawLine(spriteBatch, parameter.Label, contentX, currentY);
            switch (parameter.Type)
            {
                case EventCommandParameterType.Boolean:
                    this.DrawButton(spriteBatch, new Rectangle(contentX + 150, currentY - 8, 120, ButtonHeight), this.GetValue(condition, parameter), () => this.CycleBoolean(condition, parameter));
                    break;

                case EventCommandParameterType.Choice:
                    this.DrawButton(spriteBatch, new Rectangle(contentX + 150, currentY - 8, 160, ButtonHeight), this.GetValue(condition, parameter), () => this.CycleChoice(condition, parameter, 1), () => this.CycleChoice(condition, parameter, -1));
                    break;

                case EventCommandParameterType.Actor:
                case EventCommandParameterType.OptionalActor:
                    this.DrawButton(spriteBatch, new Rectangle(contentX + 150, currentY - 8, 180, ButtonHeight), this.GetValue(condition, parameter), () => this.CycleNpc(condition, parameter, 1), () => this.CycleNpc(condition, parameter, -1));
                    break;

                default:
                    this.DrawStringField(spriteBatch, $"{this.selectedIndex}.{parameter.Key}", new Rectangle(contentX + 150, currentY - 8, Math.Max(240, width - 220), 40), () => this.GetValue(condition, parameter), value => condition.Values[parameter.Key] = value, parameter.TextLimit);
                    break;
            }

            row++;
            if (row > 3)
            {
                this.DrawLine(spriteBatch, "More fields may require widening this panel later.", contentX, contentY + RowHeight * row, Color.DimGray);
                break;
            }
        }
    }

    private void DrawAddMenu(SpriteBatch spriteBatch, Rectangle addButton)
    {
        int rowsPerColumn = 10;
        int menuWidth = 250;
        int x = addButton.X;
        int y = addButton.Y - rowsPerColumn * RowHeight;
        for (int i = 0; i < this.catalog.Definitions.Count; i++)
        {
            EventPreconditionDefinition definition = this.catalog.Definitions[i];
            int column = i / rowsPerColumn;
            int row = i % rowsPerColumn;
            Rectangle bounds = new(x + column * menuWidth, y + row * RowHeight, menuWidth, RowHeight);
            this.DrawButton(spriteBatch, bounds, definition.DisplayName, () => this.AddCondition(definition));
        }
    }

    private void AddCondition(EventPreconditionDefinition definition)
    {
        this.state.Cutscene.Triggers.Add(definition.CreateDefaultBlock());
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

    private void ToggleNegated(EventPreconditionBlock condition)
    {
        condition.Negated = !condition.Negated;
        this.state.IsDirty = true;
    }

    private void CycleType(EventPreconditionBlock condition, int direction)
    {
        int index = Math.Max(0, this.catalog.Definitions.ToList().FindIndex(definition => definition.Id.Equals(condition.PreconditionId, StringComparison.Ordinal)));
        EventPreconditionBlock replacement = this.catalog.Definitions[WrapIndex(index + Math.Sign(direction), this.catalog.Definitions.Count)].CreateDefaultBlock();
        replacement.Negated = condition.Negated;
        condition.PreconditionId = replacement.PreconditionId;
        condition.DisplayName = replacement.DisplayName;
        condition.Verb = replacement.Verb;
        condition.Values = replacement.Values;
        this.state.IsDirty = true;
    }

    private string GetValue(EventPreconditionBlock condition, EventCommandParameter parameter)
    {
        return condition.Values.TryGetValue(parameter.Key, out string? value) ? value : parameter.DefaultValue;
    }

    private void CycleBoolean(EventPreconditionBlock condition, EventCommandParameter parameter)
    {
        string current = this.GetValue(condition, parameter);
        condition.Values[parameter.Key] = current.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
        this.state.IsDirty = true;
    }

    private void CycleChoice(EventPreconditionBlock condition, EventCommandParameter parameter, int direction)
    {
        if (parameter.Choices.Count == 0)
        {
            return;
        }

        string current = this.GetValue(condition, parameter);
        int index = Math.Max(0, parameter.Choices.ToList().FindIndex(value => value.Equals(current, StringComparison.OrdinalIgnoreCase)));
        condition.Values[parameter.Key] = parameter.Choices[WrapIndex(index + Math.Sign(direction), parameter.Choices.Count)];
        this.state.IsDirty = true;
    }

    private void CycleNpc(EventPreconditionBlock condition, EventCommandParameter parameter, int direction)
    {
        ModEntry.Instance.RefreshKnownNpcs();
        if (ModEntry.KnownNpcNames.Count == 0)
        {
            return;
        }

        string current = this.GetValue(condition, parameter);
        int index = Math.Max(0, ModEntry.KnownNpcNames.FindIndex(value => value.Equals(current, StringComparison.OrdinalIgnoreCase)));
        condition.Values[parameter.Key] = ModEntry.KnownNpcNames[WrapIndex(index + Math.Sign(direction), ModEntry.KnownNpcNames.Count)];
        this.state.IsDirty = true;
    }

    private void SaveAndClose()
    {
        this.close();
    }

    private void CancelAndClose()
    {
        this.state.Cutscene.Triggers = JsonConvert.DeserializeObject<List<EventPreconditionBlock>>(this.originalTriggersJson) ?? new List<EventPreconditionBlock>();
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

    private void DrawStringField(SpriteBatch spriteBatch, string key, Rectangle bounds, Func<string> getValue, Action<string> setValue, int textLimit)
    {
        BoundTextField field = this.GetTextField(key, getValue, value =>
        {
            setValue(value);
            this.state.IsDirty = true;
        }, textLimit);
        field.SetBounds(bounds);
        field.Draw(spriteBatch);
        this.buttons.Add((bounds, () => this.SelectTextField(field), null));
    }

    private BoundTextField GetTextField(string key, Func<string> getValue, Action<string> setValue, int textLimit)
    {
        if (!this.textFields.TryGetValue(key, out BoundTextField? field))
        {
            field = new BoundTextField(getValue, setValue, numbersOnly: false, textLimit: textLimit);
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

    private string GetSummary(EventPreconditionBlock condition)
    {
        if (condition.Values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", condition.Values.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Take(3));
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
