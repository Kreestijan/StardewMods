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
    private const int HorizontalPadding = 20;
    private const int ScrollStep = 180;
    private const int AutoScrollEdgePadding = 48;
    private readonly EditorState state;
    private readonly List<(Rectangle Bounds, int CommandIndex)> commandBlocks = new();
    private readonly List<(Rectangle Bounds, CommandType Type)> addMenuRows = new();
    private Rectangle? deleteButtonBounds;
    private Rectangle? duplicateButtonBounds;
    private Rectangle setupBlockBounds;
    private Rectangle addButtonBounds;
    private bool addMenuOpen;
    private int? contextCommandIndex;
    private int? draggedCommandIndex;
    private object? draggedCommand;
    private Point dragOffset;
    private Point dragPosition;
    private bool hasDragged;
    private int scrollOffsetX;
    private int contentWidth;

    public TimelinePanel(EditorState state)
        : base("Timeline")
    {
        this.state = state;
    }

    public bool IsDragging => this.draggedCommand is not null;

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        this.RebuildBlockBounds();

        Rectangle visibleBounds = this.GetTimelineContentBounds();
        this.DrawBlockIfVisible(spriteBatch, visibleBounds, this.setupBlockBounds, "Setup", this.state.SelectedCommandIndex == -1);
        foreach ((Rectangle bounds, int commandIndex) in this.commandBlocks)
        {
            object command = this.state.Cutscene.Commands[commandIndex];
            if (ReferenceEquals(this.draggedCommand, command))
            {
                continue;
            }

            string label = command switch
            {
                TimelineCommand timelineCommand => timelineCommand.Type.ToString(),
                RawCommandBlock => "Raw",
                _ => "Unknown"
            };

            this.DrawBlockIfVisible(spriteBatch, visibleBounds, bounds, label, this.state.SelectedCommandIndex == commandIndex);
        }

        if (this.addButtonBounds.Intersects(visibleBounds))
        {
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
        }

        if (this.addMenuOpen)
        {
            this.DrawAddMenu(spriteBatch);
        }

        if (this.contextCommandIndex.HasValue)
        {
            this.DrawContextMenu(spriteBatch);
        }

        if (this.draggedCommand is not null)
        {
            this.DrawDropMarker(spriteBatch);
            this.DrawDraggedBlock(spriteBatch);
        }

        this.DrawScrollHint(spriteBatch, visibleBounds);
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
                if (this.CanEditCommand(commandIndex))
                {
                    this.StartDrag(commandIndex, bounds, x, y);
                }

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

    public override void LeftClickHeld(int x, int y)
    {
        if (this.draggedCommand is null || !this.draggedCommandIndex.HasValue)
        {
            return;
        }

        this.AutoScrollWhileDragging(x);
        this.dragPosition = new Point(x, y);
        Rectangle draggedBounds = this.GetDraggedBounds();
        if (Math.Abs(draggedBounds.X - this.GetCommandBounds(this.draggedCommandIndex.Value).X) > 4
            || Math.Abs(draggedBounds.Y - this.GetCommandBounds(this.draggedCommandIndex.Value).Y) > 4)
        {
            this.hasDragged = true;
        }
    }

    public override void ReleaseLeftClick(int x, int y)
    {
        if (this.draggedCommand is null || !this.draggedCommandIndex.HasValue)
        {
            return;
        }

        object command = this.draggedCommand;
        this.dragPosition = new Point(x, y);

        if (this.hasDragged)
        {
            this.DropCommand(command, x);
        }

        this.draggedCommandIndex = null;
        this.draggedCommand = null;
        this.hasDragged = false;
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

    public override void ReceiveScrollWheelAction(int direction)
    {
        this.RebuildBlockBounds();
        if (this.GetMaxScrollOffset() <= 0)
        {
            return;
        }

        int delta = direction > 0 ? -ScrollStep : ScrollStep;
        this.SetScrollOffset(this.scrollOffsetX + delta);
        this.CloseTransientMenus();
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
        this.ScrollCommandIntoView(insertIndex);
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

        this.contentWidth = this.CalculateContentWidth();
        this.SetScrollOffset(this.scrollOffsetX);

        int x = this.Bounds.X + HorizontalPadding - this.scrollOffsetX;
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

    private int CalculateContentWidth()
    {
        int commandCount = this.state.Cutscene.Commands.Count;
        return BlockWidth + BlockGap
            + commandCount * (BlockWidth + BlockGap)
            + AddButtonWidth;
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

    private void DrawBlockIfVisible(SpriteBatch spriteBatch, Rectangle visibleBounds, Rectangle bounds, string label, bool selected)
    {
        if (bounds.Intersects(visibleBounds))
        {
            this.DrawBlock(spriteBatch, bounds, label, selected);
        }
    }

    private void DrawScrollHint(SpriteBatch spriteBatch, Rectangle visibleBounds)
    {
        int maxScroll = this.GetMaxScrollOffset();
        if (maxScroll <= 0)
        {
            return;
        }

        if (this.scrollOffsetX > 0)
        {
            Utility.drawTextWithShadow(spriteBatch, "<", Game1.smallFont, new Vector2(visibleBounds.X - 2, visibleBounds.Y + 16), Color.DarkGoldenrod);
        }

        if (this.scrollOffsetX < maxScroll)
        {
            Utility.drawTextWithShadow(spriteBatch, ">", Game1.smallFont, new Vector2(visibleBounds.Right - 18, visibleBounds.Y + 16), Color.DarkGoldenrod);
        }

        int trackWidth = Math.Max(1, visibleBounds.Width - 28);
        Rectangle track = new(visibleBounds.X + 14, visibleBounds.Bottom - 10, trackWidth, 4);
        spriteBatch.Draw(Game1.staminaRect, track, Color.SaddleBrown * 0.35f);

        int thumbWidth = Math.Max(24, track.Width * visibleBounds.Width / Math.Max(visibleBounds.Width, this.contentWidth));
        int thumbTravel = Math.Max(1, track.Width - thumbWidth);
        int thumbX = track.X + (int)Math.Round(thumbTravel * (this.scrollOffsetX / (float)maxScroll));
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(thumbX, track.Y, thumbWidth, track.Height), Color.DarkOrange * 0.9f);
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

    private void DrawDraggedBlock(SpriteBatch spriteBatch)
    {
        if (this.draggedCommand is null)
        {
            return;
        }

        string label = this.GetCommandLabel(this.draggedCommand);
        this.DrawBlock(spriteBatch, this.GetDraggedBounds(), label, selected: true);
    }

    private void DrawDropMarker(SpriteBatch spriteBatch)
    {
        if (this.draggedCommand is null || !this.hasDragged)
        {
            return;
        }

        int targetIndex = this.GetDropTargetIndex(this.GetDraggedBounds().Center.X);
        int markerX = this.GetDropMarkerX(targetIndex);
        Rectangle marker = new(markerX - 3, this.Bounds.Y + 52, 6, BlockHeight + 12);
        spriteBatch.Draw(Game1.staminaRect, marker, Color.DarkOrange * 0.9f);
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
            ActorSlotId = command.ActorSlotId,
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
        this.ScrollCommandIntoView(commandIndex + 1);
    }

    private void StartDrag(int commandIndex, Rectangle bounds, int x, int y)
    {
        this.draggedCommandIndex = commandIndex;
        this.draggedCommand = this.state.Cutscene.Commands[commandIndex];
        this.dragOffset = new Point(x - bounds.X, y - bounds.Y);
        this.dragPosition = new Point(x, y);
        this.hasDragged = false;
    }

    private void DropCommand(object command, int mouseX)
    {
        int commandIndex = this.IndexOfCommand(command);
        if (!this.CanEditCommand(commandIndex))
        {
            return;
        }

        int targetIndex = this.GetDropTargetIndex(mouseX);
        if (targetIndex > commandIndex)
        {
            targetIndex--;
        }

        int endIndex = Math.Max(0, this.state.Cutscene.Commands.Count - 1);
        targetIndex = Math.Clamp(targetIndex, 0, endIndex - 1);
        if (targetIndex == commandIndex)
        {
            return;
        }

        this.state.Cutscene.Commands.RemoveAt(commandIndex);
        this.state.Cutscene.Commands.Insert(targetIndex, command);
        this.state.SelectedCommandIndex = targetIndex;
        this.state.IsDirty = true;
        this.RebuildBlockBounds();
        this.ScrollCommandIntoView(targetIndex);
    }

    private int GetDropTargetIndex(int mouseX)
    {
        int endIndex = Math.Max(0, this.state.Cutscene.Commands.Count - 1);
        int targetIndex = endIndex;

        foreach ((Rectangle bounds, int commandIndex) in this.commandBlocks)
        {
            object command = this.state.Cutscene.Commands[commandIndex];
            if (ReferenceEquals(command, this.draggedCommand))
            {
                continue;
            }

            if (commandIndex >= endIndex)
            {
                if (mouseX < bounds.Center.X)
                {
                    targetIndex = endIndex;
                }

                break;
            }

            if (mouseX < bounds.Center.X)
            {
                targetIndex = commandIndex;
                break;
            }
        }

        return targetIndex;
    }

    private int GetDropMarkerX(int targetIndex)
    {
        foreach ((Rectangle bounds, int commandIndex) in this.commandBlocks)
        {
            if (commandIndex == targetIndex)
            {
                return bounds.X - BlockGap / 2;
            }
        }

        int endIndex = Math.Max(0, this.state.Cutscene.Commands.Count - 1);
        Rectangle endBounds = this.GetCommandBounds(endIndex);
        return endBounds.X - BlockGap / 2;
    }

    private Rectangle GetDraggedBounds()
    {
        return new Rectangle(
            this.dragPosition.X - this.dragOffset.X,
            this.dragPosition.Y - this.dragOffset.Y,
            BlockWidth,
            BlockHeight
        );
    }

    private Rectangle GetCommandBounds(int commandIndex)
    {
        foreach ((Rectangle bounds, int index) in this.commandBlocks)
        {
            if (index == commandIndex)
            {
                return bounds;
            }
        }

        return Rectangle.Empty;
    }

    private int IndexOfCommand(object command)
    {
        for (int index = 0; index < this.state.Cutscene.Commands.Count; index++)
        {
            if (ReferenceEquals(this.state.Cutscene.Commands[index], command))
            {
                return index;
            }
        }

        return -1;
    }

    private string GetCommandLabel(object command)
    {
        return command switch
        {
            TimelineCommand timelineCommand => timelineCommand.Type.ToString(),
            RawCommandBlock => "Raw",
            _ => "Unknown"
        };
    }

    private string GetCommandLabel(int commandIndex)
    {
        if (commandIndex < 0 || commandIndex >= this.state.Cutscene.Commands.Count)
        {
            return "Unknown";
        }

        return this.state.Cutscene.Commands[commandIndex] switch
        {
            TimelineCommand timelineCommand => timelineCommand.Type.ToString(),
            RawCommandBlock => "Raw",
            _ => "Unknown"
        };
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

    private Rectangle GetTimelineContentBounds()
    {
        return new Rectangle(
            this.Bounds.X + HorizontalPadding,
            this.Bounds.Y + 50,
            Math.Max(1, this.Bounds.Width - HorizontalPadding * 2),
            BlockHeight + 22
        );
    }

    private int GetMaxScrollOffset()
    {
        return Math.Max(0, this.contentWidth - this.GetTimelineContentBounds().Width);
    }

    private void SetScrollOffset(int value)
    {
        this.scrollOffsetX = Math.Clamp(value, 0, this.GetMaxScrollOffset());
    }

    private void ScrollCommandIntoView(int commandIndex)
    {
        if (commandIndex < 0)
        {
            this.SetScrollOffset(0);
            return;
        }

        Rectangle visibleBounds = this.GetTimelineContentBounds();
        int commandLeft = this.Bounds.X + HorizontalPadding + BlockWidth + BlockGap + commandIndex * (BlockWidth + BlockGap);
        int commandRight = commandLeft + BlockWidth;

        if (commandLeft - this.scrollOffsetX < visibleBounds.Left)
        {
            this.SetScrollOffset(commandLeft - visibleBounds.Left);
        }
        else if (commandRight - this.scrollOffsetX > visibleBounds.Right)
        {
            this.SetScrollOffset(commandRight - visibleBounds.Right);
        }
    }

    private void AutoScrollWhileDragging(int mouseX)
    {
        Rectangle visibleBounds = this.GetTimelineContentBounds();
        if (mouseX < visibleBounds.Left + AutoScrollEdgePadding)
        {
            this.SetScrollOffset(this.scrollOffsetX - ScrollStep / 3);
        }
        else if (mouseX > visibleBounds.Right - AutoScrollEdgePadding)
        {
            this.SetScrollOffset(this.scrollOffsetX + ScrollStep / 3);
        }
    }
}
