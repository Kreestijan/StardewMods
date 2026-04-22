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
- ScaleUp

## Notes

- The pack adds `Data/Objects`, `Data/Fish`, and `Data/FishPondData` entries.
- Each fish has its own enable/disable toggle in [config.json](C:\Users\Kree\Desktop\TOF\config.json).
- Fish sprites are loaded from `assets/objects.png`.
- The pack expects ScaleUp so object fish sprites can use a `32x32` atlas in-game.

## Editor

The local visual editor is here:
[editor/index.html](C:\Users\Kree\Desktop\TOF\editor\index.html)

It edits:
[fishes.json](C:\Users\Kree\Desktop\TOF\fishes.json)

And it generates:
- `manifest.json`
- `content.json`
- `config.json`
- `i18n/default.json`

Use Edge or Chrome if you want the browser to save files directly back to disk.
