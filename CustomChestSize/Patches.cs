using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System.Reflection;
using System.Linq;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Locations;
using StardewValley.Objects;

namespace CustomChestSize;

[HarmonyPatch]
internal static class Patches
{
    private const int convenientChestsReferenceColumns = 24;
    private const int convenientChestsReferenceDeltaTiles = -128;
    private static readonly int convenientChestsReferenceMenuWidth = System.Math.Max(
        800 + IClickableMenu.borderWidth * 2,
        convenientChestsReferenceColumns * 64 + (IClickableMenu.borderWidth + IClickableMenu.spaceToClearSideBorder) * 2
    );

    [ThreadStatic]
    private static int suppressCustomCapacityDepth;

    [ThreadStatic]
    private static ItemGrabMenu? activeTintMenu;

    private static PropertyInfo? usTextBoxProperty;
    private static FieldInfo? textBoxClickableComponentField;
    private static PropertyInfo? convenientChestsWidgetPositionProperty;
    private static PropertyInfo? categorizeChestsWidgetPositionProperty;
    private static FieldInfo? convenientChestsOverlayItemGrabMenuField;
    private static FieldInfo? convenientChestsOverlayChestField;
    private static FieldInfo? convenientChestsOverlayCategorizeButtonField;
    private static FieldInfo? convenientChestsOverlayStashButtonField;
    private static FieldInfo? convenientChestsOverlayCategoryMenuField;
    private static FieldInfo? categorizeChestsOverlayOpenButtonField;
    private static FieldInfo? categorizeChestsOverlayStashButtonField;
    private static FieldInfo? categorizeChestsOverlayCategoryMenuField;
    private static object? activeConvenientChestsOverlay;
    private static MethodInfo? convenientChestsPositionMethod;
    private static FieldInfo? rfsOpenChestField;
    private static MethodInfo? rfsUpdateButtonMethod;
    private static FieldInfo? rfsChestsField;
    private static FieldInfo? rfsSelectedField;
    private static FieldInfo? rfsDeselectedField;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chest), nameof(Chest.GetActualCapacity))]
    private static void Chest_GetActualCapacity_Postfix(Chest __instance, ref int __result)
    {
        if (suppressCustomCapacityDepth <= 0 && !ModEntry.IsUnlimitedStorageLoaded() && Game1.gameMode == 3)
        {
            __result = ModEntry.GetConfiguredCapacity(__instance, __result);
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(
        typeof(ItemGrabMenu),
        MethodType.Constructor,
        new[]
        {
            typeof(System.Collections.Generic.IList<StardewValley.Item>),
            typeof(bool),
            typeof(bool),
            typeof(InventoryMenu.highlightThisItem),
            typeof(ItemGrabMenu.behaviorOnItemSelect),
            typeof(string),
            typeof(ItemGrabMenu.behaviorOnItemSelect),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(StardewValley.Item),
            typeof(int),
            typeof(object),
            typeof(ItemExitBehavior),
            typeof(bool)
        }
    )]
    private static void ItemGrabMenu_Constructor_Prefix(int source, StardewValley.Item sourceItem, out bool __state)
    {
        __state = source == ItemGrabMenu.source_chest && sourceItem is Chest;
        if (__state)
        {
            suppressCustomCapacityDepth++;

            if (ModEntry.Instance.ShouldLogDebug())
            {
                Chest chest = (Chest)sourceItem;
                ModEntry.LogDebugStatic($"[ItemGrabMenu_Constructor_Prefix] sourceItem ItemId={sourceItem.ItemId} Name={sourceItem.Name} SpecialChestType={chest.SpecialChestType} playerChest={chest.playerChest.Value} source={source}");
            }
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(
        typeof(ItemGrabMenu),
        MethodType.Constructor,
        new[]
        {
            typeof(System.Collections.Generic.IList<StardewValley.Item>),
            typeof(bool),
            typeof(bool),
            typeof(InventoryMenu.highlightThisItem),
            typeof(ItemGrabMenu.behaviorOnItemSelect),
            typeof(string),
            typeof(ItemGrabMenu.behaviorOnItemSelect),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(StardewValley.Item),
            typeof(int),
            typeof(object),
            typeof(ItemExitBehavior),
            typeof(bool)
        }
    )]
    private static Exception? ItemGrabMenu_Constructor_Finalizer(Exception? __exception, bool __state)
    {
        if (__state && suppressCustomCapacityDepth > 0)
        {
            suppressCustomCapacityDepth--;
        }

        return __exception;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(
        typeof(ItemGrabMenu),
        MethodType.Constructor,
        new[]
        {
            typeof(System.Collections.Generic.IList<StardewValley.Item>),
            typeof(bool),
            typeof(bool),
            typeof(InventoryMenu.highlightThisItem),
            typeof(ItemGrabMenu.behaviorOnItemSelect),
            typeof(string),
            typeof(ItemGrabMenu.behaviorOnItemSelect),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(StardewValley.Item),
            typeof(int),
            typeof(object),
            typeof(ItemExitBehavior),
            typeof(bool)
        }
    )]
    private static void ItemGrabMenu_Constructor_Postfix(ItemGrabMenu __instance)
    {
        ModEntry.ApplyLayoutIfNeeded(__instance);
    }

    internal static void ItemGrabMenu_AnyConstructor_Postfix(ItemGrabMenu __instance)
    {
        ModEntry.ApplyLayoutIfNeeded(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.setSourceItem))]
    private static void ItemGrabMenu_SetSourceItem_Prefix(ItemGrabMenu __instance, out object? __state)
    {
        __state = __instance.sourceItem;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.setSourceItem))]
    private static void ItemGrabMenu_SetSourceItem_Postfix(ItemGrabMenu __instance, StardewValley.Item item, object? __state)
    {
        // Auto-grabber: the game's setSourceItem reconstructs ItemsToGrabMenu with
        // vanilla defaults, so always re-apply the auto grabber layout regardless
        // of the source value (some mods use source_farmhand for item transfers).
        if (ModEntry.IsAutoGrabber(item) || ModEntry.IsAutoGrabber(__instance.context))
        {
            ModEntry.ApplyLayoutIfNeeded(__instance);
            return;
        }

        if (__instance.source != ItemGrabMenu.source_chest)
        {
            return;
        }

        if (item is not Chest)
        {
            return;
        }

        // Always re-anchor the color picker — it may have been created lazily after the initial layout
        ModEntry.ReanchorColorPickerStrip(__instance);

        // Skip full layout rebuild if the source chest hasn't actually changed —
        // some mods call setSourceItem on item transfers, and rebuilding 300+
        // clickable components per click causes visible stutter.
        if (__state is Chest oldChest && ReferenceEquals(oldChest, item))
        {
            return;
        }

        ModEntry.ApplyLayoutIfNeeded(__instance);

        // Update CC's chest reference without running PositionButtons (expensive with
        // large grids). The button positions don't change on item transfers — only the
        // chest content does. CC draws the chest name etc from this reference.
        UpdateConvenientChestsChest(__instance, (Chest)item);
        if (ModEntry.TryGetLayoutState(__instance, out ChestMenuLayoutState ccState))
        {
            ccState.ConvenientChestsPositioned = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.gameWindowSizeChanged))]
    private static void ItemGrabMenu_GameWindowSizeChanged_Postfix(ItemGrabMenu __instance, Rectangle oldBounds, Rectangle newBounds)
    {
        ModEntry.ApplyLayoutIfNeeded(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.draw))]
    private static void ItemGrabMenu_Draw_Prefix(ItemGrabMenu __instance)
    {
        activeTintMenu = ModEntry.IsTintChestUIEnabled() ? __instance : null;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.draw))]
    private static void ItemGrabMenu_Draw_Postfix()
    {
        activeTintMenu = null;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(InventoryMenu), nameof(InventoryMenu.draw), typeof(SpriteBatch))]
    private static bool InventoryMenu_Draw_Prefix(InventoryMenu __instance, SpriteBatch b)
    {
        if (activeTintMenu is null)
            return true;

        if (__instance != activeTintMenu.ItemsToGrabMenu)
            return true;

        if (activeTintMenu.sourceItem is not Chest chest)
            return true;

        Color choiceColor = chest.playerChoiceColor.Value;
        if (choiceColor.R == 0 && choiceColor.G == 0 && choiceColor.B == 0)
            return true;
        if (choiceColor == Color.White)
            return true;

        if (!ModEntry.TryGetLayoutState(activeTintMenu, out ChestMenuLayoutState state))
            return true;

        Rectangle bounds = state.ChestPanelBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return true;

        int opacity = ModEntry.GetTintChestUIOpacity();
        if (opacity <= 0)
            return true;

        bounds.X += ModEntry.GetTintChestUIPaddingLeft();
        bounds.Width -= ModEntry.GetTintChestUIPaddingLeft() + ModEntry.GetTintChestUIPaddingRight();
        bounds.Y += ModEntry.GetTintChestUIPaddingTop();
        bounds.Height -= ModEntry.GetTintChestUIPaddingTop() + ModEntry.GetTintChestUIPaddingBottom();

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return true;

        float t = opacity / 100f;
        Color tintColor = new Color(
            (byte)(255 - (255 - choiceColor.R) * t),
            (byte)(255 - (255 - choiceColor.G) * t),
            (byte)(255 - (255 - choiceColor.B) * t)
        );

        IClickableMenu.drawTextureBox(b, Game1.uncoloredMenuTexture, new Rectangle(0, 256, 64, 64), bounds.X, bounds.Y, bounds.Width, bounds.Height, tintColor, 1f, false);

        return true;
    }

    internal static void ChestsAnywhere_ReinitializeBaseComponents_Postfix(object __instance)
    {
        Traverse overlay = Traverse.Create(__instance);
        if (overlay.Field("Menu").GetValue() is not ItemGrabMenu menu)
        {
            return;
        }

        if (!ModEntry.TryGetLayoutState(menu, out ChestMenuLayoutState state))
        {
            return;
        }

        int xOffset = ModEntry.GetChestsAnywhereWidgetXOffset();
        int yOffset = ModEntry.GetChestsAnywhereWidgetYOffset();
        MoveDropdown(overlay.Field("ChestDropdown").GetValue(), xOffset, state.OverlayAnchorY + yOffset);
        MoveDropdown(overlay.Field("CategoryDropdown").GetValue(), xOffset, state.OverlayAnchorY + yOffset);

        if (overlay.Field("EditButton").GetValue() is ClickableTextureComponent editButton)
        {
            editButton.bounds.X += xOffset;
            editButton.bounds.Y = state.OverlayAnchorY + yOffset;
        }
    }

    internal static void ChestsAnywhere_GetTopOffset_Postfix(object menu, ref int __result)
    {
        if (menu is not ItemGrabMenu itemGrabMenu)
        {
            return;
        }

        if (!ModEntry.TryGetLayoutState(itemGrabMenu, out ChestMenuLayoutState state))
        {
            return;
        }

        __result = state.OverlayAnchorY - itemGrabMenu.yPositionOnScreen + ModEntry.GetChestsAnywhereWidgetYOffset();
    }

    private static void MoveDropdown(object? dropdown, int xOffset, int y)
    {
        if (dropdown is not ClickableComponent clickable)
        {
            return;
        }

        clickable.bounds.X += xOffset;
        clickable.bounds.Y = y;

        MethodInfo? reinitialize = AccessTools.Method(dropdown.GetType(), "ReinitializeComponents");
        reinitialize?.Invoke(dropdown, null);
    }

    internal static void ConvenientChests_Draw_Prefix(object __instance)
    {
        convenientChestsOverlayItemGrabMenuField ??= AccessTools.Field(__instance.GetType(), "<ItemGrabMenu>k__BackingField");
        if (convenientChestsOverlayItemGrabMenuField?.GetValue(__instance) is not ItemGrabMenu menu
            || menu.sourceItem is not Chest chest)
        {
            return;
        }

        // Skip per-frame work if already positioned for this layout
        if (ModEntry.TryGetLayoutState(menu, out ChestMenuLayoutState ccState) && ccState.ConvenientChestsPositioned)
        {
            return;
        }

        activeConvenientChestsOverlay = __instance;
        convenientChestsOverlayChestField ??= AccessTools.Field(__instance.GetType(), "<Chest>k__BackingField");
        convenientChestsOverlayChestField?.SetValue(__instance, chest);
        convenientChestsPositionMethod ??= AccessTools.Method(__instance.GetType(), "PositionButtons");
        convenientChestsPositionMethod?.Invoke(__instance, null);
        ConvenientChests_PositionButtons_Postfix(__instance);

        if (ccState is not null)
        {
            ccState.ConvenientChestsPositioned = true;
        }
    }

    internal static void ConvenientChests_PositionButtons_Postfix(object __instance)
    {
        convenientChestsOverlayItemGrabMenuField ??= AccessTools.Field(__instance.GetType(), "<ItemGrabMenu>k__BackingField");
        convenientChestsOverlayCategorizeButtonField ??= AccessTools.Field(__instance.GetType(), "<CategorizeButton>k__BackingField");
        convenientChestsOverlayStashButtonField ??= AccessTools.Field(__instance.GetType(), "<StashButton>k__BackingField");
        if (convenientChestsOverlayItemGrabMenuField?.GetValue(__instance) is not ItemGrabMenu menu
            || convenientChestsOverlayCategorizeButtonField?.GetValue(__instance) is not object categorizeButton
            || convenientChestsOverlayStashButtonField?.GetValue(__instance) is not object stashButton)
        {
            return;
        }

        Point? categorizePosition = GetWidgetPosition(categorizeButton);
        Point? stashPosition = GetWidgetPosition(stashButton);
        if (categorizePosition is null || stashPosition is null)
        {
            return;
        }

        int currentInset = menu.ItemsToGrabMenu.xPositionOnScreen - menu.xPositionOnScreen;
        int targetX = menu.ItemsToGrabMenu.xPositionOnScreen
            + convenientChestsReferenceMenuWidth / 2
            + convenientChestsReferenceDeltaTiles * Game1.pixelZoom
            - GetWidgetWidth(categorizeButton)
            - currentInset
            + ModEntry.GetConvenientChestsXOffset();
        int targetY = menu.yPositionOnScreen + 22 * Game1.pixelZoom + ModEntry.GetConvenientChestsYOffset();

        SetWidgetPosition(categorizeButton, targetX, targetY);
        SetWidgetPosition(
            stashButton,
            targetX + GetWidgetWidth(categorizeButton) - GetWidgetWidth(stashButton),
            targetY + GetWidgetHeight(categorizeButton)
        );
    }

    internal static void ConvenientChests_OpenCategoryMenu_Postfix(object __instance)
    {
        convenientChestsOverlayCategoryMenuField ??= AccessTools.Field(__instance.GetType(), "<CategoryMenu>k__BackingField");
        if (convenientChestsOverlayCategoryMenuField?.GetValue(__instance) is not object categoryMenu)
        {
            return;
        }

        OffsetWidgetPosition(categoryMenu, ModEntry.GetConvenientChestsXOffset(), ModEntry.GetConvenientChestsYOffset());
    }

    internal static void CategorizeChests_PositionButtons_Postfix(object __instance)
    {
        categorizeChestsOverlayOpenButtonField ??= AccessTools.Field(__instance.GetType(), "OpenButton");
        categorizeChestsOverlayStashButtonField ??= AccessTools.Field(__instance.GetType(), "StashButton");
        if (categorizeChestsOverlayOpenButtonField?.GetValue(__instance) is not object openButton
            || categorizeChestsOverlayStashButtonField?.GetValue(__instance) is not object stashButton)
        {
            return;
        }

        OffsetCategorizeChestsWidgetPosition(openButton, ModEntry.GetCategorizeChestsXOffset(), ModEntry.GetCategorizeChestsYOffset());
        OffsetCategorizeChestsWidgetPosition(stashButton, ModEntry.GetCategorizeChestsXOffset(), ModEntry.GetCategorizeChestsYOffset());
    }

    internal static void CategorizeChests_CreateMenu_Postfix(object __instance, ItemGrabMenu itemGrabMenu)
    {
        FieldInfo? widgetHostField = AccessTools.Field(__instance.GetType(), "WidgetHost");
        if (widgetHostField?.GetValue(__instance) is not null)
        {
            return;
        }

        Chest? chest = ResolveCategorizeChestsChest(itemGrabMenu);
        if (chest is null)
        {
            return;
        }

        FieldInfo? configField = AccessTools.Field(__instance.GetType(), "Config");
        FieldInfo? chestDataManagerField = AccessTools.Field(__instance.GetType(), "ChestDataManager");
        FieldInfo? chestFillerField = AccessTools.Field(__instance.GetType(), "ChestFiller");
        FieldInfo? itemDataManagerField = AccessTools.Field(__instance.GetType(), "ItemDataManager");

        object? config = configField?.GetValue(__instance);
        object? chestDataManager = chestDataManagerField?.GetValue(__instance);
        object? chestFiller = chestFillerField?.GetValue(__instance);
        object? itemDataManager = itemDataManagerField?.GetValue(__instance);
        if (config is null || chestDataManager is null || chestFiller is null || itemDataManager is null)
        {
            return;
        }

        IModHelper? helper = Traverse.Create(__instance).Property("Helper").GetValue<IModHelper>();
        if (helper is null)
        {
            return;
        }

        Type? widgetHostType = AccessTools.TypeByName("StardewValleyMods.CategorizeChests.Interface.WidgetHost");
        Type? chestOverlayType = AccessTools.TypeByName("StardewValleyMods.CategorizeChests.Interface.Widgets.ChestOverlay");
        if (widgetHostType is null || chestOverlayType is null)
        {
            return;
        }

        object? widgetHost = Activator.CreateInstance(widgetHostType, helper);
        if (widgetHost is null)
        {
            return;
        }

        object? tooltipManager = AccessTools.Field(widgetHostType, "TooltipManager")?.GetValue(widgetHost);
        object? rootWidget = AccessTools.Field(widgetHostType, "RootWidget")?.GetValue(widgetHost);
        if (tooltipManager is null || rootWidget is null)
        {
            return;
        }

        object? chestOverlay = Activator.CreateInstance(
            chestOverlayType,
            itemGrabMenu,
            chest,
            config,
            chestDataManager,
            chestFiller,
            itemDataManager,
            tooltipManager
        );
        if (chestOverlay is null)
        {
            return;
        }

        MethodInfo? addChildMethod = rootWidget.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "AddChild" && method.IsGenericMethodDefinition);
        if (addChildMethod is null)
        {
            return;
        }

        addChildMethod.MakeGenericMethod(chestOverlayType).Invoke(rootWidget, new[] { chestOverlay });
        if (widgetHostField is not null)
        {
            widgetHostField.SetValue(__instance, widgetHost);
        }
    }

    internal static void CategorizeChests_OpenCategoryMenu_Postfix(object __instance)
    {
        categorizeChestsOverlayCategoryMenuField ??= AccessTools.Field(__instance.GetType(), "CategoryMenu");
        if (categorizeChestsOverlayCategoryMenuField?.GetValue(__instance) is not object categoryMenu)
        {
            return;
        }

        OffsetCategorizeChestsWidgetPosition(categoryMenu, ModEntry.GetCategorizeChestsXOffset(), ModEntry.GetCategorizeChestsYOffset());
    }

    internal static void RemoteFridgeStorage_UpdateButtonPosition_Postfix(object __instance)
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu menu)
        {
            return;
        }

        Type t = __instance.GetType();
        rfsOpenChestField ??= AccessTools.Field(t, "_openChest");
        rfsSelectedField ??= AccessTools.Field(t, "_fridgeSelected");
        rfsDeselectedField ??= AccessTools.Field(t, "_fridgeDeselected");

        if (rfsOpenChestField?.GetValue(__instance) is not Chest chest)
            return;

        int targetX = menu.xPositionOnScreen - Game1.pixelZoom * 16 * 2 + Game1.pixelZoom + ModEntry.GetRemoteFridgeStorageXOffset();
        int targetY = menu.yPositionOnScreen + Game1.pixelZoom + ModEntry.GetRemoteFridgeStorageYOffset();

        if (rfsSelectedField?.GetValue(__instance) is ClickableTextureComponent selected)
        {
            selected.bounds.X = targetX;
            selected.bounds.Y = targetY;
        }

        if (rfsDeselectedField?.GetValue(__instance) is ClickableTextureComponent deselected)
        {
            deselected.bounds.X = targetX;
            deselected.bounds.Y = targetY;
        }
    }

    internal static bool RemoteFridgeStorage_DrawFridgeIcon_Prefix(object __instance, RenderedActiveMenuEventArgs e)
    {
        Type t = __instance.GetType();
        rfsOpenChestField ??= AccessTools.Field(t, "_openChest");
        rfsUpdateButtonMethod ??= AccessTools.Method(t, "UpdateButtonPosition");
        rfsChestsField ??= AccessTools.Field(t, "_chests");
        rfsSelectedField ??= AccessTools.Field(t, "_fridgeSelected");
        rfsDeselectedField ??= AccessTools.Field(t, "_fridgeDeselected");

        if (rfsOpenChestField?.GetValue(__instance) is not Chest openChest)
        {
            return false;
        }

        FarmHouse? farmHouse = Game1.getLocationFromName("farmHouse") as FarmHouse;
        if (openChest == farmHouse?.fridge.Value
            || Game1.activeClickableMenu is null
            || !openChest.playerChest.Value)
        {
            return false;
        }

        rfsUpdateButtonMethod?.Invoke(__instance, new object[] { e });
        if (rfsChestsField?.GetValue(__instance) is System.Collections.IEnumerable selectedChests
            && ContainsReference(selectedChests, openChest))
        {
            if (rfsSelectedField?.GetValue(__instance) is ClickableTextureComponent selected)
            {
                selected.draw(e.SpriteBatch, Color.White, 10f);
            }
        }
        else if (rfsDeselectedField?.GetValue(__instance) is ClickableTextureComponent deselected)
        {
            deselected.draw(e.SpriteBatch, Color.White, 10f);
        }

        if (!Game1.options.hardwareCursor)
        {
            Game1.spriteBatch.Draw(
                Game1.mouseCursors,
                new Vector2(Game1.getOldMouseX(), Game1.getOldMouseY()),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, 16, 16),
                Color.White,
                0f,
                Vector2.Zero,
                4f + Game1.dialogueButtonScale / 150f,
                SpriteEffects.None,
                0f
            );
        }

        return false;
    }

    private static void SetWidgetPosition(object widget, int x, int y)
    {
        convenientChestsWidgetPositionProperty ??= AccessTools.Property(
            AccessTools.TypeByName("ConvenientChests.CategorizeChests.Interface.Widgets.Widget"),
            "Position"
        );

        PropertyInfo? positionProperty = convenientChestsWidgetPositionProperty;
        if (positionProperty is null)
        {
            return;
        }

        positionProperty.SetValue(widget, new Point(x, y));
    }

    private static Point? GetWidgetPosition(object widget)
    {
        convenientChestsWidgetPositionProperty ??= AccessTools.Property(
            AccessTools.TypeByName("ConvenientChests.CategorizeChests.Interface.Widgets.Widget"),
            "Position"
        );

        return convenientChestsWidgetPositionProperty?.GetValue(widget) is Point position
            ? position
            : null;
    }

    private static int GetWidgetWidth(object widget)
    {
        PropertyInfo? widthProperty = AccessTools.Property(
            AccessTools.TypeByName("ConvenientChests.CategorizeChests.Interface.Widgets.Widget"),
            "Width"
        );

        return widthProperty?.GetValue(widget) is int width
            ? width
            : 0;
    }

    private static int GetWidgetHeight(object widget)
    {
        PropertyInfo? heightProperty = AccessTools.Property(
            AccessTools.TypeByName("ConvenientChests.CategorizeChests.Interface.Widgets.Widget"),
            "Height"
        );

        return heightProperty?.GetValue(widget) is int height
            ? height
            : 0;
    }

    private static void OffsetCategorizeChestsWidgetPosition(object widget, int xOffset, int yOffset)
    {
        categorizeChestsWidgetPositionProperty ??= AccessTools.Property(
            AccessTools.TypeByName("StardewValleyMods.CategorizeChests.Interface.Widgets.Widget"),
            "Position"
        );

        if (categorizeChestsWidgetPositionProperty?.GetValue(widget) is not Point position)
        {
            return;
        }

        categorizeChestsWidgetPositionProperty.SetValue(widget, new Point(position.X + xOffset, position.Y + yOffset));
    }

    private static bool ContainsReference(System.Collections.IEnumerable values, object target)
    {
        foreach (object? value in values)
        {
            if (ReferenceEquals(value, target))
            {
                return true;
            }
        }

        return false;
    }

    private static Chest? ResolveCategorizeChestsChest(ItemGrabMenu menu)
    {
        object? target = ((Delegate?)(object?)menu.behaviorOnItemGrab)?.Target;
        if (target is Chest behaviorTargetChest)
        {
            return behaviorTargetChest;
        }

        if (menu.sourceItem is Chest sourceChest)
        {
            return sourceChest;
        }

        return menu.context as Chest;
    }

    private static void OffsetWidgetPosition(object widget, int xOffset, int yOffset)
    {
        convenientChestsWidgetPositionProperty ??= AccessTools.Property(
            AccessTools.TypeByName("ConvenientChests.CategorizeChests.Interface.Widgets.Widget"),
            "Position"
        );

        PropertyInfo? positionProperty = convenientChestsWidgetPositionProperty;
        if (positionProperty?.GetValue(widget) is not Point position)
        {
            return;
        }

        SetWidgetPosition(widget, position.X + xOffset, position.Y + yOffset);
    }

    private static void UpdateConvenientChestsChest(ItemGrabMenu menu, Chest chest)
    {
        if (activeConvenientChestsOverlay is null)
            return;

        convenientChestsOverlayItemGrabMenuField ??= AccessTools.Field(activeConvenientChestsOverlay.GetType(), "<ItemGrabMenu>k__BackingField");
        if (convenientChestsOverlayItemGrabMenuField?.GetValue(activeConvenientChestsOverlay) is not ItemGrabMenu overlayMenu
            || !ReferenceEquals(overlayMenu, menu))
            return;

        convenientChestsOverlayChestField ??= AccessTools.Field(activeConvenientChestsOverlay.GetType(), "<Chest>k__BackingField");
        convenientChestsOverlayChestField?.SetValue(activeConvenientChestsOverlay, chest);
    }

    private static void RefreshConvenientChestsOverlayForMenu(ItemGrabMenu menu, Chest chest)
    {
        if (activeConvenientChestsOverlay is null)
        {
            return;
        }

        convenientChestsOverlayItemGrabMenuField ??= AccessTools.Field(activeConvenientChestsOverlay.GetType(), "<ItemGrabMenu>k__BackingField");
        if (convenientChestsOverlayItemGrabMenuField?.GetValue(activeConvenientChestsOverlay) is not ItemGrabMenu overlayMenu
            || !ReferenceEquals(overlayMenu, menu))
        {
            return;
        }

        convenientChestsOverlayChestField ??= AccessTools.Field(activeConvenientChestsOverlay.GetType(), "<Chest>k__BackingField");
        convenientChestsOverlayChestField?.SetValue(activeConvenientChestsOverlay, chest);
        convenientChestsPositionMethod ??= AccessTools.Method(activeConvenientChestsOverlay.GetType(), "PositionButtons");
        convenientChestsPositionMethod?.Invoke(activeConvenientChestsOverlay, null);
        ConvenientChests_PositionButtons_Postfix(activeConvenientChestsOverlay);
        ConvenientChests_OpenCategoryMenu_Postfix(activeConvenientChestsOverlay);

        // Mark as positioned so the per-frame draw prefix skips next frame
        if (ModEntry.TryGetLayoutState(menu, out ChestMenuLayoutState refreshState))
        {
            refreshState.ConvenientChestsPositioned = true;
        }
    }

    internal static void InitUnlimitedStorageCompat()
    {
        Type? modStateType = AccessTools.TypeByName("LeFauxMods.UnlimitedStorage.Services.ModState");
        if (modStateType is not null)
        {
            usTextBoxProperty = AccessTools.Property(modStateType, "TextBox");
        }

        textBoxClickableComponentField ??= AccessTools.Field(typeof(TextBox), "CC");
    }

    internal static void ApplyUnlimitedStorageLayout(ItemGrabMenu menu)
    {
        if (usTextBoxProperty is null || !ModEntry.IsUnlimitedStorageLoaded())
        {
            return;
        }

        if (!TryGetUnlimitedStorageBaseBounds(menu, out int baseX, out int baseY, out int baseWidth))
        {
            return;
        }

        if (usTextBoxProperty.GetValue(null) is TextBox usTextBox)
        {
            ApplyUnlimitedStorageTextBoxBounds(
                usTextBox,
                baseX,
                baseY,
                baseWidth,
                ModEntry.GetUnlimitedStorageSearchXOffset(),
                ModEntry.GetUnlimitedStorageSearchYOffset(),
                ModEntry.GetUnlimitedStorageSearchLeftOffset(),
                ModEntry.GetUnlimitedStorageSearchRightOffset()
            );
        }
    }

    internal static void UnlimitedStorage_OnMenuChanged_Postfix(MenuChangedEventArgs e)
    {
        if (!ModEntry.IsUnlimitedStorageLoaded() || Game1.activeClickableMenu is not ItemGrabMenu menu)
        {
            return;
        }

        if (!TryGetUnlimitedStorageBaseBounds(menu, out int baseX, out int baseY, out int baseWidth))
        {
            return;
        }

        if (usTextBoxProperty?.GetValue(null) is not TextBox usTextBox)
        {
            return;
        }

        ApplyUnlimitedStorageTextBoxBounds(
            usTextBox,
            baseX,
            baseY,
            baseWidth,
            ModEntry.GetUnlimitedStorageSearchXOffset(),
            ModEntry.GetUnlimitedStorageSearchYOffset(),
            ModEntry.GetUnlimitedStorageSearchLeftOffset(),
            ModEntry.GetUnlimitedStorageSearchRightOffset()
        );
    }

    internal static void UnlimitedStorage_OnRenderedActiveMenu_Postfix()
    {
        if (Game1.activeClickableMenu is ItemGrabMenu menu
            && ModEntry.TryGetLayoutState(menu, out ChestMenuLayoutState state)
            && state.IsAutoGrabber)
        {
            ApplyUnlimitedStorageLayout(menu);
        }
    }

    internal static void TextBox_Hover_Prefix(TextBox __instance)
    {
        if (usTextBoxProperty is null || !ModEntry.IsUnlimitedStorageLoaded() || Game1.activeClickableMenu is not ItemGrabMenu menu)
        {
            return;
        }

        if (!TryGetUnlimitedStorageBaseBounds(menu, out int baseX, out int baseY, out int baseWidth))
        {
            return;
        }

        if (usTextBoxProperty.GetValue(null) is not TextBox usTextBox || !ReferenceEquals(__instance, usTextBox))
        {
            return;
        }

        ApplyUnlimitedStorageTextBoxBounds(
            __instance,
            baseX,
            baseY,
            baseWidth,
            ModEntry.GetUnlimitedStorageSearchXOffset(),
            ModEntry.GetUnlimitedStorageSearchYOffset(),
            ModEntry.GetUnlimitedStorageSearchLeftOffset(),
            ModEntry.GetUnlimitedStorageSearchRightOffset()
        );
    }

    private static void AdjustTextBoxClickableBounds(TextBox textBox, int deltaX, int deltaY, int deltaWidth)
    {
        if (textBoxClickableComponentField?.GetValue(textBox) is not ClickableComponent clickableComponent)
        {
            return;
        }

        clickableComponent.bounds.X += deltaX;
        clickableComponent.bounds.Y += deltaY;
        clickableComponent.bounds.Width = System.Math.Max(64, clickableComponent.bounds.Width + deltaWidth);
    }

    private static bool TryGetUnlimitedStorageBaseBounds(ItemGrabMenu menu, out int baseX, out int baseY, out int baseWidth)
    {
        baseX = 0;
        baseY = 0;
        baseWidth = 0;

        InventoryMenu inventoryMenu = menu.ItemsToGrabMenu;
        if (inventoryMenu is null)
        {
            return false;
        }

        System.Collections.Generic.List<ClickableComponent> border = inventoryMenu.GetBorder(StardewValley.Menus.InventoryMenu.BorderSide.Top);
        if (border.Count == 0)
        {
            return false;
        }

        baseX = border[0].bounds.Left;
        baseWidth = border[border.Count - 1].bounds.Right - border[0].bounds.Left;
        baseY = inventoryMenu.yPositionOnScreen - (usTextBoxProperty?.GetValue(null) is TextBox textBox ? textBox.Height : 0) - 52;

        if (menu.ItemsToGrabMenu.rows >= 5)
        {
            baseY += 20;
        }

        return true;
    }

    private static void ApplyUnlimitedStorageTextBoxBounds(TextBox textBox, int baseX, int baseY, int baseWidth, int xOffset, int yOffset, int leftOffset, int rightOffset)
    {
        int targetX = baseX + xOffset - leftOffset;
        int targetY = baseY + yOffset;
        int targetWidth = System.Math.Max(64, baseWidth + leftOffset + rightOffset);

        int deltaX = targetX - textBox.X;
        int deltaY = targetY - textBox.Y;
        int deltaWidth = targetWidth - textBox.Width;

        textBox.X = targetX;
        textBox.Y = targetY;
        textBox.Width = targetWidth;

        AdjustTextBoxClickableBounds(textBox, deltaX, deltaY, deltaWidth);
    }
}
