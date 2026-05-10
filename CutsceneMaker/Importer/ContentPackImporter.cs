using CutsceneMaker.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CutsceneMaker.Importer;

public static class ContentPackImporter
{
    private const string EventTargetPrefix = "Data/Events/";

    public static List<CutsceneData> Import(string contentJsonPath)
    {
        if (!TryImport(contentJsonPath, out List<CutsceneData> cutscenes, out string error))
        {
            throw new InvalidOperationException(error);
        }

        return cutscenes;
    }

    public static bool TryImport(string contentJsonPath, out List<CutsceneData> cutscenes, out string error)
    {
        cutscenes = new List<CutsceneData>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(contentJsonPath))
        {
            error = "Content path is required.";
            return false;
        }

        string fullContentPath;
        try
        {
            fullContentPath = Path.GetFullPath(contentJsonPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = ex.Message;
            return false;
        }

        if (!File.Exists(fullContentPath))
        {
            error = $"File not found: {fullContentPath}";
            return false;
        }

        try
        {
            ContentPackImportSession session = new(fullContentPath);
            session.Import();
            cutscenes = session.Cutscenes;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static List<CutsceneData> Import(IEnumerable<string> contentJsonPaths)
    {
        ArgumentNullException.ThrowIfNull(contentJsonPaths);

        List<CutsceneData> cutscenes = new();
        foreach (string contentJsonPath in contentJsonPaths)
        {
            cutscenes.AddRange(Import(contentJsonPath));
        }

        return cutscenes;
    }

    public static List<CutsceneData> Import(JObject content)
    {
        ArgumentNullException.ThrowIfNull(content);

        ContentPackImportSession session = new(content);
        session.Import();
        return session.Cutscenes;
    }

    private sealed class ContentPackImportSession
    {
        private readonly string? rootContentPath;
        private readonly string packRoot;
        private readonly ImportedContentPackContext? context;
        private readonly HashSet<string> visitedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase);
        private readonly JObject? rootContentObject;

        public ContentPackImportSession(string rootContentPath)
        {
            this.rootContentPath = rootContentPath;
            this.packRoot = Path.GetDirectoryName(rootContentPath) ?? string.Empty;
            this.context = new ImportedContentPackContext
            {
                ContentJsonPath = rootContentPath,
                PackRootPath = this.packRoot
            };
        }

        public ContentPackImportSession(JObject rootContentObject)
        {
            this.rootContentObject = rootContentObject;
            this.packRoot = string.Empty;
        }

        public List<CutsceneData> Cutscenes { get; } = new();

        public void Import()
        {
            if (this.rootContentObject is not null)
            {
                this.ImportContentObject(this.rootContentObject, contentPath: null, currentDirectory: Directory.GetCurrentDirectory());
                return;
            }

            if (this.rootContentPath is null)
            {
                return;
            }

            this.ImportContentFile(this.rootContentPath, Path.GetDirectoryName(this.rootContentPath) ?? this.packRoot);
        }

        private void ImportContentFile(string contentPath, string currentDirectory)
        {
            string fullContentPath = Path.GetFullPath(contentPath);
            if (!this.visitedFiles.Add(fullContentPath))
            {
                return;
            }

            JToken token;
            using (JsonTextReader reader = new(File.OpenText(fullContentPath)))
            {
                token = JToken.Load(reader);
            }

            switch (token)
            {
                case JObject content:
                    this.ImportContentObject(content, fullContentPath, Path.GetDirectoryName(fullContentPath) ?? currentDirectory);
                    break;

                case JArray changes:
                    this.ImportChanges(changes, fullContentPath, Path.GetDirectoryName(fullContentPath) ?? currentDirectory);
                    break;

                default:
                    throw new JsonReaderException($"JSON root is {token.Type}, expected object or array.");
            }
        }

        private void ImportContentObject(JObject content, string? contentPath, string currentDirectory)
        {
            this.ImportConfigDefaults(content);
            this.ImportDynamicTokens(content["DynamicTokens"]);

            if (content["Changes"] is JArray changes)
            {
                this.ImportChanges(changes, contentPath, currentDirectory);
            }
        }

        private void ImportConfigDefaults(JObject content)
        {
            if (content["ConfigSchema"] is not JObject schema)
            {
                return;
            }

            foreach (JProperty property in schema.Properties())
            {
                if (property.Value is not JObject config || config["Default"] is null)
                {
                    continue;
                }

                string? value = config["Default"]!.Type == JTokenType.String
                    ? config.Value<string>("Default")
                    : config["Default"]!.ToString(Formatting.None);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    this.tokens.TryAdd(property.Name, value);
                }
            }
        }

        private void ImportDynamicTokens(JToken? dynamicTokens)
        {
            if (dynamicTokens is not JArray tokenArray)
            {
                return;
            }

            foreach (JObject token in tokenArray.OfType<JObject>())
            {
                string? name = token.Value<string>("Name");
                string? value = token.Value<string>("Value");
                if (string.IsNullOrWhiteSpace(name) || value is null)
                {
                    continue;
                }

                // Expand using current tokens dict (which includes values from previous entries
                // for the same name). This handles self-referencing concatenation correctly:
                // "{{TokenName}}extra" expands to the previous value + "extra".
                this.tokens[name] = this.ExpandText(value, fieldTokens: null);
            }
        }

        private void ImportChanges(JArray changes, string? contentPath, string currentDirectory)
        {
            foreach (JObject change in changes.OfType<JObject>())
            {
                string action = this.ExpandText(change.Value<string>("Action") ?? string.Empty, fieldTokens: null);
                if (string.IsNullOrWhiteSpace(action))
                {
                    continue;
                }

                if ("Include".Equals(action, StringComparison.OrdinalIgnoreCase))
                {
                    this.ImportIncludedChanges(change, currentDirectory);
                    continue;
                }

                if ("Load".Equals(action, StringComparison.OrdinalIgnoreCase))
                {
                    this.ImportLoadMap(change, currentDirectory);
                    this.ImportLoadEvents(change, currentDirectory);
                    continue;
                }

                if ("EditData".Equals(action, StringComparison.OrdinalIgnoreCase))
                {
                    this.ImportEditDataEvents(change);
                }
            }
        }

        private void ImportIncludedChanges(JObject change, string currentDirectory)
        {
            foreach (string fromFile in SplitContentPatcherList(change.Value<string>("FromFile")))
            {
                string expandedFromFile = this.ExpandText(fromFile, fieldTokens: null);
                if (LooksTokenized(expandedFromFile))
                {
                    continue;
                }

                string? resolved = ResolveContentPatcherFile(this.packRoot, currentDirectory, expandedFromFile);
                if (resolved is null)
                {
                    continue;
                }

                this.ImportContentFile(resolved, Path.GetDirectoryName(resolved) ?? currentDirectory);
            }
        }

        private void ImportLoadMap(JObject change, string currentDirectory)
        {
            if (this.context is null || !IsUngatedOrNewSavePatch(change["When"]))
            {
                return;
            }

            string? rawTarget = change.Value<string>("Target");
            string? rawFromFile = change.Value<string>("FromFile");
            if (string.IsNullOrWhiteSpace(rawTarget) || string.IsNullOrWhiteSpace(rawFromFile))
            {
                return;
            }

            foreach (string target in SplitContentPatcherList(this.ExpandText(rawTarget, fieldTokens: null)))
            {
                string targetMapPath = NormalizeMapPath(target);
                if (!targetMapPath.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Dictionary<string, string> fieldTokens = GetTargetFieldTokens(targetMapPath);
                string fromFile = this.ExpandText(rawFromFile, fieldTokens);
                if (LooksTokenized(fromFile))
                {
                    continue;
                }

                string? sourceFilePath = ResolveContentPatcherFile(this.packRoot, currentDirectory, fromFile);
                if (sourceFilePath is null || !IsMapFile(sourceFilePath))
                {
                    continue;
                }

                this.context.PreviewMapOverrides[targetMapPath] = new PreviewMapOverride(
                    TargetMapPath: targetMapPath,
                    PreviewAssetPath: GetPreviewMapAssetPath(targetMapPath),
                    SourceFilePath: sourceFilePath,
                    SourceRootPath: this.packRoot,
                    SourceName: Path.GetFileName(this.packRoot)
                );
            }
        }

        private void ImportLoadEvents(JObject change, string currentDirectory)
        {
            string? rawTarget = change.Value<string>("Target");
            string? rawFromFile = change.Value<string>("FromFile");
            if (string.IsNullOrWhiteSpace(rawTarget) || string.IsNullOrWhiteSpace(rawFromFile))
            {
                return;
            }

            foreach (string target in SplitContentPatcherList(this.ExpandText(rawTarget, fieldTokens: null)))
            {
                if (!target.StartsWith(EventTargetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Dictionary<string, string> fieldTokens = GetTargetFieldTokens(target);
                string fromFile = this.ExpandText(rawFromFile, fieldTokens);
                if (LooksTokenized(fromFile))
                {
                    continue;
                }

                string? sourceFilePath = ResolveContentPatcherFile(this.packRoot, currentDirectory, fromFile);
                if (sourceFilePath is null)
                {
                    continue;
                }

                JToken token;
                using (JsonTextReader reader = new(File.OpenText(sourceFilePath)))
                {
                    token = JToken.Load(reader);
                }

                JObject? entries = token as JObject;
                if (entries?["Entries"] is JObject nestedEntries)
                {
                    entries = nestedEntries;
                }

                if (entries is not null)
                {
                    string locationName = target[EventTargetPrefix.Length..];
                    this.ImportEventEntries(locationName, entries);
                }
            }
        }

        private void ImportEditDataEvents(JObject change)
        {
            string? rawTarget = change.Value<string>("Target");
            if (string.IsNullOrWhiteSpace(rawTarget) || change["Entries"] is not JObject entries)
            {
                return;
            }

            foreach (string target in SplitContentPatcherList(this.ExpandText(rawTarget, fieldTokens: null)))
            {
                if (!target.StartsWith(EventTargetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string locationName = target[EventTargetPrefix.Length..];
                this.ImportEventEntries(locationName, entries);
            }
        }

        private void ImportEventEntries(string locationName, JObject entries)
        {
            string expandedLocationName = this.ExpandText(locationName, fieldTokens: null);
            if (string.IsNullOrWhiteSpace(expandedLocationName) || LooksTokenized(expandedLocationName))
            {
                return;
            }

            foreach (JProperty entry in entries.Properties())
            {
                if (entry.Value.Type != JTokenType.String)
                {
                    continue;
                }

                string expandedKey = this.ExpandText(entry.Name ?? string.Empty, fieldTokens: null);
                string expandedScript = this.ExpandText(entry.Value.Value<string>() ?? string.Empty, fieldTokens: null);

                CutsceneData cutscene = CutsceneData.CreateBlank();
                cutscene.LocationName = expandedLocationName;

                bool keyHasTokens = LooksTokenized(expandedKey);
                bool scriptHasTokens = LooksTokenized(expandedScript);

                // Always parse the key — EventKeyParser.Parse handles {{...}} gracefully
                (string uniqueId, List<EventPreconditionBlock> triggers) = EventKeyParser.Parse(expandedKey, ModEntry.Instance.PreconditionCatalog);
                cutscene.UniqueId = uniqueId;
                cutscene.CutsceneName = uniqueId;
                cutscene.Triggers = triggers;

                if (keyHasTokens)
                {
                    // Key has unresolved tokens — store raw key for passthrough
                    cutscene.RawEventKey = entry.Name;
                    cutscene.HasUnresolvedTokens = true;
                }

                // Phase G: Restructure if expanded token at entry [0] produced placements
                // instead of a music track, e.g. {{Summoning_SetUp}} -> "farmer 26 55 2 Evelyn ..."
                string adjustedScript = RestructureExpandedScript(expandedScript);

                // Parse script — existing parser handles tokenized content gracefully:
                // - Known verb + token args → EventCommandBlock with token in Values
                // - Unknown verb / standalone token → RawCommandBlock with token text preserved
                cutscene.Commands = EventScriptParser.Parse(adjustedScript, cutscene, ModEntry.Instance.CommandCatalog);

                if (scriptHasTokens)
                {
                    cutscene.HasUnresolvedTokens = true;
                }

                cutscene.ImportContext = this.context;
                this.Cutscenes.Add(cutscene);
            }
        }

        private string ExpandText(string text, IReadOnlyDictionary<string, string>? fieldTokens)
        {
            string result = text;
            for (int pass = 0; pass < 10; pass++)
            {
                string next = this.ExpandTextOnce(result, fieldTokens);
                if (next.Equals(result, StringComparison.Ordinal))
                {
                    return next;
                }

                result = next;
            }

            return result;
        }

        private string ExpandTextOnce(string text, IReadOnlyDictionary<string, string>? fieldTokens)
        {
            int start = text.IndexOf("{{", StringComparison.Ordinal);
            if (start < 0)
            {
                return text;
            }

            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                return text;
            }

            string tokenName = text[(start + 2)..end].Trim();
            string? replacement = ResolveToken(tokenName, fieldTokens);
            if (replacement is null)
            {
                return text;
            }

            return text[..start] + replacement + text[(end + 2)..];
        }

        private string? ResolveToken(string tokenName, IReadOnlyDictionary<string, string>? fieldTokens)
        {
            string bareName = tokenName.Split('|', 2)[0].Split(':', 2)[0].Trim();

            if (fieldTokens is not null && fieldTokens.TryGetValue(bareName, out string? fieldValue))
            {
                return fieldValue;
            }

            if (this.tokens.TryGetValue(bareName, out string? value))
            {
                return value;
            }

            if (this.context is not null && !this.context.UnresolvedTokens.Contains(bareName))
            {
                this.context.UnresolvedTokens.Add(bareName);
            }

            return null;
        }
    }

    private static Dictionary<string, string> GetTargetFieldTokens(string target)
    {
        string normalized = target.Replace('\\', '/');
        int separatorIndex = normalized.LastIndexOf('/');
        string pathOnly = separatorIndex >= 0 ? normalized[..separatorIndex] : string.Empty;
        string withoutPath = separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Target"] = normalized,
            ["TargetPathOnly"] = pathOnly,
            ["TargetWithoutPath"] = withoutPath
        };
    }

    private static bool IsUngatedOrNewSavePatch(JToken? when)
    {
        if (when is null || when.Type == JTokenType.Null)
        {
            return true;
        }

        if (when is not JObject conditions || !conditions.Properties().Any())
        {
            return true;
        }

        foreach (JProperty condition in conditions.Properties())
        {
            if (!condition.Name.Trim().StartsWith("HasSeenEvent", StringComparison.OrdinalIgnoreCase) || !IsFalseConditionValue(condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFalseConditionValue(JToken value)
    {
        if (value.Type == JTokenType.Boolean)
        {
            return value.Value<bool>() == false;
        }

        string? text = value.Type == JTokenType.String
            ? value.Value<string>()
            : value.ToString(Formatting.None);

        return text?.Trim().Equals("false", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static IEnumerable<string> SplitContentPatcherList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                yield return part;
            }
        }
    }

    private static string? ResolveContentPatcherFile(string packRoot, string currentDirectory, string relativePath)
    {
        string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        if (!string.IsNullOrWhiteSpace(packRoot))
        {
            string rootCandidate = Path.GetFullPath(Path.Combine(packRoot, normalizedRelativePath));
            if (File.Exists(rootCandidate))
            {
                return rootCandidate;
            }
        }

        string currentCandidate = Path.GetFullPath(Path.Combine(currentDirectory, normalizedRelativePath));
        return File.Exists(currentCandidate)
            ? currentCandidate
            : null;
    }

    private static bool LooksTokenized(string value)
    {
        return value.Contains("{{", StringComparison.Ordinal);
    }

    /// <summary>If entry [0] contains expanded placements instead of a music track, replace it
    /// with a default music entry. This handles DynamicTokens like <c>{{Summoning_SetUp}}</c>
    /// that resolve to a placement string at the script start.</summary>
    private static string RestructureExpandedScript(string script)
    {
        List<string> parts = QuoteAwareSplit.Split(script, '/');
        if (parts.Count == 0 || !LooksLikePlacementHeader(parts[0].Trim()))
        {
            return script;
        }

        parts[0] = "none";
        return string.Join("/", parts);
    }

    /// <summary>True if the text looks like a placement header (e.g. <c>farmer 26 55 2 Evelyn 40 60 2</c>)
    /// rather than a music track or viewport entry.</summary>
    private static bool LooksLikePlacementHeader(string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || parts.Length % 4 != 0)
        {
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 4 == 0)
            {
                // Actor name — should not parse as a number
                if (int.TryParse(parts[i], out _))
                {
                    return false;
                }
            }
            else
            {
                // Coordinate/facing — must parse as a number
                if (!int.TryParse(parts[i], out _))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsMapFile(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return extension.Equals(".tmx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tbin", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMapPath(string mapNameOrPath)
    {
        return mapNameOrPath.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase)
            ? mapNameOrPath
            : "Maps/" + mapNameOrPath;
    }

    private static string GetPreviewMapAssetPath(string targetMapPath)
    {
        string safeName = new(targetMapPath.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        return "Mods/Kree.CutsceneMaker/PreviewMaps/" + safeName;
    }
}
