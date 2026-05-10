using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CutsceneMaker.Compiler;
using CutsceneMaker.Importer;
using CutsceneMaker.Models;
using StardewValley;
using StardewValley.Locations;
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
    private const int LocationDropdownRowHeight = 34;
    private const int LocationDropdownMaxRows = 10;
    private const int LocationSearchHeight = 42;
    private const float PreviewFadeDurationMs = 500f;
    private const float PreviewFarmerMovePixelsPerSecond = 256f;
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
    private int lastLoggedPreviewCommandIndex = -1;
    private string lastLoggedPlaybackMenuType = string.Empty;
    private int previewFarmerMoveCommandIndex = -1;
    private Vector2 previewFarmerMoveStart = Vector2.Zero;
    private Vector2 previewFarmerMoveTarget = Vector2.Zero;
    private float previewFarmerMoveElapsedMs;
    private float previewFarmerMoveDurationMs;
    private int previewFarmerMoveFacing = 2;

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
            this.propertiesPanel.CommitSelectedTextFields();

            List<string> validationErrors = CutsceneValidator.Validate(this.state.Cutscene, ModEntry.Instance.CommandCatalog, ModEntry.Instance.PreconditionCatalog, forPreview: true);
            if (validationErrors.Count > 0)
            {
                this.toolbarStatusMessage = "Cannot preview: " + this.TrimToolbarText(validationErrors[0], 80);
                return;
            }

            GameLocation? location = this.state.BootstrappedMap ?? this.LoadPreviewLocation(this.state.SelectedLocationId).Location;
            if (location is null)
            {
                this.toolbarStatusMessage = $"Cannot play: {this.TrimToolbarText(this.state.MapLoadFailureMessage, 80)}";
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
            Game1.viewport = this.GetInitialPlaybackViewport(location, previewPlayerTile);
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
            ModEntry.Instance.SetImportedPreviewMapAssetRequestsEnabled(true);

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
            location.currentEvent = previewEvent;
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
            this.toolbarStatusMessage = "Preview playing.";
            ActivePlaybackMenu = this;
            this.LogPreviewMessage($"started scriptCommands={previewEvent.eventCommands.Length} location={this.FormatLocation(location)} viewport={FormatViewport(Game1.viewport)}");
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
            if (Game1.activeClickableMenu == this)
            {
                Game1.activeClickableMenu = null;
            }

            int commandBeforeUpdate = currentEvent.CurrentCommand;
            this.LogPreviewCommand(currentEvent);
            if (!this.TryHandlePreviewOnlyCommand(currentEvent))
            {
                currentEvent.Update(location, time);
            }

            currentEvent = this.SyncPlaybackLocationAfterUpdate(location, currentEvent);
            this.CapturePreviewPause(currentEvent);
            this.UpdatePreviewFarmerMovementAnimation(time);
            this.UpdatePreviewFades(currentEvent, time);
            this.CapturePlaybackMenu();

            // Save event-finished flag before ContainPreviewGameState clears Game1.eventOver
            bool eventFinished = currentEvent.CurrentCommand >= currentEvent.eventCommands.Length;

            this.ContainPreviewGameState();

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

        return false;
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
        LocationLoadResult loadResult = LocationBootstrapper.LoadDetailed(locationName);
        if (loadResult.Location is null)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker preview could not handle '{rawCommand}': {loadResult.FailureReason}", StardewModdingAPI.LogLevel.Warn);
            return false;
        }

        GameLocation? previousLocation = Game1.currentLocation;
        if (previousLocation is not null && !ReferenceEquals(previousLocation, loadResult.Location))
        {
            previousLocation.currentEvent = null;
        }

        Game1.currentLocation = loadResult.Location;
        loadResult.Location.currentEvent = currentEvent;
        loadResult.Location.ResetForEvent(currentEvent);
        this.state.BootstrappedMap = loadResult.Location;
        this.playbackLocations.Add(loadResult.Location);

        if (Game1.player is not null)
        {
            Game1.player.currentLocation = loadResult.Location;
        }

        currentEvent.farmer.currentLocation = loadResult.Location;
        currentEvent.CurrentCommand++;
        this.LogPreviewMessage($"handled changeLocation raw='{rawCommand}' after={this.FormatPreviewState()} tilesheets={FormatTileSheets(loadResult.Location)}");
        return true;
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
        try
        {
            this.LogPreviewMessage($"handling changeToTemporaryMap raw='{rawCommand}' resolved='{previewMapPath}' before={this.FormatPreviewState()}");
            GameLocation temporaryLocation = mapName.Equals("Town", StringComparison.OrdinalIgnoreCase)
                ? new Town(previewMapPath, "Temp")
                : new GameLocation(previewMapPath, "Temp");
            temporaryLocation.map.LoadTileSheets(Game1.mapDisplayDevice);

            Event runningEvent = Game1.currentLocation?.currentEvent ?? currentEvent;
            GameLocation? previousLocation = Game1.currentLocation;
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
            this.LogPreviewMessage($"handled changeToTemporaryMap map='{mapName}' resolved='{previewMapPath}' location={this.FormatLocation(temporaryLocation)} tilesheets={FormatTileSheets(temporaryLocation)} state={this.FormatPreviewState()}");
            return true;
        }
        catch (Exception ex)
        {
            ModEntry.Instance.Monitor.Log($"Cutscene Maker preview could not handle '{rawCommand}' via '{previewMapPath}': {ex.Message}", StardewModdingAPI.LogLevel.Warn);
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
        int viewportWidth = Math.Max(1, this.mapViewPanel.Bounds.Width - 32);
        int viewportHeight = Math.Max(1, this.mapViewPanel.Bounds.Height - 68);
        int tileX = this.state.Cutscene.ViewportStartX >= 0 ? this.state.Cutscene.ViewportStartX : fallbackTile.X;
        int tileY = this.state.Cutscene.ViewportStartY >= 0 ? this.state.Cutscene.ViewportStartY : fallbackTile.Y;

        int x = Math.Max(0, tileX * Game1.tileSize);
        int y = Math.Max(0, tileY * Game1.tileSize);
        int maxX = Math.Max(0, location.Map.DisplayWidth - viewportWidth);
        int maxY = Math.Max(0, location.Map.DisplayHeight - viewportHeight);
        return new xTile.Dimensions.Rectangle(
            Math.Clamp(x, 0, maxX),
            Math.Clamp(y, 0, maxY),
            viewportWidth,
            viewportHeight
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
        if (ReferenceEquals(ActivePlaybackMenu, this))
        {
            ActivePlaybackMenu = null;
        }

        this.playbackEvent = null;
        this.playbackPlayer = null;
        this.playbackMenu = null;
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
        ModEntry.Instance.SetImportedPreviewMapAssetRequestsEnabled(false);
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
            return;
        }

        object command = this.state.Cutscene.Commands[nextIndex];
        this.SimulateCommandEffect(command);
        this.state.CommandMarkerIndex = nextIndex;
    }

    private void SimulateCommandEffect(object command)
    {
        if (command is not EventCommandBlock eventCommand)
        {
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

    private void ResetMarker()
    {
        this.state.CommandMarkerIndex = -1;
        this.state.SimulatedActorPositions.Clear();
        this.toolbarStatusMessage = "Marker reset to setup.";
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
                if (cmdWord.Equals("speak", StringComparison.OrdinalIgnoreCase))
                {
                    evt.CurrentCommand++;
                }
            }
        }

        if (this.state.Mode == EditorMode.Play)
        {
            Game1.activeClickableMenu = this;
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
        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, 72, 38), "New", this.NewCutscene);
        nextX += 84;

        this.locationButtonBounds = new Rectangle(nextX, buttonY, 250, 38);
        this.DrawToolbarButton(spriteBatch, this.locationButtonBounds, "Location: " + this.TrimToolbarText(LocationBootstrapper.GetDisplayName(this.state.SelectedLocationId), 18), this.ToggleLocationDropdown);
        nextX += 262;

        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, 96, 38), this.state.Mode == EditorMode.Play ? "Stop" : "Play", this.TogglePlayback);
        nextX += 108;

        if (this.state.Mode == EditorMode.Edit)
        {
            this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, 72, 38), ">>", this.FastTrack);
            nextX += 84;

            if (this.state.CommandMarkerIndex > -1)
            {
                this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, 72, 38), "Reset", this.ResetMarker);
                nextX += 84;
            }
        }

        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, 108, 38), "Import", this.OpenImportDialog);
        nextX += 120;
        this.DrawToolbarButton(spriteBatch, new Rectangle(nextX, buttonY, 96, 38), "Save", this.OpenSaveDialog);
        nextX += 108;

        if (!string.IsNullOrWhiteSpace(this.toolbarStatusMessage))
        {
            Utility.drawTextWithShadow(
                spriteBatch,
                this.toolbarStatusMessage,
                Game1.smallFont,
                new Vector2(nextX, this.yPositionOnScreen + 24),
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

    private string TrimToolbarText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
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
