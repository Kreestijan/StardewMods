using CutsceneMaker.Commands;
using CutsceneMaker.Models;

namespace CutsceneMaker.Compiler;

public static class EventKeyBuilder
{
    public static string Build(CutsceneData cutscene, EventPreconditionCatalog? preconditionCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(cutscene);
        preconditionCatalog ??= new EventPreconditionCatalog();

        string eventId = RequireValue(cutscene.UniqueId, nameof(cutscene.UniqueId));
        if (cutscene.Triggers.Count == 0)
        {
            return eventId + "/";
        }

        List<string> parts = new() { eventId };
        foreach (EventPreconditionBlock trigger in cutscene.Triggers)
        {
            if (!preconditionCatalog.TryGetById(trigger.PreconditionId, out EventPreconditionDefinition? definition))
            {
                string raw = RequireValue(trigger.Verb, nameof(trigger.Verb));
                parts.Add(trigger.Negated ? "!" + raw : raw);
                continue;
            }

            parts.Add(definition.Compile(trigger));
        }

        return string.Join("/", parts);
    }

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }
}
