using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace CommunityCenterPins;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";
    private const string SaveDataKey = "pins";
    private const string GlobalSaveDataKeyPrefix = "pins.";
    private const int PinButtonSize = 40;
    private const int PinTextureSize = 32;
    private const int PinButtonRightInset = 64;

    private static ModEntry? instance;

    private readonly List<BundlePinOverlay> overlays = new();
    private readonly BundleInfoResolver bundleResolver = new();
    private ModConfig config = new();
    private Texture2D pinTexture = null!;

    private Rectangle? pinButtonBounds;
    private JunimoNoteMenu? observedMenu;
    private int? observedBundleIndex;
    private BundleSnapshot? observedSnapshot;
    private BundlePinOverlay? draggingOverlay;
    private Point lastDragCursor;
    private bool dragMoved;

    public override void Entry(IModHelper helper)
    {
        instance = this;
        this.config = helper.ReadConfig<ModConfig>();
        this.pinTexture = helper.ModContent.Load<Texture2D>("assets/pin.png");
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
        helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        helper.Events.Display.RenderedHud += this.OnRenderedHud;
        helper.Events.Display.WindowResized += this.OnWindowResized;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

        try
        {
            Harmony harmony = new(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(JunimoNoteMenu), nameof(JunimoNoteMenu.draw), new[] { typeof(SpriteBatch) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(JunimoNoteMenu_Draw_Postfix))
            );
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Failed to apply JunimoNoteMenu.draw postfix: {ex.Message}", LogLevel.Error);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            reset: () =>
            {
                this.config = new ModConfig();
                this.ClampOverlaysToViewport();
            },
            save: () => this.Helper.WriteConfig(this.config),
            titleScreenOnly: false
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.OverlayScalePercent,
            setValue: value =>
            {
                this.config.OverlayScalePercent = Math.Clamp(value, 50, 200);
                this.ClampOverlaysToViewport();
            },
            name: () => "Overlay scale",
            tooltip: () => "Scales the pinned bundle overlay.",
            min: 50,
            max: 200,
            interval: 5,
            formatValue: value => $"{value}%"
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.DebugHitboxes,
            setValue: value => this.config.DebugHitboxes = value,
            name: () => "Debug hitboxes",
            tooltip: () => "Draws the pin button, overlay, close button, and item slot hitboxes."
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.overlays.Clear();

        CommunityCenterPinsSaveData saveData = this.ReadPersistedSaveData();
        foreach (OverlayPinData pin in saveData.Pins)
        {
            if (!this.TryCreateOverlayFromSave(pin, out BundlePinOverlay overlay))
            {
                continue;
            }

            this.overlays.Add(overlay);
        }

        if (this.overlays.Count > 0)
        {
            this.Monitor.Log($"Restored {this.overlays.Count} pinned bundle overlay(s).", LogLevel.Trace);
        }
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.WriteSaveData();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.overlays.Clear();
        this.pinButtonBounds = null;
        this.ClearObservedBundleState();
        this.draggingOverlay = null;
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        this.pinButtonBounds = null;
        this.UpdateObservedBundleState(e.NewMenu as JunimoNoteMenu);
    }

    private void RefreshOverlay(BundleSnapshot snapshot)
    {
        BundlePinOverlay? overlay = this.overlays.FirstOrDefault(pinned => pinned.BundleIndex == snapshot.BundleIndex);
        if (overlay is null)
        {
            return;
        }

        if (snapshot.Requirements.Count == 0 || snapshot.RemainingSlots <= 0)
        {
            this.overlays.Remove(overlay);
            return;
        }

        overlay.UpdateContent(snapshot);
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        this.DrawPinButtonOnMenu(e.SpriteBatch);
        this.DrawOverlays(e.SpriteBatch);
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
        {
            return;
        }

        this.DrawOverlays(e.SpriteBatch);
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        foreach (BundlePinOverlay overlay in this.overlays)
        {
            overlay.ClampToViewport(this.GetOverlayScale());
        }

        this.WriteSaveData();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || e.Button != SButton.MouseLeft)
        {
            return;
        }

        Point cursor = this.GetUiCursorPosition();
        if (this.pinButtonBounds.HasValue && this.pinButtonBounds.Value.Contains(cursor) && this.TryGetPinnedMenu(out JunimoNoteMenu menu))
        {
            this.Helper.Input.Suppress(e.Button);
            this.TogglePin(menu);
            return;
        }

        for (int i = this.overlays.Count - 1; i >= 0; i--)
        {
            BundlePinOverlay overlay = this.overlays[i];
            if (overlay.GetCloseButtonBounds(this.GetOverlayScale()).Contains(cursor))
            {
                this.Helper.Input.Suppress(e.Button);
                this.overlays.RemoveAt(i);
                this.WriteSaveData();
                return;
            }

            if (overlay.ContainsBody(cursor, this.GetOverlayScale()))
            {
                this.Helper.Input.Suppress(e.Button);
                this.draggingOverlay = overlay;
                this.lastDragCursor = cursor;
                this.dragMoved = false;
                return;
            }
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        this.UpdateObservedBundleOverlay();

        if (this.draggingOverlay is null)
        {
            return;
        }

        if (Mouse.GetState().LeftButton != ButtonState.Pressed)
        {
            this.EndDrag();
            return;
        }

        Point cursor = this.GetUiCursorPosition();
        Point delta = cursor - this.lastDragCursor;
        if (delta == Point.Zero)
        {
            return;
        }

        Rectangle bounds = this.draggingOverlay.GetBounds(this.GetOverlayScale());
        this.draggingOverlay.SetPosition(new Vector2(bounds.X + delta.X, bounds.Y + delta.Y));
        this.draggingOverlay.ClampToViewport(this.GetOverlayScale());
        this.lastDragCursor = cursor;
        this.dragMoved = true;
    }

    private void UpdateObservedBundleState(JunimoNoteMenu? menu)
    {
        if (menu is null || !menu.specificBundlePage || menu.currentPageBundle is null)
        {
            this.ClearObservedBundleState();
            return;
        }

        this.observedMenu = menu;
        this.observedBundleIndex = menu.currentPageBundle.bundleIndex;
        this.observedSnapshot = this.bundleResolver.TryCreateSnapshot(menu, null, out BundleSnapshot snapshot)
            ? snapshot
            : null;
    }

    private void UpdateObservedBundleOverlay()
    {
        if (!this.TryGetPinnedMenu(out JunimoNoteMenu menu))
        {
            this.ClearObservedBundleState();
            return;
        }

        if (!ReferenceEquals(this.observedMenu, menu) || this.observedBundleIndex != menu.currentPageBundle.bundleIndex)
        {
            this.UpdateObservedBundleState(menu);
            return;
        }

        if (!this.bundleResolver.TryCreateSnapshot(menu, null, out BundleSnapshot currentSnapshot))
        {
            return;
        }

        if (this.observedSnapshot is not null && this.AreSnapshotsEqual(this.observedSnapshot, currentSnapshot))
        {
            return;
        }

        this.observedSnapshot = currentSnapshot;
        this.RefreshOverlay(currentSnapshot);
        this.WriteSaveData();
    }

    private void ClearObservedBundleState()
    {
        this.observedMenu = null;
        this.observedBundleIndex = null;
        this.observedSnapshot = null;
    }

    private bool AreSnapshotsEqual(BundleSnapshot left, BundleSnapshot right)
    {
        if (left.BundleIndex != right.BundleIndex
            || left.BundleName != right.BundleName
            || left.RemainingSlots != right.RemainingSlots
            || left.Requirements.Count != right.Requirements.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Requirements.Count; i++)
        {
            if (left.Requirements[i] != right.Requirements[i])
            {
                return false;
            }
        }

        return true;
    }

    private void TogglePin(JunimoNoteMenu menu)
    {
        if (!this.bundleResolver.TryCreateSnapshot(menu, null, out BundleSnapshot snapshot))
        {
            return;
        }

        BundlePinOverlay? existing = this.overlays.FirstOrDefault(overlay => overlay.BundleIndex == snapshot.BundleIndex);
        if (existing is not null)
        {
            this.overlays.Remove(existing);
            this.WriteSaveData();
            Game1.playSound("smallSelect");
            return;
        }

        Vector2 position = new(menu.xPositionOnScreen + menu.width - 320, menu.yPositionOnScreen + 96);
        BundlePinOverlay overlay = new(snapshot.BundleIndex, snapshot.BundleName, snapshot.Requirements, position);
        overlay.UpdateContent(snapshot);
        overlay.ClampToViewport(this.GetOverlayScale());
        this.overlays.Add(overlay);
        this.WriteSaveData();
        Game1.playSound("smallSelect");
    }

    private bool TryCreateOverlayFromSave(OverlayPinData pin, out BundlePinOverlay overlay)
    {
        overlay = null!;

        if (this.bundleResolver.TryCreateSnapshot(pin.BundleIndex, out BundleSnapshot snapshot))
        {
            if (snapshot.Requirements.Count == 0)
            {
                return false;
            }

            overlay = new BundlePinOverlay(pin.BundleIndex, snapshot.BundleName, snapshot.Requirements, new Vector2(pin.X, pin.Y));
            overlay.UpdateContent(snapshot);
            overlay.ClampToViewport(this.GetOverlayScale());
            return true;
        }

        if (pin.Requirements.Count == 0)
        {
            return false;
        }

        overlay = new BundlePinOverlay(
            pin.BundleIndex,
            string.IsNullOrWhiteSpace(pin.BundleName) ? $"Bundle {pin.BundleIndex}" : pin.BundleName,
            pin.Requirements.Select(line => new BundleRequirementLine(line.Name, line.Required, line.QualifiedItemId, line.Quality, line.PreservesId)).ToList(),
            new Vector2(pin.X, pin.Y)
        );
        overlay.UpdateContent(new BundleSnapshot(pin.BundleIndex, overlay.BundleName, pin.RemainingSlots, overlay.Requirements));
        overlay.ClampToViewport(this.GetOverlayScale());
        return true;
    }

    private void WriteSaveData()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        CommunityCenterPinsSaveData saveData = new()
        {
            Pins = this.overlays.Select(overlay => overlay.ToSaveData()).ToList()
        };

        this.Helper.Data.WriteSaveData(SaveDataKey, saveData);
        string? globalKey = this.GetGlobalSaveDataKey();
        if (globalKey is not null)
        {
            this.Helper.Data.WriteGlobalData(globalKey, saveData);
        }
    }

    private bool TryGetPinnedMenu(out JunimoNoteMenu menu)
    {
        if (Game1.activeClickableMenu is JunimoNoteMenu junimoMenu
            && junimoMenu.specificBundlePage
            && junimoMenu.currentPageBundle is not null)
        {
            menu = junimoMenu;
            return true;
        }

        menu = null!;
        return false;
    }

    private Rectangle GetPinButtonBounds(JunimoNoteMenu menu)
    {
        int pinX = menu.xPositionOnScreen + menu.width - PinButtonSize - PinButtonRightInset;
        return new Rectangle(pinX, menu.yPositionOnScreen + 224, PinButtonSize, PinButtonSize);
    }

    private Point GetUiCursorPosition()
    {
        return new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
    }

    private void DrawPinButton(SpriteBatch spriteBatch, Rectangle bounds, bool active)
    {
        int offset = (PinButtonSize - PinTextureSize) / 2;
        Rectangle destination = new(bounds.X + offset, bounds.Y + offset, PinTextureSize, PinTextureSize);
        spriteBatch.Draw(this.pinTexture, destination, active ? Color.White : Color.White * 0.8f);
    }

    private void DrawPinButtonOnMenu(SpriteBatch spriteBatch)
    {
        if (!Context.IsWorldReady || !this.TryGetPinnedMenu(out JunimoNoteMenu menu))
        {
            return;
        }

        Rectangle buttonBounds = this.GetPinButtonBounds(menu);
        bool alreadyPinned = this.overlays.Any(overlay => overlay.BundleIndex == menu.currentPageBundle.bundleIndex);
        this.DrawPinButton(spriteBatch, buttonBounds, alreadyPinned);

        if (this.config.DebugHitboxes)
        {
            this.DrawDebugRectangle(spriteBatch, buttonBounds, Color.Gold * 0.45f);
        }

        this.pinButtonBounds = buttonBounds;
    }

    private void DrawOverlays(SpriteBatch spriteBatch)
    {
        foreach (BundlePinOverlay overlay in this.overlays)
        {
            overlay.Draw(spriteBatch, this.bundleResolver, this.GetOverlayScale());
            if (this.config.DebugHitboxes)
            {
                overlay.DrawDebugHitboxes(spriteBatch, this.GetOverlayScale());
            }
        }

        if (this.draggingOverlay is not null)
        {
            return;
        }

        Point cursor = this.GetUiCursorPosition();
        for (int i = this.overlays.Count - 1; i >= 0; i--)
        {
            if (!this.overlays[i].TryGetRequirementAtPoint(cursor, this.GetOverlayScale(), out BundleRequirementLine? line) || line is null)
            {
                continue;
            }

            if (this.bundleResolver.TryCreateItem(line, out Item? item) && item is not null)
            {
                IClickableMenu.drawToolTip(spriteBatch, item.getDescription(), item.DisplayName, item);
            }
            break;
        }
    }

    private void ClampOverlaysToViewport()
    {
        foreach (BundlePinOverlay overlay in this.overlays)
        {
            overlay.ClampToViewport(this.GetOverlayScale());
        }
    }

    private float GetOverlayScale()
    {
        return this.config.OverlayScalePercent / 100f;
    }

    private void DrawDebugRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        spriteBatch.Draw(Game1.staminaRect, bounds, color);
    }

    private void EndDrag()
    {
        if (this.draggingOverlay is null)
        {
            return;
        }

        this.draggingOverlay.ClampToViewport(this.GetOverlayScale());
        this.draggingOverlay = null;

        if (this.dragMoved)
        {
            this.WriteSaveData();
        }

        this.dragMoved = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(JunimoNoteMenu), nameof(JunimoNoteMenu.draw))]
    private static void JunimoNoteMenu_Draw_Postfix(JunimoNoteMenu __instance, SpriteBatch b)
    {
        if (instance is null || !Context.IsWorldReady || __instance is null)
        {
            return;
        }

        if (!__instance.specificBundlePage || __instance.currentPageBundle is null)
        {
            return;
        }

        Rectangle buttonBounds = instance.GetPinButtonBounds(__instance);
        bool alreadyPinned = instance.overlays.Any(overlay => overlay.BundleIndex == __instance.currentPageBundle.bundleIndex);
        instance.DrawPinButton(b, buttonBounds, alreadyPinned);

        if (instance.config.DebugHitboxes)
        {
            instance.DrawDebugRectangle(b, buttonBounds, Color.Gold * 0.45f);
        }

        instance.pinButtonBounds = buttonBounds;
    }

    private CommunityCenterPinsSaveData ReadPersistedSaveData()
    {
        CommunityCenterPinsSaveData? saveData = this.Helper.Data.ReadSaveData<CommunityCenterPinsSaveData>(SaveDataKey);
        if (saveData?.Pins.Count > 0)
        {
            return saveData;
        }

        string? globalKey = this.GetGlobalSaveDataKey();
        if (globalKey is null)
        {
            return saveData ?? new CommunityCenterPinsSaveData();
        }

        return this.Helper.Data.ReadGlobalData<CommunityCenterPinsSaveData>(globalKey)
            ?? saveData
            ?? new CommunityCenterPinsSaveData();
    }

    private string? GetGlobalSaveDataKey()
    {
        return string.IsNullOrWhiteSpace(Constants.SaveFolderName)
            ? null
            : GlobalSaveDataKeyPrefix + Constants.SaveFolderName;
    }
}
