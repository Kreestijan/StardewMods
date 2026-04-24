# Spouse Warp

Spouse Warp adds a widget to the vanilla inventory page so you can warp to your spouse, other players, and selected NPCs.

## Features

- Shows warpable targets as portrait rows with a configurable decoration slot.
- Charges configurable gold per warp.
- Blocks warps during cutscenes, festivals, sleep, and cooldown.
- Supports multiplayer spouses, NPC spouses, and dynamically discovered modded NPCs.
- Lets you cycle decorations with `Ctrl + Right Click` on the decoration slot.

## Config

```json
{
  "RequiresMarriage": true,
  "IgnoreLocationUnlocks": false,
  "EnableSosButton": false,
  "WidgetScalePercent": 100,
  "WarpCostGold": 500,
  "CooldownSeconds": 5,
  "WidgetOffsetX": 52,
  "WidgetOffsetY": 0,
  "ShowNPCs": {},
  "Decorations": {}
}
```

## Locked Areas

By default, the mod blocks warps into areas the current save has not unlocked yet when Stardew's built-in location accessibility checks report them as locked. You can bypass that with `IgnoreLocationUnlocks`.
