using CutsceneMaker.Models;
using Newtonsoft.Json.Linq;

namespace CutsceneMaker.Importer;

public static class ContentPackImporter
{
    private const string EventTargetPrefix = "Data/Events/";

    public static List<CutsceneData> Import(string contentJsonPath)
    {
        if (string.IsNullOrWhiteSpace(contentJsonPath))
        {
            throw new ArgumentException("Content path is required.", nameof(contentJsonPath));
        }

        JObject content = JObject.Parse(File.ReadAllText(contentJsonPath));
        return Import(content);
    }

    public static List<CutsceneData> Import(JObject content)
    {
        ArgumentNullException.ThrowIfNull(content);

        List<CutsceneData> cutscenes = new();
        if (content["Changes"] is not JArray changes)
        {
            return cutscenes;
        }

        foreach (JToken change in changes)
        {
            string? action = change.Value<string>("Action");
            string? target = change.Value<string>("Target");
            if (!string.Equals(action, "EditData", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(target)
                || !target.StartsWith(EventTargetPrefix, StringComparison.Ordinal)
                || change["Entries"] is not JObject entries)
            {
                continue;
            }

            string locationName = target[EventTargetPrefix.Length..];
            foreach (JProperty entry in entries.Properties())
            {
                (string uniqueId, List<PreconditionData> triggers) = EventKeyParser.Parse(entry.Name);
                CutsceneData cutscene = CutsceneData.CreateBlank();
                cutscene.CutsceneName = uniqueId;
                cutscene.UniqueId = uniqueId;
                cutscene.LocationName = locationName;
                cutscene.Triggers = triggers;
                cutscene.Commands = EventScriptParser.Parse(entry.Value.Value<string>() ?? string.Empty, cutscene);
                cutscenes.Add(cutscene);
            }
        }

        return cutscenes;
    }
}
