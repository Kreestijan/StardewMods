using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace LoadTimeProfiler;

public static class HarmonyPatches
{
    public static void ApplyLoadProfilerPatches(Harmony harmony)
    {
        Type contextType = AccessTools.TypeByName("StardewModdingAPI.Context")
            ?? throw new MissingMemberException("Could not find SMAPI Context.");
        PropertyInfo heuristicProperty = contextType.GetProperty("HeuristicModsRunningCode", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMemberException("Could not find Context.HeuristicModsRunningCode.");
        Type heuristicStackType = heuristicProperty.PropertyType;
        MethodInfo pushMethod = heuristicStackType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == "Push" && m.GetParameters().Length == 1);
        MethodInfo tryPopMethod = heuristicStackType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == "TryPop" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsByRef);

        harmony.Patch(pushMethod, prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HeuristicPushPrefix)));
        harmony.Patch(tryPopMethod, postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HeuristicTryPopPostfix)));
    }

    public static void HeuristicPushPrefix(object[] __args) => LoadProfiler.BeforeHeuristicPush(__args);
    public static void HeuristicTryPopPostfix(bool __result, object[] __args) => LoadProfiler.AfterHeuristicTryPop(__result, __args);

    public static int ApplyRuntimeProfilerPatches(Harmony harmony)
    {
        Type eventManagerType = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.EventManager")
            ?? throw new MissingMemberException("Could not find SMAPI EventManager.");
        Type managedEventOpenType = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ManagedEvent`1")
            ?? throw new MissingMemberException("Could not find SMAPI ManagedEvent<T>.");

        MethodInfo raiseMethodDefinition = managedEventOpenType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method =>
                method.Name == "Raise"
                && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType.IsGenericParameter);

        int patchCount = 0;
        HashSet<MethodInfo> patchedMethods = new();

        foreach (FieldInfo field in eventManagerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type fieldType = field.FieldType;
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != managedEventOpenType)
            {
                continue;
            }

            Type eventArgsType = fieldType.GenericTypeArguments[0];
            MethodInfo closedRaiseMethod = AccessTools.Method(fieldType, raiseMethodDefinition.Name, new[] { eventArgsType })
                ?? throw new MissingMethodException($"Could not find SMAPI managed event raise method for {fieldType.FullName}.");

            if (!patchedMethods.Add(closedRaiseMethod))
            {
                continue;
            }

            harmony.Patch(
                closedRaiseMethod,
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(ManagedEventEnterPrefix)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(ManagedEventExitPostfix)),
                transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(ManagedEventRaiseArgsTranspiler))
            );

            patchCount++;
        }

        return patchCount;
    }

    public static void ManagedEventEnterPrefix(object __instance)
    {
        RuntimeProfiler.EnterManagedEvent(__instance);
    }

    public static void ManagedEventExitPostfix()
    {
        RuntimeProfiler.ExitManagedEvent();
    }

    public static IEnumerable<CodeInstruction> ManagedEventRaiseArgsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo invokeManagedHandlerMethod = AccessTools.Method(typeof(RuntimeProfiler), nameof(RuntimeProfiler.InvokeManagedHandler))
            ?? throw new MissingMethodException("Could not find RuntimeProfiler.InvokeManagedHandler.");

        List<CodeInstruction> codes = instructions.ToList();

        for (int index = 0; index <= codes.Count - 5; index++)
        {
            if (!codes[index].IsLdloc()
                || codes[index + 1].operand is not MethodInfo handlerGetter
                || handlerGetter.Name != "get_Handler"
                || codes[index + 2].opcode != OpCodes.Ldnull
                || !codes[index + 3].IsLdarg(1)
                || codes[index + 4].opcode != OpCodes.Callvirt)
            {
                continue;
            }

            MethodInfo? invokeMethod = codes[index + 4].operand as MethodInfo;
            if (invokeMethod?.Name != "Invoke" || invokeMethod.DeclaringType is null || !typeof(MulticastDelegate).IsAssignableFrom(invokeMethod.DeclaringType.BaseType))
            {
                continue;
            }

            Type eventArgsType = invokeMethod.GetParameters()[1].ParameterType;

            List<Label> labels = codes[index].labels.ToList();
            List<ExceptionBlock> blocks = codes[index].blocks.ToList();

            codes[index] = new CodeInstruction(codes[index].opcode, codes[index].operand)
            {
                labels = labels,
                blocks = blocks
            };
            codes[index + 1] = new CodeInstruction(OpCodes.Ldarg_1);
            codes[index + 2] = eventArgsType.IsValueType
                ? new CodeInstruction(OpCodes.Box, eventArgsType)
                : new CodeInstruction(OpCodes.Nop);
            codes[index + 3] = new CodeInstruction(OpCodes.Call, invokeManagedHandlerMethod);
            codes.RemoveAt(index + 4);

            return codes;
        }

        throw new InvalidOperationException("Could not find managed event handler invocation in SMAPI ManagedEvent<T>.Raise(TEventArgs).");
    }
}
