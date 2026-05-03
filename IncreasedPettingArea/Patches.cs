using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;

namespace IncreasedPettingArea;

/// <summary>
/// After the farmer pets an animal, also pet all un-petted animals
/// within the configured radius.
/// </summary>
[HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.pet))]
internal static class FarmAnimalPetPatch
{
    private static bool _isPettingNearby;

    public static void Postfix(FarmAnimal __instance, Farmer who, bool is_auto_pet)
    {
        if (!ModEntry.Instance.Config.EnableMod || is_auto_pet || _isPettingNearby)
            return;

        if (who is null || who.currentLocation is not Farm farm)
            return;

        int radius = ModEntry.Instance.Config.PetRadius;
        int radiusSquared = radius * radius;
        Point center = __instance.TilePoint;

        _isPettingNearby = true;
        try
        {
            foreach (var pair in farm.Animals.Pairs)
            {
                FarmAnimal animal = pair.Value;

                if (animal == __instance || animal.wasPet.Value)
                    continue;

                int dx = animal.TilePoint.X - center.X;
                int dy = animal.TilePoint.Y - center.Y;

                if (dx * dx + dy * dy > radiusSquared)
                    continue;

                animal.pet(who, is_auto_pet: false);
            }
        }
        finally
        {
            _isPettingNearby = false;
        }
    }
}
