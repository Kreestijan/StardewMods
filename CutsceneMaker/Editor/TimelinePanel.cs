using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public sealed class TimelinePanel : EditorPanel
{
    private static readonly CommandType[] AddableCommandTypes =
    {
        CommandType.Move,
        CommandType.Speak,
        CommandType.Emote,
        CommandType.Pause,
        CommandType.FadeOut,
        CommandType.FadeIn,
        CommandType.Reward
    };

    private const int BlockWidth = 132;
    private const int BlockHeight = 58;
    private const int BlockGap = 10;
    private const int AddButtonWidth = 112;
    private const int MenuRowHeight = 40;
    private readonly EditorState state;
    private readonly List<(Rectangle Bounds, int CommandIndex)> commandBlocks = new();
    private readonly List<(Rectangle Bounds, CommandType Type)> addMenuRows = new();
    private Rectangle? deleteButtonBounds;
    private Rectangle? duplicateButtonBounds;
    private Rectangle setupBlockBounds;
    private Rectangle addButtonBounds;
    private bool addMenuOpen;
    private int? contextCommandIndex;

    public TimelinePanel(EditorState state)
        : base("Timeline")
    {
        this.state = state;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        this.RebuildBlockBounds();

        this.DrawBlock(spriteBatch, this.setupBlockBounds, "Setup", this.state.SelectedCommandIndex == -1);
        foreach ((Rectangle bounds, int commandIndex) in this.commandBlocks)
        {
            object command = this.state.Cutscene.Commands[commandIndex];
            string label = command switch
            {
                TimelineCommand timelineCommand => timelineCommand.Type.ToString(),
                RawCommandBlock => "Raw",
                _ => "Unknown"
            };

            this.DrawBlock(spriteBatch, bounds, label, this.state.SelectedCommandIndex == commandIndex);
        }

        IClickableMenu.drawTextureBox(
            spriteBatch,
            this.addButtonBounds.X,
            this.addButtonBounds.Y,
            this.addButtonBounds.Width,
            this.addButtonBounds.Height,
            Color.White
        );
        Utility.drawTextWithShadow(
            spriteBatch,
            "+ Add",
            Game1.smallFont,
            new Vector2(this.addButtonBounds.X + 24, this.addButtonBounds.Y + 14),
            Game1.textColor
        );

        if (this.addMenuOpen)
        {
            this.DrawAddMenu(spriteBatch);
        }

        if (this.contextCommandIndex.HasValue)
        {
            this.DrawContextMenu(spriteBatch);
        }
    }

    public override void ReceiveLeftClick(int x, int y)
    {
        this.RebuildBlockBounds();
        if (this.TryHandleOverlayClick(x, y))
        {
            return;
        }

        if (this.setupBlockBounds.Contains(x, y))
        {
            this.state.SelectedCommandIndex = -1;
            this.CloseTransientMenus();
            return;
        }

        foreach ((Rectangle bounds, int commandIndex) in this.commandBlocks)
        {
            if (bounds.Contains(x, y))
            {
                this.state.SelectedCommandIndex = commandIndex;
                this.CloseTransientMenus();
                return;
            }
        }

        if (this.addButtonBounds.Contains(x, y))
        {
            this.addMenuOpen = !this.addMenuOpen;
            this.contextCommandIndex = null;
            return;
        }

        this.CloseTransientMenus();
    }

    public override void ReceiveRightClick(int x, int y)
    {
        this.RebuildBlockBounds();
        this.addMenuOpen = false;

        foreach ((Rectangle bounds, int commandIndex) in this.commandBlocks)
        {
            if (bounds.Contains(x, y) && this.CanEditCommand(commandIndex))
            {
                this.state.SelectedCommandIndex = commandIndex;
                this.contextCommandIndex = commandIndex;
                this.deleteButtonBounds = new Rectangle(x, y, 116, 38);
                this.duplicateButtonBounds = new Rectangle(x, y + 38, 116, 38);
                return;
            }
        }

        this.contextCommandIndex = null;
    }

    public bool WantsClick(int x, int y)
    {
        this.RebuildBlockBounds();
        return (this.addMenuOpen && this.addMenuRows.Any(row => row.Bounds.Contains(x, y)))
            || (this.contextCommandIndex.HasValue
                && ((this.deleteButtonBounds.HasValue && this.deleteButtonBounds.Value.Contains(x, y))
                    || (this.duplicateButtonBounds.HasValue && this.duplicateButtonBounds.Value.Contains(x, y))));
    }

    private void InsertDefaultCommand(CommandType commandType)
    {
        int insertIndex = Math.Max(0, this.state.Cutscene.Commands.Count - 1);
        this.state.Cutscene.Commands.Insert(insertIndex, CreateDefaultCommand(commandType));
        this.state.SelectedCommandIndex = insertIndex;
        this.state.IsDirty = true;
        this.CloseTransientMenus();
    }

    private static TimelineCommand CreateDefaultCommand(CommandType commandType)
    {
        return commandType switch
        {
            CommandType.Move => new TimelineCommand
            {
                Type = CommandType.Move,
                ActorName = "farmer",
                TileX = 0,
                TileY = 0,
                Facing = 2
            },
            CommandType.Speak => new TimelineCommand
            {
                Type = CommandType.Speak,
                ActorName = "farmer",
                DialogueText = "Hello."
            },
            CommandType.Emote => new TimelineCommand
            {
                Type = CommandType.Emote,
                ActorName = "farmer",
                EmoteId = 8
            },
            CommandType.Pause => new TimelineCommand
            {
                Type = CommandType.Pause,
                DurationMs = 500
            },
            CommandType.FadeOut => new TimelineCommand
            {
                Type = CommandType.FadeOut
            },
            CommandType.FadeIn => new TimelineCommand
            {
                Type = CommandType.FadeIn
            },
            CommandType.Reward => new TimelineCommand
            {
                Type = CommandType.Reward,
                RewardType = RewardType.Item,
                ItemId = "(O)74",
                Quantity = 1
            },
            _ => throw new InvalidOperationException($"Cannot add command type '{commandType}'.")
        };
    }

    private void RebuildBlockBounds()
    {
        this.commandBlocks.Clear();

        int x = this.Bounds.X + 20;
        int y = this.Bounds.Y + 58;
        this.setupBlockBounds = new Rectangle(x, y, BlockWidth, BlockHeight);
        x += BlockWidth + BlockGap;

        for (int i = 0; i < this.state.Cutscene.Commands.Count; i++)
        {
            this.commandBlocks.Add((new Rectangle(x, y, BlockWidth, BlockHeight), i));
            x += BlockWidth + BlockGap;
        }

        this.addButtonBounds = new Rectangle(x, y, AddButtonWidth, BlockHeight);
    }

    private void DrawBlock(SpriteBatch spriteBatch, Rectangle bounds, string label, bool selected)
    {
        IClickableMenu.drawTextureBox(
            spriteBatch,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            selected ? Color.LightGoldenrodYellow : Color.White
        );

        Vector2 labelSize = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(
            spriteBatch,
            label,
            Game1.smallFont,
            new Vector2(bounds.Center.X - labelSize.X / 2f, bounds.Center.Y - labelSize.Y / 2f),
            Game1.textColor
        );
    }

    private void DrawAddMenu(SpriteBatch spriteBatch)
    {
        this.addMenuRows.Clear();
        int x = this.addButtonBounds.X;
        int y = this.addButtonBounds.Y - MenuRowHeight * AddableCommandTypes.Length;

        foreach (CommandType type in AddableCommandTypes)
        {
            Rectangle row = new(x, y, AddButtonWidth + 40, MenuRowHeight);
            this.addMenuRows.Add((row, type));
            IClickableMenu.drawTextureBox(spriteBatch, row.X, row.Y, row.Width, row.Height, Color.White);
            Utility.drawTextWithShadow(spriteBatch, type.ToString(), Game1.smallFont, new Vector2(row.X + 12, row.Y + 8), Game1.textColor);
            y += MenuRowHeight;
        }
    }

    private void DrawContextMenu(SpriteBatch spriteBatch)
    {
        if (!this.deleteButtonBounds.HasValue || !this.duplicateButtonBounds.HasValue)
        {
            return;
        }

        this.DrawMenuButton(spriteBatch, this.deleteButtonBounds.Value, "Delete");
        this.DrawMenuButton(spriteBatch, this.duplicateButtonBounds.Value, "Duplicate");
    }

    private void DrawMenuButton(SpriteBatch spriteBatch, Rectangle bounds, string label)
    {
        IClickableMenu.drawTextureBox(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        Utility.drawTextWithShadow(spriteBatch, label, Game1.smallFont, new Vector2(bounds.X + 12, bounds.Y + 7), Game1.textColor);
    }

    private bool TryHandleAddMenuClick(int x, int y)
    {
        foreach ((Rectangle bounds, CommandType type) in this.addMenuRows)
        {
            if (bounds.Contains(x, y))
            {
                this.InsertDefaultCommand(type);
                return true;
            }
        }

        return false;
    }

    private bool TryHandleOverlayClick(int x, int y)
    {
        return (this.addMenuOpen && this.TryHandleAddMenuClick(x, y))
            || (this.contextCommandIndex.HasValue && this.TryHandleContextMenuClick(x, y));
    }

    private bool TryHandleContextMenuClick(int x, int y)
    {
        if (!this.contextCommandIndex.HasValue)
        {
            return false;
        }

        int commandIndex = this.contextCommandIndex.Value;
        if (this.deleteButtonBounds.HasValue && this.deleteButtonBounds.Value.Contains(x, y))
        {
            this.DeleteCommand(commandIndex);
            return true;
        }

        if (this.duplicateButtonBounds.HasValue && this.duplicateButtonBounds.Value.Contains(x, y))
        {
            this.DuplicateCommand(commandIndex);
            return true;
        }

        return false;
    }

    private void DeleteCommand(int commandIndex)
    {
        if (!this.CanEditCommand(commandIndex))
        {
            return;
        }

        this.state.Cutscene.Commands.RemoveAt(commandIndex);
        this.state.SelectedCommandIndex = Math.Min(commandIndex, this.state.Cutscene.Commands.Count - 1);
        this.state.IsDirty = true;
        this.CloseTransientMenus();
    }

    private void DuplicateCommand(int commandIndex)
    {
        if (!this.CanEditCommand(commandIndex) || this.state.Cutscene.Commands[commandIndex] is not TimelineCommand command)
        {
            return;
        }

        TimelineCommand clone = new()
        {
            Type = command.Type,
            ActorName = command.ActorName,
            TileX = command.TileX,
            TileY = command.TileY,
            Facing = command.Facing,
            DialogueText = command.DialogueText,
            EmoteId = command.EmoteId,
            DurationMs = command.DurationMs,
            RewardType = command.RewardType,
            ItemId = command.ItemId,
            Quantity = command.Quantity,
            GoldAmount = command.GoldAmount,
            RewardNpcName = command.RewardNpcName,
            FriendshipAmount = command.FriendshipAmount
        };

        this.state.Cutscene.Commands.Insert(commandIndex + 1, clone);
        this.state.SelectedCommandIndex = commandIndex + 1;
        this.state.IsDirty = true;
        this.CloseTransientMenus();
    }

    private bool CanEditCommand(int commandIndex)
    {
        return commandIndex >= 0
            && commandIndex < this.state.Cutscene.Commands.Count
            && this.state.Cutscene.Commands[commandIndex] is not TimelineCommand { Type: CommandType.End };
    }

    private void CloseTransientMenus()
    {
        this.addMenuOpen = false;
        this.contextCommandIndex = null;
        this.deleteButtonBounds = null;
        this.duplicateButtonBounds = null;
    }
}
