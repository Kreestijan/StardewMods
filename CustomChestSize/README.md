# Custom Chest Size

SMAPI mod that lets you set the storage size of:

- regular chests
- big chests

The size comes from `rows * columns` in `config.json`, and can also be changed in-game through Generic Mod Config Menu.
The config floors are vanilla chest sizes, so you can only increase capacity from vanilla:

- regular chest: `12 x 3`
- big chest: `14 x 5`

## Config

```json
{
  "RegularChestColumns": 16,
  "RegularChestRows": 7,
  "BigChestColumns": 24,
  "BigChestRows": 12,
  "ChestBackgroundHeightOffset": 0,
  "InventoryPanelGapOffset": -12,
  "InventoryBackgroundTopOffset": 28,
  "InventoryBackgroundBottomOffset": 0,
  "ChestsAnywhereWidgetXOffset": 0,
  "ChestsAnywhereWidgetYOffset": 28,
  "ColorPickerXOffset": 328,
  "ColorPickerYOffset": 48,
  "UnlimitedStorageSearchXOffset": 200,
  "UnlimitedStorageSearchYOffset": -16,
  "UnlimitedStorageSearchLeftOffset": -112,
  "UnlimitedStorageSearchRightOffset": -160
}
```

## Compatibility

- Generic Mod Config Menu: supported for in-game editing.
- Automate: supported automatically because the mod patches the chest's real capacity, so Automate respects the same insert limits.
- Chests Anywhere: supported, including X/Y widget tuning offsets.
- Chest color picker: supported, including X/Y tuning offsets for the opened color strip.
- Unlimited Storage: when installed, this mod stops overriding actual chest capacity and only controls the visible chest UI layout. Unlimited Storage keeps ownership of real storage size and scrolling/paging behavior, and GMCM exposes X/Y/left/right tuning offsets for its search field.

## Overflow behavior

- Existing regular and big chests use the new capacity automatically.
- If you shrink a chest below the number of item stacks already inside it, the menu temporarily shows enough rows to reach all items.
- In that overflow state, the chest becomes take-only until its stack count falls back under the configured limit.
- If the mod is removed later, vanilla chest behavior still lets those extra stacks shift forward as you take items out, but you still can't add more until the chest is back under vanilla capacity.

## Commands

- `ccs_reload`: reload `config.json` without restarting the game.
