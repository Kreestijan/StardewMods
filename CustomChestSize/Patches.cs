using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Reflection;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace CustomChestSize;

[HarmonyPatch]
internal static class Patches
{
    [ThreadStatic]
    private static int suppressCustomCapacityDepth;

    private static PropertyInfo? usTextBoxProperty;
    private static FieldInfo? textBoxClickableComponentField;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chest), nameof(Chest.GetActualCapacity))]
    private static void Chest_GetActualCapacity_Postfix(Chest __instance, ref int __result)
    {
        if (suppressCustomCapacityDepth <= 0 && !ModEntry.IsUnlimitedStorageLoaded())
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

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.gameWindowSizeChanged))]
    private static void ItemGrabMenu_GameWindowSizeChanged_Postfix(ItemGrabMenu __instance, Rectangle oldBounds, Rectangle newBounds)
    {
        ModEntry.ApplyLayoutIfNeeded(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.setSourceItem))]
    private static void ItemGrabMenu_SetSourceItem_Postfix(ItemGrabMenu __instance, StardewValley.Item item)
    {
        if (__instance.source != ItemGrabMenu.source_chest || item is not Chest)
        {
            return;
        }

        ModEntry.ReanchorColorPickerStrip(__instance);
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

    internal static void UnlimitedStorage_OnRenderedActiveMenu_Prefix()
    {
        // Intentionally empty. Unlimited Storage recalculates Y inside this method,
        // and we apply absolute Y in TextBox.Hover immediately before draw.
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
