namespace CutsceneMaker.Models;

public sealed class EventCommandBlock
{
    public string ProviderModId { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string CommandId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ActorSlotIds { get; set; } = new(StringComparer.Ordinal);
}
