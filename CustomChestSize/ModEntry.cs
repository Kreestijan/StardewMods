using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;

namespace CustomChestSize;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";
    private const string UnlimitedStorageId = "furyx639.UnlimitedStorage";
    private const string ConvenientChestsId = "aEnigma.ConvenientChests";
    private const string CategorizeChestsId = "putaohuqi.CategorizeChests";
    private const string RemoteFridgeStorageId = "EternalSoap.RemoteFridgeStorage";
    private const int PanelGap = 0;
    private const int LowerPanelHeight = 232;
    private const int LowerInventoryTopPadding = 24;
    private const int PlayerInventoryFirstRowLift = 16;
    private const int LowerBackgroundTopPadding = 84;
    private const int LowerBackgroundBottomPadding = 52;
    private static readonly int DefaultMenuWidth = 800 + IClickableMenu.borderWidth * 2;
    private static readonly int DefaultMenuHeight = 600 + IClickableMenu.borderWidth * 2;
    private static readonly int LowerPanelTopOffset = IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 64;
    private static readonly int InventoryHorizontalInset = IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth / 2;
    private const int MinRegularChestColumns = 12;
    private const int MinRegularChestRows = 3;
    private const int MinBigChestColumns = 14;
    private const int MinBigChestRows = 5;
    private const int MinBigStoneChestColumns = 14;
    private const int MinBigStoneChestRows = 5;
    private const int MinStoneChestColumns = 12;
    private const int MinStoneChestRows = 3;
    private const int MinFridgeColumns = 12;
    private const int MinFridgeRows = 3;
    private const int MinMiniFridgeColumns = 12;
    private const int MinMiniFridgeRows = 3;
    private const int MinJunimoChestColumns = 3;
    private const int MinJunimoChestRows = 3;
    private const int MinAutoGrabberColumns = 12;
    private const int MinAutoGrabberRows = 3;
    private const int MaxColumns = 24;
    private const int MaxRows = 12;
    private const int MinLayoutOffset = -200;
    private const int MaxLayoutOffset = 200;
    private const int MinConvenientChestsOffset = -1000;
    private const int MaxConvenientChestsOffset = 1000;
    private const int MinColorPickerOffset = -1000;
    private const int MaxColorPickerOffset = 1000;
    private static readonly ConditionalWeakTable<ItemGrabMenu, ChestMenuLayoutState> LayoutStates = new();

    private static readonly ConditionalWeakTable<Chest, StardewValley.Object?> AutoGrabberChests = new();

    internal void LogDebug(string message)
    {
        lock (ConfigLock)
        {
            if (this.config.DebugLogEnabled)
            {
                this.Monitor.Log(message, LogLevel.Debug);
            }
        }
    }

    internal bool ShouldLogDebug()
    {
        lock (ConfigLock)
        {
            return this.config.DebugLogEnabled;
        }
    }

    internal static void LogDebugStatic(string message)
    {
        Instance.LogDebug(message);
    }

    private string Translate(string key)
    {
        return this.Helper.Translation.Get(key).ToString();
    }

    private string Translate(string key, object tokens)
    {
        return this.Helper.Translation.Get(key, tokens).ToString();
    }

    private Func<string> TranslateGetter(string key)
    {
        return () => this.Translate(key);
    }

    private static readonly object ConfigLock = new();

    internal static ModEntry Instance { get; private set; } = null!;

    private Harmony? harmony;

    private ModConfig config = new();

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.LoadConfig();

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.harmony.PatchAll();

        // Manually patch all ItemGrabMenu constructors — the attribute-based patch with
        // MethodType.Constructor can't resolve in the Harmony version SMAPI bundles.
        foreach (ConstructorInfo ctor in typeof(ItemGrabMenu).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            this.harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ItemGrabMenu_AnyConstructor_Postfix)));
        }

        helper.ConsoleCommands.Add(
            "ccs_reload",
            this.Translate("commands.reload.description"),
            this.ReloadConfigCommand
        );

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.World.LocationListChanged += this.OnLocationListChanged;
        helper.Events.World.ObjectListChanged += this.OnObjectListChanged;
    }

    internal static bool TryGetConfiguredLayout(Chest chest, out ChestGridLayout layout)
    {
        return Instance.TryGetConfiguredLayoutCore(chest, out layout);
    }

    internal static int GetConfiguredCapacity(Chest chest, int fallbackCapacity)
    {
        return TryGetConfiguredLayout(chest, out ChestGridLayout layout)
            ? layout.Capacity
            : fallbackCapacity;
    }

    internal static bool TryGetAutoGrabberLayout(Chest chest, out ChestGridLayout layout)
    {
        if (!TryGetAutoGrabberOwner(chest, out _))
        {
            layout = default;
            return false;
        }

        lock (ConfigLock)
        {
            layout = new ChestGridLayout(Instance.config.AutoGrabberColumns, Instance.config.AutoGrabberRows);
            return true;
        }
    }

    internal static void ApplyLayoutIfNeeded(ItemGrabMenu menu)
    {
        Instance.ApplyLayoutIfNeededCore(menu);
    }

    internal static int GetChestsAnywhereWidgetXOffset()
    {
        return Instance.GetChestsAnywhereWidgetXOffsetCore();
    }

    internal static int GetChestsAnywhereWidgetYOffset()
    {
        return Instance.GetChestsAnywhereWidgetYOffsetCore();
    }

    internal static int GetColorPickerXOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.ColorPickerXOffset;
        }
    }

    internal static int GetColorPickerYOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.ColorPickerYOffset;
        }
    }

    internal static int GetUnlimitedStorageSearchXOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.UnlimitedStorageSearchXOffset;
        }
    }

    internal static int GetUnlimitedStorageSearchYOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.UnlimitedStorageSearchYOffset;
        }
    }

    internal static int GetUnlimitedStorageSearchLeftOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.UnlimitedStorageSearchLeftOffset;
        }
    }

    internal static int GetUnlimitedStorageSearchRightOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.UnlimitedStorageSearchRightOffset;
        }
    }

    internal static int GetConvenientChestsXOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.ConvenientChestsXOffset;
        }
    }

    internal static int GetConvenientChestsYOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.ConvenientChestsYOffset;
        }
    }

    internal static int GetRemoteFridgeStorageXOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.RemoteFridgeStorageXOffset;
        }
    }

    internal static int GetRemoteFridgeStorageYOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.RemoteFridgeStorageYOffset;
        }
    }

    internal static volatile bool CachedTintChestUI;
    internal static volatile int CachedTintChestUIOpacity;

    internal static bool IsTintChestUIEnabled() => CachedTintChestUI;

    internal static int GetTintChestUIOpacity() => CachedTintChestUIOpacity;

    internal static int GetTintChestUIPaddingLeft()
    {
        lock (ConfigLock)
        {
            return Instance.config.TintChestUIPaddingLeft;
        }
    }

    internal static int GetTintChestUIPaddingRight()
    {
        lock (ConfigLock)
        {
            return Instance.config.TintChestUIPaddingRight;
        }
    }

    internal static int GetTintChestUIPaddingTop()
    {
        lock (ConfigLock)
        {
            return Instance.config.TintChestUIPaddingTop;
        }
    }

    internal static int GetTintChestUIPaddingBottom()
    {
        lock (ConfigLock)
        {
            return Instance.config.TintChestUIPaddingBottom;
        }
    }

    internal static int GetCategorizeChestsXOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.CategorizeChestsXOffset;
        }
    }

    internal static int GetCategorizeChestsYOffset()
    {
        lock (ConfigLock)
        {
            return Instance.config.CategorizeChestsYOffset;
        }
    }

    internal static bool IsUnlimitedStorageLoaded()
    {
        return Instance.Helper.ModRegistry.IsLoaded(UnlimitedStorageId);
    }

    internal static bool IsConvenientChestsLoaded()
    {
        return Instance.Helper.ModRegistry.IsLoaded(ConvenientChestsId);
    }

    private void ReloadConfigCommand(string command, string[] args)
    {
        this.LoadConfig();
        this.Monitor.Log(this.Translate("commands.reload.success"), LogLevel.Info);
        this.RefreshActiveChestMenu();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RegisterGenericModConfigMenu();
        this.PatchChestsAnywhereCompatibility();
        this.PatchConvenientChestsCompatibility();
        this.PatchCategorizeChestsCompatibility();
        this.PatchRemoteFridgeStorageCompatibility();
        this.InitUnlimitedStorageCompatibility();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.RegisterAutoGrabbersInLoadedLocations();
    }

    private void OnLocationListChanged(object? sender, LocationListChangedEventArgs e)
    {
        foreach (GameLocation location in e.Added)
        {
            this.RegisterAutoGrabbersInLocation(location);
        }
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        foreach (KeyValuePair<Vector2, StardewValley.Object> pair in e.Added)
        {
            RegisterAutoGrabberInternalChest(pair.Value);
        }

        foreach (KeyValuePair<Vector2, StardewValley.Object> pair in e.Removed)
        {
            UnregisterAutoGrabberInternalChest(pair.Value);
        }
    }

    private void RegisterGenericModConfigMenu()
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        bool hasChestsAnywhere = this.Helper.ModRegistry.IsLoaded("Pathoschild.ChestsAnywhere");
        bool hasConvenientChests = this.Helper.ModRegistry.IsLoaded(ConvenientChestsId);
        bool hasCategorizeChests = this.Helper.ModRegistry.IsLoaded(CategorizeChestsId);
        bool hasRemoteFridgeStorage = this.Helper.ModRegistry.IsLoaded(RemoteFridgeStorageId);
        bool hasUnlimitedStorage = this.Helper.ModRegistry.IsLoaded(UnlimitedStorageId);

        gmcm.Register(
            this.ModManifest,
            reset: this.ResetConfig,
            save: this.SaveConfig,
            titleScreenOnly: false
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.regularChest.title"),
            tooltip: this.TranslateGetter("gmcm.regularChest.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.RegularChestColumns,
            setValue: this.SetRegularChestColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.regularChest.columns.tooltip"),
            min: MinRegularChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.RegularChestRows,
            setValue: this.SetRegularChestRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.regularChest.rows.tooltip"),
            min: MinRegularChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.bigChest.title"),
            tooltip: this.TranslateGetter("gmcm.bigChest.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.BigChestColumns,
            setValue: this.SetBigChestColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.bigChest.columns.tooltip"),
            min: MinBigChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.BigChestRows,
            setValue: this.SetBigChestRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.bigChest.rows.tooltip"),
            min: MinBigChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.stoneChest.title"),
            tooltip: this.TranslateGetter("gmcm.stoneChest.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.StoneChestColumns,
            setValue: this.SetStoneChestColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.stoneChest.columns.tooltip"),
            min: MinStoneChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.StoneChestRows,
            setValue: this.SetStoneChestRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.stoneChest.rows.tooltip"),
            min: MinStoneChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.bigStoneChest.title"),
            tooltip: this.TranslateGetter("gmcm.bigStoneChest.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.BigStoneChestColumns,
            setValue: this.SetBigStoneChestColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.bigStoneChest.columns.tooltip"),
            min: MinBigStoneChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.BigStoneChestRows,
            setValue: this.SetBigStoneChestRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.bigStoneChest.rows.tooltip"),
            min: MinBigStoneChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.fridge.title"),
            tooltip: this.TranslateGetter("gmcm.fridge.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.FridgeColumns,
            setValue: this.SetFridgeColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.fridge.columns.tooltip"),
            min: MinFridgeColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.FridgeRows,
            setValue: this.SetFridgeRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.fridge.rows.tooltip"),
            min: MinFridgeRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.miniFridge.title"),
            tooltip: this.TranslateGetter("gmcm.miniFridge.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.MiniFridgeColumns,
            setValue: this.SetMiniFridgeColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.miniFridge.columns.tooltip"),
            min: MinMiniFridgeColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.MiniFridgeRows,
            setValue: this.SetMiniFridgeRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.miniFridge.rows.tooltip"),
            min: MinMiniFridgeRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.junimoChest.title"),
            tooltip: this.TranslateGetter("gmcm.junimoChest.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.JunimoChestColumns,
            setValue: this.SetJunimoChestColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.junimoChest.columns.tooltip"),
            min: MinJunimoChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.JunimoChestRows,
            setValue: this.SetJunimoChestRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.junimoChest.rows.tooltip"),
            min: MinJunimoChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.autoGrabber.title"),
            tooltip: this.TranslateGetter("gmcm.autoGrabber.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.AutoGrabberColumns,
            setValue: this.SetAutoGrabberColumns,
            name: this.TranslateGetter("gmcm.columns.name"),
            tooltip: this.TranslateGetter("gmcm.autoGrabber.columns.tooltip"),
            min: MinAutoGrabberColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.AutoGrabberRows,
            setValue: this.SetAutoGrabberRows,
            name: this.TranslateGetter("gmcm.rows.name"),
            tooltip: this.TranslateGetter("gmcm.autoGrabber.rows.tooltip"),
            min: MinAutoGrabberRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: this.TranslateGetter("gmcm.layoutTuning.title"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.tooltip")
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.ChestBackgroundHeightOffset,
            setValue: value => this.config.ChestBackgroundHeightOffset = value,
            name: this.TranslateGetter("gmcm.layoutTuning.chestBackgroundHeight.name"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.chestBackgroundHeight.tooltip"),
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.InventoryPanelGapOffset,
            setValue: value => this.config.InventoryPanelGapOffset = value,
            name: this.TranslateGetter("gmcm.layoutTuning.inventoryGap.name"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.inventoryGap.tooltip"),
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.InventoryBackgroundTopOffset,
            setValue: value => this.config.InventoryBackgroundTopOffset = value,
            name: this.TranslateGetter("gmcm.layoutTuning.inventoryBackgroundTop.name"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.inventoryBackgroundTop.tooltip"),
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.InventoryBackgroundBottomOffset,
            setValue: value => this.config.InventoryBackgroundBottomOffset = value,
            name: this.TranslateGetter("gmcm.layoutTuning.inventoryBackgroundBottom.name"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.inventoryBackgroundBottom.tooltip"),
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.ColorPickerXOffset,
            setValue: value => this.config.ColorPickerXOffset = value,
            name: this.TranslateGetter("gmcm.layoutTuning.colorPickerX.name"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.colorPickerX.tooltip"),
            min: MinColorPickerOffset,
            max: MaxColorPickerOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.ColorPickerYOffset,
            setValue: value => this.config.ColorPickerYOffset = value,
            name: this.TranslateGetter("gmcm.layoutTuning.colorPickerY.name"),
            tooltip: this.TranslateGetter("gmcm.layoutTuning.colorPickerY.tooltip"),
            min: MinColorPickerOffset,
            max: MaxColorPickerOffset,
            interval: 4
        );
        bool hasAnyCompat = hasChestsAnywhere || hasUnlimitedStorage || hasConvenientChests || hasCategorizeChests || hasRemoteFridgeStorage;

        if (hasAnyCompat)
            gmcm.AddPageLink(this.ModManifest, "compatibility", this.TranslateGetter("gmcm.compatibility.page"), this.TranslateGetter("gmcm.compatibility.page.tooltip"));
        gmcm.AddPageLink(this.ModManifest, "extra-features", this.TranslateGetter("gmcm.extraFeatures.page"), this.TranslateGetter("gmcm.extraFeatures.page.tooltip"));
        gmcm.AddPageLink(this.ModManifest, "debug", this.TranslateGetter("gmcm.debug.page"), this.TranslateGetter("gmcm.debug.page.tooltip"));

        if (hasAnyCompat)
        {
            gmcm.AddPage(this.ModManifest, "compatibility", this.TranslateGetter("gmcm.compatibility.page"));

            if (hasChestsAnywhere)
            {
                gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.compatibility.chestsAnywhere.title"), tooltip: this.TranslateGetter("gmcm.compatibility.chestsAnywhere.tooltip"));
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.ChestsAnywhereWidgetXOffset, setValue: value => this.config.ChestsAnywhereWidgetXOffset = value, name: this.TranslateGetter("gmcm.compatibility.chestsAnywhere.x.name"), tooltip: this.TranslateGetter("gmcm.compatibility.chestsAnywhere.x.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.ChestsAnywhereWidgetYOffset, setValue: value => this.config.ChestsAnywhereWidgetYOffset = value, name: this.TranslateGetter("gmcm.compatibility.chestsAnywhere.y.name"), tooltip: this.TranslateGetter("gmcm.compatibility.chestsAnywhere.y.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
            }

            if (hasUnlimitedStorage)
            {
                gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.title"), tooltip: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.tooltip"));
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.UnlimitedStorageSearchXOffset, setValue: value => this.config.UnlimitedStorageSearchXOffset = value, name: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchX.name"), tooltip: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchX.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.UnlimitedStorageSearchYOffset, setValue: value => this.config.UnlimitedStorageSearchYOffset = value, name: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchY.name"), tooltip: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchY.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.UnlimitedStorageSearchLeftOffset, setValue: value => this.config.UnlimitedStorageSearchLeftOffset = value, name: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchLeft.name"), tooltip: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchLeft.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.UnlimitedStorageSearchRightOffset, setValue: value => this.config.UnlimitedStorageSearchRightOffset = value, name: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchRight.name"), tooltip: this.TranslateGetter("gmcm.compatibility.unlimitedStorage.searchRight.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
            }

            if (hasConvenientChests)
            {
                gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.compatibility.convenientChests.title"), tooltip: this.TranslateGetter("gmcm.compatibility.convenientChests.tooltip"));
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.ConvenientChestsXOffset, setValue: value => this.config.ConvenientChestsXOffset = value, name: this.TranslateGetter("gmcm.xOffset.name"), tooltip: this.TranslateGetter("gmcm.compatibility.convenientChests.x.tooltip"), min: MinConvenientChestsOffset, max: MaxConvenientChestsOffset, interval: 8);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.ConvenientChestsYOffset, setValue: value => this.config.ConvenientChestsYOffset = value, name: this.TranslateGetter("gmcm.yOffset.name"), tooltip: this.TranslateGetter("gmcm.compatibility.convenientChests.y.tooltip"), min: MinConvenientChestsOffset, max: MaxConvenientChestsOffset, interval: 8);
            }

            if (hasCategorizeChests)
            {
                gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.compatibility.categorizeChests.title"), tooltip: this.TranslateGetter("gmcm.compatibility.categorizeChests.tooltip"));
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.CategorizeChestsXOffset, setValue: value => this.config.CategorizeChestsXOffset = value, name: this.TranslateGetter("gmcm.xOffset.name"), tooltip: this.TranslateGetter("gmcm.compatibility.categorizeChests.x.tooltip"), min: MinConvenientChestsOffset, max: MaxConvenientChestsOffset, interval: 8);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.CategorizeChestsYOffset, setValue: value => this.config.CategorizeChestsYOffset = value, name: this.TranslateGetter("gmcm.yOffset.name"), tooltip: this.TranslateGetter("gmcm.compatibility.categorizeChests.y.tooltip"), min: MinConvenientChestsOffset, max: MaxConvenientChestsOffset, interval: 8);
            }

            if (hasRemoteFridgeStorage)
            {
                gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.compatibility.remoteFridgeStorage.title"), tooltip: this.TranslateGetter("gmcm.compatibility.remoteFridgeStorage.tooltip"));
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.RemoteFridgeStorageXOffset, setValue: value => this.config.RemoteFridgeStorageXOffset = value, name: this.TranslateGetter("gmcm.xOffset.name"), tooltip: this.TranslateGetter("gmcm.compatibility.remoteFridgeStorage.x.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
                gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.RemoteFridgeStorageYOffset, setValue: value => this.config.RemoteFridgeStorageYOffset = value, name: this.TranslateGetter("gmcm.yOffset.name"), tooltip: this.TranslateGetter("gmcm.compatibility.remoteFridgeStorage.y.tooltip"), min: MinLayoutOffset, max: MaxLayoutOffset, interval: 4);
            }
        }

        gmcm.AddPage(this.ModManifest, "extra-features", this.TranslateGetter("gmcm.extraFeatures.page"));
        gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.extraFeatures.chestUITint.title"), tooltip: this.TranslateGetter("gmcm.extraFeatures.chestUITint.tooltip"));
        gmcm.AddBoolOption(this.ModManifest, getValue: () => this.config.TintChestUI, setValue: value => this.config.TintChestUI = value, name: this.TranslateGetter("gmcm.extraFeatures.tintChestUI.name"), tooltip: this.TranslateGetter("gmcm.extraFeatures.tintChestUI.tooltip"));
        gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.TintChestUIOpacity, setValue: value => this.config.TintChestUIOpacity = value, name: this.TranslateGetter("gmcm.extraFeatures.tintOpacity.name"), tooltip: this.TranslateGetter("gmcm.extraFeatures.tintOpacity.tooltip"), min: 0, max: 100, interval: 1);
        gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.TintChestUIPaddingLeft, setValue: value => this.config.TintChestUIPaddingLeft = value, name: this.TranslateGetter("gmcm.extraFeatures.tintPaddingLeft.name"), tooltip: this.TranslateGetter("gmcm.extraFeatures.tintPaddingLeft.tooltip"), min: -200, max: 200, interval: 1);
        gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.TintChestUIPaddingRight, setValue: value => this.config.TintChestUIPaddingRight = value, name: this.TranslateGetter("gmcm.extraFeatures.tintPaddingRight.name"), tooltip: this.TranslateGetter("gmcm.extraFeatures.tintPaddingRight.tooltip"), min: -200, max: 200, interval: 1);
        gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.TintChestUIPaddingTop, setValue: value => this.config.TintChestUIPaddingTop = value, name: this.TranslateGetter("gmcm.extraFeatures.tintPaddingTop.name"), tooltip: this.TranslateGetter("gmcm.extraFeatures.tintPaddingTop.tooltip"), min: -200, max: 200, interval: 1);
        gmcm.AddNumberOption(this.ModManifest, getValue: () => this.config.TintChestUIPaddingBottom, setValue: value => this.config.TintChestUIPaddingBottom = value, name: this.TranslateGetter("gmcm.extraFeatures.tintPaddingBottom.name"), tooltip: this.TranslateGetter("gmcm.extraFeatures.tintPaddingBottom.tooltip"), min: -200, max: 200, interval: 1);

        gmcm.AddPage(this.ModManifest, "debug", this.TranslateGetter("gmcm.debug.page"));
        gmcm.AddSectionTitle(this.ModManifest, text: this.TranslateGetter("gmcm.debug.title"), tooltip: this.TranslateGetter("gmcm.debug.tooltip"));
        gmcm.AddBoolOption(this.ModManifest, getValue: () => this.config.DebugLogEnabled, setValue: value => this.config.DebugLogEnabled = value, name: this.TranslateGetter("gmcm.debug.logging.name"), tooltip: this.TranslateGetter("gmcm.debug.logging.tooltip"));

        gmcm.OnFieldChanged(
            this.ModManifest,
            (fieldId, value) => this.SaveConfig()
        );
    }

    private void LoadConfig()
    {
        lock (ConfigLock)
        {
            this.config = this.Helper.ReadConfig<ModConfig>();
            this.SanitizeConfig();
            this.Helper.WriteConfig(this.config);
        }
        this.CacheTintValues();
    }

    private void SaveConfig()
    {
        lock (ConfigLock)
        {
            this.SanitizeConfig();
            this.Helper.WriteConfig(this.config);
        }
        this.CacheTintValues();
        this.RefreshActiveChestMenu();
    }

    private void ResetConfig()
    {
        lock (ConfigLock)
        {
            this.config = new ModConfig();
        }
        this.CacheTintValues();
        this.SaveConfig();
    }

    private void CacheTintValues()
    {
        CachedTintChestUI = this.config.TintChestUI;
        CachedTintChestUIOpacity = this.config.TintChestUIOpacity;
    }

    private bool TryGetConfiguredLayoutCore(Chest chest, out ChestGridLayout layout)
    {
        lock (ConfigLock)
        {
            this.LogDebug($"[TryGetConfiguredLayoutCore] chest ItemId={chest.ItemId} Name={chest.Name} SpecialChestType={chest.SpecialChestType} playerChest={chest.playerChest.Value}");

            if (TryGetAutoGrabberLayout(chest, out layout))
            {
                this.LogDebug($"[TryGetConfiguredLayoutCore] Selected AUTO-GRABBER layout: {layout.Columns}x{layout.Rows}");
                return true;
            }

            // Note: ItemId is used to distinguish chest types because some mods may set
            // SpecialChestType to BigChest on all player chests.
            switch (chest.ItemId)
            {
                case "BigChest":
                    this.LogDebug($"[TryGetConfiguredLayoutCore] Selected BIG layout: {this.config.BigChestColumns}x{this.config.BigChestRows}");
                    layout = new ChestGridLayout(this.config.BigChestColumns, this.config.BigChestRows);
                    return true;

                case "BigStoneChest":
                    this.LogDebug($"[TryGetConfiguredLayoutCore] Selected BIG STONE layout: {this.config.BigStoneChestColumns}x{this.config.BigStoneChestRows}");
                    layout = new ChestGridLayout(this.config.BigStoneChestColumns, this.config.BigStoneChestRows);
                    return true;

                case "130":
                    this.LogDebug($"[TryGetConfiguredLayoutCore] Selected REGULAR layout: {this.config.RegularChestColumns}x{this.config.RegularChestRows}");
                    layout = new ChestGridLayout(this.config.RegularChestColumns, this.config.RegularChestRows);
                    return true;

                case "232":
                    this.LogDebug($"[TryGetConfiguredLayoutCore] Selected STONE layout: {this.config.StoneChestColumns}x{this.config.StoneChestRows}");
                    layout = new ChestGridLayout(this.config.StoneChestColumns, this.config.StoneChestRows);
                    return true;

                case "216":
                    if (chest.Location is FarmHouse farmHouse && farmHouse.fridge.Value == chest)
                    {
                        this.LogDebug($"[TryGetConfiguredLayoutCore] Selected FRIDGE layout: {this.config.FridgeColumns}x{this.config.FridgeRows}");
                        layout = new ChestGridLayout(this.config.FridgeColumns, this.config.FridgeRows);
                    }
                    else
                    {
                        this.LogDebug($"[TryGetConfiguredLayoutCore] Selected MINI-FRIDGE layout: {this.config.MiniFridgeColumns}x{this.config.MiniFridgeRows}");
                        layout = new ChestGridLayout(this.config.MiniFridgeColumns, this.config.MiniFridgeRows);
                    }
                    return true;

                case "256":
                    this.LogDebug($"[TryGetConfiguredLayoutCore] Selected JUNIMO layout: {this.config.JunimoChestColumns}x{this.config.JunimoChestRows}");
                    layout = new ChestGridLayout(this.config.JunimoChestColumns, this.config.JunimoChestRows);
                    return true;
            }

            // Fallback for any other player chest (e.g. from mods)
            if (chest.playerChest.Value)
            {
                this.LogDebug($"[TryGetConfiguredLayoutCore] Selected REGULAR layout (fallback): {this.config.RegularChestColumns}x{this.config.RegularChestRows}");
                layout = new ChestGridLayout(this.config.RegularChestColumns, this.config.RegularChestRows);
                return true;
            }

            this.LogDebug($"[TryGetConfiguredLayoutCore] No matching layout. playerChest={chest.playerChest.Value}");
            layout = default;
            return false;
        }
    }

    private void ApplyLayoutIfNeededCore(ItemGrabMenu menu)
    {
        if (TryResolveAutoGrabberMenu(menu, out Chest autoGrabberChest, out StardewValley.Object autoGrabber))
        {
            this.ApplyAutoGrabberLayout(menu, autoGrabberChest, autoGrabber);
            return;
        }

        if (menu.source != ItemGrabMenu.source_chest)
        {
            return;
        }

        if (menu.sourceItem is not Chest chest)
        {
            return;
        }

        if (!this.TryGetConfiguredLayoutCore(chest, out ChestGridLayout configuredLayout))
        {
            return;
        }

        int visibleRows = configuredLayout.Rows;
        int visibleCapacity = configuredLayout.Capacity;
        int currentItemCount = chest.GetItemsForPlayer().Count(item => item is not null);
        bool unlimitedStorageLoaded = IsUnlimitedStorageLoaded();

        if (!unlimitedStorageLoaded && currentItemCount > visibleCapacity)
        {
            visibleRows = (currentItemCount + configuredLayout.Columns - 1) / configuredLayout.Columns;
            visibleCapacity = visibleRows * configuredLayout.Columns;
        }

        int extraRows = System.Math.Max(0, visibleRows - 3);
        int menuWidth = System.Math.Max(
            DefaultMenuWidth,
            configuredLayout.Columns * 64 + (IClickableMenu.borderWidth + IClickableMenu.spaceToClearSideBorder) * 2
        );
        int chestMenuHeight = this.GetChestMenuHeight(visibleRows);
        int chestPanelHeight = chestMenuHeight + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth * 2;
        int assemblyHeight = chestPanelHeight + PanelGap + LowerPanelHeight;
        int menuX = Game1.uiViewport.Width / 2 - menuWidth / 2;
        int chestPanelTop = System.Math.Max(16, (Game1.uiViewport.Height - assemblyHeight) / 2);
        int lowerPanelTop = chestPanelTop + chestPanelHeight + PanelGap + this.config.InventoryPanelGapOffset;
        int chestX = menuX + InventoryHorizontalInset;
        int chestY = chestPanelTop + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder;
        int lowerPanelLeft = menuX - IClickableMenu.borderWidth / 2;
        int inventoryX = lowerPanelLeft + (menuWidth - menu.inventory.width) / 2;
        int inventoryY = lowerPanelTop + LowerInventoryTopPadding + PlayerInventoryFirstRowLift;

        menu.xPositionOnScreen = menuX;
        menu.yPositionOnScreen = lowerPanelTop - LowerPanelTopOffset;
        menu.width = menuWidth;
        menu.height = LowerPanelHeight + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192;
        menu.ItemsToGrabMenu = new InventoryMenu(
            chestX,
            chestY,
            playerInventory: false,
            chest.GetItemsForPlayer(),
            menu.inventory.highlightMethod,
            visibleCapacity,
            visibleRows
        );
        menu.ItemsToGrabMenu.height = chestMenuHeight;

        if (chest.SpecialChestType == Chest.SpecialChestTypes.MiniShippingBin)
        {
            menu.inventory.moveItemSound = "Ship";
        }

        menu.inventory.SetPosition(inventoryX, inventoryY);
        this.RepositionLowerPanel(menu);
        menu.storageSpaceTopBorderOffset = 0;

        if (menu.trashCan is not null)
        {
            menu.trashCan.bounds.X = menu.ItemsToGrabMenu.xPositionOnScreen + menu.ItemsToGrabMenu.width + IClickableMenu.borderWidth * 2;
            menu.trashCan.bounds.Y = menu.yPositionOnScreen + menu.height - 192 - 32 - IClickableMenu.borderWidth - 104;
        }

        if (menu.okButton is not null)
        {
            menu.okButton.bounds.X = menu.ItemsToGrabMenu.xPositionOnScreen + menu.ItemsToGrabMenu.width + IClickableMenu.borderWidth * 2;
            menu.okButton.bounds.Y = menu.yPositionOnScreen + menu.height - 192 - IClickableMenu.borderWidth;
        }

        if (menu.dropItemInvisibleButton is not null)
        {
            menu.dropItemInvisibleButton.bounds.X = menu.inventory.xPositionOnScreen - IClickableMenu.borderWidth - IClickableMenu.spaceToClearSideBorder - 128;
            menu.dropItemInvisibleButton.bounds.Y = menu.inventory.yPositionOnScreen - 12;
        }

        this.PrepareChestClickableComponents(menu);
        menu.RepositionSideButtons();
        menu.SetupBorderNeighbors();
        menu.populateClickableComponentList();
        this.LogDebug($"[ApplyLayout] chest={menu.ItemsToGrabMenu.rows}x{menu.ItemsToGrabMenu.inventory.Count / menu.ItemsToGrabMenu.rows} slots={menu.ItemsToGrabMenu.inventory.Count} inv={menu.inventory.inventory.Count} total={menu.allClickableComponents?.Count ?? 0}");

        this.SetLayoutState(menu, new ChestMenuLayoutState(chestPanelTop, chestPanelTop)
        {
            ChestPanelBounds = new Rectangle(
                menu.ItemsToGrabMenu.xPositionOnScreen,
                menu.ItemsToGrabMenu.yPositionOnScreen,
                menu.ItemsToGrabMenu.width,
                menu.ItemsToGrabMenu.height
            )
        });
        this.ReanchorColorPickerStripCore(menu, chestPanelTop);
    }

    private void ApplyAutoGrabberLayout(ItemGrabMenu menu, Chest internalChest, StardewValley.Object owner)
    {
        int cols;
        int rows;
        lock (ConfigLock)
        {
            cols = this.config.AutoGrabberColumns;
            rows = this.config.AutoGrabberRows;
        }

        IList<Item> items = internalChest.Items;
        int itemCount = items.Count(item => item is not null);
        int visibleRows = rows;
        int visibleCapacity = cols * rows;

        if (itemCount > visibleCapacity)
        {
            visibleRows = (itemCount + cols - 1) / cols;
            visibleCapacity = visibleRows * cols;
        }

        EnsureInventoryCapacity(items, visibleCapacity);

        int menuWidth = System.Math.Max(
            DefaultMenuWidth,
            cols * 64 + (IClickableMenu.borderWidth + IClickableMenu.spaceToClearSideBorder) * 2
        );
        int chestMenuHeight = this.GetChestMenuHeight(visibleRows);
        int chestPanelHeight = chestMenuHeight + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth * 2;
        int assemblyHeight = chestPanelHeight + PanelGap + LowerPanelHeight;
        int menuX = Game1.uiViewport.Width / 2 - menuWidth / 2;
        int chestPanelTop = System.Math.Max(16, (Game1.uiViewport.Height - assemblyHeight) / 2);
        int lowerPanelTop = chestPanelTop + chestPanelHeight + PanelGap + this.config.InventoryPanelGapOffset;
        int chestX = menuX + InventoryHorizontalInset;
        int chestY = chestPanelTop + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder;
        int lowerPanelLeft = menuX - IClickableMenu.borderWidth / 2;
        int inventoryX = lowerPanelLeft + (menuWidth - menu.inventory.width) / 2;
        int inventoryY = lowerPanelTop + LowerInventoryTopPadding + PlayerInventoryFirstRowLift;

        menu.xPositionOnScreen = menuX;
        menu.yPositionOnScreen = lowerPanelTop - LowerPanelTopOffset;
        menu.width = menuWidth;
        menu.height = LowerPanelHeight + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192;
        menu.ItemsToGrabMenu = new InventoryMenu(
            chestX, chestY,
            playerInventory: false,
            items,
            menu.inventory.highlightMethod,
            visibleCapacity, visibleRows
        );
        menu.ItemsToGrabMenu.height = chestMenuHeight;

        menu.inventory.SetPosition(inventoryX, inventoryY);
        this.RepositionLowerPanel(menu);
        menu.storageSpaceTopBorderOffset = 0;

        if (menu.trashCan is not null)
        {
            menu.trashCan.bounds.X = menu.ItemsToGrabMenu.xPositionOnScreen + menu.ItemsToGrabMenu.width + IClickableMenu.borderWidth * 2;
            menu.trashCan.bounds.Y = menu.yPositionOnScreen + menu.height - 192 - 32 - IClickableMenu.borderWidth - 104;
        }

        if (menu.okButton is not null)
        {
            menu.okButton.bounds.X = menu.ItemsToGrabMenu.xPositionOnScreen + menu.ItemsToGrabMenu.width + IClickableMenu.borderWidth * 2;
            menu.okButton.bounds.Y = menu.yPositionOnScreen + menu.height - 192 - IClickableMenu.borderWidth;
        }

        if (menu.dropItemInvisibleButton is not null)
        {
            menu.dropItemInvisibleButton.bounds.X = menu.inventory.xPositionOnScreen - IClickableMenu.borderWidth - IClickableMenu.spaceToClearSideBorder - 128;
            menu.dropItemInvisibleButton.bounds.Y = menu.inventory.yPositionOnScreen - 12;
        }

        this.PrepareChestClickableComponents(menu);
        menu.RepositionSideButtons();
        menu.SetupBorderNeighbors();
        menu.populateClickableComponentList();
        this.SetLayoutState(menu, new ChestMenuLayoutState(chestPanelTop, chestPanelTop)
        {
            ChestPanelBounds = new Rectangle(
                menu.ItemsToGrabMenu.xPositionOnScreen,
                menu.ItemsToGrabMenu.yPositionOnScreen,
                menu.ItemsToGrabMenu.width,
                menu.ItemsToGrabMenu.height
            ),
            IsAutoGrabber = true
        });
        this.ReanchorColorPickerStripCore(menu, chestPanelTop);

        RestoreAutoGrabberMenuIdentity(menu, internalChest, owner);

        // Remove color picker — ShowMenu() creates one when sourceItem is a Chest
        menu.chestColorPicker = null;
        menu.colorPickerToggleButton = null;

        if (IsUnlimitedStorageLoaded())
        {
            Patches.ApplyUnlimitedStorageLayout(menu);
        }
    }

    private static void RestoreAutoGrabberMenuIdentity(ItemGrabMenu menu, Chest internalChest, StardewValley.Object owner)
    {
        RegisterAutoGrabberInternalChest(owner);

        // Restore context so other mods see this as an auto-grabber
        menu.context = owner;

        // Restore behaviorOnItemGrab so taking items out uses auto-grabber logic
        MethodInfo? grabMethod = AccessTools.Method(typeof(StardewValley.Object), "grabItemFromAutoGrabber");
        if (grabMethod is not null)
        {
            menu.behaviorOnItemGrab = (ItemGrabMenu.behaviorOnItemSelect)
                Delegate.CreateDelegate(typeof(ItemGrabMenu.behaviorOnItemSelect), owner, grabMethod);
        }

        menu.sourceItem = owner;
    }

    private static int SafeAdd(int value, int addend)
    {
        int result = value + addend;
        if (addend > 0 && result < value)
            return int.MaxValue;
        if (addend < 0 && result > value)
            return int.MinValue;
        return result;
    }

    private static void EnsureInventoryCapacity(IList<Item> items, int capacity)
    {
        while (items.Count < capacity)
        {
            items.Add(null!);
        }
    }

    private void PrepareChestClickableComponents(ItemGrabMenu menu)
    {
        menu.ItemsToGrabMenu.populateClickableComponentList();

        foreach (ClickableComponent component in menu.ItemsToGrabMenu.inventory)
        {
            component.myID = SafeAdd(component.myID, ItemGrabMenu.region_itemsToGrabMenuModifier);
            component.upNeighborID = SafeAdd(component.upNeighborID, ItemGrabMenu.region_itemsToGrabMenuModifier);
            component.rightNeighborID = SafeAdd(component.rightNeighborID, ItemGrabMenu.region_itemsToGrabMenuModifier);
            component.leftNeighborID = SafeAdd(component.leftNeighborID, ItemGrabMenu.region_itemsToGrabMenuModifier);
            component.downNeighborID = SafeAdd(component.downNeighborID, ItemGrabMenu.region_itemsToGrabMenuModifier);
        }
    }

    internal static void ReanchorColorPickerStrip(ItemGrabMenu menu)
    {
        if (!TryGetLayoutState(menu, out ChestMenuLayoutState state))
        {
            return;
        }

        Instance.ReanchorColorPickerStripCore(menu, state.ChestPanelTop);
    }

    private void ReanchorColorPickerStripCore(ItemGrabMenu menu, int chestPanelTop)
    {
        if (menu.chestColorPicker is null || menu.discreteColorPickerCC is null || menu.discreteColorPickerCC.Count == 0)
        {
            return;
        }

        int xOffset;
        int yOffset;
        lock (ConfigLock)
        {
            xOffset = this.config.ColorPickerXOffset;
            yOffset = this.config.ColorPickerYOffset;
        }

        int chestPanelLeft = menu.ItemsToGrabMenu.xPositionOnScreen - IClickableMenu.spaceToClearSideBorder - IClickableMenu.borderWidth;
        int chestPanelWidth = menu.ItemsToGrabMenu.width + IClickableMenu.spaceToClearSideBorder * 2 + IClickableMenu.borderWidth * 2;
        int targetX = chestPanelLeft + (chestPanelWidth - menu.chestColorPicker.width) / 2 + xOffset;
        int targetY = chestPanelTop - 64 - IClickableMenu.borderWidth + yOffset;
        int deltaX = targetX - menu.chestColorPicker.xPositionOnScreen;
        int deltaY = targetY - menu.chestColorPicker.yPositionOnScreen;

        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        menu.chestColorPicker.xPositionOnScreen = targetX;
        menu.chestColorPicker.yPositionOnScreen = targetY;

        foreach (ClickableComponent component in menu.discreteColorPickerCC)
        {
            component.bounds.X += deltaX;
            component.bounds.Y += deltaY;
        }
    }

    private void RepositionLowerPanel(ItemGrabMenu menu)
    {
        int top = int.MaxValue;
        int bottom = int.MinValue;

        this.LogDebug($"[RepositionLowerPanel] inventory.Count={menu.inventory.inventory.Count} capacity={menu.inventory.capacity} rows={menu.inventory.rows}");

        foreach (ClickableComponent slot in menu.inventory.inventory)
        {
            if (slot.bounds.Width <= 0 || slot.bounds.Height <= 0)
            {
                continue;
            }

            top = System.Math.Min(top, slot.bounds.Top);
            bottom = System.Math.Max(bottom, slot.bounds.Bottom);
        }

        if (top == int.MaxValue || bottom == int.MinValue)
        {
            this.LogDebug($"[RepositionLowerPanel] No valid slots found, skipping.");
            return;
        }

        int lowerBackgroundTop = top - (LowerBackgroundTopPadding + this.config.InventoryBackgroundTopOffset);
        int lowerBackgroundBottom = bottom + LowerBackgroundBottomPadding + this.config.InventoryBackgroundBottomOffset;
        menu.yPositionOnScreen = lowerBackgroundTop - LowerPanelTopOffset;
        menu.height = lowerBackgroundBottom - lowerBackgroundTop + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192;

        this.LogDebug($"[RepositionLowerPanel] top={top} bottom={bottom} menu.yPositionOnScreen={menu.yPositionOnScreen} menu.height={menu.height}");
    }

    private int GetChestsAnywhereWidgetXOffsetCore()
    {
        lock (ConfigLock)
        {
            return this.config.ChestsAnywhereWidgetXOffset;
        }
    }

    private int GetChestsAnywhereWidgetYOffsetCore()
    {
        lock (ConfigLock)
        {
            return this.config.ChestsAnywhereWidgetYOffset;
        }
    }

    private void SanitizeConfig()
    {
        this.config.RegularChestColumns = this.Clamp(this.config.RegularChestColumns, MinRegularChestColumns, MaxColumns, nameof(this.config.RegularChestColumns));
        this.config.RegularChestRows = this.Clamp(this.config.RegularChestRows, MinRegularChestRows, MaxRows, nameof(this.config.RegularChestRows));
        this.config.BigChestColumns = this.Clamp(this.config.BigChestColumns, MinBigChestColumns, MaxColumns, nameof(this.config.BigChestColumns));
        this.config.BigChestRows = this.Clamp(this.config.BigChestRows, MinBigChestRows, MaxRows, nameof(this.config.BigChestRows));
        this.config.BigStoneChestColumns = this.Clamp(this.config.BigStoneChestColumns, MinBigStoneChestColumns, MaxColumns, nameof(this.config.BigStoneChestColumns));
        this.config.BigStoneChestRows = this.Clamp(this.config.BigStoneChestRows, MinBigStoneChestRows, MaxRows, nameof(this.config.BigStoneChestRows));
        this.config.StoneChestColumns = this.Clamp(this.config.StoneChestColumns, MinStoneChestColumns, MaxColumns, nameof(this.config.StoneChestColumns));
        this.config.StoneChestRows = this.Clamp(this.config.StoneChestRows, MinStoneChestRows, MaxRows, nameof(this.config.StoneChestRows));
        this.config.FridgeColumns = this.Clamp(this.config.FridgeColumns, MinFridgeColumns, MaxColumns, nameof(this.config.FridgeColumns));
        this.config.FridgeRows = this.Clamp(this.config.FridgeRows, MinFridgeRows, MaxRows, nameof(this.config.FridgeRows));
        this.config.MiniFridgeColumns = this.Clamp(this.config.MiniFridgeColumns, MinMiniFridgeColumns, MaxColumns, nameof(this.config.MiniFridgeColumns));
        this.config.MiniFridgeRows = this.Clamp(this.config.MiniFridgeRows, MinMiniFridgeRows, MaxRows, nameof(this.config.MiniFridgeRows));
        this.config.JunimoChestColumns = this.Clamp(this.config.JunimoChestColumns, MinJunimoChestColumns, MaxColumns, nameof(this.config.JunimoChestColumns));
        this.config.JunimoChestRows = this.Clamp(this.config.JunimoChestRows, MinJunimoChestRows, MaxRows, nameof(this.config.JunimoChestRows));
        this.config.AutoGrabberColumns = this.Clamp(this.config.AutoGrabberColumns, MinAutoGrabberColumns, MaxColumns, nameof(this.config.AutoGrabberColumns));
        this.config.AutoGrabberRows = this.Clamp(this.config.AutoGrabberRows, MinAutoGrabberRows, MaxRows, nameof(this.config.AutoGrabberRows));
        this.config.ChestBackgroundHeightOffset = this.Clamp(this.config.ChestBackgroundHeightOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.ChestBackgroundHeightOffset));
        this.config.InventoryPanelGapOffset = this.Clamp(this.config.InventoryPanelGapOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.InventoryPanelGapOffset));
        this.config.InventoryBackgroundTopOffset = this.Clamp(this.config.InventoryBackgroundTopOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.InventoryBackgroundTopOffset));
        this.config.InventoryBackgroundBottomOffset = this.Clamp(this.config.InventoryBackgroundBottomOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.InventoryBackgroundBottomOffset));
        this.config.ChestsAnywhereWidgetXOffset = this.Clamp(this.config.ChestsAnywhereWidgetXOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.ChestsAnywhereWidgetXOffset));
        this.config.ChestsAnywhereWidgetYOffset = this.Clamp(this.config.ChestsAnywhereWidgetYOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.ChestsAnywhereWidgetYOffset));
        this.config.ColorPickerXOffset = this.Clamp(this.config.ColorPickerXOffset, MinColorPickerOffset, MaxColorPickerOffset, nameof(this.config.ColorPickerXOffset));
        this.config.ColorPickerYOffset = this.Clamp(this.config.ColorPickerYOffset, MinColorPickerOffset, MaxColorPickerOffset, nameof(this.config.ColorPickerYOffset));
        this.config.UnlimitedStorageSearchXOffset = this.Clamp(this.config.UnlimitedStorageSearchXOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.UnlimitedStorageSearchXOffset));
        this.config.UnlimitedStorageSearchYOffset = this.Clamp(this.config.UnlimitedStorageSearchYOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.UnlimitedStorageSearchYOffset));
        this.config.UnlimitedStorageSearchLeftOffset = this.Clamp(this.config.UnlimitedStorageSearchLeftOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.UnlimitedStorageSearchLeftOffset));
        this.config.UnlimitedStorageSearchRightOffset = this.Clamp(this.config.UnlimitedStorageSearchRightOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.UnlimitedStorageSearchRightOffset));
        this.config.ConvenientChestsXOffset = this.Clamp(this.config.ConvenientChestsXOffset, MinConvenientChestsOffset, MaxConvenientChestsOffset, nameof(this.config.ConvenientChestsXOffset));
        this.config.ConvenientChestsYOffset = this.Clamp(this.config.ConvenientChestsYOffset, MinConvenientChestsOffset, MaxConvenientChestsOffset, nameof(this.config.ConvenientChestsYOffset));
        this.config.CategorizeChestsXOffset = this.Clamp(this.config.CategorizeChestsXOffset, MinConvenientChestsOffset, MaxConvenientChestsOffset, nameof(this.config.CategorizeChestsXOffset));
        this.config.CategorizeChestsYOffset = this.Clamp(this.config.CategorizeChestsYOffset, MinConvenientChestsOffset, MaxConvenientChestsOffset, nameof(this.config.CategorizeChestsYOffset));
        this.config.RemoteFridgeStorageXOffset = this.Clamp(this.config.RemoteFridgeStorageXOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.RemoteFridgeStorageXOffset));
        this.config.RemoteFridgeStorageYOffset = this.Clamp(this.config.RemoteFridgeStorageYOffset, MinLayoutOffset, MaxLayoutOffset, nameof(this.config.RemoteFridgeStorageYOffset));
        this.config.TintChestUIOpacity = this.Clamp(this.config.TintChestUIOpacity, 0, 100, nameof(this.config.TintChestUIOpacity));
        this.config.TintChestUIPaddingLeft = this.Clamp(this.config.TintChestUIPaddingLeft, -200, 200, nameof(this.config.TintChestUIPaddingLeft));
        this.config.TintChestUIPaddingRight = this.Clamp(this.config.TintChestUIPaddingRight, -200, 200, nameof(this.config.TintChestUIPaddingRight));
        this.config.TintChestUIPaddingTop = this.Clamp(this.config.TintChestUIPaddingTop, -200, 200, nameof(this.config.TintChestUIPaddingTop));
        this.config.TintChestUIPaddingBottom = this.Clamp(this.config.TintChestUIPaddingBottom, -200, 200, nameof(this.config.TintChestUIPaddingBottom));
    }

    private void SetRegularChestColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.RegularChestColumns = value;
        }
    }

    private void SetRegularChestRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.RegularChestRows = value;
        }
    }

    private void SetBigChestColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.BigChestColumns = value;
        }
    }

    private void SetBigChestRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.BigChestRows = value;
        }
    }

    private void SetBigStoneChestColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.BigStoneChestColumns = value;
        }
    }

    private void SetBigStoneChestRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.BigStoneChestRows = value;
        }
    }

    private void SetStoneChestColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.StoneChestColumns = value;
        }
    }

    private void SetStoneChestRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.StoneChestRows = value;
        }
    }

    private void SetFridgeColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.FridgeColumns = value;
        }
    }

    private void SetFridgeRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.FridgeRows = value;
        }
    }

    private void SetMiniFridgeColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.MiniFridgeColumns = value;
        }
    }

    private void SetMiniFridgeRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.MiniFridgeRows = value;
        }
    }

    private void SetJunimoChestColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.JunimoChestColumns = value;
        }
    }

    private void SetJunimoChestRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.JunimoChestRows = value;
        }
    }

    private void SetAutoGrabberColumns(int value)
    {
        lock (ConfigLock)
        {
            this.config.AutoGrabberColumns = value;
        }
    }

    private void SetAutoGrabberRows(int value)
    {
        lock (ConfigLock)
        {
            this.config.AutoGrabberRows = value;
        }
    }

    private void RefreshActiveChestMenu()
    {
        if (Game1.activeClickableMenu is ItemGrabMenu menu)
        {
            this.ApplyLayoutIfNeededCore(menu);
            if (IsUnlimitedStorageLoaded())
            {
                Patches.ApplyUnlimitedStorageLayout(menu);
            }
        }
    }

    private void PatchChestsAnywhereCompatibility()
    {
        if (!this.Helper.ModRegistry.IsLoaded("Pathoschild.ChestsAnywhere") || this.harmony is null)
        {
            return;
        }

        Type? overlayType = AccessTools.TypeByName("Pathoschild.Stardew.ChestsAnywhere.Menus.Overlays.BaseChestOverlay");
        Type? chestOverlayType = AccessTools.TypeByName("Pathoschild.Stardew.ChestsAnywhere.Menus.Overlays.ChestOverlay");
        System.Reflection.MethodInfo? method = AccessTools.Method(overlayType, "ReinitializeBaseComponents");
        System.Reflection.MethodInfo? topOffsetMethod = AccessTools.Method(chestOverlayType, "GetTopOffset");
        if (method is not null)
        {
            this.harmony.Patch(
                method,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ChestsAnywhere_ReinitializeBaseComponents_Postfix))
            );
        }
        if (topOffsetMethod is not null)
        {
            this.harmony.Patch(
                topOffsetMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ChestsAnywhere_GetTopOffset_Postfix))
            );
        }
    }

    private void PatchConvenientChestsCompatibility()
    {
        if (!this.Helper.ModRegistry.IsLoaded(ConvenientChestsId) || this.harmony is null)
        {
            return;
        }

        Type? chestOverlayType = AccessTools.TypeByName("ConvenientChests.CategorizeChests.Interface.Widgets.ChestOverlay");
        System.Reflection.MethodInfo? positionButtonsMethod = AccessTools.Method(chestOverlayType, "PositionButtons");
        System.Reflection.MethodInfo? openCategoryMenuMethod = AccessTools.Method(chestOverlayType, "OpenCategoryMenu");
        System.Reflection.MethodInfo? drawMethod = AccessTools.Method(chestOverlayType, "Draw");
        if (positionButtonsMethod is not null)
        {
            this.harmony.Patch(
                positionButtonsMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ConvenientChests_PositionButtons_Postfix))
            );
        }
        if (openCategoryMenuMethod is not null)
        {
            this.harmony.Patch(
                openCategoryMenuMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ConvenientChests_OpenCategoryMenu_Postfix))
            );
        }
        if (drawMethod is not null)
        {
            this.harmony.Patch(
                drawMethod,
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.ConvenientChests_Draw_Prefix))
            );
        }
    }

    private void PatchCategorizeChestsCompatibility()
    {
        if (!this.Helper.ModRegistry.IsLoaded(CategorizeChestsId) || this.harmony is null)
        {
            return;
        }

        Type? chestOverlayType = AccessTools.TypeByName("StardewValleyMods.CategorizeChests.Interface.Widgets.ChestOverlay");
        Type? categorizeChestsModType = AccessTools.TypeByName("StardewValleyMods.CategorizeChests.CategorizeChestsMod");
        System.Reflection.MethodInfo? createMenuMethod = AccessTools.Method(categorizeChestsModType, "CreateMenu");
        System.Reflection.MethodInfo? positionButtonsMethod = AccessTools.Method(chestOverlayType, "PositionButtons");
        System.Reflection.MethodInfo? openCategoryMenuMethod = AccessTools.Method(chestOverlayType, "OpenCategoryMenu");

        if (createMenuMethod is not null)
        {
            this.harmony.Patch(
                createMenuMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.CategorizeChests_CreateMenu_Postfix))
            );
        }

        if (positionButtonsMethod is not null)
        {
            this.harmony.Patch(
                positionButtonsMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.CategorizeChests_PositionButtons_Postfix))
            );
        }

        if (openCategoryMenuMethod is not null)
        {
            this.harmony.Patch(
                openCategoryMenuMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.CategorizeChests_OpenCategoryMenu_Postfix))
            );
        }
    }

    private void PatchRemoteFridgeStorageCompatibility()
    {
        if (!this.Helper.ModRegistry.IsLoaded(RemoteFridgeStorageId) || this.harmony is null)
        {
            return;
        }

        Type? chestControllerType = AccessTools.TypeByName("RemoteFridgeStorage.controller.ChestController");
        System.Reflection.MethodInfo? updateButtonPositionMethod = AccessTools.Method(chestControllerType, "UpdateButtonPosition");
        System.Reflection.MethodInfo? drawFridgeIconMethod = AccessTools.Method(chestControllerType, "DrawFridgeIcon");
        if (updateButtonPositionMethod is not null)
        {
            this.harmony.Patch(
                updateButtonPositionMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.RemoteFridgeStorage_UpdateButtonPosition_Postfix))
            );
        }

        if (drawFridgeIconMethod is not null)
        {
            this.harmony.Patch(
                drawFridgeIconMethod,
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.RemoteFridgeStorage_DrawFridgeIcon_Prefix))
            );
        }
    }

    private void InitUnlimitedStorageCompatibility()
    {
        if (!this.Helper.ModRegistry.IsLoaded(UnlimitedStorageId) || this.harmony is null)
        {
            return;
        }

        Patches.InitUnlimitedStorageCompat();

        Type? unlimitedStorageModEntryType = AccessTools.TypeByName("LeFauxMods.UnlimitedStorage.ModEntry");
        System.Reflection.MethodInfo? onMenuChangedMethod = AccessTools.Method(unlimitedStorageModEntryType, "OnMenuChanged");
        System.Reflection.MethodInfo? onRenderedActiveMenuMethod = AccessTools.Method(unlimitedStorageModEntryType, "OnRenderedActiveMenu");

        if (onMenuChangedMethod is not null)
        {
            this.harmony.Patch(
                onMenuChangedMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.UnlimitedStorage_OnMenuChanged_Postfix))
            );
        }

        if (onRenderedActiveMenuMethod is not null)
        {
            this.harmony.Patch(
                onRenderedActiveMenuMethod,
                postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.UnlimitedStorage_OnRenderedActiveMenu_Postfix))
            );
        }

        System.Reflection.MethodInfo? textBoxHoverMethod = AccessTools.Method(typeof(TextBox), nameof(TextBox.Hover));
        if (textBoxHoverMethod is not null)
        {
            this.harmony.Patch(
                textBoxHoverMethod,
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.TextBox_Hover_Prefix))
            );
        }
    }

    internal static bool IsAutoGrabber(object? target)
    {
        return IsAutoGrabberObject(target);
    }

    private static bool IsAutoGrabberObject(object? target)
    {
        return target is StardewValley.Object obj && (obj.QualifiedItemId == "(BC)165" || obj.ParentSheetIndex == 165);
    }

    internal static bool TryGetAutoGrabberOwner(Chest chest, out StardewValley.Object owner)
    {
        if (AutoGrabberChests.TryGetValue(chest, out StardewValley.Object? existingOwner) && existingOwner is not null)
        {
            owner = existingOwner;
            return true;
        }

        return TryRegisterAutoGrabberHoldingChest(chest, out owner);
    }

    private static bool TryResolveAutoGrabberMenu(ItemGrabMenu menu, out Chest internalChest, out StardewValley.Object owner)
    {
        if (TryResolveAutoGrabberValue(menu.context, out internalChest, out owner))
        {
            return true;
        }

        if (TryResolveAutoGrabberValue(menu.sourceItem, out internalChest, out owner))
        {
            return true;
        }

        internalChest = null!;
        owner = null!;
        return false;
    }

    private static bool TryResolveAutoGrabberValue(object? value, out Chest internalChest, out StardewValley.Object owner)
    {
        if (value is StardewValley.Object obj && IsAutoGrabberObject(obj) && obj.heldObject.Value is Chest heldChest)
        {
            RegisterAutoGrabberInternalChest(obj);
            internalChest = heldChest;
            owner = obj;
            return true;
        }

        if (value is Chest chest && TryGetAutoGrabberOwner(chest, out owner))
        {
            internalChest = chest;
            return true;
        }

        internalChest = null!;
        owner = null!;
        return false;
    }

    private void RegisterAutoGrabbersInLoadedLocations()
    {
        foreach (GameLocation location in Game1.locations)
        {
            this.RegisterAutoGrabbersInLocation(location);
        }
    }

    private void RegisterAutoGrabbersInLocation(GameLocation location)
    {
        foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
        {
            RegisterAutoGrabberInternalChest(pair.Value);
        }
    }

    private static bool RegisterAutoGrabberInternalChest(object? target)
    {
        if (target is not StardewValley.Object obj || !IsAutoGrabberObject(obj) || obj.heldObject.Value is not Chest internalChest)
        {
            return false;
        }

        AutoGrabberChests.GetValue(internalChest, _ => obj);
        return true;
    }

    private static bool TryRegisterAutoGrabberHoldingChest(Chest chest, out StardewValley.Object owner)
    {
        if (!Context.IsWorldReady)
        {
            owner = null!;
            return false;
        }

        foreach (GameLocation location in Game1.locations)
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
            {
                StardewValley.Object obj = pair.Value;
                if (IsAutoGrabberObject(obj) && ReferenceEquals(obj.heldObject.Value, chest))
                {
                    AutoGrabberChests.GetValue(chest, _ => obj);
                    owner = obj;
                    return true;
                }
            }
        }

        owner = null!;
        return false;
    }

    private static void UnregisterAutoGrabberInternalChest(object? target)
    {
        if (target is StardewValley.Object obj && IsAutoGrabberObject(obj) && obj.heldObject.Value is Chest internalChest)
        {
            AutoGrabberChests.Remove(internalChest);
        }
    }

    internal static bool TryGetLayoutState(ItemGrabMenu menu, out ChestMenuLayoutState state)
    {
        if (LayoutStates.TryGetValue(menu, out ChestMenuLayoutState? existing))
        {
            state = existing;
            return true;
        }

        state = null!;
        return false;
    }

    private void SetLayoutState(ItemGrabMenu menu, ChestMenuLayoutState state)
    {
        LayoutStates.Remove(menu);
        LayoutStates.Add(menu, state);
    }

    private int GetChestMenuHeight(int rows)
    {
        // InventoryMenu slot rows are spaced by 68px, with a small vanilla-style bottom margin.
        return rows * 68 + 4 + this.config.ChestBackgroundHeightOffset;
    }

    private int Clamp(int value, int min, int max, string fieldName)
    {
        int clamped = System.Math.Clamp(value, min, max);
        if (clamped != value)
        {
            this.Monitor.Log(this.Translate("config.clamped", new { fieldName, value, clamped }), LogLevel.Warn);
        }

        return clamped;
    }

}
