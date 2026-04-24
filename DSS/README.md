# Double Size Sprite

`Double Size Sprite` (`DSS`) is a lightweight SMAPI framework for Content Patcher packs that ship double-resolution icon atlases.

It is intentionally narrow:

- item/icon atlases only;
- Content Patcher registration only;
- same on-screen icon size as vanilla;
- higher-resolution source art under the hood.

## Content Patcher Usage

Register a double-resolution atlas through:

```json
{
  "Action": "EditData",
  "Target": "{{Kree.DSS/Assets}}",
  "Entries": {
    "YourPack_Objects": [
      {
        "Asset": "Mods/YourPack/Objects"
      }
    ]
  }
}
```

### Supported Fields

- `Asset`: one asset path to register.
- `Assets`: comma-separated asset paths to register.
- `Target`: optional asset prefix prepended to `Asset` / `Assets`.
- `Scale`: optional integer scale. Defaults to `2`.

The atlas is assumed to be uniformly scaled. Example: a vanilla `16x16` object sheet exported as `32x32` per logical tile.

## Notes

- This framework is asset-based, not item-type hardcoded.
- It works by patching sprite source-rect resolution and draw calls for registered textures only.
- It does not implement the broader character/padding systems from `Scale Up 2` / `Scale Up Unofficial`.
