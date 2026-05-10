namespace CutsceneMaker.Commands;

public static class BetterEventsCoreCommandProvider
{
    public const string ModId = "AlexGoD.BetterEventsCore";
    private const string ProviderName = "Better Events Core";
    private const string Badge = "BEC";

    public static IEnumerable<EventCommandDefinition> GetDefinitions()
    {
        yield return Define("removeClothes", "Remove Clothes", "removeClothes", new EventCommandParameter
        {
            Key = "target",
            Label = "Part",
            Type = EventCommandParameterType.Choice,
            DefaultValue = "all",
            Choices = new[] { "all", "top", "bottom" }
        });

        yield return Define("saveClothes", "Save Clothes", "saveClothes", Text("slot", "Slot", "standard"));
        yield return Define("restoreClothes", "Restore Clothes", "restoreClothes", Text("slot", "Slot", "standard"));
        yield return Define("loopSound", "Loop Sound", "loopSound", Text("sound", "Sound", "gulp"), Integer("delay", "Delay", "35"), OptionalInteger("repeatCount", "Repeats"));
        yield return Define("loopSoundStop", "Stop Loop Sound", "loopSoundStop", Text("sound", "Sound", "gulp"));
        yield return Define("advShake", "Advanced Shake", "advShake", Actor("target", "Target"), Integer("amplitudeX", "Amp X", "1"), Integer("delayX", "Delay X", "50"), Integer("amplitudeY", "Amp Y", "1"), Integer("delayY", "Delay Y", "50"));
        yield return Define("advShakeStop", "Stop Advanced Shake", "advShakeStop", Actor("target", "Target"));
        yield return Define("timeSkip", "Time Skip", "timeSkip", Integer("timeToAdd", "Time", "10"));
    }

    private static EventCommandDefinition Define(string id, string displayName, string verb, params EventCommandParameter[] parameters)
    {
        return new EventCommandDefinition
        {
            Id = id,
            ProviderModId = ModId,
            ProviderName = ProviderName,
            DisplayName = displayName,
            Verb = verb,
            Badge = Badge,
            Parameters = parameters
        };
    }

    private static EventCommandParameter Text(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Text, DefaultValue = defaultValue };
    }

    private static EventCommandParameter Integer(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Integer, DefaultValue = defaultValue };
    }

    private static EventCommandParameter OptionalInteger(string key, string label)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.OptionalInteger };
    }

    private static EventCommandParameter Actor(string key, string label)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Actor, DefaultValue = "farmer" };
    }
}
