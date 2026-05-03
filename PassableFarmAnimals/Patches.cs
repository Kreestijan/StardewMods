using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using System.Reflection.Emit;

namespace PassableFarmAnimals;

internal static class FarmAnimalCollisionPatch
{
    private static bool ShouldSkipAnimalCheck(Character character)
        => character is Farmer && ModEntry.Instance.config.EnableMod;

    internal static IEnumerable<CodeInstruction> ApplyTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        bool patched = false;

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Isinst &&
                codes[i].operand is Type type &&
                type == typeof(FarmAnimal) &&
                i + 1 < codes.Count &&
                (codes[i + 1].opcode == OpCodes.Brtrue_S || codes[i + 1].opcode == OpCodes.Brtrue))
            {
                var branchTarget = (Label)codes[i + 1].operand;
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_S, (byte)6), // character (6th param, 0=this)
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FarmAnimalCollisionPatch), nameof(ShouldSkipAnimalCheck))),
                    new CodeInstruction(OpCodes.Brtrue_S, branchTarget)
                });
                patched = true;
                break;
            }
        }

        if (!patched)
        {
            ModEntry.Instance.Monitor.Log("PassableFarmAnimals: Could not find animal collision IL pattern.", StardewModdingAPI.LogLevel.Warn);
        }

        return codes;
    }
}
