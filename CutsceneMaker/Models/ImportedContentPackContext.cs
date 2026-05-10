using Newtonsoft.Json;

namespace CutsceneMaker.Models;

public sealed class ImportedContentPackContext
{
    public string ContentJsonPath { get; init; } = string.Empty;

    public string PackRootPath { get; init; } = string.Empty;

    [JsonIgnore]
    public Dictionary<string, PreviewMapOverride> PreviewMapOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Token names from DynamicTokens that the importer couldn't resolve.</summary>
    public List<string> UnresolvedTokens { get; init; } = new();
}
