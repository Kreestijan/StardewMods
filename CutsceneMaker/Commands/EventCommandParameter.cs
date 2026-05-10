namespace CutsceneMaker.Commands;

public sealed class EventCommandParameter
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public EventCommandParameterType Type { get; init; }

    public string DefaultValue { get; init; } = string.Empty;

    public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();

    public int TextLimit { get; init; } = 120;

    public bool QuoteWhenNeeded { get; init; } = true;

    public bool Optional { get; init; }

    public string Hint { get; init; } = string.Empty;
}
