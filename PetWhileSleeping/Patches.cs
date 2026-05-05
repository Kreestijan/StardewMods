using HarmonyLib;
using System.Reflection.Emit;
using StardewModdingAPI;
using StardewValley;

namespace PetWhileSleeping;

/*
Harmony justification:
- SMAPI public APIs/events were checked first: there is no SMAPI event or public API that changes only FarmAnimal.pet's
  nighttime sleep gate while preserving vanilla petting behavior.
- Content/data/assets/frameworks cannot alter this runtime branch; the requirement is a behavior change inside
  StardewValley.FarmAnimal.pet.
- Reflection alone cannot safely alter the branch; the method must be patched.
- Exact method patched: StardewValley.FarmAnimal.pet(Farmer who, bool is_auto_pet). This is the narrowest viable point
  because the unwanted behavior is a single early return in that method.
- Patch form: transpiler replaces the time-of-day read used by the sleep check with a helper-controlled value, so the
  sleep block is skipped only when this mod is enabled. Prefix/postfix only record and adjust the final vanilla
  friendship delta for the optional sleeping penalty.
- Compatibility risk: other mods transpiling the same sleep check may conflict if they expect the original IL shape.
  This patch avoids replacing vanilla pet logic, so it is less invasive than a prefix that returns false.
*/
[HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.pet))]
internal static class FarmAnimalPetPatch
{
    public static void Prefix(FarmAnimal __instance, Farmer who, bool is_auto_pet, out SleepingPetContext? __state)
    {
        __state = ModEntry.BeginSleepingPet(__instance, who, is_auto_pet);
    }

    public static void Postfix(FarmAnimal __instance, SleepingPetContext? __state)
    {
        ModEntry.ApplySleepingFriendshipPenalty(__instance, __state);
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        var timeOfDayField = AccessTools.Field(typeof(Game1), nameof(Game1.timeOfDay));
        var replacement = AccessTools.Method(typeof(ModEntry), nameof(ModEntry.GetTimeOfDayForPetSleepCheck));

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].opcode == OpCodes.Ldsfld
                && Equals(codes[i].operand, timeOfDayField)
                && LoadsInt(codes[i + 1], 1900))
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = replacement;
                return codes;
            }
        }

        ModEntry.Instance.Monitor.Log(
            "Could not find FarmAnimal.pet sleep check; sleeping pet support was not applied.",
            LogLevel.Warn
        );
        return codes;
    }

    private static bool LoadsInt(CodeInstruction instruction, int value)
    {
        if (instruction.opcode == OpCodes.Ldc_I4)
        {
            return instruction.operand is int operand && operand == value;
        }

        if (instruction.opcode == OpCodes.Ldc_I4_S)
        {
            return instruction.operand is sbyte operand && operand == value;
        }

        if (value < -1 || value > 8)
        {
            return false;
        }

        return value switch
        {
            -1 => instruction.opcode == OpCodes.Ldc_I4_M1,
            0 => instruction.opcode == OpCodes.Ldc_I4_0,
            1 => instruction.opcode == OpCodes.Ldc_I4_1,
            2 => instruction.opcode == OpCodes.Ldc_I4_2,
            3 => instruction.opcode == OpCodes.Ldc_I4_3,
            4 => instruction.opcode == OpCodes.Ldc_I4_4,
            5 => instruction.opcode == OpCodes.Ldc_I4_5,
            6 => instruction.opcode == OpCodes.Ldc_I4_6,
            7 => instruction.opcode == OpCodes.Ldc_I4_7,
            8 => instruction.opcode == OpCodes.Ldc_I4_8,
            _ => false
        };
    }
}
