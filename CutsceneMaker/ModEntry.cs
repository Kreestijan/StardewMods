using StardewModdingAPI;
using StardewModdingAPI.Events;
using CutsceneMaker.Commands;
using CutsceneMaker.Compiler;
using CutsceneMaker.Editor;
using CutsceneMaker.Importer;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Locations;
using xTile;
using xTile.Format;

namespace CutsceneMaker;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;

    public static List<string> KnownNpcNames { get; } = new();
    public static Dictionary<string, string> KnownNpcSpriteAssets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, Point> KnownNpcSpriteSizes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public EventCommandCatalog CommandCatalog { get; private set; } = EventCommandCatalog.Empty;
    public EventPreconditionCatalog PreconditionCatalog { get; } = new();
    public string ModsDirectoryPath { get; private set; } = string.Empty;
    private TitleMenuButtonController titleMenuButton = null!;
    private Dictionary<string, ModFarmType>? cachedContentPatcherFarmTypes;
    private Dictionary<string, PreviewMapOverride>? cachedContentPatcherPreviewMapOverrides;
    private readonly Dictionary<string, PreviewMapOverride> importedPreviewMapOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> previewTextureSources = new(StringComparer.OrdinalIgnoreCase);
    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.ModsDirectoryPath = Path.Combine(Constants.GamePath, "Mods");
        this.titleMenuButton = new TitleMenuButtonController(helper);
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        this.RefreshCommandCatalog();

#if DEBUG
        this.RunCompilerSmokeTest();
