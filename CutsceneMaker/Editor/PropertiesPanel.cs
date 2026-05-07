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
    private const int ButtonHeight = 32;
    private const int ButtonGap = 8;
    private readonly EditorState state;
    private readonly Action openPreconditionEditor;
    private readonly List<(Rectangle Bounds, Action LeftClick, Action? RightClick)> buttons = new();
    private readonly Dictionary<string, BoundTextField> textFields = new();

    public PropertiesPanel(EditorState state, Action openPreconditionEditor)
        : base("Properties")
    {
        this.state = state;
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
            case TimelineCommand timelineCommand:
                this.DrawTimelineCommand(spriteBatch, timelineCommand, x, y);
                break;

            case RawCommandBlock raw:
                this.DrawLine(spriteBatch, "Raw / unsupported command", x, y);
                this.DrawLine(spriteBatch, raw.RawText, x, y + 34);
                break;

            default:
                this.DrawLine(spriteBatch, "Unknown command.", x, y);
                break;
        }
    }

    public override void ReceiveLeftClick(int x, int y)
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

    private void DrawTimelineCommand(SpriteBatch spriteBatch, TimelineCommand command, int x, int y)
    {
        this.DrawLine(spriteBatch, command.Type.ToString(), x, y);
        this.EnsureActorSlotLink(command);

        switch (command.Type)
        {
            case CommandType.Move:
                this.DrawLine(spriteBatch, $"Actor: {this.ResolveCommandActorName(command)}", x, y + RowHeight);
                this.DrawLine(spriteBatch, "Tile X", x, y + RowHeight * 2);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "move.tileX"), new Rectangle(x + 90, y + RowHeight * 2 - 8, 78, 40), () => command.TileX ?? 0, value => command.TileX = value);
                this.DrawLine(spriteBatch, "Y", x + 180, y + RowHeight * 2);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "move.tileY"), new Rectangle(x + 210, y + RowHeight * 2 - 8, 78, 40), () => command.TileY ?? 0, value => command.TileY = value);
                this.DrawLine(spriteBatch, $"Facing: {FacingName(command.Facing ?? 2)}", x, y + RowHeight * 3);
                this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 4, 120, ButtonHeight), "Actor", () => this.CycleActor(command, 1), () => this.CycleActor(command, -1));
                this.DrawButton(spriteBatch, new Rectangle(x + 128, y + RowHeight * 4, 136, ButtonHeight), "Facing", () => this.CycleFacing(command, 1), () => this.CycleFacing(command, -1));
                break;

            case CommandType.Speak:
                this.DrawLine(spriteBatch, $"Actor: {this.ResolveCommandActorName(command)}", x, y + RowHeight);
                this.DrawLine(spriteBatch, "Dialogue", x, y + RowHeight * 2);
                this.DrawStringField(spriteBatch, CommandFieldKey(command, "speak.dialogue"), new Rectangle(x, y + RowHeight * 3, Math.Max(160, this.Bounds.Width - 48), 44), () => command.DialogueText ?? string.Empty, value => command.DialogueText = value, textLimit: 220);
                this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 5, 120, ButtonHeight), "Actor", () => this.CycleActor(command, 1), () => this.CycleActor(command, -1));
                break;

            case CommandType.Pause:
                this.DrawLine(spriteBatch, "Duration", x, y + RowHeight);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "pause.duration"), new Rectangle(x + 120, y + RowHeight - 8, 120, 40), () => command.DurationMs ?? 0, value => command.DurationMs = Math.Max(0, value));
                this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 2, 72, ButtonHeight), "-100", () => this.ChangePause(command, -100));
                this.DrawButton(spriteBatch, new Rectangle(x + 80, y + RowHeight * 2, 72, ButtonHeight), "+100", () => this.ChangePause(command, 100));
                break;

            case CommandType.Reward:
                this.DrawLine(spriteBatch, $"Reward: {command.RewardType?.ToString() ?? "Item"}", x, y + RowHeight);
                this.DrawLine(spriteBatch, this.GetRewardSummary(command), x, y + RowHeight * 2);
                this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 3, 120, ButtonHeight), "Type", () => this.CycleRewardType(command, 1), () => this.CycleRewardType(command, -1));
                this.DrawButton(spriteBatch, new Rectangle(x + 128, y + RowHeight * 3, 72, ButtonHeight), "-1", () => this.ChangeRewardAmount(command, -1));
                this.DrawButton(spriteBatch, new Rectangle(x + 208, y + RowHeight * 3, 72, ButtonHeight), "+1", () => this.ChangeRewardAmount(command, 1));
                this.DrawRewardFields(spriteBatch, command, x, y + RowHeight * 5);
                break;

            case CommandType.Emote:
                this.DrawLine(spriteBatch, $"Actor: {this.ResolveCommandActorName(command)}", x, y + RowHeight);
                this.DrawLine(spriteBatch, $"Emote: {GetEmoteName(command.EmoteId ?? 8)}", x, y + RowHeight * 2);
                this.DrawLine(spriteBatch, "ID", x, y + RowHeight * 3);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "emote.id"), new Rectangle(x + 48, y + RowHeight * 3 - 8, 88, 40), () => command.EmoteId ?? 8, value => command.EmoteId = Math.Max(0, value));
                this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 4, 120, ButtonHeight), "Actor", () => this.CycleActor(command, 1), () => this.CycleActor(command, -1));
                this.DrawButton(spriteBatch, new Rectangle(x + 128, y + RowHeight * 4, 72, ButtonHeight), "-1", () => this.ChangeEmote(command, -1));
                this.DrawButton(spriteBatch, new Rectangle(x + 208, y + RowHeight * 4, 72, ButtonHeight), "+1", () => this.ChangeEmote(command, 1));
                this.DrawEmoteReference(spriteBatch, x, y + RowHeight * 6);
                break;

            case CommandType.FadeOut:
            case CommandType.FadeIn:
                this.DrawLine(spriteBatch, "No parameters.", x, y + RowHeight);
                break;

            case CommandType.End:
                this.DrawLine(spriteBatch, "Final command, always preserved.", x, y + RowHeight);
                break;
        }
    }

    public override void ReceiveKeyPress(Keys key)
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

    private void DrawLine(SpriteBatch spriteBatch, string text, int x, int y)
    {
        Utility.drawTextWithShadow(
            spriteBatch,
            text,
            Game1.smallFont,
            new Vector2(x, y),
            Game1.textColor
        );
    }

    private void DrawSetup(SpriteBatch spriteBatch, int x, int y)
    {
        this.DrawLine(spriteBatch, "Setup", x, y);
        this.DrawLine(spriteBatch, $"Location: {this.state.Cutscene.LocationName}", x, y + RowHeight);
        this.DrawLine(spriteBatch, $"Music: {this.state.Cutscene.MusicTrack}", x, y + RowHeight * 2);
        this.DrawLine(spriteBatch, $"Skippable: {this.state.Cutscene.Skippable}", x, y + RowHeight * 3);
        this.DrawButton(spriteBatch, new Rectangle(x, y + RowHeight * 4, 150, ButtonHeight), "Toggle Skip", () =>
        {
            this.state.Cutscene.Skippable = !this.state.Cutscene.Skippable;
            this.state.IsDirty = true;
        });
        this.DrawButton(spriteBatch, new Rectangle(x + 160, y + RowHeight * 4, 160, ButtonHeight), "Edit Triggers", this.openPreconditionEditor);

        int actorY = y + RowHeight * 6;
        this.DrawLine(spriteBatch, $"Actors: {this.state.Cutscene.Actors.Count}", x, actorY);
        this.DrawButton(spriteBatch, new Rectangle(x + 120, actorY - 8, 72, ButtonHeight), "+", this.AddActor);

        actorY += RowHeight;
        for (int i = 0; i < this.state.Cutscene.Actors.Count; i++)
        {
            NpcPlacement actor = this.state.Cutscene.Actors[i];
            this.DrawLine(spriteBatch, actor.ActorName, x, actorY);
            this.DrawIntegerField(spriteBatch, $"setup.actor.{i}.x", new Rectangle(x + 112, actorY - 8, 52, 40), () => actor.TileX, value => actor.TileX = value);
            this.DrawIntegerField(spriteBatch, $"setup.actor.{i}.y", new Rectangle(x + 170, actorY - 8, 52, 40), () => actor.TileY, value => actor.TileY = value);
            int controlsY = actorY + RowHeight - 2;
            this.DrawButton(spriteBatch, new Rectangle(x, controlsY, 86, ButtonHeight), FacingName(actor.Facing), () => this.CyclePlacementFacing(actor, 1), () => this.CyclePlacementFacing(actor, -1));
            this.DrawButton(spriteBatch, new Rectangle(x + 96, controlsY, 76, ButtonHeight), "NPC", () => this.CycleSetupActor(actor, 1), () => this.CycleSetupActor(actor, -1));
            this.DrawButton(spriteBatch, new Rectangle(x + 182, controlsY, 48, ButtonHeight), "X", () => this.RemoveActor(actor));
            actorY += RowHeight * 2;
        }
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

    private void DrawRewardFields(SpriteBatch spriteBatch, TimelineCommand command, int x, int y)
    {
        switch (command.RewardType ?? RewardType.Item)
        {
            case RewardType.Item:
                this.DrawLine(spriteBatch, "Item ID", x, y);
                this.DrawStringField(spriteBatch, CommandFieldKey(command, "reward.itemId"), new Rectangle(x + 90, y - 8, 160, 40), () => command.ItemId ?? string.Empty, value => command.ItemId = value, textLimit: 80);
                this.DrawLine(spriteBatch, "Qty", x, y + RowHeight);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "reward.qty"), new Rectangle(x + 90, y + RowHeight - 8, 80, 40), () => command.Quantity ?? 1, value => command.Quantity = Math.Max(1, value));
                break;

            case RewardType.Gold:
                this.DrawLine(spriteBatch, "Gold", x, y);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "reward.gold"), new Rectangle(x + 90, y - 8, 120, 40), () => command.GoldAmount ?? 0, value => command.GoldAmount = Math.Max(0, value));
                break;

            case RewardType.Friendship:
                this.DrawLine(spriteBatch, "NPC", x, y);
                this.DrawStringField(spriteBatch, CommandFieldKey(command, "reward.npc"), new Rectangle(x + 90, y - 8, 160, 40), () => command.RewardNpcName ?? string.Empty, value => command.RewardNpcName = value, textLimit: 80);
                this.DrawLine(spriteBatch, "Points", x, y + RowHeight);
                this.DrawIntegerField(spriteBatch, CommandFieldKey(command, "reward.friendship"), new Rectangle(x + 90, y + RowHeight - 8, 120, 40), () => command.FriendshipAmount ?? 0, value => command.FriendshipAmount = Math.Max(0, value));
                break;

            default:
                this.DrawLine(spriteBatch, "ID", x, y);
                this.DrawStringField(spriteBatch, CommandFieldKey(command, "reward.id"), new Rectangle(x + 90, y - 8, 180, 40), () => command.ItemId ?? string.Empty, value => command.ItemId = value, textLimit: 100);
                break;
        }
    }

    private static string CommandFieldKey(TimelineCommand command, string fieldName)
    {
        return $"{RuntimeHelpers.GetHashCode(command)}.{fieldName}";
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

    private void CycleActor(TimelineCommand command, int direction)
    {
        List<NpcPlacement> actors = new() { this.state.Cutscene.FarmerPlacement };
        actors.AddRange(this.state.Cutscene.Actors.Where(actor => !string.IsNullOrWhiteSpace(actor.ActorName)));
        int currentIndex = Math.Max(0, actors.FindIndex(actor => string.Equals(actor.ActorSlotId, command.ActorSlotId, StringComparison.Ordinal)));
        NpcPlacement selectedActor = actors[WrapIndex(currentIndex + Math.Sign(direction), actors.Count)];
        command.ActorSlotId = selectedActor.ActorSlotId;
        command.ActorName = selectedActor.ActorName;
        this.state.IsDirty = true;
    }

    private void AddActor()
    {
        ModEntry.Instance.RefreshKnownNpcs();
        string actorName = ModEntry.KnownNpcNames.FirstOrDefault(name => name.Equals("Penny", StringComparison.OrdinalIgnoreCase))
            ?? ModEntry.KnownNpcNames.FirstOrDefault()
            ?? "Penny";
        this.state.Cutscene.Actors.Add(new NpcPlacement
        {
            ActorName = actorName,
            TileX = this.state.Cutscene.FarmerPlacement.TileX + 1,
            TileY = this.state.Cutscene.FarmerPlacement.TileY,
            Facing = 3
        });
        this.state.IsDirty = true;
    }

    private void RemoveActor(NpcPlacement actor)
    {
        this.state.Cutscene.Actors.Remove(actor);
        this.state.IsDirty = true;
    }

    private void CycleSetupActor(NpcPlacement actor, int direction)
    {
        this.BackfillActorSlotLinks();
        ModEntry.Instance.RefreshKnownNpcs();
        if (ModEntry.KnownNpcNames.Count == 0)
        {
            return;
        }

        int currentIndex = Math.Max(0, ModEntry.KnownNpcNames.FindIndex(name => string.Equals(name, actor.ActorName, StringComparison.OrdinalIgnoreCase)));
        actor.ActorName = ModEntry.KnownNpcNames[WrapIndex(currentIndex + Math.Sign(direction), ModEntry.KnownNpcNames.Count)];
        this.RenameTimelineActorReferences(actor);
        this.state.IsDirty = true;
    }

    private void RenameTimelineActorReferences(NpcPlacement actor)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorSlotId) || string.IsNullOrWhiteSpace(actor.ActorName))
        {
            return;
        }

        foreach (object command in this.state.Cutscene.Commands)
        {
            if (command is TimelineCommand timelineCommand
                && timelineCommand.ActorSlotId?.Equals(actor.ActorSlotId, StringComparison.Ordinal) == true)
            {
                timelineCommand.ActorName = actor.ActorName;
            }
        }
    }

    private void BackfillActorSlotLinks()
    {
        foreach (object command in this.state.Cutscene.Commands)
        {
            if (command is TimelineCommand timelineCommand)
            {
                this.EnsureActorSlotLink(timelineCommand);
            }
        }
    }

    private void EnsureActorSlotLink(TimelineCommand command)
    {
        if (!CommandUsesActor(command.Type))
        {
            return;
        }

        NpcPlacement? actor = this.ResolveCommandActor(command);
        if (actor is null)
        {
            return;
        }

        command.ActorSlotId = actor.ActorSlotId;
        command.ActorName = actor.ActorName;
    }

    private NpcPlacement? ResolveCommandActor(TimelineCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ActorSlotId))
        {
            NpcPlacement? actorBySlot = this.GetAllActors()
                .FirstOrDefault(actor => actor.ActorSlotId.Equals(command.ActorSlotId, StringComparison.Ordinal));
            if (actorBySlot is not null)
            {
                return actorBySlot;
            }
        }

        string actorName = string.IsNullOrWhiteSpace(command.ActorName) ? "farmer" : command.ActorName!;
        List<NpcPlacement> nameMatches = this.GetAllActors()
            .Where(actor => actor.ActorName.Equals(actorName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return nameMatches.Count == 1 ? nameMatches[0] : null;
    }

    private string ResolveCommandActorName(TimelineCommand command)
    {
        return this.ResolveCommandActor(command)?.ActorName
            ?? (string.IsNullOrWhiteSpace(command.ActorName) ? "farmer" : command.ActorName!);
    }

    private IEnumerable<NpcPlacement> GetAllActors()
    {
        yield return this.state.Cutscene.FarmerPlacement;
        foreach (NpcPlacement actor in this.state.Cutscene.Actors)
        {
            yield return actor;
        }
    }

    private static bool CommandUsesActor(CommandType commandType)
    {
        return commandType is CommandType.Move or CommandType.Speak or CommandType.Emote;
    }

    private void CyclePlacementFacing(NpcPlacement placement, int direction)
    {
        placement.Facing = WrapIndex(placement.Facing + Math.Sign(direction), 4);
        this.state.IsDirty = true;
    }

    private void CycleFacing(TimelineCommand command, int direction)
    {
        command.Facing = WrapIndex((command.Facing ?? 2) + Math.Sign(direction), 4);
        this.state.IsDirty = true;
    }

    private void ChangePause(TimelineCommand command, int delta)
    {
        command.DurationMs = Math.Max(0, (command.DurationMs ?? 0) + delta);
        this.state.IsDirty = true;
    }

    private void ChangeEmote(TimelineCommand command, int delta)
    {
        command.EmoteId = Math.Max(0, (command.EmoteId ?? 8) + delta);
        this.state.IsDirty = true;
    }

    private void CycleRewardType(TimelineCommand command, int direction)
    {
        RewardType[] values = Enum.GetValues<RewardType>();
        int currentIndex = Array.IndexOf(values, command.RewardType ?? RewardType.Item);
        command.RewardType = values[WrapIndex(currentIndex + Math.Sign(direction), values.Length)];
        this.EnsureRewardDefaults(command);
        this.state.IsDirty = true;
    }

    private void ChangeRewardAmount(TimelineCommand command, int delta)
    {
        if ((command.RewardType ?? RewardType.Item) == RewardType.Gold)
        {
            command.GoldAmount = Math.Max(0, (command.GoldAmount ?? 0) + delta * 100);
        }
        else if ((command.RewardType ?? RewardType.Item) == RewardType.Friendship)
        {
            command.FriendshipAmount = Math.Max(0, (command.FriendshipAmount ?? 0) + delta * 10);
        }
        else
        {
            command.Quantity = Math.Max(1, (command.Quantity ?? 1) + delta);
        }

        this.state.IsDirty = true;
    }

    private void EnsureRewardDefaults(TimelineCommand command)
    {
        switch (command.RewardType ?? RewardType.Item)
        {
            case RewardType.Item:
                command.ItemId ??= "(O)74";
                command.Quantity ??= 1;
                break;

            case RewardType.Gold:
                command.GoldAmount ??= 100;
                break;

            case RewardType.Friendship:
                command.RewardNpcName ??= this.state.Cutscene.Actors.FirstOrDefault()?.ActorName ?? "Lewis";
                command.FriendshipAmount ??= 100;
                break;

            default:
                command.ItemId ??= "ExampleId";
                break;
        }
    }

    private string GetRewardSummary(TimelineCommand command)
    {
        return (command.RewardType ?? RewardType.Item) switch
        {
            RewardType.Item => $"{command.ItemId ?? "(O)74"} x{command.Quantity ?? 1}",
            RewardType.Gold => $"{command.GoldAmount ?? 0}g",
            RewardType.Friendship => $"{command.RewardNpcName ?? "Lewis"} +{command.FriendshipAmount ?? 0}",
            _ => command.ItemId ?? "ExampleId"
        };
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

    private void DrawEmoteReference(SpriteBatch spriteBatch, int x, int y)
    {
        this.DrawLine(spriteBatch, "Common emotes:", x, y);
        this.DrawLine(spriteBatch, "8 Exclaim   16 Heart", x, y + RowHeight);
        this.DrawLine(spriteBatch, "32 Sleep    40 Question", x, y + RowHeight * 2);
    }

    private static string GetEmoteName(int emoteId)
    {
        return emoteId switch
        {
            8 => "Exclaim",
            16 => "Heart",
            32 => "Sleep",
            40 => "Question",
            _ => "Custom"
        };
    }
}
