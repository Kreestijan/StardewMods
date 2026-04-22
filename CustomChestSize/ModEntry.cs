using HarmonyLib;
using System.Runtime.CompilerServices;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace CustomChestSize;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";
    private const string UnlimitedStorageId = "furyx639.UnlimitedStorage";
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
    private const int MaxColumns = 24;
    private const int MaxRows = 12;
    private const int MinLayoutOffset = -200;
    private const int MaxLayoutOffset = 200;
    private const int MinColorPickerOffset = -1000;
    private const int MaxColorPickerOffset = 1000;
    private static readonly ConditionalWeakTable<ItemGrabMenu, ChestMenuLayoutState> LayoutStates = new();

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

        helper.ConsoleCommands.Add(
            "ccs_reload",
            "Reload Custom Chest Size config.json without restarting the game.",
            this.ReloadConfigCommand
        );

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
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

    internal static bool IsUnlimitedStorageLoaded()
    {
        return Instance.Helper.ModRegistry.IsLoaded(UnlimitedStorageId);
    }

    private void ReloadConfigCommand(string command, string[] args)
    {
        this.LoadConfig();
        this.Monitor.Log("Reloaded config.json.", LogLevel.Info);
        this.RefreshActiveChestMenu();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RegisterGenericModConfigMenu();
        this.PatchChestsAnywhereCompatibility();
        this.InitUnlimitedStorageCompatibility();
    }

    private void RegisterGenericModConfigMenu()
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        bool hasChestsAnywhere = this.Helper.ModRegistry.IsLoaded("Pathoschild.ChestsAnywhere");
        bool hasUnlimitedStorage = this.Helper.ModRegistry.IsLoaded(UnlimitedStorageId);

        gmcm.Register(
            this.ModManifest,
            reset: this.ResetConfig,
            save: this.SaveConfig,
            titleScreenOnly: false
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: () => "Regular chest",
            tooltip: () => "Configure vanilla regular chest size."
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.RegularChestColumns,
            setValue: this.SetRegularChestColumns,
            name: () => "Columns",
            tooltip: () => "How many slots each row has in a regular chest.",
            min: MinRegularChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.RegularChestRows,
            setValue: this.SetRegularChestRows,
            name: () => "Rows",
            tooltip: () => "How many rows a regular chest has.",
            min: MinRegularChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: () => "Big chest",
            tooltip: () => "Configure vanilla big chest size."
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.BigChestColumns,
            setValue: this.SetBigChestColumns,
            name: () => "Columns",
            tooltip: () => "How many slots each row has in a big chest.",
            min: MinBigChestColumns,
            max: MaxColumns,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.BigChestRows,
            setValue: this.SetBigChestRows,
            name: () => "Rows",
            tooltip: () => "How many rows a big chest has.",
            min: MinBigChestRows,
            max: MaxRows,
            interval: 1
        );

        gmcm.AddSectionTitle(
            this.ModManifest,
            text: () => "Layout tuning",
            tooltip: () => "Use these offsets to tune menu backgrounds in-game. Slot positions stay unchanged unless noted."
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.ChestBackgroundHeightOffset,
            setValue: value => this.config.ChestBackgroundHeightOffset = value,
            name: () => "Chest bg height",
            tooltip: () => "Adds or removes height from the chest background only.",
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.InventoryPanelGapOffset,
            setValue: value => this.config.InventoryPanelGapOffset = value,
            name: () => "Inventory gap",
            tooltip: () => "Moves the lower inventory panel background closer to or farther from the chest.",
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.InventoryBackgroundTopOffset,
            setValue: value => this.config.InventoryBackgroundTopOffset = value,
            name: () => "Inventory bg top",
            tooltip: () => "Extends the lower background upward from the inventory slots.",
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.InventoryBackgroundBottomOffset,
            setValue: value => this.config.InventoryBackgroundBottomOffset = value,
            name: () => "Inventory bg bottom",
            tooltip: () => "Extends the lower background downward from the inventory slots.",
            min: MinLayoutOffset,
            max: MaxLayoutOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.ColorPickerXOffset,
            setValue: value => this.config.ColorPickerXOffset = value,
            name: () => "Color picker X",
            tooltip: () => "Moves the opened chest color picker left or right relative to the resized chest.",
            min: MinColorPickerOffset,
            max: MaxColorPickerOffset,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.ColorPickerYOffset,
            setValue: value => this.config.ColorPickerYOffset = value,
            name: () => "Color picker Y",
            tooltip: () => "Moves the opened chest color picker up or down relative to the resized chest.",
            min: MinColorPickerOffset,
            max: MaxColorPickerOffset,
            interval: 4
        );
        if (hasChestsAnywhere)
        {
            gmcm.AddSectionTitle(
                this.ModManifest,
                text: () => "Chests Anywhere",
                tooltip: () => "These offsets move Chests Anywhere's chest overlay widgets when that mod is installed."
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.config.ChestsAnywhereWidgetXOffset,
                setValue: value => this.config.ChestsAnywhereWidgetXOffset = value,
                name: () => "CA widget X",
                tooltip: () => "Moves the Chests Anywhere overlay widgets left or right.",
                min: MinLayoutOffset,
                max: MaxLayoutOffset,
                interval: 4
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.config.ChestsAnywhereWidgetYOffset,
                setValue: value => this.config.ChestsAnywhereWidgetYOffset = value,
                name: () => "CA widget Y",
                tooltip: () => "Moves the Chests Anywhere overlay widgets up or down.",
                min: MinLayoutOffset,
                max: MaxLayoutOffset,
                interval: 4
            );
        }

        if (hasUnlimitedStorage)
        {
            gmcm.AddSectionTitle(
                this.ModManifest,
                text: () => "Unlimited Storage",
                tooltip: () => "These offsets move Unlimited Storage's search field when that mod is installed."
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.config.UnlimitedStorageSearchXOffset,
                setValue: value => this.config.UnlimitedStorageSearchXOffset = value,
                name: () => "Search X",
                tooltip: () => "Moves Unlimited Storage's search field left or right.",
                min: MinLayoutOffset,
                max: MaxLayoutOffset,
                interval: 4
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.config.UnlimitedStorageSearchYOffset,
                setValue: value => this.config.UnlimitedStorageSearchYOffset = value,
                name: () => "Search Y",
                tooltip: () => "Moves Unlimited Storage's search field up or down.",
                min: MinLayoutOffset,
                max: MaxLayoutOffset,
                interval: 4
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.config.UnlimitedStorageSearchLeftOffset,
                setValue: value => this.config.UnlimitedStorageSearchLeftOffset = value,
                name: () => "Search left",
                tooltip: () => "Extends or shrinks the left side of Unlimited Storage's search field.",
                min: MinLayoutOffset,
                max: MaxLayoutOffset,
                interval: 4
            );
            gmcm.AddNumberOption(
                this.ModManifest,
                getValue: () => this.config.UnlimitedStorageSearchRightOffset,
                setValue: value => this.config.UnlimitedStorageSearchRightOffset = value,
                name: () => "Search right",
                tooltip: () => "Extends or shrinks the right side of Unlimited Storage's search field.",
                min: MinLayoutOffset,
                max: MaxLayoutOffset,
                interval: 4
            );
        }

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
    }

    private void SaveConfig()
    {
        lock (ConfigLock)
        {
            this.SanitizeConfig();
            this.Helper.WriteConfig(this.config);
        }

        this.RefreshActiveChestMenu();
    }

    private void ResetConfig()
    {
        lock (ConfigLock)
        {
            this.config = new ModConfig();
        }

        this.SaveConfig();
    }

    private bool TryGetConfiguredLayoutCore(Chest chest, out ChestGridLayout layout)
    {
        lock (ConfigLock)
        {
            switch (chest.SpecialChestType)
            {
                case Chest.SpecialChestTypes.BigChest:
                    layout = new ChestGridLayout(this.config.BigChestColumns, this.config.BigChestRows);
                    return true;

                case Chest.SpecialChestTypes.None when chest.playerChest.Value:
                    layout = new ChestGridLayout(this.config.RegularChestColumns, this.config.RegularChestRows);
                    return true;

                default:
                    layout = default;
                    return false;
            }
        }
    }

    private void ApplyLayoutIfNeededCore(ItemGrabMenu menu)
    {
        if (menu.source != ItemGrabMenu.source_chest || menu.sourceItem is not Chest chest)
        {
            return;
        }

        if (!this.TryGetConfiguredLayoutCore(chest, out ChestGridLayout configuredLayout))
        {
            return;
        }

        chest.clearNulls();

        int visibleRows = configuredLayout.Rows;
        int visibleCapacity = configuredLayout.Capacity;
        int currentItemCount = chest.GetItemsForPlayer().Count;
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
        this.SetLayoutState(menu, new ChestMenuLayoutState(chestPanelTop, chestPanelTop));
        this.ReanchorColorPickerStripCore(menu, chestPanelTop);

        if (Game1.options.SnappyMenus)
        {
            menu.snapToDefaultClickableComponent();
        }
    }

    private void PrepareChestClickableComponents(ItemGrabMenu menu)
    {
        menu.ItemsToGrabMenu.populateClickableComponentList();

        foreach (ClickableComponent component in menu.ItemsToGrabMenu.inventory)
        {
            component.myID += ItemGrabMenu.region_itemsToGrabMenuModifier;
            component.upNeighborID += ItemGrabMenu.region_itemsToGrabMenuModifier;
            component.rightNeighborID += ItemGrabMenu.region_itemsToGrabMenuModifier;
            component.leftNeighborID += ItemGrabMenu.region_itemsToGrabMenuModifier;
            component.downNeighborID = -7777;
            component.fullyImmutable = true;
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

        foreach (ClickableComponent slot in menu.inventory.inventory)
        {
            top = System.Math.Min(top, slot.bounds.Top);
            bottom = System.Math.Max(bottom, slot.bounds.Bottom);
        }

        if (top == int.MaxValue || bottom == int.MinValue)
        {
            return;
        }

        int lowerBackgroundTop = top - (LowerBackgroundTopPadding + this.config.InventoryBackgroundTopOffset);
        int lowerBackgroundBottom = bottom + LowerBackgroundBottomPadding + this.config.InventoryBackgroundBottomOffset;
        menu.yPositionOnScreen = lowerBackgroundTop - LowerPanelTopOffset;
        menu.height = lowerBackgroundBottom - lowerBackgroundTop + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192;
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
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.UnlimitedStorage_OnRenderedActiveMenu_Prefix))
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
            this.Monitor.Log($"{fieldName} was set to {value}, so it was clamped to {clamped}.", LogLevel.Warn);
        }

        return clamped;
    }
}
