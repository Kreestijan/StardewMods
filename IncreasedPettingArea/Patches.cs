using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Reflection.Emit;
using StardewValley;

namespace IncreasedPettingArea;

/*
Harmony justification:
- SMAPI has no public event/API that fires after a successful manual FarmAnimal pet with access to vanilla's final
  wasPet state and no API to ask vanilla to pet multiple animals.
- Content/data/assets/framework integrations cannot change this runtime behavior.
- Reflection alone cannot safely alter FarmAnimal.pet's early return while the farmer is in the first pet animation.
- Exact method patched: StardewValley.FarmAnimal.pet(Farmer who, bool is_auto_pet), the narrowest point where vanilla
  petting state is known and where nearby animals can be routed back through vanilla petting.
- Patch form: postfix reacts only after a successful manual pet; transpiler replaces the PauseForSingleAnimation getter
  with a helper that ignores that guard only during this mod's internal nearby-petting loop.
- Compatibility risk: other mods patching FarmAnimal.pet may alter prefix/postfix order. The nearby calls use
  is_auto_pet:false intentionally so vanilla and other manual-petting integrations still run.
*/
/// <summary>
/// After the farmer pets an animal, also pet all un-petted animals
/// within the configured radius. Works on both Farm and AnimalHouse locations.
/// </summary>
[HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.pet))]
internal static class FarmAnimalPetPatch
{
    private static bool _isPettingNearby;

    public static void Prefix(FarmAnimal __instance, bool is_auto_pet, out bool __state)
    {
        __state = !is_auto_pet && !__instance.wasPet.Value;
    }

    public static void Postfix(FarmAnimal __instance, Farmer who, bool is_auto_pet, bool __state)
    {
        if (!ModEntry.Instance.Config.EnableMod || is_auto_pet || _isPettingNearby)
            return;

        if (!__state || !__instance.wasPet.Value)
            return;

        if (who is null || who.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
            return;

        if (who.currentLocation is not { } loc || !loc.Animals.Pairs.Any())
            return;

        int radius = ModEntry.Instance.Config.PetRadius;
        int radiusSquared = radius * radius;
        Point center = __instance.TilePoint;

        _isPettingNearby = true;
        try
        {
            foreach (var pair in loc.Animals.Pairs)
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

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        var pauseGetter = AccessTools.PropertyGetter(typeof(FarmerSprite), nameof(FarmerSprite.PauseForSingleAnimation));
        var replacement = AccessTools.Method(typeof(FarmAnimalPetPatch), nameof(ShouldHonorPauseForSingleAnimation));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, pauseGetter))
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = replacement;
                return codes;
            }
        }

        ModEntry.Instance.Monitor.Log("IncreasedPettingArea: Could not find FarmAnimal.pet animation pause check.", StardewModdingAPI.LogLevel.Warn);
        return codes;
    }

    private static bool ShouldHonorPauseForSingleAnimation(FarmerSprite sprite)
    {
        return !_isPettingNearby && sprite.PauseForSingleAnimation;
    }
}
