using HarmonyLib;
using StardewValley;

namespace PassableFarmAnimals;

/// <summary>
/// When the farmer moves, temporarily remove farm animals from the location
/// so they don't block collision detection. Restore them immediately after.
/// </summary>
[HarmonyPatch(typeof(GameLocation), nameof(GameLocation.isCollidingPosition))]
internal static class FarmAnimalCollisionPatch
{
    private static KeyValuePair<long, FarmAnimal>[]? _savedPairs;

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
        int count = animals.Pairs.Count();
        if (count <= 0)
        {
            return;
        }

        _savedPairs = new KeyValuePair<long, FarmAnimal>[count];
        for (int i = 0; i < count; i++)
        {
            _savedPairs[i] = animals.Pairs.ElementAt(i);
        }

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
    }
}
