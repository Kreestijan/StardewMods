using CutsceneMaker.Models;

namespace CutsceneMaker.Compiler;

public static class EventKeyBuilder
{
    public static string Build(CutsceneData cutscene)
    {
        ArgumentNullException.ThrowIfNull(cutscene);

        string eventId = RequireValue(cutscene.UniqueId, nameof(cutscene.UniqueId));
        if (cutscene.Triggers.Count == 0)
        {
            return eventId + "/";
        }

        List<string> parts = new() { eventId };
        foreach (PreconditionData trigger in cutscene.Triggers)
        {
            string compiled = CompileTrigger(trigger);
            parts.Add(trigger.Negated ? "!" + compiled : compiled);
        }

        return string.Join("/", parts);
    }

    private static string CompileTrigger(PreconditionData trigger)
    {
        return trigger.Type switch
        {
            PreconditionType.Time => $"t {RequireInt(trigger.TimeStart, nameof(trigger.TimeStart))} {RequireInt(trigger.TimeEnd, nameof(trigger.TimeEnd))}",
            PreconditionType.Season => $"z {RequireValue(trigger.Season, nameof(trigger.Season)).ToLowerInvariant()}",
            PreconditionType.Weather => $"w {CompileWeather(trigger)}",
            PreconditionType.Year => $"y {RequireInt(trigger.MinYear, nameof(trigger.MinYear))}",
            PreconditionType.DaysPlayed => $"j {RequireInt(trigger.DaysPlayed, nameof(trigger.DaysPlayed))}",
            PreconditionType.Friendship => $"f {RequireValue(trigger.NpcName, nameof(trigger.NpcName))} {RequireInt(trigger.HeartLevel, nameof(trigger.HeartLevel)) * 250}",
            PreconditionType.HasSeenEvent => $"Hn {RequireValue(trigger.FlagOrEventId, nameof(trigger.FlagOrEventId))}",
            PreconditionType.HasMailFlag => $"h {RequireValue(trigger.FlagOrEventId, nameof(trigger.FlagOrEventId))}",
            PreconditionType.GameStateQuery => $"GAME_STATE_QUERY {RequireValue(trigger.QueryString, nameof(trigger.QueryString))}",
            _ => throw new InvalidOperationException($"Unsupported precondition type '{trigger.Type}'.")
        };
    }

    private static string CompileWeather(PreconditionData trigger)
    {
        string weather = RequireValue(trigger.Weather, nameof(trigger.Weather));
        return weather.Equals("Sun", StringComparison.OrdinalIgnoreCase)
            ? "Sun"
            : weather.ToLowerInvariant();
    }

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }

    private static int RequireInt(int? value, string fieldName)
    {
        return value ?? throw new InvalidOperationException($"{fieldName} is required.");
    }
}
