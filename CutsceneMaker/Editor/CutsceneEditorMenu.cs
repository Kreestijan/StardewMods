using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CutsceneMaker.Compiler;
using CutsceneMaker.Importer;
using CutsceneMaker.Models;
using StardewValley;
using StardewValley.Menus;
using System.Reflection;

namespace CutsceneMaker.Editor;

public sealed class CutsceneEditorMenu : IClickableMenu
{
    private const int ScreenPadding = 24;
    private const int ToolbarHeight = 64;
    private const int TimelineHeight = 140;
    private const int PanelGap = 12;
    private const int MinimumWidth = 960;
    private const int MinimumHeight = 640;
    // Public SMAPI/game APIs don't expose a writable Game1.player at the title screen.
    // Play preview needs a temporary farmer because Event initialization reads Game1.player.
    private static readonly PropertyInfo? GamePlayerProperty = typeof(Game1).GetProperty("player", BindingFlags.Public | BindingFlags.Static);
    private static readonly FieldInfo? GamePlayerField = typeof(Game1).GetField("_player", BindingFlags.NonPublic | BindingFlags.Static);
    private readonly EditorState state = new();
    private readonly MapViewPanel mapViewPanel;
    private readonly TimelinePanel timelinePanel;
    private readonly PropertiesPanel propertiesPanel;
    private readonly List<(Rectangle Bounds, Action LeftClick, Action? RightClick)> toolbarButtons = new();
    private readonly HashSet<int> registeredPreviewEmoteCommands = new();
    private PreconditionEditorPanel? preconditionEditorPanel;
    private SaveDialogPanel? saveDialogPanel;
    private EventPickerPanel? eventPickerPanel;
    private string toolbarStatusMessage = string.Empty;
    private Farmer? previousPlayer;
    private GameLocation? previousPlayerLocation;
    private Vector2 previousPlayerPosition;
    private int previousPlayerFacing;
    private GameLocation? previousLocation;
    private IClickableMenu? playbackDialogueBox;
    private bool previousEventUp;
    private bool yieldPlaybackFrame;
    private bool closeConfirmationOpen;
    private Rectangle closeConfirmYesBounds;
    private Rectangle closeConfirmNoBounds;
    private string closeConfirmTitle = "Exit Cutscene Maker?";
    private string closeConfirmMessage = "Unsaved changes will be lost.";
    private string closeConfirmYesLabel = "Exit";
    private Action? closeConfirmAction;
    private bool previewPlayerCreated;
    private bool playbackBootstrapActive;
    private bool playWarningShown;

    public CutsceneEditorMenu()
        : base(
            ScreenPadding,
            ScreenPadding,
            Math.Max(MinimumWidth, Game1.uiViewport.Width - ScreenPadding * 2),
            Math.Max(MinimumHeight, Game1.uiViewport.Height - ScreenPadding * 2),
            showUpperRightCloseButton: true
        )
    {
        ModEntry.Instance.RefreshKnownNpcs();
        this.state.BootstrappedMap = LocationBootstrapper.Load(this.state.Cutscene.LocationName);
        this.mapViewPanel = new MapViewPanel(this.state);
        this.timelinePanel = new TimelinePanel(this.state);
        this.propertiesPanel = new PropertiesPanel(this.state, this.OpenPreconditionEditor);
        this.RecalculateLayout();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        this.RecalculateLayout();
        base.gameWindowSizeChanged(oldBounds, newBounds);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.55f);
        IClickableMenu.drawTextureBox(
            b,
            this.xPositionOnScreen,
            this.yPositionOnScreen,
            this.width,
            this.height,
            Color.White
        );

        this.DrawToolbar(b);
        this.mapViewPanel.Draw(b);
        this.propertiesPanel.Draw(b);
        this.timelinePanel.Draw(b);
        this.playbackDialogueBox?.draw(b);
        this.preconditionEditorPanel?.Draw(b);
        this.saveDialogPanel?.Draw(b);
        this.eventPickerPanel?.Draw(b);
        if (this.closeConfirmationOpen)
        {
            this.DrawCloseConfirmation(b);
        }