#endif
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RefreshCommandCatalog();
        this.RefreshKnownLocations();
        this.RefreshKnownNpcs();

        LocationLoadResult town = LocationBootstrapper.LoadDetailed("Town");
        this.Monitor.Log(
            !town.Loaded
                ? $"Cutscene Maker bootstrap check could not load Town: {town.FailureReason}"
                : "Cutscene Maker bootstrap check loaded Town.",
            !town.Loaded ? LogLevel.Warn : LogLevel.Trace
        );
    }

    public void RefreshCommandCatalog()
    {
        this.CommandCatalog = EventCommandCatalog.Build(this.Helper.ModRegistry);
    }

    public void RefreshKnownLocations()
    {
        try
        {
            Dictionary<string, LocationData> locationData = this.Helper.GameContent.Load<Dictionary<string, LocationData>>("Data/Locations");
            Dictionary<string, ModFarmType> farmTypes = this.LoadAdditionalFarms();
            Dictionary<string, PreviewMapOverride> previewMapOverrides = this.GetContentPatcherPreviewMapOverrides();
            LocationBootstrapper.RebuildCatalog(this.Helper, locationData, farmTypes, previewMapOverrides);
            LocationBootstrapper.RegisterPreviewMapOverrides(this.importedPreviewMapOverrides.Values);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Cutscene Maker could not read Data/Locations: {ex.Message}", LogLevel.Warn);
        }
    }

    public void RegisterImportedPreviewMapOverrides(IEnumerable<PreviewMapOverride> previewMapOverrides)
    {
        ArgumentNullException.ThrowIfNull(previewMapOverrides);

        foreach (PreviewMapOverride preview in previewMapOverrides)
        {
            if (string.IsNullOrWhiteSpace(preview.TargetMapPath) || string.IsNullOrWhiteSpace(preview.SourceFilePath))
            {
                continue;
            }

            this.importedPreviewMapOverrides[preview.TargetMapPath] = preview;
        }

        LocationBootstrapper.RegisterPreviewMapOverrides(this.importedPreviewMapOverrides.Values);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        PreviewMapOverride? preview = this.GetAllPreviewMapOverrides()
            .FirstOrDefault(entry => e.NameWithoutLocale.IsEquivalentTo(entry.PreviewAssetPath));

        if (preview is null)
        {
            if (this.previewTextureSources.TryGetValue(e.NameWithoutLocale.Name, out string? texturePath))
            {
                if (!typeof(Texture2D).IsAssignableFrom(e.DataType))
                {
                    this.Monitor.Log($"Cutscene Maker preview texture asset '{e.NameWithoutLocale}' was requested as {e.DataType.Name}; expected {nameof(Texture2D)}.", LogLevel.Warn);
                    return;
                }

                e.LoadFrom(
                    load: () =>
                    {
                        using FileStream stream = File.OpenRead(texturePath);
                        return Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                    },
                    priority: AssetLoadPriority.Exclusive
                );
            }

            return;
        }

        if (!typeof(Map).IsAssignableFrom(e.DataType))
        {
            this.Monitor.Log($"Cutscene Maker preview map asset '{preview.PreviewAssetPath}' was requested as {e.DataType.Name}; expected {nameof(Map)}.", LogLevel.Warn);
            return;
        }

        e.LoadFrom(
            load: () => this.LoadPreviewMap(preview),
            priority: AssetLoadPriority.Exclusive
        );
    }

    private Map LoadPreviewMap(PreviewMapOverride preview)
    {
        Map map = FormatManager.Instance.LoadMap(preview.SourceFilePath);
        this.NormalizePreviewMapTileSheets(map, preview);
        return map;
    }

    private IEnumerable<PreviewMapOverride> GetAllPreviewMapOverrides()
    {
        foreach (PreviewMapOverride preview in this.GetContentPatcherPreviewMapOverrides().Values)
        {
            yield return preview;
        }

        foreach (PreviewMapOverride preview in this.importedPreviewMapOverrides.Values)
        {
            yield return preview;
        }
    }

    private void NormalizePreviewMapTileSheets(Map map, PreviewMapOverride preview)
    {
        string mapDirectory = Path.GetDirectoryName(preview.SourceFilePath) ?? string.Empty;
        foreach (xTile.Tiles.TileSheet tileSheet in map.TileSheets)
        {
            string? imageSource = tileSheet.ImageSource;
            if (string.IsNullOrWhiteSpace(imageSource))
            {
                continue;
            }

            string normalized = imageSource.Replace('\\', '/').Trim();
            string? localPath = ResolvePreviewTileSheetFile(preview, mapDirectory, normalized);
            if (localPath is not null)
            {
                string assetPath = GetPreviewTextureAssetPath(preview.TargetMapPath, normalized);
                this.previewTextureSources[assetPath] = localPath;
                tileSheet.ImageSource = assetPath;
                continue;
            }

            tileSheet.ImageSource = NormalizeTileSheetAssetName(normalized);
        }
    }

    private Dictionary<string, ModFarmType> LoadAdditionalFarms()
    {
        Dictionary<string, ModFarmType> farmTypes = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (KeyValuePair<string, ModFarmType> pair in this.Helper.GameContent.Load<Dictionary<string, ModFarmType>>("Data/AdditionalFarms"))
            {
                farmTypes[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Cutscene Maker could not read Data/AdditionalFarms; modded farm previews will be limited: {ex.Message}", LogLevel.Trace);
        }

        foreach (KeyValuePair<string, ModFarmType> pair in this.GetContentPatcherFarmTypes())
        {
            farmTypes.TryAdd(pair.Key, pair.Value);
        }

        return farmTypes;
    }

    private Dictionary<string, ModFarmType> GetContentPatcherFarmTypes()
    {
        if (this.cachedContentPatcherFarmTypes is not null)
        {
            return this.cachedContentPatcherFarmTypes;
        }

        Dictionary<string, ModFarmType> farmTypes = new(StringComparer.OrdinalIgnoreCase);
        string modsPath = Path.Combine(Constants.GamePath, "Mods");
        if (!Directory.Exists(modsPath))
        {
            this.cachedContentPatcherFarmTypes = farmTypes;
            return farmTypes;
        }

        foreach (string contentPath in Directory.EnumerateFiles(modsPath, "content.json", SearchOption.AllDirectories))
        {
            this.ReadContentPatcherFarmTypes(contentPath, farmTypes);
        }

        this.cachedContentPatcherFarmTypes = farmTypes;
        return farmTypes;
    }

    private Dictionary<string, PreviewMapOverride> GetContentPatcherPreviewMapOverrides()
    {
        if (this.cachedContentPatcherPreviewMapOverrides is not null)
        {
            return this.cachedContentPatcherPreviewMapOverrides;
        }

        Dictionary<string, PreviewMapOverride> overrides = new(StringComparer.OrdinalIgnoreCase);
        string modsPath = Path.Combine(Constants.GamePath, "Mods");
        if (!Directory.Exists(modsPath))
        {
            this.cachedContentPatcherPreviewMapOverrides = overrides;
            return overrides;
        }

        foreach (string contentPath in Directory.EnumerateFiles(modsPath, "content.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string? packRoot = Path.GetDirectoryName(contentPath);
            if (string.IsNullOrWhiteSpace(packRoot))
            {
                continue;
            }

            this.ReadContentPatcherPreviewMapOverrides(contentPath, packRoot, overrides, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        this.cachedContentPatcherPreviewMapOverrides = overrides;
        return overrides;
    }

    private void ReadContentPatcherFarmTypes(string contentPath, Dictionary<string, ModFarmType> farmTypes)
    {
        try
        {
            using JsonTextReader reader = new(File.OpenText(contentPath));
            JObject content = JObject.Load(reader);
            if (content["Changes"] is not JArray changes)
            {
                return;
            }

            foreach (JObject change in changes.OfType<JObject>())
            {
                string? action = change.Value<string>("Action");
                string? target = change.Value<string>("Target");
                if (!"EditData".Equals(action, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(target)
                    || !target.Split(',').Any(part => part.Trim().Equals("Data/AdditionalFarms", StringComparison.OrdinalIgnoreCase))
                    || change["Entries"] is not JObject entries)
                {
                    continue;
                }

                foreach (JProperty entry in entries.Properties())
                {
                    if (entry.Value is not JObject data)
                    {
                        continue;
                    }

                    string? mapName = data.Value<string>("MapName");
                    if (string.IsNullOrWhiteSpace(mapName))
                    {
                        continue;
                    }

                    string id = data.Value<string>("Id")
                        ?? data.Value<string>("ID")
                        ?? entry.Name.Split('/').Last();

                    farmTypes.TryAdd(entry.Name, new ModFarmType
                    {
                        Id = id,
                        TooltipStringPath = data.Value<string>("TooltipStringPath"),
                        MapName = mapName,
                        IconTexture = data.Value<string>("IconTexture"),
                        WorldMapTexture = data.Value<string>("WorldMapTexture")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Cutscene Maker skipped Content Patcher farm scan for '{contentPath}': {ex.Message}", LogLevel.Trace);
        }
    }

    private void ReadContentPatcherPreviewMapOverrides(
        string contentPath,
        string packRoot,
        Dictionary<string, PreviewMapOverride> overrides,
        HashSet<string> visitedFiles
    )
    {
        string fullContentPath;
        try
        {
            fullContentPath = Path.GetFullPath(contentPath);
        }
        catch
        {
            return;
        }

        if (!visitedFiles.Add(fullContentPath) || !File.Exists(fullContentPath))
        {
            return;
        }

        try
        {
            using JsonTextReader reader = new(File.OpenText(fullContentPath));
            JObject content = JObject.Load(reader);
            if (content["Changes"] is not JArray changes)
            {
                return;
            }

            string currentDirectory = Path.GetDirectoryName(fullContentPath) ?? packRoot;
            foreach (JObject change in changes.OfType<JObject>())
            {
                string? action = change.Value<string>("Action");
                if (string.IsNullOrWhiteSpace(action) || !this.IsUngatedOrNewSavePatch(change["When"]))
                {
                    continue;
                }

                if ("Include".Equals(action, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string includePath in SplitContentPatcherList(change.Value<string>("FromFile")))
                    {
                        if (LooksTokenized(includePath))
                        {
                            continue;
                        }

                        string? resolved = ResolveContentPatcherFile(packRoot, currentDirectory, includePath);
                        if (resolved is not null)
                        {
                            this.ReadContentPatcherPreviewMapOverrides(resolved, packRoot, overrides, visitedFiles);
                        }
                    }

                    continue;
                }

                if (!"Load".Equals(action, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? fromFile = change.Value<string>("FromFile");
                if (string.IsNullOrWhiteSpace(fromFile) || LooksTokenized(fromFile))
                {
                    continue;
                }

                string? sourceFilePath = ResolveContentPatcherFile(packRoot, currentDirectory, fromFile);
                if (sourceFilePath is null || !IsMapFile(sourceFilePath))
                {
                    continue;
                }

                foreach (string target in SplitContentPatcherList(change.Value<string>("Target")))
                {
                    if (LooksTokenized(target))
                    {
                        continue;
                    }

                    string targetMapPath = NormalizeMapPath(target);
                    if (!targetMapPath.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    overrides[targetMapPath] = new PreviewMapOverride(
                        TargetMapPath: targetMapPath,
                        PreviewAssetPath: GetPreviewMapAssetPath(targetMapPath),
                        SourceFilePath: sourceFilePath,
                        SourceRootPath: packRoot,
                        SourceName: Path.GetFileName(packRoot)
                    );
                }
            }
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Cutscene Maker skipped Content Patcher map scan for '{fullContentPath}': {ex.Message}", LogLevel.Trace);
        }
    }

    private bool IsUngatedOrNewSavePatch(JToken? when)
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
        string rootCandidate = Path.GetFullPath(Path.Combine(packRoot, normalizedRelativePath));
        if (File.Exists(rootCandidate))
        {
            return rootCandidate;
        }

        string currentCandidate = Path.GetFullPath(Path.Combine(currentDirectory, normalizedRelativePath));
        return File.Exists(currentCandidate)
            ? currentCandidate
            : null;
    }

    private static bool LooksTokenized(string value)
    {
        return value.Contains("{{", StringComparison.Ordinal) || value.Contains('{', StringComparison.Ordinal);
    }

    private static bool IsMapFile(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return extension.Equals(".tmx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tbin", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveRelativeFile(string baseDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        string candidate = Path.GetFullPath(Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(candidate)
            ? candidate
            : null;
    }

    private static string? ResolvePreviewTileSheetFile(PreviewMapOverride preview, string mapDirectory, string imageSource)
    {
        string? directMapPath = ResolveRelativeFile(mapDirectory, imageSource);
        if (directMapPath is not null)
        {
            return directMapPath;
        }

        string? directPackPath = ResolveRelativeFile(preview.SourceRootPath, imageSource);
        if (directPackPath is not null)
        {
            return directPackPath;
        }

        string fileName = Path.GetFileName(imageSource);
        foreach (string candidate in new[]
        {
            Path.Combine(preview.SourceRootPath, "Assets", "Tilesheets", fileName),
            Path.Combine(preview.SourceRootPath, "assets", "tilesheets", fileName)
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        try
        {
            return Directory.EnumerateFiles(preview.SourceRootPath, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeTileSheetAssetName(string imageSource)
    {
        string withoutExtension = imageSource.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? imageSource[..^4]
            : imageSource;

        if (withoutExtension.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase)
            || withoutExtension.StartsWith("TileSheets/", StringComparison.OrdinalIgnoreCase)
            || withoutExtension.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase)
            || withoutExtension.StartsWith("LooseSprites/", StringComparison.OrdinalIgnoreCase))
        {
            return withoutExtension;
        }

        return "Maps/" + Path.GetFileName(withoutExtension);
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

    private static string GetPreviewTextureAssetPath(string targetMapPath, string imageSource)
    {
        string key = targetMapPath + "/" + imageSource;
        string safeName = new(key.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        return "Mods/Kree.CutsceneMaker/PreviewTextures/" + safeName;
    }

    public void RefreshKnownNpcs()
    {
        Dictionary<string, CharacterData> characters = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (KeyValuePair<string, CharacterData> pair in this.Helper.GameContent.Load<Dictionary<string, CharacterData>>("Data/Characters"))
            {
                characters[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Cutscene Maker could not read Data/Characters: {ex.Message}", LogLevel.Warn);
        }

        if (Game1.characterData is not null)
        {
            foreach (KeyValuePair<string, CharacterData> pair in Game1.characterData)
            {
                characters[pair.Key] = pair.Value;
            }
        }

        KnownNpcNames.Clear();
        KnownNpcSpriteAssets.Clear();
        KnownNpcSpriteSizes.Clear();

        foreach (KeyValuePair<string, CharacterData> pair in characters.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            KnownNpcNames.Add(pair.Key);

            CharacterData? data = pair.Value;
            string textureName = string.IsNullOrWhiteSpace(data?.TextureName)
                ? pair.Key
                : data.TextureName;
            KnownNpcSpriteAssets[pair.Key] = "Characters\\" + textureName;
            KnownNpcSpriteSizes[pair.Key] = data?.Size == Point.Zero ? new Point(16, 32) : data?.Size ?? new Point(16, 32);
        }
    }

#if DEBUG
    private void RunCompilerSmokeTest()
    {
        CutsceneData sample = CutsceneData.CreateBlank();
        sample.CutsceneName = "Debug Sample";
        sample.LocationName = "Town";
        sample.MusicTrack = "none";
        sample.ViewportStartX = -100;
        sample.ViewportStartY = -100;
        sample.FarmerPlacement = new NpcPlacement
        {
            ActorName = "farmer",
            TileX = 10,
            TileY = 12,
            Facing = 2
        };
        sample.Actors.Add(new NpcPlacement
        {
            ActorName = "Penny",
            TileX = 12,
            TileY = 12,
            Facing = 3
        });
        EventPreconditionBlock season = this.PreconditionCatalog.Definitions.First(definition => definition.Verb == "Season").CreateDefaultBlock();
        season.Values["seasons"] = "Spring";
        sample.Triggers.Add(season);

        EventCommandBlock speak = this.CommandCatalog.TryGetById("vanilla.speak", out EventCommandDefinition? speakDefinition)
            ? speakDefinition.CreateDefaultBlock()
            : VanillaEventCommandProvider.GetDefinitions().First(definition => definition.Id == "vanilla.speak").CreateDefaultBlock();
        speak.Values["actor"] = "Penny";
        speak.Values["text"] = "This is a Cutscene Maker compiler test.$h";
        sample.Commands.Insert(0, speak);

        this.Monitor.Log("Cutscene Maker compiler smoke test:", LogLevel.Debug);
        this.Monitor.Log("Key: " + EventKeyBuilder.Build(sample), LogLevel.Debug);
        this.Monitor.Log("Script: " + EventScriptBuilder.Build(sample, this.CommandCatalog), LogLevel.Debug);

        List<string> split = QuoteAwareSplit.Split("none/-100 -100/speak Penny \"hello / still dialogue\"/end", '/');
        this.Monitor.Log("Quote-aware split sample parts: " + split.Count, LogLevel.Debug);
    }
#endif
}
