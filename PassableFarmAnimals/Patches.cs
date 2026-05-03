using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;

namespace PassableFarmAnimals;

/// <summary>
/// When the farmer moves, temporarily remove farm animals from the location
/// so they don't block collision detection. Restore them immediately after
/// and nudge any animal the farmer walked into.
/// </summary>
[HarmonyPatch(typeof(GameLocation), nameof(GameLocation.isCollidingPosition))]
internal static class FarmAnimalCollisionPatch
{
    private static KeyValuePair<long, FarmAnimal>[]? _savedPairs;

    private static Point? _previousFarmerTile;

    public static void Prefix(GameLocation __instance, bool isFarmer)
    {
        if (!ModEntry.Instance.config.EnableMod || !isFarmer || _savedPairs is not null)
        {
            return;
        }

        if (__instance is not Farm farm)
        {
            return;
        }

        var animals = farm.Animals;
        if (animals.Count <= 0)
        {
            return;
        }

        _savedPairs = animals.Pairs.ToArray();

        animals.Clear();
    }

    public static void Postfix()
    {
        if (_savedPairs is null)
        {
            return;
        }

        if (Game1.currentLocation is Farm farm)
        {
            var animals = farm.Animals;
            foreach (var pair in _savedPairs)
            {
                animals[pair.Key] = pair.Value;
            }
        }

        _savedPairs = null;

        if (ModEntry.Instance.config.EnableMod)
        {
            TryNudgeAnimals();
        }
    }

    private static void TryNudgeAnimals()
    {
        Farmer farmer = Game1.player;
        if (farmer is null || Game1.currentLocation is not Farm farm)
            return;

        Point currentTile = farmer.TilePoint;

        // Only nudge when the farmer actually moves to a new tile
        if (_previousFarmerTile == currentTile)
            return;

        _previousFarmerTile = currentTile;

        Rectangle farmerBounds = farmer.GetBoundingBox();

        foreach (var pair in farm.Animals.Pairs)
        {
            FarmAnimal animal = pair.Value;
            if (!farmerBounds.Intersects(animal.GetBoundingBox()))
                continue;

            // Try directions in priority order: forward → left → right → back
            Vector2[] candidates = GetNudgeDirections(farmer.FacingDirection);
            foreach (Vector2 offset in candidates)
            {
                Vector2 targetPos = new(animal.Position.X + offset.X, animal.Position.Y + offset.Y);
                if (IsValidNudgeTarget(targetPos, farm, animal))
                {
                    animal.Position = targetPos;
                    break;
                }
            }
        }
    }

    private static Vector2[] GetNudgeDirections(int facingDir)
    {
        // facingDir: 0=up, 1=right, 2=down, 3=left
        return facingDir switch
        {
            0 => new[] { new Vector2(0, -64), new Vector2(-64, 0), new Vector2(64, 0), new Vector2(0, 64) },
            1 => new[] { new Vector2(64, 0), new Vector2(0, -64), new Vector2(0, 64), new Vector2(-64, 0) },
            2 => new[] { new Vector2(0, 64), new Vector2(-64, 0), new Vector2(64, 0), new Vector2(0, -64) },
            3 => new[] { new Vector2(-64, 0), new Vector2(0, -64), new Vector2(0, 64), new Vector2(64, 0) },
            _ => new[] { new Vector2(0, -64) },
        };
    }

    private static bool IsValidNudgeTarget(Vector2 pos, Farm farm, FarmAnimal self)
    {
        int tileX = (int)(pos.X / 64f);
        int tileY = (int)(pos.Y / 64f);

        if (tileX < 0 || tileY < 0)
            return false;

        if (tileX >= farm.Map.Layers[0].LayerWidth || tileY >= farm.Map.Layers[0].LayerHeight)
            return false;

        if (farm.isWaterTile(tileX, tileY))
            return false;

        // No "Back" tile means the tile doesn't exist (cliffs, void, etc.)
        if (farm.Map.GetLayer("Back")?.Tiles[tileX, tileY] is null)
            return false;

        // Don't stack animals
        foreach (var pair in farm.Animals.Pairs)
        {
            FarmAnimal other = pair.Value;
            if (other != self && other.TilePoint.X == tileX && other.TilePoint.Y == tileY)
                return false;
        }

        return true;
    }
}
