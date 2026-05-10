using CutsceneMaker.Models;

namespace CutsceneMaker.Commands;

public sealed class EventCommandDefinition
{
    public string Id { get; init; } = string.Empty;

    public string ProviderModId { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Verb { get; init; } = string.Empty;

    public string Badge { get; init; } = string.Empty;

    public IReadOnlyList<EventCommandParameter> Parameters { get; init; } = Array.Empty<EventCommandParameter>();

    public bool UnsafeForPreview { get; init; }

    public EventCommandBlock CreateDefaultBlock()
    {
        EventCommandBlock block = new()
        {
            ProviderModId = this.ProviderModId,
            ProviderName = this.ProviderName,
            CommandId = this.Id,
            DisplayName = this.DisplayName
        };

        foreach (EventCommandParameter parameter in this.Parameters)
        {
            if (parameter.Type == EventCommandParameterType.AnswerList)
            {
                string[] answers = parameter.DefaultValue
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                block.Values[$"{parameter.Key}.count"] = Math.Max(1, answers.Length).ToString();
                for (int index = 0; index < answers.Length; index++)
                {
                    block.Values[$"{parameter.Key}.{index}"] = answers[index];
                }

                if (answers.Length == 0)
                {
                    block.Values[$"{parameter.Key}.0"] = string.Empty;
                }

                continue;
            }

            block.Values[parameter.Key] = parameter.DefaultValue;
        }

        return block;
    }

    public string Compile(EventCommandBlock command, Func<string?, string?, string> resolveActor)
    {
        List<string> parts = new() { this.Verb };
        foreach (EventCommandParameter parameter in this.Parameters)
        {
            string value = command.Values.TryGetValue(parameter.Key, out string? configuredValue)
                ? configuredValue
                : parameter.DefaultValue;

            if (parameter.Type == EventCommandParameterType.Actor)
            {
                string? slotId = command.ActorSlotIds.TryGetValue(parameter.Key, out string? configuredSlot)
                    ? configuredSlot
                    : null;
                value = resolveActor(slotId, value);
            }
            else if (parameter.Type == EventCommandParameterType.OptionalActor && !string.IsNullOrWhiteSpace(value))
            {
                string? slotId = command.ActorSlotIds.TryGetValue(parameter.Key, out string? configuredSlot)
                    ? configuredSlot
                    : null;
                value = resolveActor(slotId, value);
                if (!value.EndsWith("?", StringComparison.Ordinal))
                {
                    value += "?";
                }
            }

            if ((parameter.Type == EventCommandParameterType.OptionalInteger || parameter.Optional) && string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (parameter.Type == EventCommandParameterType.RawArguments)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value);
                }

                continue;
            }

            if (parameter.Type == EventCommandParameterType.Text && parameter.QuoteWhenNeeded)
            {
                value = QuoteArgumentIfNeeded(value);
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        return string.Join(" ", parts);
    }

    private static string QuoteArgumentIfNeeded(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
        {
            return value; // CP token — output verbatim
        }

        if (value.Length == 0 || value.Any(character => char.IsWhiteSpace(character) || character == '/'))
        {
            return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
