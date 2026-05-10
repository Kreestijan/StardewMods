namespace CutsceneMaker.Models;

public sealed class EventPreconditionBlock
{
    public string PreconditionId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Verb { get; set; } = string.Empty;

    public bool Negated { get; set; }

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
}
