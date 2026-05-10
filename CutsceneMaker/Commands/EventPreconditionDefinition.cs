using CutsceneMaker.Models;

namespace CutsceneMaker.Commands;

public sealed class EventPreconditionDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Verb { get; init; } = string.Empty;

    public IReadOnlyList<EventCommandParameter> Parameters { get; init; } = Array.Empty<EventCommandParameter>();

    public EventPreconditionBlock CreateDefaultBlock()
    {
        EventPreconditionBlock block = new()
        {
            PreconditionId = this.Id,
            DisplayName = this.DisplayName,
            Verb = this.Verb
        };

        foreach (EventCommandParameter parameter in this.Parameters)
        {
            block.Values[parameter.Key] = parameter.DefaultValue;
        }

        return block;
    }

    public string Compile(EventPreconditionBlock block)
    {
        List<string> parts = new() { this.Verb };
        foreach (EventCommandParameter parameter in this.Parameters)
        {
            string value = block.Values.TryGetValue(parameter.Key, out string? configuredValue)
                ? configuredValue
                : parameter.DefaultValue;

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

        string compiled = string.Join(" ", parts);
        return block.Negated ? "!" + compiled : compiled;
    }

    private static string QuoteArgumentIfNeeded(string value)
    {
        if (value.Length == 0 || value.Any(character => char.IsWhiteSpace(character) || character == '/'))
        {
            return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
