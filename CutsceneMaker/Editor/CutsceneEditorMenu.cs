using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CutsceneMaker.Compiler;
using CutsceneMaker.Importer;
using CutsceneMaker.Models;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Quests;
using xTile;
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
    private const int LocationDropdownRowHeight = 34;
    private const int LocationDropdownMaxRows = 10;
    private const int LocationSearchHeight = 42;
    private const float PreviewFadeDurationMs = 500f;
    private const float PreviewFarmerMovePixelsPerSecond = 256f;
    // Actor name → Character lookup cache built at playback start for O(1) lookup.
    // NPC actors use the public showTextAboveHead() API; Farmer (which extends
    // Character, not NPC) falls back to Character-level reflection fields.
    private Dictionary<string, Character>? playbackActorCache;
    private static readonly FieldInfo? NPCTextAboveHeadField = typeof(Character).GetField("textAboveHead", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NPCTextAboveHeadTimerField = typeof(Character).GetField("textAboveHeadTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NPCTextAboveHeadColorField = typeof(Character).GetField("textAboveHeadColor", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NPCTextAboveHeadPreTimerField = typeof(Character).GetField("textAboveHeadPreTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    // Text-above-head fields for NPC actors (declared on NPC, not Character).
    // These are used by UpdateTextAboveHeadTimers() to tick bubble timers during preview —
    // kept separate from the Character-targeting fields above (Farmer fallback path).
    private static readonly FieldInfo? NPCAboveHeadTextField = typeof(NPC).GetField("textAboveHead", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NPCAboveHeadPreTimerField = typeof(NPC).GetField("textAboveHeadPreTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NPCAboveHeadTimerField = typeof(NPC).GetField("textAboveHeadTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NPCAboveHeadAlphaField = typeof(NPC).GetField("textAboveHeadAlpha", BindingFlags.Instance | BindingFlags.NonPublic);
    // Public SMAPI/game APIs don't expose a writable Game1.player at the title screen.
    // Play preview needs a temporary farmer because Event initialization reads Game1.player.
    private static readonly PropertyInfo? GamePlayerProperty = typeof(Game1).GetProperty("player", BindingFlags.Public | BindingFlags.Static);
    private static readonly FieldInfo? GamePlayerField = typeof(Game1).GetField("_player", BindingFlags.NonPublic | BindingFlags.Static);
    // Event keeps the active temporary map in a private field; preview sets it so vanilla follow-up commands
    // that read the temporary map keep working after we load imported CP maps through our preview asset path.
    private static readonly FieldInfo? EventTemporaryLocationField = typeof(Event).GetField("temporaryLocation", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly EditorState state = new();
    private readonly MapViewPanel mapViewPanel;
    private readonly TimelinePanel timelinePanel;
    private readonly PropertiesPanel propertiesPanel;
    private readonly List<(Rectangle Bounds, Action LeftClick, Action? RightClick)> toolbarButtons = new();
    private readonly List<(Rectangle Bounds, LocationCatalogEntry Location)> locationDropdownRows = new();
    private readonly HashSet<GameLocation> playbackLocations = new();
    private PreconditionEditorPanel? preconditionEditorPanel;
    private SaveDialogPanel? saveDialogPanel;
    private EventPickerPanel? eventPickerPanel;
    private string toolbarStatusMessage = string.Empty;
    private Color toolbarStatusColor = Color.DarkGreen;
    private bool simulateOnClick = true;
    private Farmer? previousPlayer;
    private GameLocation? previousPlayerLocation;
    private Vector2 previousPlayerPosition;
    private int previousPlayerFacing;
    private byte previousGameMode;
    private Color previousBgColor;
    private Color previousAmbientLight;
    private GameLocation? previousLocation;
    private Event? playbackEvent;
    private Farmer? playbackPlayer;
    private IClickableMenu? playbackMenu;
    private bool previousEventUp;
    private bool previousGlobalFade;
    private bool previousFadeIn;
    private bool previousFadeToBlack;
    private bool previousNonWarpFade;
    private float previousFadeToBlackAlpha;
    private float previousGlobalFadeSpeed;
    private xTile.Dimensions.Rectangle previousViewport;
    private string? previousBootstrappedMap;
    private bool yieldPlaybackFrame;
    private float previewPauseRemainingMs;
    private int previewPauseCommandIndex = -1;
    private bool closeConfirmationOpen;
    private Rectangle closeConfirmYesBounds;
    private Rectangle closeConfirmNoBounds;
    private string closeConfirmTitle = "Exit Cutscene Maker?";
    private string closeConfirmMessage = "Unsaved changes will be lost.";
    private string closeConfirmYesLabel = "Exit";
    private Action? closeConfirmAction;
    private bool locationDropdownOpen;
    private int locationDropdownScrollIndex;
    private string locationSearchText = string.Empty;
    private Rectangle locationButtonBounds;
    private bool previewPlayerCreated;
    private bool playbackBootstrapActive;
    private bool playWarningShown;
    private PlayerStateSnapshot? previewPlayerState;
    private int lastLoggedPreviewCommandIndex = -1;
    private string lastLoggedPlaybackMenuType = string.Empty;
    private int previewFarmerMoveCommandIndex = -1;
    private Vector2 previewFarmerMoveStart = Vector2.Zero;
    private Vector2 previewFarmerMoveTarget = Vector2.Zero;
    private float previewFarmerMoveElapsedMs;
    private float previewFarmerMoveDurationMs;
    private int previewFarmerMoveFacing = 2;
    private int stuckCommandIndex = -1;
    private int stuckFrameCount;
    // Tracks original (non-temp) locations used during playback so we can
    // restore state when changeLocation returns to them after a temp map switch.
    private readonly Dictionary<string, List<NPC>> playbackOriginalLocationCharacters = new(StringComparer.OrdinalIgnoreCase);

    public CutsceneEditorMenu()
        : base(
            ScreenPadding,
            ScreenPadding,
            Math.Max(MinimumWidth, Game1.uiViewport.Width - ScreenPadding * 2),
            Math.Max(MinimumHeight, Game1.uiViewport.Height - ScreenPadding * 2),
            showUpperRightCloseButton: true
        )
    {
        ModEntry.Instance.RefreshKnownLocations();
        ModEntry.Instance.RefreshKnownNpcs();
        ModEntry.Instance.RefreshCommandCatalog();
        this.LoadPreviewLocation(this.state.Cutscene.LocationName);
        this.mapViewPanel = new MapViewPanel(this.state);
        this.timelinePanel = new TimelinePanel(this.state, ModEntry.Instance.CommandCatalog);
        this.timelinePanel.OnCommandClicked = this.JumpToCommand;
        this.propertiesPanel = new PropertiesPanel(this.state, ModEntry.Instance.CommandCatalog, this.OpenPreconditionEditor);
        this.RecalculateLayout();
    }

    internal static CutsceneEditorMenu? ActivePlaybackMenu { get; private set; }

    internal void ContainPreviewGameState()
    {
        if (this.state.Mode != EditorMode.Play || this.playbackEvent is null)
        {
            return;
        }

        bool contained = false;
        if (Game1.gameMode != this.previousGameMode)
        {
            Game1.gameMode = this.previousGameMode;
            contained = true;
        }

        GameLocation? location = this.state.BootstrappedMap;
        if (location is not null)
        {
            if (!ReferenceEquals(Game1.currentLocation, location))
            {
                Game1.currentLocation = location;
                contained = true;
            }

            if (!ReferenceEquals(location.currentEvent, this.playbackEvent))
            {
                location.currentEvent = this.playbackEvent;
                contained = true;
            }
        }

        if (this.playbackPlayer is not null)
        {
            if (!ReferenceEquals(Game1.player, this.playbackPlayer))
            {
                SetGamePlayer(this.playbackPlayer);
                contained = true;
            }

            if (location is not null && !ReferenceEquals(this.playbackPlayer.currentLocation, location))
            {
                this.playbackPlayer.currentLocation = location;
                this.playbackEvent.farmer.currentLocation = location;
                contained = true;
            }
        }

        if (!ReferenceEquals(Game1.activeClickableMenu, this))
        {
            Game1.activeClickableMenu = this;
            contained = true;
        }

        if (Game1.eventUp || Game1.eventOver)
        {
            Game1.eventUp = false;
            Game1.eventOver = false;
            contained = true;
        }

        if (Game1.newDay)
        {
            Game1.newDay = false;
            contained = true;
        }

        if (Game1.quit)
        {
            Game1.quit = false;
            contained = true;
        }

        if (Game1.currentMinigame is not null)
        {
            Game1.currentMinigame = null;
            contained = true;
        }

        // Prevent title screen's UpdateTitleScreen() from detecting fade completion
        // at the START of the next frame and calling setGameMode(6) + exitActiveMenu().
        // UpdateTitleScreen runs unconditionally (no activeClickableMenu gate) and
        // checks fadeToBlack && fadeToBlackAlpha <= 0f to trigger new-game loading.
        if (Game1.fadeToBlack)
        {
            Game1.fadeToBlack = false;
            contained = true;
        }

        if (contained)
        {
            this.LogPreviewMessage($"contained leaked game state; {this.FormatPreviewState()}");
        }
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
        if (this.locationDropdownOpen)
        {
            this.DrawLocationDropdown(b);
        }

        this.playbackMenu?.draw(b);
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
        else
        {
            this.UpdatePlaybackDialogue(time);
        }
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

        if (this.TryHandleLocationDropdownClick(x, y))
        {
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

        this.CloseLocationDropdown();

        if (this.state.Mode == EditorMode.Play)
        {
            if (this.playbackMenu is not null)
            {
                this.RouteInputToPlaybackMenu(menu => menu.receiveLeftClick(x, y, playSound));
            }

            return;
        }

        if (this.timelinePanel.HasTransientMenu || this.timelinePanel.WantsClick(x, y))
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

    public override void leftClickHeld(int x, int y)
    {
        if (this.state.Mode != EditorMode.Play && (this.timelinePanel.Bounds.Contains(x, y) || this.timelinePanel.IsDragging))
        {
            this.timelinePanel.LeftClickHeld(x, y);
            return;
        }

        base.leftClickHeld(x, y);
    }

    public override void releaseLeftClick(int x, int y)
    {
        if (this.state.Mode == EditorMode.Play && this.playbackMenu is not null)
        {
            this.RouteInputToPlaybackMenu(menu => menu.releaseLeftClick(x, y));
            return;
        }

        this.timelinePanel.ReleaseLeftClick(x, y);
        base.releaseLeftClick(x, y);
    }

    public override void performHoverAction(int mouseX, int mouseY)
    {
        if (this.state.Mode == EditorMode.Play && this.playbackMenu is not null)
        {
            this.playbackMenu.performHoverAction(mouseX, mouseY);
        }

        base.performHoverAction(mouseX, mouseY);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (this.closeConfirmationOpen)
        {
            return;
        }

        this.CloseLocationDropdown();

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

        if (this.eventPickerPanel is not null)
        {
            this.eventPickerPanel.ReceiveScrollWheelAction(direction);
            return;
        }

        if (this.preconditionEditorPanel is not null || this.saveDialogPanel is not null || this.state.Mode == EditorMode.Play)
        {
            return;
        }

        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);

        if (this.locationDropdownOpen && this.GetLocationDropdownBounds().Contains(mouseX, mouseY))
        {
            this.ScrollLocationDropdown(direction);
            return;
        }

        if (this.timelinePanel.AddMenuOpen)
        {
            this.timelinePanel.ReceiveScrollWheelAction(direction);
            return;
        }

        if (this.mapViewPanel.Bounds.Contains(mouseX, mouseY))
        {
            this.mapViewPanel.ReceiveScrollWheelAction(direction);
            return;
        }

        if (this.propertiesPanel.Bounds.Contains(mouseX, mouseY))
        {
            this.propertiesPanel.ReceiveScrollWheelAction(direction);
            return;
        }

        if (this.timelinePanel.Bounds.Contains(mouseX, mouseY))
        {
            this.timelinePanel.ReceiveScrollWheelAction(direction);
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

        if (this.locationDropdownOpen && this.HandleLocationDropdownKey(key))
        {
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

        if (key == Keys.Escape && this.timelinePanel.HasTransientMenu)
        {
            this.timelinePanel.CloseTransientMenusByUser();
            return;
        }

        if (this.timelinePanel.HasSelectedTextField())
        {
            this.timelinePanel.ReceiveKeyPress(key);
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

        if (this.state.Mode == EditorMode.Play && this.playbackMenu is not null)
        {
            this.RouteInputToPlaybackMenu(menu => menu.receiveKeyPress(key));
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
        this.preconditionEditorPanel = new PreconditionEditorPanel(this.state, ModEntry.Instance.PreconditionCatalog, () => this.preconditionEditorPanel = null);
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
        this.state.CommandMarkerIndex = -1;
        this.state.SimulatedActorPositions.Clear();
        this.state.Mode = EditorMode.Edit;
        this.state.UndoStack.Clear();
        this.state.RedoStack.Clear();
        this.LoadPreviewLocation(this.state.Cutscene.LocationName);
        this.state.IsDirty = false;
        this.toolbarStatusMessage = "Started a new cutscene.";
    }

    private void ToggleLocationDropdown()
    {
        ModEntry.Instance.RefreshKnownLocations();
        if (LocationBootstrapper.SupportedLocationEntries.Count == 0)
        {
            this.toolbarStatusMessage = "No locations found.";
            return;
        }

        this.locationDropdownOpen = !this.locationDropdownOpen;
        if (this.locationDropdownOpen)
        {
            this.locationSearchText = string.Empty;
            IReadOnlyList<LocationCatalogEntry> filteredLocations = this.GetFilteredLocations();
            int selectedIndex = FindLocationIndex(filteredLocations, this.state.SelectedLocationId, this.state.Cutscene.LocationName);
            this.locationDropdownScrollIndex = Math.Clamp(
                selectedIndex < 0 ? 0 : selectedIndex - LocationDropdownMaxRows / 2,
                0,
                this.GetMaxLocationScrollIndex()
            );
        }
        else
        {
            this.locationSearchText = string.Empty;
        }
    }

    private void SetLocation(LocationCatalogEntry nextLocation)
    {
        if (string.IsNullOrWhiteSpace(nextLocation.Id)
            || nextLocation.Id.Equals(this.state.SelectedLocationId, StringComparison.OrdinalIgnoreCase))
        {
            this.CloseLocationDropdown();
            return;
        }

        this.CloseLocationDropdown();
        this.StopPlayback();
        this.state.SelectedLocationId = nextLocation.Id;
        this.state.Cutscene.LocationName = nextLocation.EventLocationName;
        LocationLoadResult loadResult = this.LoadPreviewLocation(nextLocation.Id);
        this.state.IsDirty = true;
        this.toolbarStatusMessage = loadResult.Loaded
            ? $"Location set to {nextLocation.DisplayName}."
            : $"Location '{nextLocation.DisplayName}' preview failed: {this.TrimToolbarText(loadResult.FailureReason ?? "unknown reason", 70)}";
    }

    private LocationLoadResult LoadPreviewLocation(string locationIdOrName)
    {
        LocationCatalogEntry entry = LocationBootstrapper.ResolveEntry(locationIdOrName);
        this.state.SelectedLocationId = entry.Id;
        this.state.Cutscene.LocationName = entry.EventLocationName;

        LocationLoadResult result = LocationBootstrapper.LoadDetailed(entry.Id);
        this.state.BootstrappedMap = result.Location;
        this.state.MapLoadFailureMessage = result.Loaded
            ? string.Empty
            : result.FailureReason ?? "Map could not be loaded for an unknown reason.";
        return result;
    }

    private bool TryHandleLocationDropdownClick(int x, int y)
    {
        if (!this.locationDropdownOpen)
        {
            return false;
        }

        foreach ((Rectangle bounds, LocationCatalogEntry location) in this.locationDropdownRows)
        {
            if (bounds.Contains(x, y))
            {
                this.SetLocation(location);
                return true;
            }
        }

        if (this.locationButtonBounds.Contains(x, y))
        {
            return false;
        }

        if (this.GetLocationDropdownBounds().Contains(x, y))
        {
            return true;
        }

        this.CloseLocationDropdown();
        return false;
    }

    private void ScrollLocationDropdown(int direction)
    {
        int delta = direction > 0 ? -1 : 1;
        this.locationDropdownScrollIndex = Math.Clamp(
            this.locationDropdownScrollIndex + delta,
            0,
            this.GetMaxLocationScrollIndex()
        );
    }

    private int GetMaxLocationScrollIndex()
    {
        return Math.Max(0, this.GetFilteredLocations().Count - LocationDropdownMaxRows);
    }

    private void CloseLocationDropdown()
    {
        this.locationDropdownOpen = false;
        this.locationSearchText = string.Empty;
        this.locationDropdownScrollIndex = 0;
    }

    private IReadOnlyList<LocationCatalogEntry> GetFilteredLocations()
    {
        if (string.IsNullOrWhiteSpace(this.locationSearchText))
        {
            return LocationBootstrapper.SupportedLocationEntries;
        }

        return LocationBootstrapper.SupportedLocationEntries
            .Where(entry => entry.SearchText.Contains(this.locationSearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private bool HandleLocationDropdownKey(Keys key)
    {
        if (key == Keys.Escape)
        {
            this.CloseLocationDropdown();
            return true;
        }

        if (key == Keys.Enter)
        {
            IReadOnlyList<LocationCatalogEntry> filteredLocations = this.GetFilteredLocations();
            if (filteredLocations.Count > 0)
            {
                this.SetLocation(filteredLocations[Math.Clamp(this.locationDropdownScrollIndex, 0, filteredLocations.Count - 1)]);
            }

            return true;
        }

        if (key == Keys.Back)
        {
            if (this.locationSearchText.Length > 0)
            {
                this.locationSearchText = this.locationSearchText[..^1];
                this.locationDropdownScrollIndex = 0;
            }

            return true;
        }

        if (key == Keys.Delete)
        {
            this.locationSearchText = string.Empty;
            this.locationDropdownScrollIndex = 0;
            return true;
        }

        char? character = GetLocationSearchCharacter(key);
        if (character is null)
        {
            return false;
        }

        this.locationSearchText += character.Value;
        this.locationDropdownScrollIndex = 0;
        return true;
    }

    private static int FindLocationIndex(IReadOnlyList<LocationCatalogEntry> locations, string selectedLocationId, string eventLocationName)
    {
        for (int index = 0; index < locations.Count; index++)
        {
            LocationCatalogEntry location = locations[index];
            if (location.Id.Equals(selectedLocationId, StringComparison.OrdinalIgnoreCase)
                || location.EventLocationName.Equals(eventLocationName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static char? GetLocationSearchCharacter(Keys key)
    {
        KeyboardState keyboardState = Keyboard.GetState();
        bool shiftHeld = keyboardState.IsKeyDown(Keys.LeftShift)
            || keyboardState.IsKeyDown(Keys.RightShift);

        if (key is >= Keys.A and <= Keys.Z)
        {
            return (char)('a' + ((int)key - (int)Keys.A));
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            string normalDigits = "0123456789";
            string shiftedDigits = ")!@#$%^&*(";
            int index = (int)key - (int)Keys.D0;
            return shiftHeld ? shiftedDigits[index] : normalDigits[index];
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            return (char)('0' + ((int)key - (int)Keys.NumPad0));
        }

        return key switch
        {
            Keys.Space => ' ',
            Keys.OemMinus => shiftHeld ? '_' : '-',
            Keys.OemPeriod => '.',
            Keys.OemComma => ',',
            Keys.OemPlus => shiftHeld ? '+' : '=',
            Keys.OemQuestion => shiftHeld ? '?' : '/',
            Keys.OemSemicolon => shiftHeld ? ':' : ';',
            Keys.OemQuotes => shiftHeld ? '"' : '\'',
            Keys.OemOpenBrackets => shiftHeld ? '{' : '[',
            Keys.OemCloseBrackets => shiftHeld ? '}' : ']',
            Keys.OemPipe => shiftHeld ? '|' : '\\',
            _ => null
        };
    }

    private void ImportCutscene(CutsceneData cutscene)
    {
        this.StopPlayback();
        if (cutscene.ImportContext?.PreviewMapOverrides.Count > 0)
        {
            ModEntry.Instance.RegisterImportedPreviewMapOverrides(cutscene.ImportContext.PreviewMapOverrides.Values);
        }

        this.state.Cutscene = cutscene;
        this.state.SelectedCommandIndex = -1;
        this.state.PlaybackCommandIndex = -1;
        this.state.CommandMarkerIndex = -1;
        this.state.SimulatedActorPositions.Clear();
        this.state.Mode = EditorMode.Edit;
        this.state.UndoStack.Clear();
        this.state.RedoStack.Clear();
        this.LoadPreviewLocation(cutscene.LocationName);
        this.state.IsDirty = false;
        this.toolbarStatusMessage = cutscene.HasUnresolvedTokens
            ? $"Imported {cutscene.UniqueId} (has unresolved CP tokens)."
            : $"Imported {cutscene.UniqueId}.";
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
            this.propertiesPanel.CommitSelectedTextFields();

            List<string> validationErrors = CutsceneValidator.Validate(this.state.Cutscene, ModEntry.Instance.CommandCatalog, ModEntry.Instance.PreconditionCatalog, forPreview: true);
            if (validationErrors.Count > 0)
            {
                this.toolbarStatusMessage = "Cannot preview: " + validationErrors[0];
                this.toolbarStatusColor = Color.Red;
                return;
            }

            GameLocation? location = this.state.BootstrappedMap ?? this.LoadPreviewLocation(this.state.SelectedLocationId).Location;
            if (location is null)
            {
                this.toolbarStatusMessage = $"Cannot play: {this.state.MapLoadFailureMessage}";
                this.toolbarStatusColor = Color.Red;
                return;
            }

            this.previousPlayer = Game1.player;
            this.previousLocation = Game1.currentLocation;
            this.previousGameMode = Game1.gameMode;
            this.previousBgColor = Game1.bgColor;
            this.previousAmbientLight = Game1.ambientLight;
            this.previousEventUp = Game1.eventUp;
            this.previousGlobalFade = Game1.globalFade;
            this.previousFadeIn = Game1.fadeIn;
            this.previousFadeToBlack = Game1.fadeToBlack;
            this.previousNonWarpFade = Game1.nonWarpFade;
            this.previousFadeToBlackAlpha = Game1.fadeToBlackAlpha;
            this.previousGlobalFadeSpeed = Game1.globalFadeSpeed;
            this.previousViewport = Game1.viewport;
            this.previousBootstrappedMap = this.state.SelectedLocationId;
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
            this.playbackPlayer = previewPlayer;
            previewPlayer.currentLocation = location;
            Point previewPlayerTile = this.state.Cutscene.IncludeFarmer
                ? this.GetPreviewPlayerTile()
                : new Point(-100, -100);
            previewPlayer.Position = new Vector2(previewPlayerTile.X * Game1.tileSize, previewPlayerTile.Y * Game1.tileSize);

            Game1.currentLocation = location;
            location.currentEvent = null;
            location.characters.Clear();
            this.playbackLocations.Clear();
            this.playbackLocations.Add(location);
            this.playbackMenu = null;
            this.yieldPlaybackFrame = false;
            this.previewPauseRemainingMs = 0f;
            this.previewPauseCommandIndex = -1;
            this.ResetPreviewFarmerMove();
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            Game1.globalFade = false;
            Game1.fadeIn = false;
            Game1.fadeToBlack = false;
            Game1.nonWarpFade = false;
            Game1.fadeToBlackAlpha = 0f;
            Game1.pauseTime = 0f;
            string script = EventScriptBuilder.Build(
    this.state.Cutscene,
    ModEntry.Instance.CommandCatalog,
    startCommandIndex: this.state.CommandMarkerIndex,
    initialPositions: this.state.CommandMarkerIndex >= 0
        ? this.state.SimulatedActorPositions
        : null
);
            Event previewEvent = new(script, null, this.state.Cutscene.UniqueId, previewPlayer)
            {
                markEventSeen = false
            };
            this.playbackEvent = previewEvent;
            this.playbackActorCache = BuildPlaybackActorCache(previewEvent);
            location.currentEvent = previewEvent;
            location.ResetForEvent(previewEvent);
            Game1.viewport = this.GetInitialPlaybackViewport(location, previewPlayerTile);
            Game1.activeClickableMenu = this;
            // Keep the preview local to the editor. Setting the global event flag makes other mods treat
            // this as a real in-game event even though we're driving Event.Update manually from a menu.
            Game1.eventUp = false;
            Game1.eventOver = false;
            Game1.pauseTime = 0f;
            this.state.BootstrappedMap = location;
            this.state.Mode = EditorMode.Play;
            this.state.PlaybackCommandIndex = Math.Max(0, this.state.CommandMarkerIndex);
            this.lastLoggedPreviewCommandIndex = -1;
            this.lastLoggedPlaybackMenuType = string.Empty;
            string npcInfo = string.Empty;
            if (this.state.Cutscene.Actors.Count > previewEvent.actors.Count)
            {
                int missing = this.state.Cutscene.Actors.Count - previewEvent.actors.Count;
                npcInfo = $" ({missing} NPC(s) unavailable at title screen)";
            }

            this.toolbarStatusMessage = "Preview playing." + npcInfo;
            ActivePlaybackMenu = this;
            this.LogPreviewMessage($"started scriptCommands={previewEvent.eventCommands.Length} location={this.FormatLocation(location)} viewport={FormatViewport(Game1.viewport)}");
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker play preview failed: {ex}", StardewModdingAPI.LogLevel.Error);
            this.StopPlayback();
            this.toolbarStatusMessage = "Preview failed. See SMAPI log.";
            this.toolbarStatusColor = Color.Red;
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
            this.UpdateTextAboveHeadTimers(currentEvent, time);
            if (this.playbackMenu is not null)
            {
                this.UpdateEventActorEmotes(currentEvent, time);
                this.UpdatePreviewFades(currentEvent, time);
                this.UpdatePlaybackDialogue(time);
                if (this.playbackMenu is not null)
                {
                    return;
                }
            }

            if (this.yieldPlaybackFrame)
            {
                this.yieldPlaybackFrame = false;
                this.UpdateEventActorEmotes(currentEvent, time);
                this.CapturePlaybackMenu();
                return;
            }

            if (this.UpdatePreviewPause(currentEvent, time))
            {
                return;
            }

if (this.UpdatePreviewFarmerMove(currentEvent, time))
            {
                this.CapturePlaybackMenu();
                return;
            }

            Game1.player?.updateEmote(time);

            int commandBeforeUpdate = currentEvent.CurrentCommand;
            this.LogPreviewCommand(currentEvent);
            if (this.TryHandlePreviewOnlyCommand(currentEvent))
            {
                // Commands handled here do not mutate player state, so skip
                // the expensive CapturePlayerState deep-copy for this frame.
                this.previewPlayerState = null;
            }
            else
            {
                this.previewPlayerState = this.CapturePlayerState();

                // Save critical game mode state before Event.Update().  Some event
                // commands (end, switchEvent, or commands that set Game1.newDay) may
                // call Game1.setGameMode(6) internally, which has irreversible side
                // effects (clearing menus, resetting game state) that our downstream
                // ContainPreviewGameState cannot undo.  Clamp immediately.
                byte savedGameMode = Game1.gameMode;

                currentEvent.Update(location, time);

                // When playing from a marker, the header (eventCommands[0-2]) sets Game1.viewport
                // to the event file's viewport coordinates. After the first Event.Update processes
                // the header, correct the viewport to the backwards-search position instead.
                if (this.state.CommandMarkerIndex >= 0 && commandBeforeUpdate < 3 && currentEvent.CurrentCommand >= 3)
                {
                    Game1.viewport = this.GetInitialPlaybackViewport(location, this.GetPreviewPlayerTile());
                }

                Game1.gameMode = savedGameMode;
                Game1.newDay = false;
                Game1.quit = false;
            }

            // Stuck detection: force-advance past commands that neither our handler nor
            // vanilla Event.Update can process, preventing infinite loops (e.g. loading
            // a map whose tile sheets are only available through the content pipeline).
            if (currentEvent.CurrentCommand == commandBeforeUpdate)
            {
                if (currentEvent.CurrentCommand == this.stuckCommandIndex && this.stuckFrameCount > 0)
                {
                    if (++this.stuckFrameCount >= 300)
                    {
                        ModEntry.Instance.Monitor.Log($"Cutscene Maker preview stuck at command {commandBeforeUpdate} for {this.stuckFrameCount} frames, force-advancing.", StardewModdingAPI.LogLevel.Warn);
                        currentEvent.CurrentCommand++;
                        this.stuckCommandIndex = -1;
                        this.stuckFrameCount = 0;
                    }
                }
                else
                {
                    this.stuckCommandIndex = currentEvent.CurrentCommand;
                    this.stuckFrameCount = 1;
                }
            }
            else
            {
                this.stuckCommandIndex = -1;
                this.stuckFrameCount = 0;
            }

            currentEvent = this.SyncPlaybackLocationAfterUpdate(location, currentEvent);
            this.CapturePreviewPause(currentEvent);
            this.UpdatePreviewFarmerMovementAnimation(time);
            this.UpdatePreviewFades(currentEvent, time);
            this.CapturePlaybackMenu();

            // Save event-finished flag before ContainPreviewGameState clears Game1.eventOver
            bool eventFinished = currentEvent.CurrentCommand >= currentEvent.eventCommands.Length;

            this.ContainPreviewGameState();
            if (this.previewPlayerState is not null)
            {
                this.RestorePlayerState(this.previewPlayerState);
            }

            if (currentEvent.CurrentCommand != commandBeforeUpdate)
            {
                this.yieldPlaybackFrame = true;
            }

            // Track which command is being processed, mapped back to CutsceneData.Command index.
            // Use commandBeforeUpdate because CurrentCommand already points to the next script entry.
            // Treat CommandMarkerIndex -1 (setup) as 0 — setup isn't a real playable command.
            int effectiveMarker = Math.Max(0, this.state.CommandMarkerIndex);
            int headerEntries = EventScriptBuilder.GetHeaderEntryCount(this.state.Cutscene);
            int playbackIndex = effectiveMarker + Math.Max(0, commandBeforeUpdate - headerEntries);
            this.state.PlaybackCommandIndex = Math.Clamp(playbackIndex, effectiveMarker, this.state.Cutscene.Commands.Count - 1);

            if (eventFinished)
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
            string commandContext = GetEventCommandContext(currentEvent);
            ModEntry.Instance.Monitor.Log($"Cutscene Maker play preview update failed at {commandContext}: {ex}", StardewModdingAPI.LogLevel.Error);
            this.StopPlayback("Preview failed. See SMAPI log.");
            Game1.activeClickableMenu = this;
        }
    }

    private bool TryHandlePreviewOnlyCommand(Event currentEvent)
    {
        string? rawCommand = GetCurrentEventCommand(currentEvent);
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            return false;
        }

        List<string> parts = QuoteAwareSplit.Split(rawCommand, ' ')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToList();
        if (parts.Count < 2)
        {
            return false;
        }

        if (parts[0].Equals("move", StringComparison.Ordinal)
            && parts[1].Equals("farmer", StringComparison.OrdinalIgnoreCase))
        {
            return this.HandlePreviewFarmerMove(currentEvent, parts, rawCommand);
        }

        if (parts[0].Equals("changeLocation", StringComparison.Ordinal))
        {
            return this.HandlePreviewChangeLocation(currentEvent, parts, rawCommand);
        }

        if (parts[0].Equals("changeToTemporaryMap", StringComparison.Ordinal))
        {
            return this.HandlePreviewChangeToTemporaryMap(currentEvent, parts, rawCommand);
        }

        if (parts[0].Equals("message", StringComparison.Ordinal))
        {
            // Message uses Game1.drawObjectDialogue() which sets dialogueUp but never
            // creates an activeClickableMenu, so CapturePlaybackMenu can't capture it.
            // Instead create a real DialogueBox as playbackMenu so the standard preview
            // pause/dismiss/advance mechanism handles it like a speak command.
            // Set activeClickableMenu to match so CapturePlaybackMenu's Phase 2 dismissal
            // check (!ReferenceEquals) doesn't fire on the same frame — Phase 3 restores it.
            string text = Unquote(string.Join(" ", parts.Skip(1)));
            this.playbackMenu = new DialogueBox(text);
            Game1.activeClickableMenu = this.playbackMenu;
            return true;
        }

        if (parts[0].Equals("end", StringComparison.Ordinal) && parts.Count >= 4
            && (parts[1].Equals("dialogue", StringComparison.OrdinalIgnoreCase)
                || parts[1].Equals("dialogueWarpOut", StringComparison.OrdinalIgnoreCase)))
        {
            // end dialogue and end dialogueWarpOut use Game1.drawObjectDialogue() internally
            // which never sets activeClickableMenu, so CapturePlaybackMenu can't capture it.
            // Same root cause as message — create a real DialogueBox.
            string text = Unquote(string.Join(" ", parts.Skip(3)));
            this.playbackMenu = new DialogueBox(text);
            Game1.activeClickableMenu = this.playbackMenu;
            return true;
        }

        if (parts[0].Equals("quickQuestion", StringComparison.Ordinal))
        {
            // Replicate the vanilla SDV QuickQuestion handler EXACTLY.
            // The handler checks Game1.activeClickableMenu == null, but our
            // ContainPreviewGameState sets it to the editor every frame — so the
            // vanilla handler silently skips the command. We bypass by calling
            // createQuestionDialogue directly.
            string cmdText = currentEvent.eventCommands[currentEvent.CurrentCommand];
            int spaceIndex = cmdText.IndexOf(' ');
            string afterVerb = spaceIndex >= 0 ? cmdText.Substring(spaceIndex + 1) : "";
            string[] splitByBreak = afterVerb.Split(new[] { "(break)" }, StringSplitOptions.None);
            string qSection = splitByBreak.Length > 0 ? splitByBreak[0] : "";
            string[] fields = qSection.Split('#');

            string question = fields.Length > 0 ? fields[0].Trim().Trim('"') : "Choose an option:";
            var responses = new List<Response>();
            for (int i = 1; i < fields.Length; i++)
            {
                string text = fields[i].Trim().Trim('"');
                responses.Add(new Response((i - 1).ToString(), text));
            }

            // For the no-# case (entire text is the question), or 0-option question
            if (responses.Count == 0)
            {
                this.playbackMenu = new DialogueBox(question);
            }
            else
            {
                // Use the afterQuestionBehavior callback overload to replicate
                // Event.answerDialogue("quickQuestion", ...) sub-command insertion.
                // Game1.eventUp is false in preview mode, so the vanilla answer chain
                // (GameLocation.answerDialogue → Event.answerDialogue) never fires.
                Game1.currentLocation.createQuestionDialogue(
                    question,
                    responses.ToArray(),
                    (who, whichAnswer) =>
                    {
                        int answerChoice = int.Parse(whichAnswer);
                        // Sub-commands start at splitByBreak[1 + answerChoice], split by \
                        if (answerChoice + 1 < splitByBreak.Length)
                        {
                            string[] subCommands = splitByBreak[1 + answerChoice].Split('\\');
                            if (subCommands.Length > 0)
                            {
                                var cmdList = currentEvent.eventCommands.ToList();
                                cmdList.InsertRange(currentEvent.CurrentCommand + 1, subCommands);
                                currentEvent.eventCommands = cmdList.ToArray();
                            }
                        }
                    },
                    null  // speaker
                );
                this.playbackMenu = Game1.activeClickableMenu as DialogueBox;
            }

            Game1.activeClickableMenu = this.playbackMenu;
            return true;
        }

        if (parts[0].Equals("viewport", StringComparison.Ordinal))
        {
            // SDV's Event.Update crashes on viewport commands when Game1.eventUp is false.
            // Handle the viewport positioning directly in the preview.
            // Use panel content dimensions from MapViewPanel for centering — Game1.viewport.Width/Height
            // may reflect the full game window rather than the panel, producing off-center positioning.
            string target = parts.Count > 1 ? Unquote(parts[1]) : "";
            int panelWidth = Math.Max(1, this.mapViewPanel.Bounds.Width - 32);
            int panelHeight = Math.Max(1, this.mapViewPanel.Bounds.Height - 68);

            GameLocation? bootstrappedMap = this.state.BootstrappedMap;
            int mapMaxX = bootstrappedMap is not null
                ? Math.Max(0, bootstrappedMap.Map.DisplayWidth - Game1.viewport.Width)
                : int.MaxValue;
            int mapMaxY = bootstrappedMap is not null
                ? Math.Max(0, bootstrappedMap.Map.DisplayHeight - Game1.viewport.Height)
                : int.MaxValue;

            if (target.Equals("farmer", StringComparison.OrdinalIgnoreCase))
            {
                Farmer farmer = Game1.player;
                if (farmer is not null)
                {
                    int targetX = (int)farmer.Position.X - panelWidth / 2;
                    int targetY = (int)farmer.Position.Y - panelHeight / 2;
                    Game1.viewport = new xTile.Dimensions.Rectangle(
                        Math.Clamp(targetX, 0, mapMaxX),
                        Math.Clamp(targetY, 0, mapMaxY),
                        panelWidth, panelHeight);
                }
            }
            else if (target.Equals("move", StringComparison.OrdinalIgnoreCase) && parts.Count >= 5
                     && int.TryParse(Unquote(parts[2]), out int dx)
                     && int.TryParse(Unquote(parts[3]), out int dy))
            {
                // viewport move <dx> <dy> <duration> — snap to offset, ignores duration.
                // Vanilla SDV has no smooth-pan viewport, so the preview snaps to match.
                int targetX = Game1.viewport.X + dx * Game1.tileSize;
                int targetY = Game1.viewport.Y + dy * Game1.tileSize;

                Game1.viewport = new xTile.Dimensions.Rectangle(
                    Math.Clamp(targetX, 0, mapMaxX),
                    Math.Clamp(targetY, 0, mapMaxY),
                    panelWidth, panelHeight);
                Game1.nonWarpFade = false;
                currentEvent.CurrentCommand++;
                return true;
            }
            else
            {
                Character? character = null;
                if (this.playbackActorCache is not null && this.playbackActorCache.TryGetValue(target, out Character? cached))
                {
                    character = cached;
                }
                else
                {
                    character = currentEvent.actors.FirstOrDefault(a => a.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
                }

                int targetX, targetY;
                if (character is not null)
                {
                    targetX = (int)character.Position.X - panelWidth / 2;
                    targetY = (int)character.Position.Y - panelHeight / 2;
                }
                else if (int.TryParse(target, out int x) && parts.Count >= 3 && int.TryParse(Unquote(parts[2]), out int y))
                {
                    targetX = x * Game1.tileSize + Game1.tileSize / 2 - panelWidth / 2;
                    targetY = y * Game1.tileSize + Game1.tileSize / 2 - panelHeight / 2;
                }
                else
                {
                    targetX = Game1.viewport.X;
                    targetY = Game1.viewport.Y;
                }

                Game1.viewport = new xTile.Dimensions.Rectangle(
                    Math.Clamp(targetX, 0, mapMaxX),
                    Math.Clamp(targetY, 0, mapMaxY),
                    panelWidth, panelHeight);
            }

            Game1.nonWarpFade = false;
            currentEvent.CurrentCommand++;
            return true;
        }

        if (parts[0].Equals("textAboveHead", StringComparison.Ordinal))
        {
            return this.HandlePreviewTextAboveHead(currentEvent, parts);
        }

        return false;
    }

    private bool HandlePreviewTextAboveHead(Event currentEvent, List<string> parts)
    {
        if (parts.Count < 3)
        {
            currentEvent.CurrentCommand++;
            return true;
        }

        // Use the public NPC.showTextAboveHead() API for NPC actors.
        // Farmer (extends Character, not NPC) falls back to Character-level
        // reflection fields — these ARE on Character so typeof(Character) works.
        string actorName = Unquote(parts[1]);
        string text = Unquote(string.Join(" ", parts.Skip(2)));

        Character? character;
        if (this.playbackActorCache is not null && this.playbackActorCache.TryGetValue(actorName, out Character? cached))
        {
            character = cached;
        }
        else
        {
            character = currentEvent.actors.FirstOrDefault(a => a.Name == actorName);
            if (character is null && actorName.Equals("farmer", StringComparison.OrdinalIgnoreCase))
            {
                character = Game1.player;
            }
        }

        if (character is NPC npc)
        {
            npc.showTextAboveHead(text);
        }
        else if (character is not null)
        {
            // Farmer fallback via Character-level reflection fields
            NPCTextAboveHeadField?.SetValue(character, text);
            float timer = text.Contains('^') ? text.Split('^').Length * 1750f : 1750f;
            NPCTextAboveHeadTimerField?.SetValue(character, timer);
            NPCTextAboveHeadColorField?.SetValue(character, Color.White);
            NPCTextAboveHeadPreTimerField?.SetValue(character, 500f);
        }

        currentEvent.CurrentCommand++;
        return true;
    }

    private static Dictionary<string, Character> BuildPlaybackActorCache(Event ev)
    {
        var cache = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
        foreach (NPC actor in ev.actors)
        {
            cache[actor.Name] = actor;
        }
        if (Game1.player is not null)
        {
            cache["farmer"] = Game1.player;
        }
        return cache;
    }

    private bool HandlePreviewFarmerMove(Event currentEvent, List<string> parts, string rawCommand)
    {
        if (parts.Count < 5
            || !int.TryParse(Unquote(parts[2]), out int deltaX)
            || !int.TryParse(Unquote(parts[3]), out int deltaY)
            || !int.TryParse(Unquote(parts[4]), out int facing))
        {
            return false;
        }

        Farmer? player = Game1.player;
        if (player is null)
        {
            currentEvent.CurrentCommand++;
            return true;
        }

        bool continueImmediately = parts.Count >= 6 && bool.TryParse(Unquote(parts[5]), out bool parsedContinue) && parsedContinue;
        Vector2 start = player.Position;
        Vector2 target = start + new Vector2(deltaX * Game1.tileSize, deltaY * Game1.tileSize);
        facing = NormalizeFacingDirection(facing);
        player.faceDirection(facing);

        if (continueImmediately || start == target)
        {
            player.Position = target;
            player.Halt();
            currentEvent.CurrentCommand++;
            this.ResetPreviewFarmerMove();
            this.LogPreviewMessage($"handled preview farmer move raw='{rawCommand}' immediately target={target.X:0},{target.Y:0} state={this.FormatPreviewState()}");
            return true;
        }

        this.previewFarmerMoveCommandIndex = currentEvent.CurrentCommand;
        this.previewFarmerMoveStart = start;
        this.previewFarmerMoveTarget = target;
        this.previewFarmerMoveElapsedMs = 0f;
        this.previewFarmerMoveDurationMs = Math.Max(1f, Vector2.Distance(start, target) / PreviewFarmerMovePixelsPerSecond * 1000f);
        this.previewFarmerMoveFacing = facing;
        this.LogPreviewMessage($"started preview farmer move raw='{rawCommand}' from={start.X:0},{start.Y:0} to={target.X:0},{target.Y:0} durationMs={this.previewFarmerMoveDurationMs:0.##} state={this.FormatPreviewState()}");
        return true;
    }

    private bool UpdatePreviewFarmerMove(Event currentEvent, GameTime time)
    {
        if (this.previewFarmerMoveCommandIndex < 0)
        {
            return false;
        }

        Farmer? player = Game1.player;
        if (player is null || currentEvent.CurrentCommand != this.previewFarmerMoveCommandIndex)
        {
            this.ResetPreviewFarmerMove();
            return false;
        }

        this.previewFarmerMoveElapsedMs += Math.Max(1f, (float)time.ElapsedGameTime.TotalMilliseconds);
        float progress = Math.Clamp(this.previewFarmerMoveElapsedMs / Math.Max(1f, this.previewFarmerMoveDurationMs), 0f, 1f);
        player.faceDirection(this.previewFarmerMoveFacing);
        player.Position = Vector2.Lerp(this.previewFarmerMoveStart, this.previewFarmerMoveTarget, progress);
        this.UpdatePreviewFarmerMovementAnimation(time);

        if (progress >= 1f)
        {
            player.Position = this.previewFarmerMoveTarget;
            player.Halt();
            currentEvent.CurrentCommand++;
            this.ResetPreviewFarmerMove();
            this.yieldPlaybackFrame = true;
        }

        Game1.pauseTime = 0f;
        this.UpdateEventActorEmotes(currentEvent, time);
        this.UpdatePreviewFades(currentEvent, time);
        return true;
    }

    private bool HandlePreviewChangeLocation(Event currentEvent, List<string> parts, string rawCommand)
    {
        string locationName = Unquote(parts[1]);
        this.LogPreviewMessage($"handling changeLocation raw='{rawCommand}' before={this.FormatPreviewState()}");

        // Extract the pan flag (parts[2] = "true") or detect it from a backward-compat
        // trailing " true" absorbed into the location name by the old importer
        bool shouldPan = parts.Count >= 3
            && bool.TryParse(Unquote(parts[2]), out bool parsedPan)
            && parsedPan;

        try
        {
            LocationLoadResult loadResult = LocationBootstrapper.LoadDetailed(locationName);

            // Backward compatibility: old imports absorbed " true" into the location
            // name (e.g. "Town true") because the changeLocation definition was missing
            // the pan parameter. Strip and retry.
            if (!loadResult.Loaded
                && locationName.EndsWith(" true", StringComparison.OrdinalIgnoreCase)
                && locationName.Length > 5)
            {
                string strippedName = locationName[..^5];
                shouldPan = true;
                loadResult = LocationBootstrapper.LoadDetailed(strippedName);
                if (loadResult.Loaded)
                {
                    locationName = strippedName;
                }
            }

            if (loadResult.Location is null)
            {
                ModEntry.Instance.Monitor.Log($"Cutscene Maker preview could not handle '{rawCommand}': {loadResult.FailureReason}", StardewModdingAPI.LogLevel.Warn);
                return false;
            }

            Event runningEvent = Game1.currentLocation?.currentEvent ?? currentEvent;
            GameLocation? previousLocation = Game1.currentLocation;
            if (previousLocation is not null && !ReferenceEquals(previousLocation, loadResult.Location))
            {
                previousLocation.cleanupBeforePlayerExit();
                previousLocation.currentEvent = null;
            }

            Game1.currentLightSources.Clear();
            Game1.currentLocation = loadResult.Location;
            loadResult.Location.resetForPlayerEntry();
            loadResult.Location.UpdateMapSeats();
            loadResult.Location.currentEvent = runningEvent;
            loadResult.Location.ResetForEvent(runningEvent);
            // Clear the temporary location field since we're leaving a temp map
            EventTemporaryLocationField?.SetValue(runningEvent, null);

            if (Game1.player is not null)
            {
                Game1.player.currentLocation = loadResult.Location;
            }

            runningEvent.farmer.currentLocation = loadResult.Location;
            runningEvent.CurrentCommand++;

            // Restore original characters if this is a location we previously saved
            // before a temp map's cleanup destroyed them
            if (this.playbackOriginalLocationCharacters.TryGetValue(locationName, out List<NPC>? savedCharacters))
            {
                loadResult.Location.characters.Clear();
                foreach (NPC character in savedCharacters)
                {
                    loadResult.Location.characters.Add(character);
                }
            }

            if (shouldPan)
            {
                Game1.panScreen(0, 0);
            }

            this.state.BootstrappedMap = loadResult.Location;
            this.playbackLocations.Add(loadResult.Location);

            this.LogPreviewMessage($"handled changeLocation raw='{rawCommand}' after={this.FormatPreviewState()} tilesheets={FormatTileSheets(loadResult.Location)}");
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker preview could not complete '{rawCommand}' for location '{locationName}': {ex.Message}", StardewModdingAPI.LogLevel.Warn);
            return false;
        }
    }

    private bool HandlePreviewChangeToTemporaryMap(Event currentEvent, List<string> parts, string rawCommand)
    {
        string mapName = Unquote(parts[1]);
        bool shouldPan = true;
        if (parts.Count >= 3 && bool.TryParse(Unquote(parts[2]), out bool parsedShouldPan))
        {
            shouldPan = parsedShouldPan;
        }

        string previewMapPath = LocationBootstrapper.ResolvePreviewMapPathForMapName(mapName);
        GameLocation? temporaryLocation;
        try
        {
            temporaryLocation = mapName.Equals("Town", StringComparison.OrdinalIgnoreCase)
                ? new Town(previewMapPath, "Temp")
                : new GameLocation(previewMapPath, "Temp");
        }
        catch
        {
            // File path failed — try content pipeline. When there's a preview override
            // (previewMapPath is our own asset namespace like Mods/Kree.CutsceneMaker/PreviewMaps/...),
            // try it first — no Content Patcher conflict since it's our own asset.
            // Otherwise fall back to the standard Maps/ path.
            Map? loadedMap = null;
            if (!previewMapPath.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    loadedMap = Game1.content.Load<Map>(previewMapPath);
                }
                catch
                {
                    // Fall through to Maps/ path below
                }
            }

            if (loadedMap is null)
            {
                try
                {
                    loadedMap = Game1.content.Load<Map>("Maps/" + mapName);
                }
                catch
                {
                    // Both paths failed — report and bail
                }
            }

            if (loadedMap is not null)
            {
                temporaryLocation = new GameLocation("Maps/Farm", "Temp");
                temporaryLocation.map = loadedMap;
                temporaryLocation.name.Value = "Temp";
                ModEntry.Instance.Monitor.Log($"Cutscene Maker loaded map '{mapName}' via content pipeline.", StardewModdingAPI.LogLevel.Trace);
            }
            else
            {
                ModEntry.Instance.Monitor.Log($"Cutscene Maker preview could not load map '{mapName}' via file '{previewMapPath}' or content pipeline.", StardewModdingAPI.LogLevel.Warn);
                return false;
            }
        }

        // Load tile sheets. If some tile sheets fail (e.g. CP-provided textures only
        // available through the content pipeline), skip them rather than blocking playback.
        try
        {
            temporaryLocation.map.LoadTileSheets(Game1.mapDisplayDevice);
        }
        catch
        {
            // Map will render with partial or missing tile textures in preview
        }

        this.LogPreviewMessage($"handling changeToTemporaryMap raw='{rawCommand}' resolved='{previewMapPath}' before={this.FormatPreviewState()}");
        try
        {
            Event runningEvent = Game1.currentLocation?.currentEvent ?? currentEvent;
            GameLocation? previousLocation = Game1.currentLocation;
            if (previousLocation is not null && previousLocation.NameOrUniqueName != "Temp")
            {
                // Save original location characters before cleanup so we can restore
                // them when changeLocation brings us back (Town → temp map → Town).
                if (!this.playbackOriginalLocationCharacters.ContainsKey(previousLocation.NameOrUniqueName))
                {
                    this.playbackOriginalLocationCharacters[previousLocation.NameOrUniqueName] = previousLocation.characters.ToList();
                }
            }

            previousLocation?.cleanupBeforePlayerExit();
            if (previousLocation is not null)
            {
                previousLocation.currentEvent = null;
            }

            Game1.currentLightSources.Clear();
            Game1.currentLocation = temporaryLocation;
            temporaryLocation.resetForPlayerEntry();
            temporaryLocation.UpdateMapSeats();
            temporaryLocation.currentEvent = runningEvent;
            EventTemporaryLocationField?.SetValue(runningEvent, temporaryLocation);
            runningEvent.CurrentCommand++;

            if (Game1.player is not null)
            {
                Game1.player.currentLocation = temporaryLocation;
            }

            runningEvent.farmer.currentLocation = temporaryLocation;
            temporaryLocation.ResetForEvent(runningEvent);
            if (shouldPan)
            {
                Game1.panScreen(0, 0);
            }

            this.state.BootstrappedMap = temporaryLocation;
            this.playbackLocations.Add(temporaryLocation);
            this.LogPreviewMessage($"handled changeToTemporaryMap map='{mapName}' resolved='{previewMapPath}' location={this.FormatLocation(temporaryLocation)} state={this.FormatPreviewState()}");
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker preview could not complete '{rawCommand}' for map '{mapName}': {ex.Message}", StardewModdingAPI.LogLevel.Warn);
            return false;
        }
    }

    private Event SyncPlaybackLocationAfterUpdate(GameLocation previousLocation, Event previousEvent)
    {
        GameLocation? currentLocation = Game1.currentLocation;
        if (currentLocation is null || ReferenceEquals(currentLocation, previousLocation))
        {
            return previousEvent;
        }

        Event? currentEvent = currentLocation.currentEvent;
        if (currentEvent is null)
        {
            currentLocation.currentEvent = previousEvent;
            currentEvent = previousEvent;
        }

        previousLocation.currentEvent = null;
        this.state.BootstrappedMap = currentLocation;
        this.playbackLocations.Add(currentLocation);

        if (Game1.player is not null)
        {
            Game1.player.currentLocation = currentLocation;
        }

        return currentEvent;
    }

    private bool UpdatePreviewPause(Event currentEvent, GameTime time)
    {
        if (this.previewPauseRemainingMs <= 0f)
        {
            return false;
        }

        this.previewPauseRemainingMs -= time.ElapsedGameTime.Milliseconds;
        if (this.previewPauseRemainingMs <= 0f)
        {
            this.previewPauseRemainingMs = 0f;
            if (this.previewPauseCommandIndex == currentEvent.CurrentCommand)
            {
                currentEvent.CurrentCommand++;
            }

            this.previewPauseCommandIndex = -1;
        }

        Game1.pauseTime = 0f;
        Game1.player?.updateEmote(time);
        this.UpdateEventActorEmotes(currentEvent, time);
        this.UpdatePreviewFarmerMovementAnimation(time);
        this.UpdatePreviewFades(currentEvent, time);
        this.ContainPreviewGameState();
        return true;
    }

    private void CapturePreviewPause(Event currentEvent)
    {
        if (Game1.pauseTime <= 0f)
        {
            return;
        }

        this.previewPauseRemainingMs = Game1.pauseTime;
        this.previewPauseCommandIndex = currentEvent.CurrentCommand;
        Game1.pauseTime = 0f;
        this.LogPreviewMessage($"captured pause command={currentEvent.CurrentCommand + 1}/{currentEvent.eventCommands.Length} durationMs={this.previewPauseRemainingMs:0.##} state={this.FormatPreviewState()}");
    }

    private static string? GetCurrentEventCommand(Event currentEvent)
    {
        string[]? commands = currentEvent.eventCommands;
        int commandIndex = currentEvent.CurrentCommand;
        return commands is not null && commandIndex >= 0 && commandIndex < commands.Length
            ? commands[commandIndex]
            : null;
    }

    private static string Unquote(string value)
    {
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal)
            : value;
    }

    private static int NormalizeFacingDirection(int facing)
    {
        return facing is >= 0 and <= 3 ? facing : 2;
    }

    private void ResetPreviewFarmerMove()
    {
        this.previewFarmerMoveCommandIndex = -1;
        this.previewFarmerMoveStart = Vector2.Zero;
        this.previewFarmerMoveTarget = Vector2.Zero;
        this.previewFarmerMoveElapsedMs = 0f;
        this.previewFarmerMoveDurationMs = 0f;
        this.previewFarmerMoveFacing = 2;
    }

    private PlayerStateSnapshot? CapturePlayerState()
    {
        Farmer? player = Game1.player;
        if (player is null)
        {
            return null;
        }

        var friendshipData = new Dictionary<string, Friendship>();
        if (player.friendshipData is not null)
        {
            foreach (string key in player.friendshipData.Keys)
            {
                friendshipData[key] = player.friendshipData[key];
            }
        }

        var activeDialogueEvents = new Dictionary<string, int>();
        if (player.activeDialogueEvents is not null)
        {
            foreach (string key in player.activeDialogueEvents.Keys)
            {
                activeDialogueEvents[key] = player.activeDialogueEvents[key];
            }
        }

        var cookingRecipes = new Dictionary<string, int>();
        if (player.cookingRecipes is not null)
        {
            foreach (string key in player.cookingRecipes.Keys)
            {
                cookingRecipes[key] = player.cookingRecipes[key];
            }
        }

        var craftingRecipes = new Dictionary<string, int>();
        if (player.craftingRecipes is not null)
        {
            foreach (string key in player.craftingRecipes.Keys)
            {
                craftingRecipes[key] = player.craftingRecipes[key];
            }
        }

        return new PlayerStateSnapshot(
            Money: player.Money,
            Stamina: player.Stamina,
            Items: player.Items?.ToList() ?? new List<Item?>(),
            FriendshipData: friendshipData,
            MailReceived: player.mailReceived is not null
                ? new HashSet<string>(player.mailReceived)
                : new HashSet<string>(),
            MailForTomorrow: player.mailForTomorrow?.ToList() ?? new List<string>(),
            QuestLog: player.questLog?.ToList() ?? new List<Quest>(),
            ActiveDialogueEvents: activeDialogueEvents,
            EventsSeen: player.eventsSeen is not null
                ? new HashSet<string>(player.eventsSeen)
                : new HashSet<string>(),
            CookingRecipes: cookingRecipes,
            CraftingRecipes: craftingRecipes
        );
    }

    private void RestorePlayerState(PlayerStateSnapshot? snapshot)
    {
        Farmer? player = Game1.player;
        if (player is null || snapshot is null)
        {
            return;
        }

        player.Money = snapshot.Money;
        player.Stamina = snapshot.Stamina;

        player.Items.Clear();
        foreach (Item? item in snapshot.Items)
        {
            player.Items.Add(item);
        }

        player.friendshipData.Clear();
        foreach (KeyValuePair<string, Friendship> kvp in snapshot.FriendshipData)
        {
            player.friendshipData[kvp.Key] = kvp.Value;
        }

        player.mailReceived.Clear();
        foreach (string mailId in snapshot.MailReceived)
        {
            player.mailReceived.Add(mailId);
        }

        player.mailForTomorrow.Clear();
        foreach (string mailId in snapshot.MailForTomorrow)
        {
            player.mailForTomorrow.Add(mailId);
        }

        player.questLog.Clear();
        foreach (Quest quest in snapshot.QuestLog)
        {
            player.questLog.Add(quest);
        }

        player.activeDialogueEvents.Clear();
        foreach (KeyValuePair<string, int> kvp in snapshot.ActiveDialogueEvents)
        {
            player.activeDialogueEvents[kvp.Key] = kvp.Value;
        }

        player.eventsSeen.Clear();
        foreach (string eventId in snapshot.EventsSeen)
        {
            player.eventsSeen.Add(eventId);
        }

        player.cookingRecipes.Clear();
        foreach (KeyValuePair<string, int> kvp in snapshot.CookingRecipes)
        {
            player.cookingRecipes[kvp.Key] = kvp.Value;
        }

        player.craftingRecipes.Clear();
        foreach (KeyValuePair<string, int> kvp in snapshot.CraftingRecipes)
        {
            player.craftingRecipes[kvp.Key] = kvp.Value;
        }
    }

    private Point GetPreviewPlayerTile()
    {
        if (this.state.Cutscene.IncludeFarmer)
        {
            return new Point(this.state.Cutscene.FarmerPlacement.TileX, this.state.Cutscene.FarmerPlacement.TileY);
        }

        if (this.state.Cutscene.ViewportStartX >= 0 && this.state.Cutscene.ViewportStartY >= 0)
        {
            return new Point(this.state.Cutscene.ViewportStartX, this.state.Cutscene.ViewportStartY);
        }

        NpcPlacement? actor = this.state.Cutscene.Actors.FirstOrDefault();
        return actor is not null
            ? new Point(actor.TileX, actor.TileY)
            : Point.Zero;
    }

    private xTile.Dimensions.Rectangle GetInitialPlaybackViewport(GameLocation location, Point fallbackTile)
    {
        // The event preview renders within the map panel, not the full game window.
        // GetDrawViewport overrides the viewport W/H to panel content dimensions
        // during rendering, so centering must use panel dimensions.
        int vpW = Math.Max(1, this.mapViewPanel.Bounds.Width - 32);
        int vpH = Math.Max(1, this.mapViewPanel.Bounds.Height - 68);

        if (this.state.CommandMarkerIndex >= 0)
        {
            // Path A: search backwards from marker for the closest viewport command
            for (int i = this.state.CommandMarkerIndex; i >= 0; i--)
            {
                if (this.state.Cutscene.Commands[i] is EventCommandBlock cmd
                    && cmd.CommandId.Equals("vanilla.viewport", StringComparison.Ordinal))
                {
                    xTile.Dimensions.Rectangle? result = this.ComputeViewportFromCommand(cmd, location, vpW, vpH);
                    if (result.HasValue)
                    {
                        return result.Value;
                    }
                }
            }

            // Path B: no viewport command found to the left — re-center editor viewport
            int panelW = Math.Max(1, this.mapViewPanel.Bounds.Width - 32);
            int panelH = Math.Max(1, this.mapViewPanel.Bounds.Height - 68);
            int centerX = Game1.viewport.X + panelW / 2;
            int centerY = Game1.viewport.Y + panelH / 2;
            int vx = centerX - vpW / 2;
            int vy = centerY - vpH / 2;

            int clampMaxX = Math.Max(0, location.Map.DisplayWidth - vpW);
            int clampMaxY = Math.Max(0, location.Map.DisplayHeight - vpH);
            return new xTile.Dimensions.Rectangle(
                Math.Clamp(vx, 0, clampMaxX),
                Math.Clamp(vy, 0, clampMaxY),
                vpW, vpH
            );
        }

        // Path C: no marker — use cutscene header or fallback tile
        int tileX = this.state.Cutscene.ViewportStartX >= 0 ? this.state.Cutscene.ViewportStartX : fallbackTile.X;
        int tileY = this.state.Cutscene.ViewportStartY >= 0 ? this.state.Cutscene.ViewportStartY : fallbackTile.Y;
        int px = Math.Max(0, tileX * Game1.tileSize);
        int py = Math.Max(0, tileY * Game1.tileSize);
        int maxX = Math.Max(0, location.Map.DisplayWidth - vpW);
        int maxY = Math.Max(0, location.Map.DisplayHeight - vpH);
        return new xTile.Dimensions.Rectangle(
            Math.Clamp(px, 0, maxX),
            Math.Clamp(py, 0, maxY),
            vpW, vpH
        );
    }

    private xTile.Dimensions.Rectangle? ComputeViewportFromCommand(EventCommandBlock cmd, GameLocation location, int vpW, int vpH)
    {
        string raw = cmd.Values.GetValueOrDefault("target", string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        int cx = -1, cy = -1;

        // Try numeric tile coordinates first (e.g., "viewport 50 50")
        if (parts.Length >= 2
            && int.TryParse(Unquote(parts[0]), out int tileX)
            && int.TryParse(Unquote(parts[1]), out int tileY))
        {
            cx = tileX * Game1.tileSize + Game1.tileSize / 2;
            cy = tileY * Game1.tileSize + Game1.tileSize / 2;
        }
        else
        {
            // Named actor target — use GetSimulatedActorTile for fallback chain
            Point? tilePos = this.GetSimulatedActorTile(parts[0]);
            if (tilePos.HasValue)
            {
                cx = tilePos.Value.X * Game1.tileSize + Game1.tileSize / 2;
                cy = tilePos.Value.Y * Game1.tileSize + Game1.tileSize / 2;
            }
        }

        if (cx < 0 || cy < 0)
        {
            return null;
        }

        int vx = cx - vpW / 2;
        int vy = cy - vpH / 2;
        int maxX = Math.Max(0, location.Map.DisplayWidth - vpW);
        int maxY = Math.Max(0, location.Map.DisplayHeight - vpH);
        return new xTile.Dimensions.Rectangle(
            Math.Clamp(vx, 0, maxX),
            Math.Clamp(vy, 0, maxY),
            vpW, vpH
        );
    }

    private void StopPlayback(string? statusMessage = null)
    {
        if (this.state.Mode != EditorMode.Play && !this.playbackBootstrapActive)
        {
            return;
        }

        foreach (GameLocation location in this.playbackLocations)
        {
            location.currentEvent = null;
            location.characters.Clear();
        }

        this.playbackLocations.Clear();
        this.playbackOriginalLocationCharacters.Clear();
        if (ReferenceEquals(ActivePlaybackMenu, this))
        {
            ActivePlaybackMenu = null;
        }

        this.playbackEvent = null;
        this.playbackPlayer = null;
        this.playbackMenu = null;
        this.playbackActorCache = null;
        this.yieldPlaybackFrame = false;
        this.previewPauseRemainingMs = 0f;
        this.previewPauseCommandIndex = -1;
                this.ResetPreviewFarmerMove();
        this.lastLoggedPreviewCommandIndex = -1;
        this.lastLoggedPlaybackMenuType = string.Empty;
        Game1.dialogueUp = false;
        Game1.dialogueTyping = false;
        Game1.eventUp = this.previousEventUp;
        Game1.eventOver = false;
        Game1.pauseTime = 0f;
        Game1.newDay = false;
        Game1.quit = false;
        Game1.currentMinigame = null;
        Game1.gameMode = this.previousGameMode;
        Game1.bgColor = this.previousBgColor;
        Game1.ambientLight = this.previousAmbientLight;
        Game1.viewport = this.previousViewport;
        Game1.globalFade = this.previousGlobalFade;
        Game1.fadeIn = this.previousFadeIn;
        Game1.fadeToBlack = this.previousFadeToBlack;
        Game1.nonWarpFade = this.previousNonWarpFade;
        Game1.fadeToBlackAlpha = this.previousFadeToBlackAlpha;
        Game1.globalFadeSpeed = this.previousGlobalFadeSpeed;
        this.state.Mode = EditorMode.Edit;
        this.state.PlaybackCommandIndex = -1;
        this.stuckCommandIndex = -1;
        this.stuckFrameCount = 0;

        // Restore BootstrappedMap so the editor renders the original map after a failed preview
        if (this.previousBootstrappedMap is not null)
        {
            this.LoadPreviewLocation(this.previousBootstrappedMap);
        }

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
        this.previousGameMode = 0;
        this.previousBgColor = default;
        this.previousAmbientLight = default;
        this.previousLocation = null;
        this.previousEventUp = false;
        this.previousGlobalFade = false;
        this.previousFadeIn = false;
        this.previousFadeToBlack = false;
        this.previousNonWarpFade = false;
        this.previousFadeToBlackAlpha = 0f;
        this.previousGlobalFadeSpeed = 0f;
        this.previousViewport = default;

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            this.toolbarStatusMessage = statusMessage;
        }
    }

    private void FastTrack()
    {
        int nextIndex = this.state.CommandMarkerIndex + 1;

        if (nextIndex >= this.state.Cutscene.Commands.Count)
        {
            this.toolbarStatusMessage = "Already at the end of the cutscene.";
            this.toolbarStatusColor = Color.Red;
            return;
        }

        object command = this.state.Cutscene.Commands[nextIndex];
        this.SimulateCommandEffect(command);
        this.state.CommandMarkerIndex = nextIndex;

        // Pan viewport to center on the command's actor during fast-track
        if (this.state.BootstrappedMap is not null)
        {
            this.CenterViewportOnCommandMarker();
        }

        this.toolbarStatusColor = Color.DarkGreen;
    }

    private void BackTrack()
    {
        if (this.state.CommandMarkerIndex < 0)
        {
            this.toolbarStatusMessage = "Already at the setup block.";
            this.toolbarStatusColor = Color.Red;
            return;
        }

        this.state.CommandMarkerIndex--;

        // Re-simulate all actor positions from scratch up to the new marker
        this.state.SimulatedActorPositions.Clear();
        this.state.SimulatedViewportCenterX = -1;
        this.state.SimulatedViewportCenterY = -1;
        this.state.SimulatedViewportX = -1;
        this.state.SimulatedViewportY = -1;
        for (int i = 0; i <= this.state.CommandMarkerIndex; i++)
        {
            this.SimulateCommandEffect(this.state.Cutscene.Commands[i]);
        }

        if (this.state.BootstrappedMap is not null)
        {
            this.CenterViewportOnCommandMarker();
        }

        this.toolbarStatusMessage = this.state.CommandMarkerIndex < 0
            ? "Back to setup block."
            : $"Back to command {this.state.CommandMarkerIndex + 1}.";
        this.toolbarStatusColor = Color.DarkGreen;
    }

    private void CenterViewportOnCommandMarker()
    {
        if (this.state.CommandMarkerIndex < 0 || this.state.BootstrappedMap is null)
        {
            return;
        }

        object command = this.state.Cutscene.Commands[this.state.CommandMarkerIndex];
        if (command is not EventCommandBlock eventCommand)
        {
            return;
        }

        string actorName = this.ResolveActorNameForSimulation(eventCommand);
        if (string.IsNullOrEmpty(actorName))
        {
            return;
        }

        Point? tilePos = this.GetSimulatedActorTile(actorName);
        if (!tilePos.HasValue)
        {
            return;
        }

        int vpW = Math.Max(1, this.mapViewPanel.Bounds.Width - 32);
        int vpH = Math.Max(1, this.mapViewPanel.Bounds.Height - 68);
        int tileCenterX = tilePos.Value.X * Game1.tileSize + Game1.tileSize / 2;
        int tileCenterY = tilePos.Value.Y * Game1.tileSize + Game1.tileSize / 2;
        int vpX = tileCenterX - vpW / 2;
        int vpY = tileCenterY - vpH / 2;
        int maxX = Math.Max(0, this.state.BootstrappedMap.Map.DisplayWidth - vpW);
        int maxY = Math.Max(0, this.state.BootstrappedMap.Map.DisplayHeight - vpH);
        Game1.viewport = new xTile.Dimensions.Rectangle(
            Math.Clamp(vpX, 0, maxX),
            Math.Clamp(vpY, 0, maxY),
            vpW, vpH);
    }

    private void SimulateCommandEffect(object command)
    {
        if (command is not EventCommandBlock eventCommand)
        {
            return;
        }

        // Handle viewport commands first — they don't use the standard actor resolution
        if (eventCommand.CommandId.Equals("vanilla.viewport", StringComparison.Ordinal))
        {
            this.SimulateViewportEffect(eventCommand);
            return;
        }

        string actorName = this.ResolveActorNameForSimulation(eventCommand);
        if (string.IsNullOrEmpty(actorName))
        {
            return;
        }

        switch (eventCommand.CommandId)
        {
            case "vanilla.move":
            {
                string tx = eventCommand.Values.GetValueOrDefault("targetX", string.Empty);
                string ty = eventCommand.Values.GetValueOrDefault("targetY", string.Empty);
                if (int.TryParse(tx, out int targetX) && int.TryParse(ty, out int targetY))
                {
                    this.state.SimulatedActorPositions[actorName] = new Point(targetX, targetY);
                }

                break;
            }

            case "vanilla.warp":
            {
                string wx = eventCommand.Values.GetValueOrDefault("x", string.Empty);
                string wy = eventCommand.Values.GetValueOrDefault("y", string.Empty);
                if (int.TryParse(wx, out int warpX) && int.TryParse(wy, out int warpY))
                {
                    this.state.SimulatedActorPositions[actorName] = new Point(warpX, warpY);
                }

                break;
            }
        }
    }

    private void SimulateViewportEffect(EventCommandBlock eventCommand)
    {
        string raw = eventCommand.Values.GetValueOrDefault("target", string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        string[] vpParts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (vpParts.Length == 0)
        {
            return;
        }

        string vpTarget = vpParts[0];

        int cx = -1, cy = -1;

        if (vpTarget.Equals("farmer", StringComparison.OrdinalIgnoreCase))
        {
            Point? farmerPos = this.GetSimulatedActorTile("farmer");
            if (farmerPos.HasValue)
            {
                cx = farmerPos.Value.X * Game1.tileSize + Game1.tileSize / 2;
                cy = farmerPos.Value.Y * Game1.tileSize + Game1.tileSize / 2;
            }
        }
        else if (vpParts.Length >= 2
                 && int.TryParse(Unquote(vpParts[0]), out int vx)
                 && int.TryParse(Unquote(vpParts[1]), out int vy))
        {
            cx = vx * Game1.tileSize + Game1.tileSize / 2;
            cy = vy * Game1.tileSize + Game1.tileSize / 2;
        }
        else
        {
            Point? targetPos = this.GetSimulatedActorTile(vpTarget);
            if (targetPos.HasValue)
            {
                cx = targetPos.Value.X * Game1.tileSize + Game1.tileSize / 2;
                cy = targetPos.Value.Y * Game1.tileSize + Game1.tileSize / 2;
            }
        }

        if (cx >= 0 && cy >= 0)
        {
            // Store the target center so GetInitialPlaybackViewport can compute
            // the correct viewport position for the current game viewport dimensions
            this.state.SimulatedViewportCenterX = cx;
            this.state.SimulatedViewportCenterY = cy;

            // Also compute panel-centered position for the editor viewport preview
            int panelW = Math.Max(1, this.mapViewPanel.Bounds.Width - 32);
            int panelH = Math.Max(1, this.mapViewPanel.Bounds.Height - 68);
            int vpX = cx - panelW / 2;
            int vpY = cy - panelH / 2;

            if (this.state.BootstrappedMap is not null)
            {
                int maxX = Math.Max(0, this.state.BootstrappedMap.Map.DisplayWidth - Game1.viewport.Width);
                int maxY = Math.Max(0, this.state.BootstrappedMap.Map.DisplayHeight - Game1.viewport.Height);
                this.state.SimulatedViewportX = Math.Clamp(vpX, 0, maxX);
                this.state.SimulatedViewportY = Math.Clamp(vpY, 0, maxY);
            }
            else
            {
                this.state.SimulatedViewportX = vpX;
                this.state.SimulatedViewportY = vpY;
            }
        }
        else
        {
            this.state.SimulatedViewportCenterX = -1;
            this.state.SimulatedViewportCenterY = -1;
        }
    }

    private string ResolveActorNameForSimulation(EventCommandBlock command)
    {
        string? slotId = command.ActorSlotIds.GetValueOrDefault("actor");
        if (!string.IsNullOrWhiteSpace(slotId))
        {
            if (slotId.Equals(this.state.Cutscene.FarmerPlacement.ActorSlotId, StringComparison.Ordinal))
            {
                return "farmer";
            }

            NpcPlacement? actor = this.state.Cutscene.Actors.FirstOrDefault(a =>
                a.ActorSlotId.Equals(slotId, StringComparison.Ordinal));
            if (actor is not null && !string.IsNullOrWhiteSpace(actor.ActorName))
            {
                return actor.ActorName;
            }

            return string.Empty;
        }

        return command.Values.GetValueOrDefault("actor", string.Empty);
    }

    private Point? GetSimulatedActorTile(string actorName)
    {
        if (this.state.SimulatedActorPositions.TryGetValue(actorName, out Point simulated))
        {
            return simulated;
        }

        if (actorName.Equals("farmer", StringComparison.OrdinalIgnoreCase))
        {
            return new Point(this.state.Cutscene.FarmerPlacement.TileX, this.state.Cutscene.FarmerPlacement.TileY);
        }

        NpcPlacement? placement = this.state.Cutscene.Actors
            .FirstOrDefault(a => a.ActorName.Equals(actorName, StringComparison.OrdinalIgnoreCase));
        return placement is not null
            ? new Point(placement.TileX, placement.TileY)
            : null;
    }

    private void ResetMarker()
    {
        if (this.state.CommandMarkerIndex < 0)
        {
            this.toolbarStatusMessage = "No marker to reset.";
            this.toolbarStatusColor = Color.Red;
            return;
        }

        this.state.CommandMarkerIndex = -1;
        this.state.SimulatedActorPositions.Clear();
        this.state.SimulatedViewportCenterX = -1;
        this.state.SimulatedViewportCenterY = -1;
        this.state.SimulatedViewportX = -1;
        this.state.SimulatedViewportY = -1;
        this.toolbarStatusMessage = "Marker reset to setup.";
        this.toolbarStatusColor = Color.DarkGreen;
    }

    private void ToggleSimulateOnClick()
    {
        this.simulateOnClick = !this.simulateOnClick;
        this.toolbarStatusMessage = this.simulateOnClick ? "Simulate on click: On" : "Simulate on click: Off";
        this.toolbarStatusColor = Color.DarkGreen;
    }

    private void JumpToCommand(int commandIndex)
    {
        if (this.state.Mode != EditorMode.Edit || !this.simulateOnClick)
        {
            return;
        }

        this.state.CommandMarkerIndex = commandIndex;

        this.state.SimulatedActorPositions.Clear();
        this.state.SimulatedViewportCenterX = -1;
        this.state.SimulatedViewportCenterY = -1;
        this.state.SimulatedViewportX = -1;
        this.state.SimulatedViewportY = -1;

        for (int i = 0; i <= commandIndex; i++)
        {
            this.SimulateCommandEffect(this.state.Cutscene.Commands[i]);
        }

        if (this.state.BootstrappedMap is not null)
        {
            this.CenterViewportOnCommandMarker();
        }

        this.toolbarStatusMessage = commandIndex < 0
            ? "Jumped to setup block."
            : $"Jumped to command {commandIndex + 1}.";
        this.toolbarStatusColor = Color.DarkGreen;
    }

    private void UpdatePreviewFades(Event currentEvent, GameTime time)
    {
        this.UpdatePreviewGlobalFade(currentEvent, time);
        this.UpdatePreviewScreenFade(time);
    }

    private void UpdatePreviewGlobalFade(Event currentEvent, GameTime time)
    {
        if (!Game1.globalFade)
        {
            return;
        }

        float elapsedMs = Math.Max(1f, (float)time.ElapsedGameTime.TotalMilliseconds);
        float fadeStep = Math.Max(Game1.globalFadeSpeed, elapsedMs / PreviewFadeDurationMs);
        if (Game1.fadeIn)
        {
            Game1.fadeToBlackAlpha = Math.Max(0f, Game1.fadeToBlackAlpha - fadeStep);
            if (Game1.fadeToBlackAlpha <= 0f)
            {
                this.FinishPreviewGlobalFade(currentEvent);
                return;
            }

            return;
        }

        Game1.fadeToBlackAlpha = Math.Min(1f, Game1.fadeToBlackAlpha + fadeStep);
        if (Game1.fadeToBlackAlpha >= 1f)
        {
            this.FinishPreviewGlobalFade(currentEvent);
        }
    }

    private void FinishPreviewGlobalFade(Event currentEvent)
    {
        currentEvent.incrementCommandAfterFade();
        Game1.globalFade = false;
        if (Game1.nonWarpFade)
        {
            Game1.fadeToBlack = false;
        }
    }

    private void UpdatePreviewScreenFade(GameTime time)
    {
        if (!Game1.fadeToBlack)
        {
            return;
        }

        // The vanilla viewport command uses a non-warp fade as a short transition after moving the camera.
        // In the editor preview there is no full game fade update loop, so start clearing it as soon as it
        // reaches black or it can leave the map panel hidden behind a permanent black overlay.
        if (!Game1.globalFade && Game1.nonWarpFade && Game1.fadeIn && Game1.fadeToBlackAlpha >= 1f)
        {
            Game1.fadeIn = false;
            Game1.nonWarpFade = false;
        }

        float elapsedMs = Math.Max(1f, (float)time.ElapsedGameTime.TotalMilliseconds);
        float fadeStep = 0.0008f * elapsedMs;
        if (Game1.fadeIn)
        {
            Game1.fadeToBlackAlpha += fadeStep;
            if (Game1.fadeToBlackAlpha > 1.1f)
            {
                Game1.fadeToBlackAlpha = 1f;
                Game1.nonWarpFade = false;
                Game1.fadeIn = false;
            }

            return;
        }

        Game1.fadeToBlackAlpha -= fadeStep;
        if (Game1.fadeToBlackAlpha < -0.1f)
        {
            Game1.fadeToBlackAlpha = 0f;
            Game1.fadeToBlack = false;
            Game1.nonWarpFade = false;
        }
    }

    private static string GetEventCommandContext(Event? currentEvent)
    {
        if (currentEvent is null)
        {
            return "no active event";
        }

        string[]? commands = currentEvent.eventCommands;
        int commandIndex = currentEvent.CurrentCommand;
        if (commands is null || commands.Length == 0)
        {
            return $"command {commandIndex}: no command list";
        }

        if (commandIndex < 0 || commandIndex >= commands.Length)
        {
            return $"command {commandIndex + 1}/{commands.Length}: out of range";
        }

        return $"command {commandIndex + 1}/{commands.Length} '{commands[commandIndex]}'";
    }

    private void LogPreviewCommand(Event currentEvent)
    {
        int commandIndex = currentEvent.CurrentCommand;
        if (commandIndex == this.lastLoggedPreviewCommandIndex)
        {
            return;
        }

        this.lastLoggedPreviewCommandIndex = commandIndex;
        string command = GetCurrentEventCommand(currentEvent) ?? "<out of range>";
        this.LogPreviewMessage($"cmd {commandIndex + 1}/{currentEvent.eventCommands.Length}: {command} | {this.FormatPreviewState()}");
    }

    private void LogPreviewMessage(string message)
    {
#if DEBUG
        ModEntry.Instance.Monitor.Log($"[PreviewTrace] {message}", StardewModdingAPI.LogLevel.Info);
#endif
    }

    private string FormatPreviewState()
    {
        GameLocation? location = Game1.currentLocation;
        string menu = Game1.activeClickableMenu is null
            ? "null"
            : ReferenceEquals(Game1.activeClickableMenu, this)
                ? "CutsceneEditorMenu"
                : Game1.activeClickableMenu.GetType().Name;

        return string.Join(
            " ",
            $"loc={this.FormatLocation(location)}",
            $"viewport={FormatViewport(Game1.viewport)}",
            $"fade(global={Game1.globalFade},toBlack={Game1.fadeToBlack},in={Game1.fadeIn},nonWarp={Game1.nonWarpFade},alpha={Game1.fadeToBlackAlpha:0.###},speed={Game1.globalFadeSpeed:0.###})",
            $"bg={FormatColor(Game1.bgColor)}",
            $"ambient={FormatColor(Game1.ambientLight)}",
            $"eventUp={Game1.eventUp}",
            $"eventOver={Game1.eventOver}",
            $"dialogueUp={Game1.dialogueUp}",
            $"messagePause={Game1.messagePause}",
            $"pauseTime={Game1.pauseTime:0.##}",
            $"activeMenu={menu}",
            $"boot={this.FormatLocation(this.state.BootstrappedMap)}"
        );
    }

    private string FormatLocation(GameLocation? location)
    {
        if (location is null)
        {
            return "null";
        }

        int width = location.Map?.DisplayWidth ?? 0;
        int height = location.Map?.DisplayHeight ?? 0;
        return $"{location.NameOrUniqueName}({width}x{height})";
    }

    private static string FormatTileSheets(GameLocation location)
    {
        if (location.Map?.TileSheets is null || location.Map.TileSheets.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ";",
            location.Map.TileSheets
                .Cast<xTile.Tiles.TileSheet>()
                .Take(8)
                .Select(tileSheet => $"{tileSheet.Id}:{tileSheet.ImageSource}")
        );
    }

    private static string FormatViewport(xTile.Dimensions.Rectangle viewport)
    {
        return $"{viewport.X},{viewport.Y},{viewport.Width}x{viewport.Height}";
    }

    private static string FormatColor(Color color)
    {
        return $"{color.R},{color.G},{color.B},{color.A}";
    }

    private bool HasActivePlaybackEmote(Event currentEvent)
    {
        if (Game1.player?.IsEmoting == true)
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

    private void UpdateTextAboveHeadTimers(Event currentEvent, GameTime time)
    {
        int dt = time.ElapsedGameTime.Milliseconds;
        if (dt <= 0)
            return;

        foreach (NPC actor in currentEvent.actors)
        {
            int preTimer = (int)(NPCAboveHeadPreTimerField?.GetValue(actor) ?? 0);
            int timer = (int)(NPCAboveHeadTimerField?.GetValue(actor) ?? 0);
            string? text = NPCAboveHeadTextField?.GetValue(actor) as string;

            if (text is null || timer <= 0)
                continue;

            if (preTimer > 0)
            {
                preTimer -= dt;
                NPCAboveHeadPreTimerField?.SetValue(actor, preTimer < 0 ? 0 : preTimer);
            }
            else
            {
                timer -= dt;
                NPCAboveHeadTimerField?.SetValue(actor, timer < 0 ? 0 : timer);

                float alpha = (float)(NPCAboveHeadAlphaField?.GetValue(actor) ?? 0f);
                if (timer > 500)
                {
                    alpha = Math.Min(1f, alpha + 0.1f);
                }
                else
                {
                    alpha = Math.Max(0f, alpha - 0.03f);
                    if (alpha <= 0f)
                    {
                        NPCAboveHeadTimerField?.SetValue(actor, 0);
                        NPCAboveHeadTextField?.SetValue(actor, null);
                    }
                }
                NPCAboveHeadAlphaField?.SetValue(actor, alpha);
            }
        }
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

    private void CapturePlaybackMenu()
    {
        if (Game1.activeClickableMenu is not null && !ReferenceEquals(Game1.activeClickableMenu, this))
        {
            this.playbackMenu = Game1.activeClickableMenu;
            string menuType = this.playbackMenu.GetType().FullName ?? this.playbackMenu.GetType().Name;
            if (!menuType.Equals(this.lastLoggedPlaybackMenuType, StringComparison.Ordinal))
            {
                this.lastLoggedPlaybackMenuType = menuType;
                this.LogPreviewMessage($"captured menu type={menuType} state={this.FormatPreviewState()}");
            }
        }

        // Clear playbackMenu when the captured menu is no longer active (was dismissed).
        // For speak commands: DialogueBox.closeDialogue() sets Game1.dialogueUp = false,
        // which causes the event to re-show the dialogue on the next Update.  We restore
        // dialogueUp so the event detects the dismissal and advances naturally.
        // Choice/selection menus (makeSelect, question, etc.) use their own state and are
        // unaffected — the event processes their result regardless of dialogueUp.
        if (this.playbackMenu is not null
            && !ReferenceEquals(Game1.activeClickableMenu, this.playbackMenu)
            && (Game1.activeClickableMenu is null || ReferenceEquals(Game1.activeClickableMenu, this)))
        {
            if (!string.IsNullOrEmpty(this.lastLoggedPlaybackMenuType))
            {
                this.LogPreviewMessage($"cleared menu type={this.lastLoggedPlaybackMenuType} state={this.FormatPreviewState()}");
                this.lastLoggedPlaybackMenuType = string.Empty;
            }

            this.playbackMenu = null;
            // Speak commands show dialogue but don't advance CurrentCommand until the
            // dialogue is dismissed.  DialogueBox.closeDialogue() sets dialogueUp=false
            // and activeClickableMenu=null, which makes the event re-show the dialogue
            // on the next Update.  Since our capture/containment already consumed the
            // dismissal, we advance past the speak command ourselves.
            // Other blocking commands (makeSelect, question) manage their own advancement
            // and are left alone.
            Event? evt = this.state.BootstrappedMap?.currentEvent;
            if (evt is not null && evt.CurrentCommand < evt.eventCommands.Length)
            {
                string raw = evt.eventCommands[evt.CurrentCommand];
                string cmdWord = QuoteAwareSplit.Split(raw, ' ')
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .FirstOrDefault() ?? string.Empty;
                if (cmdWord.Equals("speak", StringComparison.OrdinalIgnoreCase)
                    || cmdWord.Equals("message", StringComparison.OrdinalIgnoreCase)
                    || cmdWord.Equals("end", StringComparison.OrdinalIgnoreCase)
                    || cmdWord.Equals("quickQuestion", StringComparison.OrdinalIgnoreCase))
                {
                    evt.CurrentCommand++;
                }
            }
        }

        if (this.state.Mode == EditorMode.Play)
        {
            Game1.activeClickableMenu = this;
            Game1.fadeToBlack = false;
        }
    }

    private void UpdatePlaybackDialogue(GameTime time)
    {
        if (this.state.Mode != EditorMode.Play || this.playbackMenu is null)
        {
            this.playbackMenu?.update(time);
            return;
        }

        this.RouteInputToPlaybackMenu(menu => menu.update(time));
    }

    private void RouteInputToPlaybackMenu(Action<IClickableMenu> action)
    {
        IClickableMenu? menu = this.playbackMenu;
        if (menu is null)
        {
            return;
        }

        Game1.activeClickableMenu = menu;
        action(menu);
        this.CapturePlaybackMenu();
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

        int nextX = this.xPositionOnScreen + 28 + (int)Math.Ceiling(Game1.smallFont.MeasureString(title).X) + 36;
        int buttonY = this.yPositionOnScreen + 16;

        int newW = (int)Game1.smallFont.MeasureString("New").X + 24;
        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, newW, 38), "New", this.NewCutscene);
        nextX += newW + 12;

        this.locationButtonBounds = new Rectangle(nextX, buttonY, 250, 38);
        this.DrawToolbarButton(spriteBatch, this.locationButtonBounds, "Location: " + this.TrimToolbarText(LocationBootstrapper.GetDisplayName(this.state.SelectedLocationId), 18), this.ToggleLocationDropdown);
        nextX += 262;

        string playLabel = this.state.Mode == EditorMode.Play ? "Stop" : "Play";
        int playW = (int)Game1.smallFont.MeasureString(playLabel).X + 24;
        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, playW, 38), playLabel, this.TogglePlayback);
        nextX += playW + 12;

        if (this.state.Mode == EditorMode.Edit)
        {
            int btW = (int)Game1.smallFont.MeasureString("Back-Track").X + 24;
            this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, btW, 38), "Back-Track", this.BackTrack);
            nextX += btW + 12;

            int ftW = (int)Game1.smallFont.MeasureString("Fast-Track").X + 24;
            this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, ftW, 38), "Fast-Track", this.FastTrack);
            nextX += ftW + 12;

            int resetW = (int)Game1.smallFont.MeasureString("Reset").X + 24;
            this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, resetW, 38), "Reset", this.ResetMarker);
            nextX += resetW + 12;
        }

        int importW = (int)Game1.smallFont.MeasureString("Import").X + 24;
        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, importW, 38), "Import", this.OpenImportDialog);
        nextX += importW + 12;

        int saveW = (int)Game1.smallFont.MeasureString("Save").X + 24;
        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, saveW, 38), "Save", this.OpenSaveDialog);
        nextX += saveW + 12;

        // Sim toggle on the far right, before the X close button
        string simLabel = this.simulateOnClick ? "Simulate on Click ON" : "Simulate on Click OFF";
        int simW = (int)Game1.smallFont.MeasureString(simLabel).X + 24;
        int simX = this.xPositionOnScreen + this.width - 56 - simW - 12;
        this.DrawToolbarButton(spriteBatch, new Rectangle(simX, buttonY, simW, 38), simLabel, this.ToggleSimulateOnClick);

        // Status message fills remaining space between Save and Sim toggle
        int statusEndX = simX - 8;
        if (!string.IsNullOrWhiteSpace(this.toolbarStatusMessage))
        {
            int maxWidth = statusEndX - nextX - 8;
            if (maxWidth > 0)
            {
                this.DrawWrappedText(spriteBatch, this.toolbarStatusMessage, new Vector2(nextX, this.yPositionOnScreen + 24), maxWidth, this.toolbarStatusColor);
            }
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

    private void DrawLocationDropdown(SpriteBatch spriteBatch)
    {
        this.locationDropdownRows.Clear();

        IReadOnlyList<LocationCatalogEntry> locations = this.GetFilteredLocations();
        if (LocationBootstrapper.SupportedLocationEntries.Count == 0)
        {
            return;
        }

        this.locationDropdownScrollIndex = Math.Clamp(this.locationDropdownScrollIndex, 0, this.GetMaxLocationScrollIndex());
        Rectangle dropdownBounds = this.GetLocationDropdownBounds();
        IClickableMenu.drawTextureBox(spriteBatch, dropdownBounds.X, dropdownBounds.Y, dropdownBounds.Width, dropdownBounds.Height, Color.White);

        Rectangle searchBounds = new(
            dropdownBounds.X + 12,
            dropdownBounds.Y + 12,
            dropdownBounds.Width - 24,
            LocationSearchHeight - 8
        );
        spriteBatch.Draw(Game1.staminaRect, searchBounds, Color.White * 0.45f);
        string searchLabel = string.IsNullOrEmpty(this.locationSearchText)
            ? "Type to search locations..."
            : this.locationSearchText;
        Utility.drawTextWithShadow(
            spriteBatch,
            this.TrimToolbarText(searchLabel, 30),
            Game1.smallFont,
            new Vector2(searchBounds.X + 8, searchBounds.Y + 5),
            string.IsNullOrEmpty(this.locationSearchText) ? Color.DimGray : Game1.textColor
        );

        if (locations.Count == 0)
        {
            Utility.drawTextWithShadow(
                spriteBatch,
                "No matches.",
                Game1.smallFont,
                new Vector2(dropdownBounds.X + 20, dropdownBounds.Y + LocationSearchHeight + 16),
                Color.DarkRed
            );
            return;
        }

        int visibleRows = Math.Min(LocationDropdownMaxRows, locations.Count);
        for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
        {
            int locationIndex = this.locationDropdownScrollIndex + rowIndex;
            if (locationIndex >= locations.Count)
            {
                break;
            }

            LocationCatalogEntry location = locations[locationIndex];
            Rectangle rowBounds = new(
                dropdownBounds.X + 12,
                dropdownBounds.Y + LocationSearchHeight + 12 + rowIndex * LocationDropdownRowHeight,
                dropdownBounds.Width - 24,
                LocationDropdownRowHeight
            );

            if (location.Id.Equals(this.state.SelectedLocationId, StringComparison.OrdinalIgnoreCase))
            {
                spriteBatch.Draw(Game1.staminaRect, rowBounds, Color.LightGoldenrodYellow * 0.7f);
            }

            Utility.drawTextWithShadow(
                spriteBatch,
                this.TrimToolbarText(location.DisplayName, 28),
                Game1.smallFont,
                new Vector2(rowBounds.X + 8, rowBounds.Y + 6),
                Game1.textColor
            );
            this.locationDropdownRows.Add((rowBounds, location));
        }

        if (locations.Count > LocationDropdownMaxRows)
        {
            string scrollText = $"{this.locationDropdownScrollIndex + 1}-{Math.Min(locations.Count, this.locationDropdownScrollIndex + LocationDropdownMaxRows)} / {locations.Count}";
            Vector2 size = Game1.smallFont.MeasureString(scrollText);
            Utility.drawTextWithShadow(
                spriteBatch,
                scrollText,
                Game1.smallFont,
                new Vector2(dropdownBounds.Right - size.X - 18, dropdownBounds.Bottom - 28),
                Color.DimGray
            );
        }
    }

    private Rectangle GetLocationDropdownBounds()
    {
        IReadOnlyList<LocationCatalogEntry> locations = this.GetFilteredLocations();
        int visibleRows = Math.Min(LocationDropdownMaxRows, Math.Max(1, locations.Count));
        int height = LocationSearchHeight + 24 + visibleRows * LocationDropdownRowHeight + (locations.Count > LocationDropdownMaxRows ? 28 : 0);
        return new Rectangle(this.locationButtonBounds.X, this.locationButtonBounds.Bottom + 4, 340, height);
    }

    private void DrawWrappedText(SpriteBatch spriteBatch, string text, Vector2 position, int maxWidth, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        List<string> lines = new();
        string currentLine = string.Empty;
        foreach (string word in text.Split(' '))
        {
            string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (Game1.smallFont.MeasureString(testLine).X > maxWidth)
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    lines.Add(word);
                    currentLine = string.Empty;
                }
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        float y = position.Y;
        foreach (string line in lines)
        {
            Utility.drawTextWithShadow(spriteBatch, line, Game1.smallFont, new Vector2(position.X, y), color);
            y += Game1.smallFont.LineSpacing;
        }
    }

    private string TrimToolbarText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }

    private sealed record PlayerStateSnapshot(
        int Money,
        float Stamina,
        List<Item?> Items,
        Dictionary<string, Friendship> FriendshipData,
        HashSet<string> MailReceived,
        List<string> MailForTomorrow,
        List<Quest> QuestLog,
        Dictionary<string, int> ActiveDialogueEvents,
        HashSet<string> EventsSeen,
        Dictionary<string, int> CookingRecipes,
        Dictionary<string, int> CraftingRecipes
    );

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
