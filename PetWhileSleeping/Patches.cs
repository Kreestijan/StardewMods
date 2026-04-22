using HarmonyLib;
using StardewValley;

namespace PetWhileSleeping;

[HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.pet))]
internal static class FarmAnimalPetPatch
{
    public static bool Prefix(FarmAnimal __instance, Farmer who, bool is_auto_pet)
    {
        if (!ModEntry.ShouldHandleSleepingPet(__instance, who, is_auto_pet))
        {
            return true;
        }

        ModEntry.PetSleepingAnimal(__instance, who);
        return false;
    }
}
