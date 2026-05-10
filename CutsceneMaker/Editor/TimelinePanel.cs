using CutsceneMaker.Commands;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.CompilerServices;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public sealed class TimelinePanel : EditorPanel
{
    private sealed class AddMenuEntry
    {
        public string Label { get; init; } = string.Empty;

        public Func<object>? CreateCommand { get; init; }
    }

    private const int BlockWidth = 132;
    private const int BlockHeight = 93;
    private const int MaxBlockWidth = 210;
    private const int BlockGap = 10;
    private const int AddButtonWidth = 112;
    private const int AddMenuWidth = 224;
    private const int AddMenuTextPadding = 28;
    private const int AddMenuSearchHeight = 42;
    private const int AddMenuSearchFieldWidthBonus = 20;
    private const int AddMenuSearchFieldHeightBonus = 5;
    private const int MenuRowHeight = 40;
    private const int MaxAddMenuRowsPerColumn = 18;
    private const int AddMenuWheelRows = 3;
    private const int HorizontalPadding = 20;
    private const int BlockHorizontalTextPadding = 20;
    private const int BlockVerticalTextPadding = 16;
    private const int ScrollStep = 180;
    private const int AutoScrollEdgePadding = 48;
    private readonly EditorState state;
    private readonly EventCommandCatalog commandCatalog;
    private readonly List<(Rectangle Bounds, int CommandIndex)> commandBlocks = new();
    private readonly List<(Rectangle Bounds, AddMenuEntry Entry)> addMenuRows = new();
    private readonly Dictionary<string, BoundTextField> textFields = new();
    private Rectangle? deleteButtonBounds;
    private Rectangle? duplicateButtonBounds;
    private Rectangle? addMenuSearchBounds;
    private Rectangle setupBlockBounds;
    private Rectangle addButtonBounds;
    private bool addMenuOpen;
    private int addMenuScrollIndex;
    private string addMenuSearchText = string.Empty;
    private int? contextCommandIndex;
    private int? draggedCommandIndex;
    private object? draggedCommand;
    private Point dragOffset;
    private Point dragPosition;
    private bool hasDragged;
    private int scrollOffsetX;
    private int contentWidth;

    public TimelinePanel(EditorState state, EventCommandCatalog commandCatalog)
        : base("Timeline")
    {
        this.state = state;
        this.commandCatalog = commandCatalog;
    }

    public bool IsDragging => this.draggedCommand is not null;

    public bool AddMenuOpen => this.addMenuOpen;

    public bool HasTransientMenu => this.addMenuOpen || this.contextCommandIndex.HasValue;

    public bool HasSelectedTextField()
    {
        return this.textFields.Values.Any(field => field.Selected);
    }

    public override void ReceiveKeyPress(Keys key)
    {
        if (this.addMenuOpen && key == Keys.Enter)
        {
            return;
        }

        foreach (BoundTextField field in this.textFields.Values)
        {
            if (field.Selected)
            {
                field.ReceiveKeyPress(key);
            }
        }
    }

    public override void Update()
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            field.Update();
        }
    }

    public void CloseTransientMenusByUser()
    {
        this.CloseTransientMenus();
    }

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
                EventCommandBlock eventCommand => this.GetEventCommandLabel(eventCommand),
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

        this.DrawCommandMarker(spriteBatch);

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
            this.addMenuScrollIndex = 0;
            this.contextCommandIndex = null;
            if (this.addMenuOpen)
            {
                this.SelectAddMenuSearchField();
            }
            else
            {
                this.addMenuSearchText = string.Empty;
                this.DeselectTextFields();
            }

            return;
        }

        this.CloseTransientMenus();
        this.DeselectTextFields();
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
        if (this.addMenuOpen)
        {
            this.ScrollAddMenu(direction);
            return;
        }

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
            || (this.addMenuOpen && this.addMenuSearchBounds.HasValue && this.addMenuSearchBounds.Value.Contains(x, y))
            || (this.contextCommandIndex.HasValue
                && ((this.deleteButtonBounds.HasValue && this.deleteButtonBounds.Value.Contains(x, y))
                    || (this.duplicateButtonBounds.HasValue && this.duplicateButtonBounds.Value.Contains(x, y))));
    }

    private void InsertCommand(object command)
    {
        int insertIndex = Math.Max(0, this.state.Cutscene.Commands.Count - 1);
        this.state.Cutscene.Commands.Insert(insertIndex, command);
        this.state.SelectedCommandIndex = insertIndex;
        this.state.IsDirty = true;
        this.CloseTransientMenus();
        this.ScrollCommandIntoView(insertIndex);
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
            int blockWidth = this.GetCommandBlockWidth(this.state.Cutscene.Commands[i]);
            this.commandBlocks.Add((new Rectangle(x, y, blockWidth, BlockHeight), i));
            x += blockWidth + BlockGap;
        }

        this.addButtonBounds = new Rectangle(x, y, AddButtonWidth, BlockHeight);
    }

    private int CalculateContentWidth()
    {
        int width = BlockWidth + BlockGap;
        foreach (object command in this.state.Cutscene.Commands)
        {
            width += this.GetCommandBlockWidth(command) + BlockGap;
        }

        return width + AddButtonWidth;
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

        IReadOnlyList<string> lines = this.WrapBlockLabel(label, bounds.Width - BlockHorizontalTextPadding * 2);
        float totalTextHeight = lines.Count * Game1.smallFont.LineSpacing;
        float textY = bounds.Center.Y - totalTextHeight / 2f;
        foreach (string line in lines)
        {
            Vector2 labelSize = Game1.smallFont.MeasureString(line);
            Utility.drawTextWithShadow(
                spriteBatch,
                line,
                Game1.smallFont,
                new Vector2(bounds.Center.X - labelSize.X / 2f, textY),
                Game1.textColor
            );
            textY += Game1.smallFont.LineSpacing;
        }
    }

    private void DrawBlockIfVisible(SpriteBatch spriteBatch, Rectangle visibleBounds, Rectangle bounds, string label, bool selected)
    {
        if (bounds.Intersects(visibleBounds))
        {
            this.DrawBlock(spriteBatch, bounds, label, selected);
        }
    }

    private Rectangle? GetBlockBounds(int commandIndex)
    {
        foreach ((Rectangle bounds, int index) in this.commandBlocks)
        {
            if (index == commandIndex)
            {
                return bounds;
            }
        }

        return null;
    }

    private void DrawCommandMarker(SpriteBatch spriteBatch)
    {
        int markerIndex = this.state.Mode == EditorMode.Play
            ? this.state.PlaybackCommandIndex
            : this.state.CommandMarkerIndex;

        Rectangle? blockBounds = markerIndex == -1
            ? this.setupBlockBounds
            : GetBlockBounds(markerIndex);

        if (blockBounds.HasValue && this.GetTimelineContentBounds().Intersects(blockBounds.Value))
        {
            Rectangle marker = new(blockBounds.Value.X, blockBounds.Value.Y - 6, blockBounds.Value.Width, 4);
            spriteBatch.Draw(Game1.staminaRect, marker, Color.LimeGreen);
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
        List<AddMenuEntry> entries = this.BuildAddMenuEntries();
        if (entries.Count == 0)
        {
            this.DrawEmptyAddMenu(spriteBatch);
            return;
        }

        int visibleRows = Math.Min(entries.Count, Math.Min(MaxAddMenuRowsPerColumn, Math.Max(1, (this.addButtonBounds.Y - 48 - AddMenuSearchHeight) / MenuRowHeight)));
        this.addMenuScrollIndex = Math.Clamp(this.addMenuScrollIndex, 0, Math.Max(0, entries.Count - visibleRows));

        int menuWidth = Math.Min(this.GetAddMenuWidth(entries), Math.Max(AddMenuWidth, this.Bounds.Width - 32));
        int minX = this.Bounds.X + 16;
        int maxX = Math.Max(minX, this.Bounds.Right - menuWidth - 16);
        int startX = Math.Clamp(this.addButtonBounds.X, minX, maxX);
        int startY = Math.Max(48, this.addButtonBounds.Y - AddMenuSearchHeight - visibleRows * MenuRowHeight);

        Rectangle searchBounds = new(startX, startY, menuWidth, AddMenuSearchHeight);
        this.addMenuSearchBounds = searchBounds;
        IClickableMenu.drawTextureBox(spriteBatch, searchBounds.X, searchBounds.Y, searchBounds.Width, searchBounds.Height, Color.White);
        this.DrawStringField(
            spriteBatch,
            "add-menu.search",
            GetSearchFieldBounds(searchBounds),
            () => this.addMenuSearchText,
            value =>
            {
                this.addMenuSearchText = value;
                this.addMenuScrollIndex = 0;
            },
            80
        );
        if (string.IsNullOrWhiteSpace(this.addMenuSearchText) && !this.HasSelectedTextField())
        {
            Utility.drawTextWithShadow(spriteBatch, "Search commands...", Game1.smallFont, new Vector2(searchBounds.X + 20, searchBounds.Y + 11), Color.DimGray);
        }

        for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
        {
            int index = this.addMenuScrollIndex + rowIndex;
            AddMenuEntry entry = entries[index];
            Rectangle row = new(startX, startY + AddMenuSearchHeight + rowIndex * MenuRowHeight, menuWidth, MenuRowHeight);
            IClickableMenu.drawTextureBox(spriteBatch, row.X, row.Y, row.Width, row.Height, Color.White);
            Utility.drawTextWithShadow(
                spriteBatch,
                this.Ellipsize(entry.Label, row.Width - 24),
                Game1.smallFont,
                new Vector2(row.X + 12, row.Y + 8),
                Game1.textColor
            );
            this.addMenuRows.Add((row, entry));
        }

        if (entries.Count > visibleRows)
        {
            this.DrawAddMenuScrollBar(spriteBatch, new Rectangle(startX, startY + AddMenuSearchHeight, menuWidth, visibleRows * MenuRowHeight), entries.Count, visibleRows);
        }
    }

    private void DrawAddMenuScrollBar(SpriteBatch spriteBatch, Rectangle menuBounds, int entryCount, int visibleRows)
    {
        int trackHeight = Math.Max(1, menuBounds.Height - 16);
        Rectangle track = new(menuBounds.Right - 8, menuBounds.Y + 8, 4, trackHeight);
        spriteBatch.Draw(Game1.staminaRect, track, Color.SaddleBrown * 0.35f);

        int thumbHeight = Math.Max(16, track.Height * visibleRows / entryCount);
        int maxScroll = Math.Max(1, entryCount - visibleRows);
        int thumbTravel = Math.Max(1, track.Height - thumbHeight);
        int thumbY = track.Y + (int)Math.Round(thumbTravel * (this.addMenuScrollIndex / (float)maxScroll));
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Color.DarkOrange * 0.9f);
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
        if (this.addMenuSearchBounds.HasValue && this.addMenuSearchBounds.Value.Contains(x, y))
        {
            if (this.textFields.TryGetValue("add-menu.search", out BoundTextField? searchField))
            {
                this.DeselectTextFields();
                searchField.Select();
            }

            return true;
        }

        foreach ((Rectangle bounds, AddMenuEntry entry) in this.addMenuRows)
        {
            if (bounds.Contains(x, y))
            {
                if (entry.CreateCommand is not null)
                {
                    this.InsertCommand(entry.CreateCommand());
                }

                return true;
            }
        }

        this.CloseTransientMenus();
        return true;
    }

    private List<AddMenuEntry> BuildAddMenuEntries()
    {
        List<AddMenuEntry> entries = new();

        foreach (IGrouping<string, EventCommandDefinition> group in this.commandCatalog.Definitions.GroupBy(definition => definition.ProviderName))
        {
            foreach (EventCommandDefinition definition in group)
            {
                if (definition.Id == "vanilla.end")
                {
                    continue;
                }

                entries.Add(new AddMenuEntry
                {
                    Label = $"({definition.Badge}) {definition.DisplayName}",
                    CreateCommand = definition.CreateDefaultBlock
                });
            }
        }

        entries.Add(new AddMenuEntry
        {
            Label = "Raw Command",
            CreateCommand = () => new RawCommandBlock { RawText = "pause 500" }
        });

        if (string.IsNullOrWhiteSpace(this.addMenuSearchText))
        {
            return entries;
        }

        return entries
            .Where(entry => entry.Label.Contains(this.addMenuSearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private bool TryHandleOverlayClick(int x, int y)
    {
        if (this.addMenuOpen)
        {
            return this.TryHandleAddMenuClick(x, y);
        }

        return this.contextCommandIndex.HasValue && this.TryHandleContextMenuClick(x, y);
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

        this.CloseTransientMenus();
        return true;
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
        if (!this.CanEditCommand(commandIndex))
        {
            return;
        }

        object clone = this.CloneCommand(this.state.Cutscene.Commands[commandIndex]);
        this.state.Cutscene.Commands.Insert(commandIndex + 1, clone);
        this.state.SelectedCommandIndex = commandIndex + 1;
        this.state.IsDirty = true;
        this.CloseTransientMenus();
        this.ScrollCommandIntoView(commandIndex + 1);
    }

    private object CloneCommand(object command)
    {
        if (command is EventCommandBlock eventCommand)
        {
            return new EventCommandBlock
            {
                ProviderModId = eventCommand.ProviderModId,
                ProviderName = eventCommand.ProviderName,
                CommandId = eventCommand.CommandId,
                DisplayName = eventCommand.DisplayName,
                Values = new Dictionary<string, string>(eventCommand.Values, StringComparer.Ordinal),
                ActorSlotIds = new Dictionary<string, string>(eventCommand.ActorSlotIds, StringComparer.Ordinal)
            };
        }

        if (command is RawCommandBlock rawCommand)
        {
            return new RawCommandBlock { RawText = rawCommand.RawText };
        }

        throw new InvalidOperationException($"Cannot duplicate command '{command.GetType().FullName}'.");
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
        int blockWidth = this.draggedCommand is null ? BlockWidth : this.GetCommandBlockWidth(this.draggedCommand);
        return new Rectangle(
            this.dragPosition.X - this.dragOffset.X,
            this.dragPosition.Y - this.dragOffset.Y,
            blockWidth,
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
            EventCommandBlock eventCommand => this.GetEventCommandLabel(eventCommand),
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
            EventCommandBlock eventCommand => this.GetEventCommandLabel(eventCommand),
            RawCommandBlock => "Raw",
            _ => "Unknown"
        };
    }

    private string GetEventCommandLabel(EventCommandBlock command)
    {
        if (this.commandCatalog.TryGetById(command.CommandId, out EventCommandDefinition? definition))
        {
            return $"({definition.Badge}) {definition.DisplayName}";
        }

        return string.IsNullOrWhiteSpace(command.DisplayName) ? "Custom" : command.DisplayName;
    }

    private int GetCommandBlockWidth(object command)
    {
        string label = this.GetCommandLabel(command);
        int textWidth = (int)Math.Ceiling(Game1.smallFont.MeasureString(label).X);
        int oneLineWidth = textWidth + BlockHorizontalTextPadding * 2;
        if (oneLineWidth <= MaxBlockWidth)
        {
            return Math.Max(BlockWidth, oneLineWidth);
        }

        return MaxBlockWidth;
    }

    private IReadOnlyList<string> WrapBlockLabel(string label, int maxWidth)
    {
        if (Game1.smallFont.MeasureString(label).X <= maxWidth)
        {
            return new[] { label };
        }

        List<string> lines = new();
        string current = string.Empty;
        foreach (string word in label.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
            if (Game1.smallFont.MeasureString(candidate).X <= maxWidth || string.IsNullOrEmpty(current))
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = word;
            if (lines.Count == 1)
            {
                break;
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(this.Ellipsize(label, maxWidth));
        }
        else
        {
            string remaining = current;
            int consumedWordCount = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            string[] words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (consumedWordCount < words.Length)
            {
                remaining = string.Join(" ", words.Skip(consumedWordCount));
            }

            lines.Add(this.Ellipsize(remaining, maxWidth));
        }

        return lines.Take(2).ToArray();
    }

    private string Ellipsize(string text, int maxWidth)
    {
        if (Game1.smallFont.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        string trimmed = text.Trim();
        while (trimmed.Length > 0 && Game1.smallFont.MeasureString(trimmed + ellipsis).X > maxWidth)
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }

    private int GetAddMenuWidth(IReadOnlyList<AddMenuEntry> entries)
    {
        int maxTextWidth = entries.Count == 0
            ? 0
            : entries.Max(entry => (int)Math.Ceiling(Game1.smallFont.MeasureString(entry.Label).X));
        return Math.Max(AddMenuWidth, maxTextWidth + AddMenuTextPadding * 2);
    }

    private bool CanEditCommand(int commandIndex)
    {
        return commandIndex >= 0
            && commandIndex < this.state.Cutscene.Commands.Count
            && this.state.Cutscene.Commands[commandIndex] is not EventCommandBlock { CommandId: "vanilla.end" };
    }

    private void CloseTransientMenus()
    {
        this.addMenuOpen = false;
        this.addMenuScrollIndex = 0;
        this.addMenuSearchText = string.Empty;
        this.addMenuSearchBounds = null;
        this.DeselectTextFields();
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
        Rectangle commandBounds = this.GetCommandBounds(commandIndex);
        if (commandBounds == Rectangle.Empty)
        {
            return;
        }

        int commandLeft = commandBounds.X + this.scrollOffsetX;
        int commandRight = commandLeft + commandBounds.Width;

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

    private void ScrollAddMenu(int direction)
    {
        List<AddMenuEntry> entries = this.BuildAddMenuEntries();
        if (entries.Count == 0)
        {
            this.addMenuScrollIndex = 0;
            return;
        }

        int availableRowsAboveButton = Math.Max(1, (this.addButtonBounds.Y - 48 - AddMenuSearchHeight) / MenuRowHeight);
        int visibleRows = Math.Min(entries.Count, Math.Min(MaxAddMenuRowsPerColumn, availableRowsAboveButton));
        int delta = direction > 0 ? -AddMenuWheelRows : AddMenuWheelRows;
        this.addMenuScrollIndex = Math.Clamp(this.addMenuScrollIndex + delta, 0, Math.Max(0, entries.Count - visibleRows));
    }

    private void DrawEmptyAddMenu(SpriteBatch spriteBatch)
    {
        int menuWidth = Math.Min(AddMenuWidth + 80, Math.Max(AddMenuWidth, this.Bounds.Width - 32));
        int minX = this.Bounds.X + 16;
        int maxX = Math.Max(minX, this.Bounds.Right - menuWidth - 16);
        int startX = Math.Clamp(this.addButtonBounds.X, minX, maxX);
        int startY = Math.Max(48, this.addButtonBounds.Y - AddMenuSearchHeight - MenuRowHeight);

        Rectangle searchBounds = new(startX, startY, menuWidth, AddMenuSearchHeight);
        this.addMenuSearchBounds = searchBounds;
        IClickableMenu.drawTextureBox(spriteBatch, searchBounds.X, searchBounds.Y, searchBounds.Width, searchBounds.Height, Color.White);
        this.DrawStringField(
            spriteBatch,
            "add-menu.search",
            GetSearchFieldBounds(searchBounds),
            () => this.addMenuSearchText,
            value =>
            {
                this.addMenuSearchText = value;
                this.addMenuScrollIndex = 0;
            },
            80
        );

        Rectangle messageBounds = new(startX, startY + AddMenuSearchHeight, menuWidth, MenuRowHeight);
        IClickableMenu.drawTextureBox(spriteBatch, messageBounds.X, messageBounds.Y, messageBounds.Width, messageBounds.Height, Color.White);
        Utility.drawTextWithShadow(spriteBatch, "No commands found", Game1.smallFont, new Vector2(messageBounds.X + 12, messageBounds.Y + 8), Color.DimGray);
    }

    private void DrawStringField(SpriteBatch spriteBatch, string key, Rectangle bounds, Func<string> getValue, Action<string> setValue, int textLimit)
    {
        BoundTextField field = this.GetTextField(key, getValue, setValue, textLimit);
        field.SetBounds(bounds);
        field.Draw(spriteBatch);
    }

    private static Rectangle GetSearchFieldBounds(Rectangle searchBounds)
    {
        int width = Math.Min(searchBounds.Width - 8, searchBounds.Width - 24 + AddMenuSearchFieldWidthBonus);
        int height = Math.Min(searchBounds.Height - 2, searchBounds.Height - 8 + AddMenuSearchFieldHeightBonus);
        return new Rectangle(
            searchBounds.Center.X - width / 2,
            searchBounds.Center.Y - height / 2,
            width,
            height
        );
    }

    private BoundTextField GetTextField(string key, Func<string> getValue, Action<string> setValue, int textLimit)
    {
        if (!this.textFields.TryGetValue(key, out BoundTextField? field))
        {
            field = new BoundTextField(getValue, setValue, numbersOnly: false, textLimit);
            this.textFields[key] = field;
        }

        return field;
    }

    private void SelectAddMenuSearchField()
    {
        BoundTextField field = this.GetTextField(
            "add-menu.search",
            () => this.addMenuSearchText,
            value =>
            {
                this.addMenuSearchText = value;
                this.addMenuScrollIndex = 0;
            },
            80
        );
        this.DeselectTextFields();
        field.Select();
    }

    private void DeselectTextFields()
    {
        foreach (BoundTextField field in this.textFields.Values)
        {
            field.Selected = false;
        }
    }
}
