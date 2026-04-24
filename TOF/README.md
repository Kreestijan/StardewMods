# TOF

TOF is a Content Patcher pack that adds a first batch of six new fish:

- Mirror Koi
- Reed Catfish
- Mossfin
- Lantern Eel
- Stormray
- Surf Perch

## Requirements

- SMAPI
- Content Patcher
- Double Size Sprite (DSS)

## Notes

- The pack adds `Data/Objects`, `Data/Fish`, and `Data/FishPondData` entries.
- Each fish has its own enable/disable toggle in [config.json](/C:/Users/Kree/Desktop/StardewMods/TOF/config.json).
- Fish sprites are loaded from `assets/objects.png`.
- The pack expects Double Size Sprite (`DSS`) so object fish sprites can use a `32x32` atlas in-game while keeping vanilla icon size.

## Editor

The local visual editor is here:
[editor/index.html](/C:/Users/Kree/Desktop/StardewMods/TOF/editor/index.html)

It edits:
[fishes.json](/C:/Users/Kree/Desktop/StardewMods/TOF/fishes.json)

And it generates:
- `manifest.json`
- `content.json`
- `config.json`
- `i18n/default.json`

Use Edge or Chrome if you want the browser to save files directly back to disk.