        base.draw(b);
        this.drawMouse(b);
    }

    public override void update(GameTime time)
    {
        if (this.state.Mode == EditorMode.Play)
        {
            this.UpdatePlayback(time);
        }

        this.UpdatePlaybackDialogue(time);
        this.mapViewPanel.Update();
        this.timelinePanel.Update();
        this.propertiesPanel.Update();
        this.preconditionEditorPanel?.Update();
        this.saveDialogPanel?.Update();
        this.eventPickerPanel?.Update();
        base.update(time);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeConfirmationOpen)
        {
            this.ReceiveCloseConfirmationClick(x, y);
            return;
        }

        if (this.upperRightCloseButton.containsPoint(x, y))
        {
            this.RequestClose();
            return;
        }

        if (this.eventPickerPanel is not null)
        {
            this.eventPickerPanel.ReceiveLeftClick(x, y);
            return;
        }

        if (this.saveDialogPanel is not null)
        {
            this.saveDialogPanel.ReceiveLeftClick(x, y);
            return;
        }

        if (this.preconditionEditorPanel is not null)
        {
            this.preconditionEditorPanel.ReceiveLeftClick(x, y);
            return;
        }

        foreach ((Rectangle bounds, Action click, _) in this.toolbarButtons)
        {
            if (bounds.Contains(x, y))
            {
                click();
                return;
            }
        }

        if (this.state.Mode == EditorMode.Play)
        {
            if (this.playbackDialogueBox is not null)
            {
                this.RouteInputToPlaybackDialogue(dialogueBox => dialogueBox.receiveLeftClick(x, y, playSound));
            }

            return;
        }

        if (this.timelinePanel.WantsClick(x, y))
        {
            this.timelinePanel.ReceiveLeftClick(x, y);
            return;
        }

        if (this.mapViewPanel.Bounds.Contains(x, y))
        {
            this.mapViewPanel.ReceiveLeftClick(x, y);
            return;
        }

        if (this.timelinePanel.Bounds.Contains(x, y))
        {
            this.timelinePanel.ReceiveLeftClick(x, y);
            return;
        }

        if (this.propertiesPanel.Bounds.Contains(x, y))
        {
            this.propertiesPanel.ReceiveLeftClick(x, y);
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (this.closeConfirmationOpen)
        {
            return;
        }

        if (this.saveDialogPanel is not null || this.eventPickerPanel is not null || this.state.Mode == EditorMode.Play)
        {
            return;
        }

        if (this.preconditionEditorPanel is not null)
        {
            this.preconditionEditorPanel.ReceiveRightClick(x, y);
            return;
        }

        foreach ((Rectangle bounds, _, Action? rightClick) in this.toolbarButtons)
        {
            if (bounds.Contains(x, y))
            {
                rightClick?.Invoke();
                return;
            }
        }

        if (this.mapViewPanel.Bounds.Contains(x, y))
        {
            this.mapViewPanel.ReceiveRightClick(x, y);
            return;
        }

        if (this.timelinePanel.Bounds.Contains(x, y))
        {
            this.timelinePanel.ReceiveRightClick(x, y);
            return;
        }

        if (this.propertiesPanel.Bounds.Contains(x, y))
        {
            this.propertiesPanel.ReceiveRightClick(x, y);
            return;
        }

        base.receiveRightClick(x, y, playSound);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (this.closeConfirmationOpen)
        {
            return;
        }

        if (this.preconditionEditorPanel is not null || this.saveDialogPanel is not null || this.eventPickerPanel is not null || this.state.Mode == EditorMode.Play)
        {
            return;
        }

        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);

        if (this.mapViewPanel.Bounds.Contains(mouseX, mouseY))
        {
            this.mapViewPanel.ReceiveScrollWheelAction(direction);
            return;
        }

        base.receiveScrollWheelAction(direction);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (this.closeConfirmationOpen)
        {
            if (key == Keys.Escape)
            {
                this.closeConfirmationOpen = false;
            }

            return;
        }

        if (this.eventPickerPanel is not null)
        {
            this.eventPickerPanel.ReceiveKeyPress(key);
            return;
        }

        if (this.saveDialogPanel is not null)
        {
            this.saveDialogPanel.ReceiveKeyPress(key);
            return;
        }

        if (this.preconditionEditorPanel is not null)
        {
            if (key == Keys.Escape && !this.preconditionEditorPanel.HasSelectedTextField())
            {
                this.preconditionEditorPanel.Cancel();
                return;
            }

            this.preconditionEditorPanel.ReceiveKeyPress(key);
            return;
        }

        if (this.propertiesPanel.HasSelectedTextField())
        {
            this.propertiesPanel.ReceiveKeyPress(key);
            return;
        }

        if (key == Keys.Escape && this.state.Mode == EditorMode.Play)
        {
            this.StopPlayback("Preview stopped.");
            return;
        }

        if (key == Keys.Escape)
        {
            this.RequestClose();
            return;
        }

        base.receiveKeyPress(key);
    }

    private void OpenPreconditionEditor()
    {
        this.preconditionEditorPanel = new PreconditionEditorPanel(this.state, () => this.preconditionEditorPanel = null);
    }

    private void OpenSaveDialog()
    {
        this.saveDialogPanel = new SaveDialogPanel(
            this.state,
            ModEntry.Instance.ModsDirectoryPath,
            () => this.saveDialogPanel = null,
            message => this.toolbarStatusMessage = message
        );
    }

    private void OpenImportDialog()
    {
        string initialPath = File.Exists(this.state.LastSavedContentJsonPath)
            ? this.state.LastSavedContentJsonPath
            : ModEntry.Instance.ModsDirectoryPath;
        this.eventPickerPanel = new EventPickerPanel(
            () => this.eventPickerPanel = null,
            this.ImportCutscene,
            initialPath
        );
    }

    private void NewCutscene()
    {
        if (this.state.IsDirty)
        {
            this.RequestConfirmation(
                "Start New Cutscene?",
                "Unsaved changes will be lost.",
                "New",
                this.ResetCutscene
            );
            return;
        }

        this.ResetCutscene();
    }

    private void ResetCutscene()
    {
        this.StopPlayback();
        this.state.Cutscene = CutsceneData.CreateBlank();
        this.state.SelectedCommandIndex = -1;
        this.state.PlaybackCommandIndex = -1;
        this.state.Mode = EditorMode.Edit;
        this.state.UndoStack.Clear();
        this.state.RedoStack.Clear();
        this.state.PreviewEmotes.Clear();
        this.state.BootstrappedMap = LocationBootstrapper.Load(this.state.Cutscene.LocationName);
        this.state.IsDirty = false;
        this.toolbarStatusMessage = "Started a new cutscene.";
    }

    private void CycleLocation(int direction)
    {
        IReadOnlyList<string> locations = LocationBootstrapper.SupportedLocations;
        if (locations.Count == 0)
        {
            this.toolbarStatusMessage = "No locations found.";
            return;
        }

        int currentIndex = Math.Max(0, locations.ToList().FindIndex(name => name.Equals(this.state.Cutscene.LocationName, StringComparison.OrdinalIgnoreCase)));
        string nextLocation = locations[WrapIndex(currentIndex + Math.Sign(direction), locations.Count)];
        if (nextLocation.Equals(this.state.Cutscene.LocationName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this.StopPlayback();
        GameLocation? loaded = LocationBootstrapper.Load(nextLocation);
        this.state.Cutscene.LocationName = nextLocation;
        this.state.BootstrappedMap = loaded;
        this.state.IsDirty = true;
        this.toolbarStatusMessage = loaded is null
            ? $"Location '{nextLocation}' could not be previewed."
            : $"Location set to {nextLocation}.";
    }

    private void ImportCutscene(CutsceneData cutscene)
    {
        this.StopPlayback();
        this.state.Cutscene = cutscene;
        this.state.SelectedCommandIndex = -1;
        this.state.PlaybackCommandIndex = -1;
        this.state.Mode = EditorMode.Edit;
        this.state.UndoStack.Clear();
        this.state.RedoStack.Clear();
        this.state.BootstrappedMap = LocationBootstrapper.Load(cutscene.LocationName);
        this.state.IsDirty = false;
        this.toolbarStatusMessage = $"Imported {cutscene.UniqueId}.";
    }

    private void TogglePlayback()
    {
        if (this.state.Mode == EditorMode.Play)
        {
            this.StopPlayback("Preview stopped.");
            return;
        }

        if (!this.playWarningShown)
        {
            this.playWarningShown = true;
            this.toolbarStatusMessage = "Play preview is experimental at title screen.";
        }

        this.StartPlayback();
    }

    private void StartPlayback()
    {
        try
        {
            GameLocation? location = this.state.BootstrappedMap ?? LocationBootstrapper.Load(this.state.Cutscene.LocationName);
            if (location is null)
            {
                this.toolbarStatusMessage = "Cannot play: map failed to load.";
                return;
            }

            this.previousPlayer = Game1.player;
            this.previousLocation = Game1.currentLocation;
            this.previousEventUp = Game1.eventUp;
            this.previewPlayerCreated = Game1.player is null;
            this.playbackBootstrapActive = true;

            Farmer? previewPlayer = Game1.player;
            if (previewPlayer is null)
            {
                previewPlayer = new Farmer
                {
                    Name = "Preview",
                    UniqueMultiplayerID = 0
                };
                SetGamePlayer(previewPlayer);
            }

            this.previousPlayerLocation = previewPlayer.currentLocation;
            this.previousPlayerPosition = previewPlayer.Position;
            this.previousPlayerFacing = previewPlayer.FacingDirection;
            previewPlayer.currentLocation = location;
            previewPlayer.Position = new Vector2(
                this.state.Cutscene.FarmerPlacement.TileX * Game1.tileSize,
                this.state.Cutscene.FarmerPlacement.TileY * Game1.tileSize
            );

            Game1.currentLocation = location;
            location.currentEvent = null;
            location.characters.Clear();

            string script = EventScriptBuilder.Build(this.state.Cutscene);
            Event previewEvent = new(script, null, this.state.Cutscene.UniqueId, previewPlayer)
            {
                markEventSeen = false
            };
            location.currentEvent = previewEvent;
            Game1.activeClickableMenu = this;
            Game1.eventUp = true;
            Game1.eventOver = false;
            Game1.pauseTime = 0f;
            this.state.BootstrappedMap = location;
            this.state.Mode = EditorMode.Play;
            this.state.PlaybackCommandIndex = 0;
            this.state.PreviewEmotes.Clear();
            this.registeredPreviewEmoteCommands.Clear();
            this.toolbarStatusMessage = "Preview playing.";
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker play preview failed: {ex}", StardewModdingAPI.LogLevel.Error);
            this.StopPlayback();
            this.toolbarStatusMessage = "Preview failed. See SMAPI log.";
        }
    }

    private void UpdatePlayback(GameTime time)
    {
        GameLocation? location = this.state.BootstrappedMap;
        Event? currentEvent = location?.currentEvent;
        if (location is null || currentEvent is null)
        {
            this.StopPlayback();
            return;
        }

        try
        {
            Game1.currentLocation = location;
            if (this.yieldPlaybackFrame)
            {
                this.yieldPlaybackFrame = false;
                this.UpdatePreviewEmotes(time);
                this.UpdateEventActorEmotes(currentEvent, time);
                this.CapturePlaybackDialogue();
                return;
            }

            if (Game1.pauseTime > 0f)
            {
                Game1.pauseTime = Math.Max(0f, Game1.pauseTime - (float)time.ElapsedGameTime.TotalMilliseconds);
            }

            Game1.player?.updateEmote(time);
            this.UpdatePreviewEmotes(time);
            if (Game1.activeClickableMenu == this)
            {
                Game1.activeClickableMenu = null;
            }

            int commandBeforeUpdate = currentEvent.CurrentCommand;
            this.RegisterPreviewEmotes(currentEvent, commandBeforeUpdate, commandBeforeUpdate + 1);
            currentEvent.Update(location, time);
            this.UpdatePreviewFarmerMovementAnimation(time);
            this.RegisterPreviewEmotes(currentEvent, commandBeforeUpdate, currentEvent.CurrentCommand);
            this.CapturePlaybackDialogue();
            if (!Game1.eventOver && currentEvent.CurrentCommand != commandBeforeUpdate)
            {
                this.yieldPlaybackFrame = true;
            }

            if (Game1.eventOver && currentEvent.CurrentCommand >= currentEvent.eventCommands.Length)
            {
                if (this.HasActivePlaybackEmote(currentEvent))
                {
                    this.UpdateEventActorEmotes(currentEvent, time);
                    return;
                }

                this.StopPlayback("Preview finished.");
            }
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker play preview update failed: {ex}", StardewModdingAPI.LogLevel.Error);
            this.StopPlayback();
            this.toolbarStatusMessage = "Preview failed. See SMAPI log.";
        }
    }

    private void StopPlayback(string? statusMessage = null)
    {
        if (this.state.Mode != EditorMode.Play && !this.playbackBootstrapActive)
        {
            return;
        }

        if (this.state.BootstrappedMap is not null)
        {
            this.state.BootstrappedMap.currentEvent = null;
            this.state.BootstrappedMap.characters.Clear();
        }

        this.playbackDialogueBox = null;
        this.yieldPlaybackFrame = false;
        this.state.PreviewEmotes.Clear();
        this.registeredPreviewEmoteCommands.Clear();
        Game1.dialogueUp = false;
        Game1.dialogueTyping = false;
        Game1.eventUp = this.previousEventUp;
        Game1.eventOver = false;
        Game1.pauseTime = 0f;
        this.state.Mode = EditorMode.Edit;
        this.state.PlaybackCommandIndex = -1;

        if (this.previewPlayerCreated)
        {
            SetGamePlayer(this.previousPlayer);
        }
        else if (this.previousPlayer is not null)
        {
            this.previousPlayer.currentLocation = this.previousPlayerLocation;
            this.previousPlayer.Position = this.previousPlayerPosition;
            this.previousPlayer.faceDirection(this.previousPlayerFacing);
            this.previousPlayer.Halt();
            this.previousPlayer.FarmerSprite.StopAnimation();
        }

        Game1.currentLocation = this.previousLocation;
        this.previewPlayerCreated = false;
        this.playbackBootstrapActive = false;
        this.previousPlayer = null;
        this.previousPlayerLocation = null;
        this.previousPlayerPosition = Vector2.Zero;
        this.previousPlayerFacing = 2;
        this.previousLocation = null;
        this.previousEventUp = false;

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            this.toolbarStatusMessage = statusMessage;
        }
    }

    private bool HasActivePlaybackEmote(Event currentEvent)
    {
        if (Game1.player?.IsEmoting == true)
        {
            return true;
        }

        if (this.state.PreviewEmotes.Count > 0)
        {
            return true;
        }

        foreach (NPC actor in currentEvent.actors)
        {
            if (actor.IsEmoting)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateEventActorEmotes(Event currentEvent, GameTime time)
    {
        foreach (NPC actor in currentEvent.actors)
        {
            actor.updateEmote(time);
        }
    }

    private void UpdatePreviewFarmerMovementAnimation(GameTime time)
    {
        Farmer? player = Game1.player;
        if (player is null || this.state.Mode != EditorMode.Play)
        {
            return;
        }

        player.updateMovementAnimation(time);
    }

    private void RegisterPreviewEmotes(Event currentEvent, int startCommand, int endCommand)
    {
        if (endCommand <= startCommand)
        {
            return;
        }

        int start = Math.Max(0, startCommand);
        int end = Math.Min(endCommand, currentEvent.eventCommands.Length);
        for (int index = start; index < end; index++)
        {
            if (!this.registeredPreviewEmoteCommands.Add(index))
            {
                continue;
            }

            this.TryRegisterPreviewEmote(currentEvent.eventCommands[index]);
        }
    }

    private void TryRegisterPreviewEmote(string rawCommand)
    {
        List<string> parts = QuoteAwareSplit.Split(rawCommand, ' ')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (parts.Count < 3 || !parts[0].Equals("emote", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string actorName = parts[1];
        if (actorName.Equals("farmer", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!int.TryParse(parts[2], out int emoteId))
        {
            return;
        }

        this.state.PreviewEmotes.RemoveAll(emote => emote.ActorName.Equals(actorName, StringComparison.OrdinalIgnoreCase));
        this.state.PreviewEmotes.Add(new PreviewEmote(actorName, emoteId));
    }

    private void UpdatePreviewEmotes(GameTime time)
    {
        for (int index = this.state.PreviewEmotes.Count - 1; index >= 0; index--)
        {
            PreviewEmote emote = this.state.PreviewEmotes[index];
            emote.Update(time);
            if (emote.IsFinished)
            {
                this.state.PreviewEmotes.RemoveAt(index);
            }
        }
    }

    private void CapturePlaybackDialogue()
    {
        if (Game1.activeClickableMenu is DialogueBox dialogueBox)
        {
            this.playbackDialogueBox = dialogueBox;
        }

        if (!Game1.dialogueUp)
        {
            this.playbackDialogueBox = null;
        }

        if (this.state.Mode == EditorMode.Play)
        {
            Game1.activeClickableMenu = this;
        }
    }

    private void UpdatePlaybackDialogue(GameTime time)
    {
        if (this.state.Mode != EditorMode.Play || this.playbackDialogueBox is not DialogueBox)
        {
            this.playbackDialogueBox?.update(time);
            return;
        }

        this.RouteInputToPlaybackDialogue(dialogueBox => dialogueBox.update(time));
    }

    private void RouteInputToPlaybackDialogue(Action<IClickableMenu> action)
    {
        IClickableMenu? dialogueBox = this.playbackDialogueBox;
        if (dialogueBox is null)
        {
            return;
        }

        Game1.activeClickableMenu = dialogueBox;
        action(dialogueBox);
        this.CapturePlaybackDialogue();
    }

    private void RequestClose()
    {
        if (!this.state.IsDirty)
        {
            this.ConfirmClose();
            return;
        }

        this.RequestConfirmation(
            "Exit Cutscene Maker?",
            "Unsaved changes will be lost.",
            "Exit",
            this.ConfirmClose
        );
    }

    private void ConfirmClose()
    {
        this.StopPlayback();
        this.closeConfirmationOpen = false;
        Game1.exitActiveMenu();
    }

    private void RequestConfirmation(string title, string message, string yesLabel, Action action)
    {
        this.closeConfirmTitle = title;
        this.closeConfirmMessage = message;
        this.closeConfirmYesLabel = yesLabel;
        this.closeConfirmAction = action;
        this.closeConfirmationOpen = true;
    }

    private void ReceiveCloseConfirmationClick(int x, int y)
    {
        if (this.closeConfirmYesBounds.Contains(x, y))
        {
            Action? action = this.closeConfirmAction;
            this.closeConfirmationOpen = false;
            this.closeConfirmAction = null;
            action?.Invoke();
            return;
        }

        if (this.closeConfirmNoBounds.Contains(x, y))
        {
            this.closeConfirmationOpen = false;
            this.closeConfirmAction = null;
        }
    }

    private void DrawCloseConfirmation(SpriteBatch spriteBatch)
    {
        Rectangle overlayBounds = new(
            this.xPositionOnScreen + this.width / 2 - 240,
            this.yPositionOnScreen + this.height / 2 - 92,
            480,
            184
        );
        this.closeConfirmYesBounds = new Rectangle(overlayBounds.X + 92, overlayBounds.Bottom - 62, 120, 42);
        this.closeConfirmNoBounds = new Rectangle(overlayBounds.Right - 212, overlayBounds.Bottom - 62, 120, 42);

        spriteBatch.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.45f);
        IClickableMenu.drawTextureBox(spriteBatch, overlayBounds.X, overlayBounds.Y, overlayBounds.Width, overlayBounds.Height, Color.White);

        Vector2 titleSize = Game1.smallFont.MeasureString(this.closeConfirmTitle);
        Vector2 messageSize = Game1.smallFont.MeasureString(this.closeConfirmMessage);
        Utility.drawTextWithShadow(
            spriteBatch,
            this.closeConfirmTitle,
            Game1.smallFont,
            new Vector2(overlayBounds.Center.X - titleSize.X / 2f, overlayBounds.Y + 32),
            Game1.textColor
        );
        Utility.drawTextWithShadow(
            spriteBatch,
            this.closeConfirmMessage,
            Game1.smallFont,
            new Vector2(overlayBounds.Center.X - messageSize.X / 2f, overlayBounds.Y + 70),
            Game1.textColor
        );

        this.DrawDialogButton(spriteBatch, this.closeConfirmYesBounds, this.closeConfirmYesLabel);
        this.DrawDialogButton(spriteBatch, this.closeConfirmNoBounds, "Cancel");
    }

    private void DrawDialogButton(SpriteBatch spriteBatch, Rectangle bounds, string label)
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
    }

    private static void SetGamePlayer(Farmer? player)
    {
        if (player is not null)
        {
            GamePlayerProperty?.SetValue(null, player);
            return;
        }

        GamePlayerField?.SetValue(null, null);
    }

    private void RecalculateLayout()
    {
        this.xPositionOnScreen = ScreenPadding;
        this.yPositionOnScreen = ScreenPadding;
        this.width = Math.Max(MinimumWidth, Game1.uiViewport.Width - ScreenPadding * 2);
        this.height = Math.Max(MinimumHeight, Game1.uiViewport.Height - ScreenPadding * 2);

        int contentX = this.xPositionOnScreen + PanelGap;
        int contentY = this.yPositionOnScreen + ToolbarHeight + PanelGap;
        int contentWidth = this.width - PanelGap * 2;
        int middleHeight = this.height - ToolbarHeight - TimelineHeight - PanelGap * 3;
        int propertiesWidth = Math.Max(280, (int)(contentWidth * 0.32f));
        int mapWidth = contentWidth - propertiesWidth - PanelGap;

        this.mapViewPanel.SetBounds(new Rectangle(contentX, contentY, mapWidth, middleHeight));
        this.propertiesPanel.SetBounds(new Rectangle(contentX + mapWidth + PanelGap, contentY, propertiesWidth, middleHeight));
        this.timelinePanel.SetBounds(new Rectangle(contentX, contentY + middleHeight + PanelGap, contentWidth, TimelineHeight));
        this.upperRightCloseButton.bounds.X = this.xPositionOnScreen + this.width - 56;
        this.upperRightCloseButton.bounds.Y = this.yPositionOnScreen + 16;
    }

    private void DrawToolbar(SpriteBatch spriteBatch)
    {
        this.toolbarButtons.Clear();

        string title = this.state.IsDirty ? "Cutscene Maker *" : "Cutscene Maker";
        Utility.drawTextWithShadow(
            spriteBatch,
            title,
            Game1.smallFont,
            new Vector2(this.xPositionOnScreen + 28, this.yPositionOnScreen + 24),
            Game1.textColor
        );

        this.DrawToolbarButton(spriteBatch, new Rectangle(this.xPositionOnScreen + 190, this.yPositionOnScreen + 16, 72, 38), "New", this.NewCutscene);
        this.DrawToolbarButton(spriteBatch, new Rectangle(this.xPositionOnScreen + 274, this.yPositionOnScreen + 16, 96, 38), "Location", () => this.CycleLocation(1), () => this.CycleLocation(-1));

        Utility.drawTextWithShadow(
            spriteBatch,
            $"Location: {this.state.Cutscene.LocationName}",
            Game1.smallFont,
            new Vector2(this.xPositionOnScreen + 382, this.yPositionOnScreen + 24),
            Game1.textColor
        );

        this.DrawToolbarButton(spriteBatch, new Rectangle(this.xPositionOnScreen + 580, this.yPositionOnScreen + 16, 96, 38), this.state.Mode == EditorMode.Play ? "Stop" : "Play", this.TogglePlayback);
        this.DrawToolbarButton(spriteBatch, new Rectangle(this.xPositionOnScreen + 688, this.yPositionOnScreen + 16, 108, 38), "Import", this.OpenImportDialog);
        this.DrawToolbarButton(spriteBatch, new Rectangle(this.xPositionOnScreen + 808, this.yPositionOnScreen + 16, 96, 38), "Save", this.OpenSaveDialog);

        if (!string.IsNullOrWhiteSpace(this.toolbarStatusMessage))
        {
            Utility.drawTextWithShadow(
                spriteBatch,
                this.toolbarStatusMessage,
                Game1.smallFont,
                new Vector2(this.xPositionOnScreen + 916, this.yPositionOnScreen + 24),
                Color.DarkGreen
            );
        }
    }

    private void DrawToolbarButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Action leftClick, Action? rightClick = null)
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
        this.toolbarButtons.Add((bounds, leftClick, rightClick));
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
}
