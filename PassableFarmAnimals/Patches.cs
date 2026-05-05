using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using System.Reflection;
using System.Reflection.Emit;

namespace PassableFarmAnimals;

internal static class FarmAnimalCollisionPatch
{
    private static bool ShouldSkipAnimalCheck(Character character)
        => character is Farmer && ModEntry.Instance.config.EnableMod;

    internal static IEnumerable<CodeInstruction> ApplyTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var codes = instructions.ToList();
        int characterArgIndex = GetCharacterArgumentIndex(__originalMethod);
        if (characterArgIndex < 0)
        {
            ModEntry.Instance.Monitor.Log(
                $"PassableFarmAnimals: Could not identify character parameter for {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}.",
                StardewModdingAPI.LogLevel.Error
            );
            return codes;
        }

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
                    CreateLoadArgumentInstruction(characterArgIndex),
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

    private static int GetCharacterArgumentIndex(MethodBase method)
    {
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (typeof(Character).IsAssignableFrom(parameters[i].ParameterType))
                return method.IsStatic ? i : i + 1;
        }

        return -1;
    }

    private static CodeInstruction CreateLoadArgumentInstruction(int index)
    {
        return index switch
        {
            0 => new CodeInstruction(OpCodes.Ldarg_0),
            1 => new CodeInstruction(OpCodes.Ldarg_1),
            2 => new CodeInstruction(OpCodes.Ldarg_2),
            3 => new CodeInstruction(OpCodes.Ldarg_3),
            <= byte.MaxValue => new CodeInstruction(OpCodes.Ldarg_S, (byte)index),
            _ => new CodeInstruction(OpCodes.Ldarg, index)
        };
    }
}

/*
Harmony justification:
- SMAPI draw events can render extra sprites, but they cannot suppress or offset the vanilla FarmAnimal draw call.
- Content/data/assets/framework integrations cannot create a temporary per-animal draw-only offset.
- Reflection alone cannot affect drawing.
- Exact method patched: StardewValley.FarmAnimal.draw(SpriteBatch), the narrowest point where an individual animal is
  rendered.
- Patch form: prefix/postfix/finalizer temporarily offsets Position for the duration of the draw call only. The original
  position is restored even if another draw patch throws.
- Compatibility risk: other mods patching FarmAnimal.draw may see the temporary draw position depending on patch order.
  No saved position, tile, controller, or pathing state is intentionally changed.
*/
internal static class FarmAnimalDrawPatch
{
    internal static void Prefix(FarmAnimal __instance, out NudgeDrawState __state)
    {
        __state = NudgeDrawState.None;
        if (!ModEntry.Instance.Nudges.TryGetDrawOffset(__instance, out Vector2 offset))
        {
            return;
        }

        __state = new NudgeDrawState(Applied: true, OriginalPosition: __instance.Position);
        __instance.Position += offset;
    }

    internal static void Postfix(FarmAnimal __instance, NudgeDrawState __state)
    {
        RestorePosition(__instance, __state);
    }

    internal static void Finalizer(FarmAnimal __instance, NudgeDrawState __state)
    {
        RestorePosition(__instance, __state);
    }

    private static void RestorePosition(FarmAnimal animal, NudgeDrawState state)
    {
        if (state.Applied)
        {
            animal.Position = state.OriginalPosition;
        }
    }
}

internal readonly record struct NudgeDrawState(bool Applied, Vector2 OriginalPosition)
{
    internal static readonly NudgeDrawState None = new(Applied: false, OriginalPosition: Vector2.Zero);
}
