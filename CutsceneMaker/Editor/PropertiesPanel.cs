using CutsceneMaker.Commands;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.CompilerServices;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public sealed class PropertiesPanel : EditorPanel
{
    private const int RowHeight = 34;
    private const int ActorRowHeight = RowHeight * 2;
    private const int ButtonHeight = 32;
    private const int PickerRowHeight = 34;
    private const int PickerSearchHeight = 42;
    private const int PickerMaxRows = 8;
    private const float HintScale = 0.78f;
    private readonly EditorState state;
    private readonly EventCommandCatalog commandCatalog;
    private readonly Action openPreconditionEditor;
    private readonly List<(Rectangle Bounds, Action LeftClick, Action? RightClick)> buttons = new();
    private readonly Dictionary<string, BoundTextField> textFields = new();
    private readonly List<(Rectangle Bounds, string ActorName)> actorPickerRows = new();
    private Rectangle setupActorListBounds;
    private Rectangle actorPickerBounds;
    private int setupActorScrollIndex;
    private int actorPickerScrollIndex;
    private bool actorPickerOpen;
    private bool actorPickerFocusPending;
    private string actorPickerSearchText = string.Empty;

    public PropertiesPanel(EditorState state, EventCommandCatalog commandCatalog, Action openPreconditionEditor)
        : base("Properties")
    {
        this.state = state;
        this.commandCatalog = commandCatalog;
        this.openPreconditionEditor = openPreconditionEditor;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        this.buttons.Clear();

        int x = this.Bounds.X + 24;
        int y = this.Bounds.Y + 58;
        if (this.state.SelectedCommandIndex == -1)
        {
            this.DrawSetup(spriteBatch, x, y);
            return;
        }

        if (this.state.SelectedCommandIndex < 0 || this.state.SelectedCommandIndex >= this.state.Cutscene.Commands.Count)
        {
            this.DrawLine(spriteBatch, "No command selected.", x, y);
            return;
        }

        object command = this.state.Cutscene.Commands[this.state.SelectedCommandIndex];
        switch (command)
        {
            case EventCommandBlock eventCommand:
                this.DrawEventCommand(spriteBatch, eventCommand, x, y);
                break;

            case RawCommandBlock raw:
                this.DrawRawCommand(spriteBatch, raw, x, y);
                break;

            default:
                this.DrawLine(spriteBatch, "Unknown command.", x, y);
                break;
        }
    }

    public override void ReceiveLeftClick(int x, int y)
    {
        if (this.actorPickerOpen && this.HandleActorPickerClick(x, y))
        {
            return;
        }

        foreach ((Rectangle bounds, Action leftClick, _) in this.buttons)
        {
            if (bounds.Contains(x, y))
            {
                leftClick();
                return;
            }
        }

        if (this.actorPickerOpen)
        {
            if (this.actorPickerBounds.Contains(x, y))
            {
                return;
            }

            this.CloseActorPicker();
            return;
        }

        this.DeselectTextFields();
    }

    public override void ReceiveRightClick(int x, int y)
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

    public override void ReceiveKeyPress(Keys key)
    {
        if (this.actorPickerOpen)
        {
            if (key == Keys.Escape)
            {
                this.CloseActorPicker();
                return;
            }

            if (key == Keys.Enter)
            {
                string? firstMatch = this.GetFilteredNpcNames().FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstMatch))
                {
                    this.AddActor(firstMatch);
                    this.CloseActorPicker();
                }

                return;
            }
        }

        foreach (BoundTextField field in this.textFields.Values)
        {
            if (field.Selected)
            {
                field.ReceiveKeyPress(key);
                return;
            }
        }
    }

    public override void ReceiveScrollWheelAction(int direction)
    {
        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);
        if (this.actorPickerOpen && this.actorPickerBounds.Contains(mouseX, mouseY))
        {
            this.ScrollActorPicker(direction);
            return;
        }

        if (this.state.SelectedCommandIndex == -1 && this.setupActorListBounds.Contains(mouseX, mouseY))
        {
            this.ScrollSetupActors(direction);
        }
    }

    public bool HasSelectedTextField()
    {
        return this.textFields.Values.Any(field => field.Selected);
    }

    public override void Update()
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            field.Update();
        }
    }

    private void DrawSetup(SpriteBatch spriteBatch, int x, int y)
    {
        this.actorPickerRows.Clear();
        this.DrawLine(spriteBatch, "Setup", x, y);
        this.DrawLine(spriteBatch, $"Location: {this.state.Cutscene.LocationName}", x, y + RowHeight);
        this.DrawLine(spriteBatch, $"Music: {this.state.Cutscene.MusicTrack}", x, y + RowHeight * 2);
        this.DrawLine(spriteBatch, $"Skippable: {this.state.Cutscene.Skippable}", x, y + RowHeight * 3);
        this.DrawLine(spriteBatch, $"Farmer: {(this.state.Cutscene.IncludeFarmer ? "Included" : "Hidden")}", x, y + RowHeight * 4);
        this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 5, 180, ButtonHeight), "Toggle Skip", () =>
        {
            this.state.Cutscene.Skippable = !this.state.Cutscene.Skippable;
            this.state.IsDirty = true;
        });
        this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 6, 180, ButtonHeight), "Edit Triggers", this.openPreconditionEditor);
        this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 7, 180, ButtonHeight), "Toggle Farmer", this.ToggleFarmer);

        int actorY = y + RowHeight * 9;
        this.DrawLine(spriteBatch, $"Actors: {this.state.Cutscene.Actors.Count}", x, actorY);
        this.DrawButton(spriteBatch, new Rectangle(x + 120, actorY - 8, 72, ButtonHeight), "+", this.OpenActorPicker);
        actorY += RowHeight;

        this.setupActorListBounds = new Rectangle(
            x,
            actorY,
            Math.Max(1, this.Bounds.Right - x - 20),
            Math.Max(ActorRowHeight, this.Bounds.Bottom - actorY - 20)
        );
        IReadOnlyList<NpcPlacement> setupActors = this.GetVisibleSetupActors();
        int visibleRows = Math.Max(1, this.setupActorListBounds.Height / ActorRowHeight);
        this.setupActorScrollIndex = Math.Clamp(this.setupActorScrollIndex, 0, Math.Max(0, setupActors.Count - visibleRows));

        for (int visibleIndex = 0; visibleIndex < visibleRows; visibleIndex++)
        {
            int actorIndex = this.setupActorScrollIndex + visibleIndex;
            if (actorIndex >= setupActors.Count)
            {
                break;
            }

            NpcPlacement actor = setupActors[actorIndex];
            int rowY = this.setupActorListBounds.Y + visibleIndex * ActorRowHeight;
            this.DrawSetupActor(spriteBatch, actor, x, rowY, ReferenceEquals(actor, this.state.Cutscene.FarmerPlacement));
        }

        if (setupActors.Count > visibleRows)
        {
            string scrollText = $"{this.setupActorScrollIndex + 1}-{Math.Min(setupActors.Count, this.setupActorScrollIndex + visibleRows)} / {setupActors.Count}";
            Vector2 size = Game1.smallFont.MeasureString(scrollText);
            Utility.drawTextWithShadow(spriteBatch, scrollText, Game1.smallFont, new Vector2(this.setupActorListBounds.Right - size.X - 4, this.setupActorListBounds.Bottom - 28), Color.DimGray);
        }

        if (this.actorPickerOpen)
        {
            this.DrawActorPicker(spriteBatch, x, y + RowHeight * 8);
        }
    }

    private IReadOnlyList<NpcPlacement> GetVisibleSetupActors()
    {
        List<NpcPlacement> actors = new();
        if (this.state.Cutscene.IncludeFarmer)
        {
            actors.Add(this.state.Cutscene.FarmerPlacement);
        }

        actors.AddRange(this.state.Cutscene.Actors);
        return actors;
    }

    private void DrawSetupActor(SpriteBatch spriteBatch, NpcPlacement actor, int x, int actorY, bool isFarmer)
    {
        bool selected = this.state.SelectedSetupActorSlotId.Equals(actor.ActorSlotId, StringComparison.Ordinal);
        this.DrawLine(spriteBatch, selected ? "> " + actor.ActorName : actor.ActorName, x, actorY);
        this.DrawStringField(spriteBatch, $"setup.actor.{actor.ActorSlotId}.x", new Rectangle(x + 112, actorY - 8, 52, 40), () => actor.TileX.ToString(), value =>
        {
            if (int.TryParse(value, out int parsed))
            {
                actor.TileX = parsed;
            }
        }, 8, numbersOnly: true);
        this.DrawStringField(spriteBatch, $"setup.actor.{actor.ActorSlotId}.y", new Rectangle(x + 170, actorY - 8, 52, 40), () => actor.TileY.ToString(), value =>
        {
            if (int.TryParse(value, out int parsed))
            {
                actor.TileY = parsed;
            }
        }, 8, numbersOnly: true);

        int controlsY = actorY + RowHeight - 2;
        this.DrawButton(spriteBatch, new Rectangle(x, controlsY, 86, ButtonHeight), FacingName(actor.Facing), () => this.CyclePlacementFacing(actor, 1), () => this.CyclePlacementFacing(actor, -1));
        this.DrawButton(spriteBatch, new Rectangle(x + 96, controlsY, 76, ButtonHeight), "Select", () => this.SelectSetupActor(actor));
        if (!isFarmer)
        {
            this.DrawButton(spriteBatch, new Rectangle(x + 182, controlsY, 76, ButtonHeight), "NPC", () => this.CycleSetupActor(actor, 1), () => this.CycleSetupActor(actor, -1));
            this.DrawButton(spriteBatch, new Rectangle(x + 268, controlsY, 48, ButtonHeight), "X", () => this.RemoveActor(actor));
        }
    }

    private void DrawEventCommand(SpriteBatch spriteBatch, EventCommandBlock command, int x, int y)
    {
        if (!this.commandCatalog.TryGetById(command.CommandId, out EventCommandDefinition? definition))
        {
            this.DrawLine(spriteBatch, "Missing command", x, y);
            this.DrawLine(spriteBatch, command.DisplayName, x, y + RowHeight);
            this.DrawLine(spriteBatch, command.ProviderModId, x, y + RowHeight * 2);
            return;
        }

        this.EnsureActorSlotLinks(command, definition);
        this.DrawLine(spriteBatch, $"({definition.Badge}) {definition.DisplayName}", x, y);
        this.DrawLine(spriteBatch, definition.ProviderName, x, y + RowHeight);
        if (definition.UnsafeForPreview)
        {
            this.DrawLine(spriteBatch, "Preview disabled for this command.", x, y + RowHeight * 2, Color.DarkRed);
        }

        int row = definition.UnsafeForPreview ? 3 : 2;
        if (command.CommandId.Equals("helper.reward", StringComparison.Ordinal))
        {
            this.DrawRewardParameters(spriteBatch, command, definition, x, y + RowHeight * row);
            return;
        }

        foreach (EventCommandParameter parameter in definition.Parameters)
        {
            int currentY = y + RowHeight * row;
            this.DrawParameter(spriteBatch, command, parameter, x, currentY);
            row += this.GetParameterRowSpan(parameter);
        }
    }

    private void DrawParameter(SpriteBatch spriteBatch, EventCommandBlock command, EventCommandParameter parameter, int x, int y)
    {
        this.DrawLine(spriteBatch, parameter.Label, x, y);
        switch (parameter.Type)
        {
            case EventCommandParameterType.Actor:
            case EventCommandParameterType.OptionalActor:
                this.DrawLine(spriteBatch, this.ResolveActorName(command, parameter.Key), x + 96, y);
                this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight - 2, 120, ButtonHeight), "Actor", () => this.CycleActor(command, parameter.Key, 1), () => this.CycleActor(command, parameter.Key, -1));
                break;

            case EventCommandParameterType.Boolean:
                this.DrawButton(spriteBatch, new Rectangle(this.GetParameterButtonX(x), y - 8, 120, ButtonHeight), this.FormatBoolean(command, parameter), () => this.CycleBoolean(command, parameter));
                this.DrawParameterHint(spriteBatch, parameter, x + 120, y + RowHeight);
                break;

            case EventCommandParameterType.Choice:
            case EventCommandParameterType.RewardKind:
            case EventCommandParameterType.Direction:
                this.DrawButton(spriteBatch, new Rectangle(this.GetParameterButtonX(x), y - 8, 160, ButtonHeight), this.FormatChoice(command, parameter), () => this.CycleChoice(command, parameter, 1), () => this.CycleChoice(command, parameter, -1));
                this.DrawParameterHint(spriteBatch, parameter, x + 120, y + RowHeight);
                break;

            case EventCommandParameterType.AnswerList:
                this.DrawAnswerList(spriteBatch, command, parameter, x, y);
                break;

            default:
                this.DrawStringField(spriteBatch, FieldKey(command, parameter.Key), new Rectangle(x + 120, y - 8, Math.Max(160, this.Bounds.Width - 168), 40), () => this.GetValue(command, parameter), value => command.Values[parameter.Key] = value, parameter.TextLimit, parameter.Type is EventCommandParameterType.Integer or EventCommandParameterType.OptionalInteger or EventCommandParameterType.TileX or EventCommandParameterType.TileY);
                this.DrawParameterHint(spriteBatch, parameter, x + 120, y + RowHeight);
                break;
        }
    }

    private int GetParameterRowSpan(EventCommandParameter parameter)
    {
        int baseRows = parameter.Type switch
        {
            EventCommandParameterType.Actor or EventCommandParameterType.OptionalActor => 2,
            EventCommandParameterType.Text or EventCommandParameterType.RawArguments => 2,
            EventCommandParameterType.AnswerList => 1 + Math.Max(1, this.GetAnswerCount(this.GetSelectedEventCommand(), parameter)),
            _ => 1
        };

        if (string.IsNullOrWhiteSpace(parameter.Hint))
        {
            return baseRows;
        }

        int maxWidth = Math.Max(120, this.Bounds.Width - 168 - 24);
        int hintHeight = this.GetHintLines(parameter.Hint, maxWidth).Count * ((int)(Game1.smallFont.LineSpacing * HintScale) + 1);
        return baseRows + (int)Math.Ceiling(hintHeight / (float)RowHeight);
    }

    private void DrawParameterHint(SpriteBatch spriteBatch, EventCommandParameter parameter, int x, int y)
    {
        if (string.IsNullOrWhiteSpace(parameter.Hint))
        {
            return;
        }

        this.DrawInlineHint(spriteBatch, parameter.Hint, x, y);
    }

    private void DrawRewardParameters(SpriteBatch spriteBatch, EventCommandBlock command, EventCommandDefinition definition, int x, int y)
    {
        EventCommandParameter kindParameter = definition.Parameters.First(parameter => parameter.Key.Equals("kind", StringComparison.Ordinal));
        this.DrawParameter(spriteBatch, command, kindParameter, x, y);

        RewardFieldSpec spec = GetRewardFieldSpec(this.GetValue(command, kindParameter));
        int targetY = y + RowHeight;
        this.DrawLine(spriteBatch, spec.TargetLabel, x, targetY);
        this.DrawStringField(spriteBatch, FieldKey(command, "target"), new Rectangle(x + 120, targetY - 8, Math.Max(160, this.Bounds.Width - 168), 40), () => this.GetRewardTargetValue(command, spec), value => command.Values["target"] = value, spec.TargetTextLimit);
        this.DrawInlineHint(spriteBatch, spec.TargetHint, x + 120, targetY + RowHeight);

        if (!spec.ShowAmount)
        {
            return;
        }

        int amountY = targetY + RowHeight * 2;
        this.DrawLine(spriteBatch, spec.AmountLabel, x, amountY);
        this.DrawStringField(spriteBatch, FieldKey(command, "amount"), new Rectangle(x + 120, amountY - 8, Math.Max(160, this.Bounds.Width - 168), 40), () => this.GetRewardAmountValue(command, spec), value => command.Values["amount"] = value, 8, numbersOnly: true);
        this.DrawInlineHint(spriteBatch, spec.AmountHint, x + 120, amountY + RowHeight);

        if (!spec.ShowQuality)
        {
            return;
        }

        int qualityY = amountY + RowHeight * 2;
        this.DrawLine(spriteBatch, "Quality", x, qualityY);
        this.DrawStringField(spriteBatch, FieldKey(command, "quality"), new Rectangle(x + 120, qualityY - 8, Math.Max(160, this.Bounds.Width - 168), 40), () => command.Values.TryGetValue("quality", out string? value) ? value : string.Empty, value => command.Values["quality"] = value, 8, numbersOnly: true);
        this.DrawInlineHint(spriteBatch, "Optional: 0 normal, 1 silver, 2 gold, 4 iridium.", x + 120, qualityY + RowHeight);
    }

    private void DrawInlineHint(SpriteBatch spriteBatch, string hint, int x, int y)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return;
        }

        int maxWidth = Math.Max(120, this.Bounds.Right - x - 24);
        int lineY = y;
        foreach (string line in this.GetHintLines(hint, maxWidth))
        {
            spriteBatch.DrawString(Game1.smallFont, line, new Vector2(x, lineY), Color.SaddleBrown, 0f, Vector2.Zero, HintScale, SpriteEffects.None, 1f);
            lineY += (int)(Game1.smallFont.LineSpacing * HintScale) + 1;
        }
    }

    private void DrawAnswerList(SpriteBatch spriteBatch, EventCommandBlock command, EventCommandParameter parameter, int x, int y)
    {
        this.EnsureAnswerListDefaults(command, parameter);
        int count = this.GetAnswerCount(command, parameter);
        this.DrawLine(spriteBatch, parameter.Label, x, y);
        this.DrawButton(spriteBatch, new Rectangle(this.Bounds.Right - 100, y - 8, 76, ButtonHeight), "+ Add", () => this.AddAnswer(command, parameter));

        int fieldWidth = Math.Max(120, this.Bounds.Right - x - 120);
        for (int index = 0; index < count; index++)
        {
            int answerIndex = index;
            int rowY = y + RowHeight * (index + 1);
            this.DrawLine(spriteBatch, $"{index + 1}.", x, rowY);
            this.DrawStringField(
                spriteBatch,
                FieldKey(command, $"{parameter.Key}.{answerIndex}"),
                new Rectangle(x + 36, rowY - 8, fieldWidth - 48, 40),
                () => command.Values.TryGetValue($"{parameter.Key}.{answerIndex}", out string? value) ? value : string.Empty,
                value => command.Values[$"{parameter.Key}.{answerIndex}"] = value,
                parameter.TextLimit
            );

            if (count > 1)
            {
                this.DrawButton(spriteBatch, new Rectangle(this.Bounds.Right - 58, rowY - 8, 34, ButtonHeight), "X", () => this.RemoveAnswer(command, parameter, answerIndex));
            }
        }
    }

    private void DrawRawCommand(SpriteBatch spriteBatch, RawCommandBlock raw, int x, int y)
    {
        this.DrawLine(spriteBatch, "Raw / unsupported command", x, y);
        this.DrawStringField(spriteBatch, $"raw.{RuntimeHelpers.GetHashCode(raw)}", new Rectangle(x, y + RowHeight, Math.Max(160, this.Bounds.Width - 48), 44), () => raw.RawText, value => raw.RawText = value, 240);
    }

    private void OpenActorPicker()
    {
        ModEntry.Instance.RefreshKnownNpcs();
        this.actorPickerOpen = true;
        this.actorPickerFocusPending = true;
        this.actorPickerSearchText = string.Empty;
        this.actorPickerScrollIndex = 0;
        this.textFields.Remove("setup.actorPicker.search");
    }

    private void AddActor(string actorName)
    {
        NpcPlacement actor = new()
        {
            ActorName = actorName,
            TileX = this.state.Cutscene.FarmerPlacement.TileX + 1,
            TileY = this.state.Cutscene.FarmerPlacement.TileY,
            Facing = 3
        };
        this.state.Cutscene.Actors.Add(actor);
        this.state.SelectedSetupActorSlotId = actor.ActorSlotId;
        this.ScrollSelectedActorIntoView();
        this.state.IsDirty = true;
    }

    private void RemoveActor(NpcPlacement actor)
    {
        this.state.Cutscene.Actors.Remove(actor);
        if (this.state.SelectedSetupActorSlotId.Equals(actor.ActorSlotId, StringComparison.Ordinal))
        {
            this.state.SelectedSetupActorSlotId = this.state.Cutscene.Actors.LastOrDefault()?.ActorSlotId
                ?? (this.state.Cutscene.IncludeFarmer ? this.state.Cutscene.FarmerPlacement.ActorSlotId : string.Empty);
        }

        this.setupActorScrollIndex = Math.Clamp(this.setupActorScrollIndex, 0, this.GetMaxSetupActorScrollIndex());
        this.state.IsDirty = true;
    }

    private void SelectSetupActor(NpcPlacement actor)
    {
        this.state.SelectedSetupActorSlotId = actor.ActorSlotId;
    }

    private void ToggleFarmer()
    {
        this.state.Cutscene.IncludeFarmer = !this.state.Cutscene.IncludeFarmer;
        this.state.SelectedSetupActorSlotId = this.state.Cutscene.IncludeFarmer
            ? this.state.Cutscene.FarmerPlacement.ActorSlotId
            : this.state.Cutscene.Actors.FirstOrDefault()?.ActorSlotId ?? string.Empty;
        this.setupActorScrollIndex = Math.Clamp(this.setupActorScrollIndex, 0, this.GetMaxSetupActorScrollIndex());
        this.state.IsDirty = true;
    }

    private void DrawActorPicker(SpriteBatch spriteBatch, int x, int y)
    {
        IReadOnlyList<string> actors = this.GetFilteredNpcNames();
        int visibleRows = Math.Min(PickerMaxRows, Math.Max(1, actors.Count));
        this.actorPickerScrollIndex = Math.Clamp(this.actorPickerScrollIndex, 0, Math.Max(0, actors.Count - PickerMaxRows));
        this.actorPickerBounds = new Rectangle(
            x,
            y,
            Math.Min(340, Math.Max(220, this.Bounds.Right - x - 24)),
            PickerSearchHeight + 24 + visibleRows * PickerRowHeight + (actors.Count > PickerMaxRows ? 28 : 0)
        );

        IClickableMenu.drawTextureBox(spriteBatch, this.actorPickerBounds.X, this.actorPickerBounds.Y, this.actorPickerBounds.Width, this.actorPickerBounds.Height, Color.White);
        this.DrawStringField(
            spriteBatch,
            "setup.actorPicker.search",
            new Rectangle(this.actorPickerBounds.X + 12, this.actorPickerBounds.Y + 12, this.actorPickerBounds.Width - 24, PickerSearchHeight - 8),
            () => this.actorPickerSearchText,
            value =>
            {
                this.actorPickerSearchText = value;
                this.actorPickerScrollIndex = 0;
            },
            64
        );
        if (this.actorPickerFocusPending && this.textFields.TryGetValue("setup.actorPicker.search", out BoundTextField? searchField))
        {
            this.DeselectTextFields();
            searchField.Select();
            this.actorPickerFocusPending = false;
        }

        if (actors.Count == 0)
        {
            this.DrawLine(spriteBatch, "No matches.", this.actorPickerBounds.X + 20, this.actorPickerBounds.Y + PickerSearchHeight + 16, Color.DarkRed);
            return;
        }

        for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
        {
            int actorIndex = this.actorPickerScrollIndex + rowIndex;
            if (actorIndex >= actors.Count)
            {
                break;
            }

            string actorName = actors[actorIndex];
            Rectangle rowBounds = new(
                this.actorPickerBounds.X + 12,
                this.actorPickerBounds.Y + PickerSearchHeight + 12 + rowIndex * PickerRowHeight,
                this.actorPickerBounds.Width - 24,
                PickerRowHeight
            );

            if (rowIndex % 2 == 0)
            {
                spriteBatch.Draw(Game1.staminaRect, rowBounds, Color.LightGoldenrodYellow * 0.25f);
            }

            Utility.drawTextWithShadow(spriteBatch, this.TrimText(actorName, rowBounds.Width - 16), Game1.smallFont, new Vector2(rowBounds.X + 8, rowBounds.Y + 6), Game1.textColor);
            this.actorPickerRows.Add((rowBounds, actorName));
        }

        if (actors.Count > PickerMaxRows)
        {
            string scrollText = $"{this.actorPickerScrollIndex + 1}-{Math.Min(actors.Count, this.actorPickerScrollIndex + PickerMaxRows)} / {actors.Count}";
            Vector2 size = Game1.smallFont.MeasureString(scrollText);
            Utility.drawTextWithShadow(spriteBatch, scrollText, Game1.smallFont, new Vector2(this.actorPickerBounds.Right - size.X - 18, this.actorPickerBounds.Bottom - 28), Color.DimGray);
        }
    }

    private bool HandleActorPickerClick(int x, int y)
    {
        foreach ((Rectangle bounds, string actorName) in this.actorPickerRows)
        {
            if (bounds.Contains(x, y))
            {
                this.AddActor(actorName);
                this.CloseActorPicker();
                return true;
            }
        }

        if (this.actorPickerBounds.Contains(x, y))
        {
            return false;
        }

        this.CloseActorPicker();
        return false;
    }

    private IReadOnlyList<string> GetFilteredNpcNames()
    {
        if (string.IsNullOrWhiteSpace(this.actorPickerSearchText))
        {
            return ModEntry.KnownNpcNames;
        }

        return ModEntry.KnownNpcNames
            .Where(name => name.Contains(this.actorPickerSearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void CloseActorPicker()
    {
        this.actorPickerOpen = false;
        this.actorPickerFocusPending = false;
        this.actorPickerSearchText = string.Empty;
        this.actorPickerScrollIndex = 0;
        this.textFields.Remove("setup.actorPicker.search");
    }

    private void ScrollActorPicker(int direction)
    {
        int delta = direction > 0 ? -1 : 1;
        this.actorPickerScrollIndex = Math.Clamp(this.actorPickerScrollIndex + delta, 0, Math.Max(0, this.GetFilteredNpcNames().Count - PickerMaxRows));
    }

    private void ScrollSetupActors(int direction)
    {
        int delta = direction > 0 ? -1 : 1;
        this.setupActorScrollIndex = Math.Clamp(this.setupActorScrollIndex + delta, 0, this.GetMaxSetupActorScrollIndex());
    }

    private int GetMaxSetupActorScrollIndex()
    {
        int visibleRows = Math.Max(1, this.setupActorListBounds.Height / ActorRowHeight);
        return Math.Max(0, this.GetVisibleSetupActors().Count - visibleRows);
    }

    private void ScrollSelectedActorIntoView()
    {
        IReadOnlyList<NpcPlacement> actors = this.GetVisibleSetupActors();
        int selectedIndex = actors.ToList().FindIndex(actor => actor.ActorSlotId.Equals(this.state.SelectedSetupActorSlotId, StringComparison.Ordinal));
        if (selectedIndex < 0)
        {
            return;
        }

        int visibleRows = Math.Max(1, this.setupActorListBounds.Height / ActorRowHeight);
        if (selectedIndex < this.setupActorScrollIndex)
        {
            this.setupActorScrollIndex = selectedIndex;
        }
        else if (selectedIndex >= this.setupActorScrollIndex + visibleRows)
        {
            this.setupActorScrollIndex = selectedIndex - visibleRows + 1;
        }

        this.setupActorScrollIndex = Math.Clamp(this.setupActorScrollIndex, 0, this.GetMaxSetupActorScrollIndex());
    }

    private void CycleSetupActor(NpcPlacement actor, int direction)
    {
        ModEntry.Instance.RefreshKnownNpcs();
        if (ModEntry.KnownNpcNames.Count == 0)
        {
            return;
        }

        int currentIndex = Math.Max(0, ModEntry.KnownNpcNames.FindIndex(name => string.Equals(name, actor.ActorName, StringComparison.OrdinalIgnoreCase)));
        actor.ActorName = ModEntry.KnownNpcNames[WrapIndex(currentIndex + Math.Sign(direction), ModEntry.KnownNpcNames.Count)];
        this.RenameActorReferences(actor);
        this.state.IsDirty = true;
    }

    private void RenameActorReferences(NpcPlacement actor)
    {
        foreach (EventCommandBlock command in this.state.Cutscene.Commands.OfType<EventCommandBlock>())
        {
            foreach (KeyValuePair<string, string> pair in command.ActorSlotIds)
            {
                if (pair.Value.Equals(actor.ActorSlotId, StringComparison.Ordinal))
                {
                    command.Values[pair.Key] = actor.ActorName;
                }
            }
        }
    }

    private void EnsureActorSlotLinks(EventCommandBlock command, EventCommandDefinition definition)
    {
        foreach (EventCommandParameter parameter in definition.Parameters.Where(parameter => parameter.Type is EventCommandParameterType.Actor or EventCommandParameterType.OptionalActor))
        {
            if (command.ActorSlotIds.ContainsKey(parameter.Key))
            {
                continue;
            }

            string actorName = this.GetValue(command, parameter).TrimEnd('?');
            NpcPlacement? actor = this.ResolveActorByName(actorName);
            if (actor is not null)
            {
                command.ActorSlotIds[parameter.Key] = actor.ActorSlotId;
                command.Values[parameter.Key] = actor.ActorName;
            }
        }
    }

    private string ResolveActorName(EventCommandBlock command, string parameterKey)
    {
        if (command.ActorSlotIds.TryGetValue(parameterKey, out string? actorSlotId))
        {
            NpcPlacement? actor = this.GetAllActors().FirstOrDefault(actor => actor.ActorSlotId.Equals(actorSlotId, StringComparison.Ordinal));
            if (actor is not null)
            {
                command.Values[parameterKey] = actor.ActorName;
                return actor.ActorName;
            }
        }

        return command.Values.TryGetValue(parameterKey, out string? actorName) && !string.IsNullOrWhiteSpace(actorName)
            ? actorName
            : "farmer";
    }

    private void CycleActor(EventCommandBlock command, string parameterKey, int direction)
    {
        List<NpcPlacement> actors = this.GetSelectableActors().ToList();
        if (actors.Count == 0)
        {
            return;
        }

        int currentIndex = Math.Max(0, actors.FindIndex(actor =>
            command.ActorSlotIds.TryGetValue(parameterKey, out string? slotId)
            && actor.ActorSlotId.Equals(slotId, StringComparison.Ordinal)));
        NpcPlacement selectedActor = actors[WrapIndex(currentIndex + Math.Sign(direction), actors.Count)];
        command.ActorSlotIds[parameterKey] = selectedActor.ActorSlotId;
        command.Values[parameterKey] = selectedActor.ActorName;
        this.state.IsDirty = true;
    }

    private NpcPlacement? ResolveActorByName(string actorName)
    {
        List<NpcPlacement> matches = this.GetAllActors()
            .Where(actor => actor.ActorName.Equals(actorName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private IEnumerable<NpcPlacement> GetAllActors()
    {
        yield return this.state.Cutscene.FarmerPlacement;
        foreach (NpcPlacement actor in this.state.Cutscene.Actors)
        {
            yield return actor;
        }
    }

    private IEnumerable<NpcPlacement> GetSelectableActors()
    {
        if (this.state.Cutscene.IncludeFarmer)
        {
            yield return this.state.Cutscene.FarmerPlacement;
        }

        foreach (NpcPlacement actor in this.state.Cutscene.Actors.Where(actor => !string.IsNullOrWhiteSpace(actor.ActorName)))
        {
            yield return actor;
        }
    }

    private string GetValue(EventCommandBlock command, EventCommandParameter parameter)
    {
        return command.Values.TryGetValue(parameter.Key, out string? value) ? value : parameter.DefaultValue;
    }

    private string GetRewardTargetValue(EventCommandBlock command, RewardFieldSpec spec)
    {
        if (command.Values.TryGetValue("target", out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        command.Values["target"] = spec.DefaultTarget;
        return spec.DefaultTarget;
    }

    private string GetRewardAmountValue(EventCommandBlock command, RewardFieldSpec spec)
    {
        if (command.Values.TryGetValue("amount", out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        command.Values["amount"] = spec.DefaultAmount;
        return spec.DefaultAmount;
    }

    private EventCommandBlock? GetSelectedEventCommand()
    {
        return this.state.SelectedCommandIndex >= 0
            && this.state.SelectedCommandIndex < this.state.Cutscene.Commands.Count
            && this.state.Cutscene.Commands[this.state.SelectedCommandIndex] is EventCommandBlock command
            ? command
            : null;
    }

    private void EnsureAnswerListDefaults(EventCommandBlock command, EventCommandParameter parameter)
    {
        if (command.Values.ContainsKey($"{parameter.Key}.count"))
        {
            return;
        }

        string[] defaults = parameter.DefaultValue.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        command.Values[$"{parameter.Key}.count"] = Math.Max(1, defaults.Length).ToString();
        for (int index = 0; index < defaults.Length; index++)
        {
            command.Values[$"{parameter.Key}.{index}"] = defaults[index];
        }

        if (defaults.Length == 0)
        {
            command.Values[$"{parameter.Key}.0"] = string.Empty;
        }
    }

    private int GetAnswerCount(EventCommandBlock? command, EventCommandParameter parameter)
    {
        if (command is null)
        {
            return 1;
        }

        this.EnsureAnswerListDefaults(command, parameter);
        return command.Values.TryGetValue($"{parameter.Key}.count", out string? value) && int.TryParse(value, out int count)
            ? Math.Max(1, count)
            : 1;
    }

    private void AddAnswer(EventCommandBlock command, EventCommandParameter parameter)
    {
        this.CommitSelectedTextFields();
        int count = this.GetAnswerCount(command, parameter);
        command.Values[$"{parameter.Key}.{count}"] = "Answer";
        command.Values[$"{parameter.Key}.count"] = (count + 1).ToString();
        this.ClearAnswerTextFields(command, parameter);
        this.state.IsDirty = true;
    }

    private void RemoveAnswer(EventCommandBlock command, EventCommandParameter parameter, int answerIndex)
    {
        this.CommitSelectedTextFields();
        int count = this.GetAnswerCount(command, parameter);
        if (count <= 1)
        {
            return;
        }

        for (int index = answerIndex; index < count - 1; index++)
        {
            command.Values[$"{parameter.Key}.{index}"] = command.Values.TryGetValue($"{parameter.Key}.{index + 1}", out string? nextAnswer)
                ? nextAnswer
                : string.Empty;
        }

        command.Values.Remove($"{parameter.Key}.{count - 1}");
        command.Values[$"{parameter.Key}.count"] = (count - 1).ToString();
        this.ClearAnswerTextFields(command, parameter);
        this.state.IsDirty = true;
    }

    public void CommitSelectedTextFields()
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            if (field.Selected)
            {
                field.CommitAndDeselect();
            }
        }
    }

    private void ClearAnswerTextFields(EventCommandBlock command, EventCommandParameter parameter)
    {
        string keyPrefix = FieldKey(command, $"{parameter.Key}.");
        foreach (string key in this.textFields.Keys.Where(key => key.StartsWith(keyPrefix, StringComparison.Ordinal)).ToList())
        {
            this.textFields.Remove(key);
        }
    }

    private string FormatBoolean(EventCommandBlock command, EventCommandParameter parameter)
    {
        string value = this.GetValue(command, parameter);
        return string.IsNullOrWhiteSpace(value) ? "(omit)" : value;
    }

    private string FormatChoice(EventCommandBlock command, EventCommandParameter parameter)
    {
        string value = this.GetValue(command, parameter);
        if (parameter.Type == EventCommandParameterType.Direction && int.TryParse(value, out int direction))
        {
            return FacingName(direction);
        }

        return string.IsNullOrWhiteSpace(value) ? "(omit)" : value;
    }

    private void CycleBoolean(EventCommandBlock command, EventCommandParameter parameter)
    {
        string current = this.GetValue(command, parameter);
        command.Values[parameter.Key] = parameter.Optional
            ? string.IsNullOrWhiteSpace(current) ? "true" : current.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : string.Empty
            : current.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
        this.state.IsDirty = true;
    }

    private void CycleChoice(EventCommandBlock command, EventCommandParameter parameter, int direction)
    {
        if (parameter.Choices.Count == 0)
        {
            return;
        }

        string current = this.GetValue(command, parameter);
        int currentIndex = Math.Max(0, parameter.Choices.ToList().FindIndex(choice => choice.Equals(current, StringComparison.Ordinal)));
        command.Values[parameter.Key] = parameter.Choices[WrapIndex(currentIndex + Math.Sign(direction), parameter.Choices.Count)];
        if (parameter.Type == EventCommandParameterType.RewardKind)
        {
            RewardFieldSpec spec = GetRewardFieldSpec(command.Values[parameter.Key]);
            command.Values["target"] = spec.DefaultTarget;
            command.Values["amount"] = spec.DefaultAmount;
            command.Values["quality"] = string.Empty;
            this.textFields.Remove(FieldKey(command, "target"));
            this.textFields.Remove(FieldKey(command, "amount"));
            this.textFields.Remove(FieldKey(command, "quality"));
        }

        this.state.IsDirty = true;
    }

    private void CyclePlacementFacing(NpcPlacement placement, int direction)
    {
        placement.Facing = WrapIndex(placement.Facing + Math.Sign(direction), 4);
        this.state.IsDirty = true;
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Action leftClick, Action? rightClick = null)
    {
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        Vector2 labelSize = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(spriteBatch, label, Game1.smallFont, new Vector2(bounds.Center.X - labelSize.X / 2f, bounds.Center.Y - labelSize.Y / 2f), Game1.textColor);
        this.buttons.Add((bounds, leftClick, rightClick));
    }

    private int GetParameterButtonX(int labelX)
    {
        return Math.Max(labelX + 120, this.Bounds.Right - 24 - 160);
    }

    private void DrawStringField(SpriteBatch spriteBatch, string key, Rectangle bounds, Func<string> getValue, Action<string> setValue, int textLimit, bool numbersOnly = false)
    {
        BoundTextField field = this.GetTextField(key, getValue, value =>
        {
            setValue(value);
            this.state.IsDirty = true;
        }, numbersOnly, textLimit);
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

    private IReadOnlyList<string> GetHintLines(string text, int maxWidth)
    {
        return this.WrapHint(text, maxWidth).ToList();
    }

    private IEnumerable<string> WrapHint(string text, int maxWidth)
    {
        foreach (string paragraph in text.Split('\n'))
        {
            string current = string.Empty;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (Game1.smallFont.MeasureString(candidate).X * HintScale <= maxWidth || string.IsNullOrEmpty(current))
                {
                    current = candidate;
                    continue;
                }

                yield return current;
                current = word;
            }

            if (!string.IsNullOrEmpty(current))
            {
                yield return current;
            }
        }
    }

    private static string FieldKey(EventCommandBlock command, string fieldName)
    {
        return $"{RuntimeHelpers.GetHashCode(command)}.{fieldName}";
    }

    private static string FacingName(int facing)
    {
        return facing switch
        {
            0 => "Up",
            1 => "Right",
            2 => "Down",
            3 => "Left",
            _ => facing.ToString()
        };
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

    private string TrimText(string text, int maxPixelWidth)
    {
        if (Game1.smallFont.MeasureString(text).X <= maxPixelWidth)
        {
            return text;
        }

        string trimmed = text;
        while (trimmed.Length > 3 && Game1.smallFont.MeasureString(trimmed + "...").X > maxPixelWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed + "...";
    }

    private static RewardFieldSpec GetRewardFieldSpec(string kind)
    {
        return kind switch
        {
            "Gold" => new RewardFieldSpec("Gold", "Amount of gold to add. Use a negative value to remove gold.", "500", ShowAmount: false),
            "Friendship" => new RewardFieldSpec("NPC", "NPC internal name.", "Lewis", "Friendship Points", "250", "250 points = 1 heart."),
            "Mail" => new RewardFieldSpec("Mail ID", "Mail flag to queue for tomorrow.", "exampleLetter", ShowAmount: false),
            "Quest" => new RewardFieldSpec("Quest ID", "Quest ID to add.", "1", ShowAmount: false),
            "Cooking Recipe" => new RewardFieldSpec("Recipe Name", "Cooking recipe name.", "Fried Egg", ShowAmount: false),
            "Crafting Recipe" => new RewardFieldSpec("Recipe Name", "Crafting recipe name.", "Chest", ShowAmount: false),
            _ => new RewardFieldSpec("Item ID", "Qualified item ID or vanilla item ID.", "(O)74", "Count", "1", "Number of items to add.", ShowQuality: true)
        };
    }

    private readonly record struct RewardFieldSpec(
        string TargetLabel,
        string TargetHint,
        string DefaultTarget,
        string AmountLabel = "",
        string DefaultAmount = "",
        string AmountHint = "",
        bool ShowAmount = true,
        bool ShowQuality = false,
        int TargetTextLimit = 120
    );
}
