using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using xTile;

namespace CutsceneMaker;

public static class LocationBootstrapper
{
    private static readonly Dictionary<string, LocationCatalogEntry> EntriesById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, LocationCatalogEntry> EntriesByEventLocationName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, LocationData> KnownLocationData = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> FarmTypeMapNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, PreviewMapOverride> PreviewMapOverrides = new(StringComparer.OrdinalIgnoreCase);

    private static IModHelper? Helper;

    public static List<string> SupportedLocations { get; } = new();

    public static List<LocationCatalogEntry> SupportedLocationEntries { get; } = new();

    public static Dictionary<string, GameLocation> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static void RebuildCatalog(
        IModHelper helper,
        Dictionary<string, LocationData> locationData,
        Dictionary<string, ModFarmType> farmTypes,
        Dictionary<string, PreviewMapOverride>? previewMapOverrides = null
    )
    {
        Helper = helper;
        Cache.Clear();
        KnownLocationData.Clear();
        FarmTypeMapNames.Clear();
        PreviewMapOverrides.Clear();

        foreach (KeyValuePair<string, LocationData> pair in locationData)
        {
            KnownLocationData[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, ModFarmType> pair in farmTypes)
        {
            string? mapName = pair.Value.MapName;
            if (string.IsNullOrWhiteSpace(mapName))
            {
                continue;
            }

            FarmTypeMapNames[pair.Key] = mapName;
            if (!string.IsNullOrWhiteSpace(pair.Value.Id))
            {
                FarmTypeMapNames[pair.Value.Id] = mapName;
            }
        }

        if (previewMapOverrides is not null)
        {
            foreach (KeyValuePair<string, PreviewMapOverride> pair in previewMapOverrides)
            {
                PreviewMapOverrides[pair.Key] = pair.Value;
            }
        }

        List<LocationCatalogEntry> entries = new();
        foreach (KeyValuePair<string, LocationData> pair in locationData)
        {
            if (IsVanillaFarmVariantLocationName(pair.Key))
            {
                continue;
            }

            LocationCatalogEntry? entry = TryCreateLocationDataEntry(pair.Key, pair.Value);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        AddVanillaFarmEntries(entries);
        AddModFarmEntries(entries, farmTypes);
        SetCatalog(entries);
    }

    public static void SetSupportedLocations(IEnumerable<string> locationNames)
    {
        List<LocationCatalogEntry> entries = locationNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new LocationCatalogEntry(
                Id: name,
                DisplayName: name,
                EventLocationName: name,
                PreviewMapPath: NormalizeMapPath(name),
                LocationData: null,
                IsFarmVariant: false,
                Source: "Explicit"
            ))
            .ToList();

        SetCatalog(entries);
    }

    public static void SetSupportedLocations(Dictionary<string, LocationData> locationData)
    {
        RebuildCatalog(Helper ?? ModEntry.Instance.Helper, locationData, new Dictionary<string, ModFarmType>());
    }

    public static void SetFarmTypes(Dictionary<string, ModFarmType> farmTypes)
    {
        FarmTypeMapNames.Clear();
        foreach (KeyValuePair<string, ModFarmType> pair in farmTypes)
        {
            string? mapName = pair.Value.MapName;
            if (!string.IsNullOrWhiteSpace(mapName))
            {
                FarmTypeMapNames[pair.Key] = mapName;
                if (!string.IsNullOrWhiteSpace(pair.Value.Id))
                {
                    FarmTypeMapNames[pair.Value.Id] = mapName;
                }
            }
        }
    }

    public static void RegisterPreviewMapOverrides(IEnumerable<PreviewMapOverride> previewMapOverrides)
    {
        ArgumentNullException.ThrowIfNull(previewMapOverrides);

        foreach (PreviewMapOverride preview in previewMapOverrides)
        {
            if (string.IsNullOrWhiteSpace(preview.TargetMapPath) || string.IsNullOrWhiteSpace(preview.PreviewAssetPath))
            {
                continue;
            }

            PreviewMapOverrides[preview.TargetMapPath] = preview;
        }
    }

    public static GameLocation? Load(string locationName)
    {
        return LoadDetailed(locationName).Location;
    }

    public static LocationLoadResult LoadDetailed(string locationIdOrName)
    {
        if (string.IsNullOrWhiteSpace(locationIdOrName))
        {
            return LocationLoadResult.Fail("No location name was provided.");
        }

        LocationCatalogEntry entry = ResolveEntry(locationIdOrName);
        if (Cache.TryGetValue(entry.Id, out GameLocation? cached))
        {
            return LocationLoadResult.Success(cached);
        }

        LocationLoadResult result = TryLoadPreviewMapFirst(entry);

        if (!result.Loaded || result.Location is null)
        {
            return result;
        }

        try
        {
            result.Location.updateMap();
        }
        catch (Exception ex)
        {
            return LocationLoadResult.Fail($"Map update failed: {FormatException(ex)}");
        }

        Cache[entry.Id] = result.Location;
        return result;
    }

    public static LocationCatalogEntry ResolveEntry(string locationIdOrName)
    {
        if (EntriesById.TryGetValue(locationIdOrName, out LocationCatalogEntry? byId))
        {
            return byId;
        }

        if (EntriesByEventLocationName.TryGetValue(locationIdOrName, out LocationCatalogEntry? byEventName))
        {
            return byEventName;
        }

        LocationData? data = KnownLocationData.GetValueOrDefault(locationIdOrName);
        string? previewMapPath = GetMapPathCandidates(locationIdOrName)
            .Select(ResolvePreviewMapPath)
            .FirstOrDefault();
        return new LocationCatalogEntry(
            Id: locationIdOrName,
            DisplayName: locationIdOrName,
            EventLocationName: locationIdOrName,
            PreviewMapPath: previewMapPath,
            LocationData: data,
            IsFarmVariant: false,
            Source: "Ad-hoc"
        );
    }

    public static string ResolvePreviewMapPathForMapName(string mapNameOrPath)
    {
        return ResolvePreviewMapPath(NormalizeMapPath(mapNameOrPath));
    }

    public static string GetDisplayName(string locationIdOrName)
    {
        return ResolveEntry(locationIdOrName).DisplayName;
    }

    public static IEnumerable<string> GetLocationNamesFromData(Dictionary<string, LocationData> locationData)
    {
        return locationData
            .Select(pair => TryCreateLocationDataEntry(pair.Key, pair.Value))
            .Where(entry => entry is not null)
            .Select(entry => entry!.Id);
    }

    private static void SetCatalog(IEnumerable<LocationCatalogEntry> entries)
    {
        SupportedLocationEntries.Clear();
        SupportedLocationEntries.AddRange(entries
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase));

        SupportedLocations.Clear();
        SupportedLocations.AddRange(SupportedLocationEntries.Select(entry => entry.Id));

        EntriesById.Clear();
        EntriesByEventLocationName.Clear();
        foreach (LocationCatalogEntry entry in SupportedLocationEntries)
        {
            EntriesById[entry.Id] = entry;
        }

        foreach (LocationCatalogEntry entry in SupportedLocationEntries.Where(entry => entry.Id.Equals(entry.EventLocationName, StringComparison.OrdinalIgnoreCase)))
        {
            EntriesByEventLocationName.TryAdd(entry.EventLocationName, entry);
        }

        foreach (LocationCatalogEntry entry in SupportedLocationEntries.Where(entry => !entry.IsFarmVariant))
        {
            EntriesByEventLocationName.TryAdd(entry.EventLocationName, entry);
        }

        if (EntriesById.TryGetValue("Farm:Standard", out LocationCatalogEntry? standardFarm))
        {
            EntriesByEventLocationName.TryAdd(standardFarm.EventLocationName, standardFarm);
        }

        foreach (LocationCatalogEntry entry in SupportedLocationEntries)
        {
            EntriesByEventLocationName.TryAdd(entry.EventLocationName, entry);
        }
    }

    private static LocationCatalogEntry? TryCreateLocationDataEntry(string locationName, LocationData data)
    {
        if (IsVanillaFarmVariantLocationName(locationName))
        {
            return null;
        }

        string? previewMapPath = null;
        if (!string.IsNullOrWhiteSpace(data.CreateOnLoad?.MapPath))
        {
            previewMapPath = ResolvePreviewMapPath(NormalizeMapPath(data.CreateOnLoad.MapPath));
        }
        else
        {
            previewMapPath = GetMapPathCandidates(locationName)
                .Select(ResolvePreviewMapPath)
                .FirstOrDefault(MapAssetExists);
        }

        if (data.CreateOnLoad is null && string.IsNullOrWhiteSpace(previewMapPath))
        {
            return null;
        }

        return new LocationCatalogEntry(
            Id: locationName,
            DisplayName: locationName,
            EventLocationName: locationName,
            PreviewMapPath: previewMapPath,
            LocationData: data,
            IsFarmVariant: false,
            Source: "Data/Locations"
        );
    }

    private static void AddVanillaFarmEntries(List<LocationCatalogEntry> entries)
    {
        AddFarmEntry(entries, "Farm:Standard", "Farm: Standard", "Farm", "Maps/Farm", source: "Vanilla");
        AddFarmEntry(entries, "Farm:Riverland", "Farm: Riverland", "Farm", "Maps/Farm_Fishing", source: "Vanilla");
        AddFarmEntry(entries, "Farm:Forest", "Farm: Forest", "Farm", "Maps/Farm_Foraging", source: "Vanilla");
        AddFarmEntry(entries, "Farm:Hilltop", "Farm: Hill-top", "Farm", "Maps/Farm_Mining", source: "Vanilla");
        AddFarmEntry(entries, "Farm:Wilderness", "Farm: Wilderness", "Farm", "Maps/Farm_Combat", source: "Vanilla");
        AddFarmEntry(entries, "Farm:Beach", "Farm: Beach", "Farm", "Maps/Farm_Island", source: "Vanilla");
        AddFarmEntry(entries, "Farm:FourCorners", "Farm: Four Corners", "Farm", "Maps/Farm_FourCorners", source: "Vanilla");
        AddFarmEntry(entries, "Farm:Meadowlands", "Farm: Meadowlands", "Farm", "Maps/Farm_Ranching", source: "Vanilla");
    }

    private static void AddModFarmEntries(List<LocationCatalogEntry> entries, Dictionary<string, ModFarmType> farmTypes)
    {
        foreach (KeyValuePair<string, ModFarmType> pair in farmTypes)
        {
            string? mapName = pair.Value.MapName;
            if (string.IsNullOrWhiteSpace(mapName))
            {
                continue;
            }

            string id = string.IsNullOrWhiteSpace(pair.Value.Id) ? pair.Key : pair.Value.Id;
            AddFarmEntry(
                entries,
                "Farm:" + id,
                "Farm: " + id,
                "Farm",
                ResolvePreviewMapPath(NormalizeMapPath(mapName)),
                source: "Data/AdditionalFarms"
            );
        }
    }

    private static void AddFarmEntry(
        List<LocationCatalogEntry> entries,
        string id,
        string displayName,
        string eventLocationName,
        string previewMapPath,
        string source
    )
    {
        if (entries.Any(entry => entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        entries.Add(new LocationCatalogEntry(
            Id: id,
            DisplayName: displayName,
            EventLocationName: eventLocationName,
            PreviewMapPath: previewMapPath,
            LocationData: KnownLocationData.GetValueOrDefault(eventLocationName),
            IsFarmVariant: true,
            Source: source
        ));
    }

    private static LocationLoadResult TryLoadPreviewMapFirst(LocationCatalogEntry entry)
    {
        LocationLoadResult mapResult = TryLoadMapPath(entry);
        if (mapResult.Loaded)
        {
            return mapResult;
        }

        LocationLoadResult createResult = TryLoadCreateOnLoad(entry);
        if (createResult.Loaded)
        {
            return createResult;
        }

        LocationLoadResult gameResult = TryLoadGameLocation(entry);
        return gameResult.Loaded ? gameResult : LastFailure(mapResult, createResult, gameResult);
    }

    private static LocationLoadResult TryLoadGameLocation(LocationCatalogEntry entry)
    {
        try
        {
            GameLocation? location = Game1.getLocationFromName(entry.EventLocationName);
            return location is null
                ? LocationLoadResult.Fail($"No loaded game location named '{entry.EventLocationName}' was found.")
                : LocationLoadResult.Success(location);
        }
        catch (Exception ex)
        {
            return LocationLoadResult.Fail($"Game lookup failed for '{entry.EventLocationName}': {FormatException(ex)}");
        }
    }

    private static LocationLoadResult TryLoadCreateOnLoad(LocationCatalogEntry entry)
    {
        if (entry.LocationData?.CreateOnLoad is null)
        {
            return LocationLoadResult.Fail($"Location '{entry.EventLocationName}' has no CreateOnLoad map data.");
        }

        try
        {
            GameLocation? location = Game1.CreateGameLocation(entry.EventLocationName, entry.LocationData.CreateOnLoad);
            return location is null
                ? LocationLoadResult.Fail($"CreateOnLoad returned no location for map '{entry.LocationData.CreateOnLoad.MapPath}'.")
                : LocationLoadResult.Success(location);
        }
        catch (Exception ex)
        {
            return LocationLoadResult.Fail($"CreateOnLoad failed for map '{entry.LocationData.CreateOnLoad.MapPath}': {FormatException(ex)}");
        }
    }

    private static LocationLoadResult TryLoadMapPath(LocationCatalogEntry entry)
    {
        List<string> attempted = new();
        foreach (string mapPath in GetPreviewMapPathCandidates(entry))
        {
            attempted.Add(mapPath);
            try
            {
                return LocationLoadResult.Success(new GameLocation(mapPath, entry.EventLocationName));
            }
            catch
            {
                // Try the next known alias before reporting failure.
            }
        }

        return LocationLoadResult.Fail(
            attempted.Count == 0
                ? $"No preview map path is known for '{entry.DisplayName}'."
                : $"Could not load any preview map for '{entry.DisplayName}'. Tried: {string.Join(", ", attempted)}"
        );
    }

    private static IEnumerable<string> GetPreviewMapPathCandidates(LocationCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.PreviewMapPath))
        {
            yield return entry.PreviewMapPath;
        }

        foreach (string candidate in GetMapPathCandidates(entry.EventLocationName))
        {
            string previewCandidate = ResolvePreviewMapPath(candidate);
            if (!previewCandidate.Equals(entry.PreviewMapPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return previewCandidate;
            }
        }
    }

    private static LocationLoadResult LastFailure(params LocationLoadResult[] results)
    {
        for (int index = results.Length - 1; index >= 0; index--)
        {
            if (!string.IsNullOrWhiteSpace(results[index].FailureReason))
            {
                return results[index];
            }
        }

        return LocationLoadResult.Fail("Location lookup returned no location.");
    }

    private static bool MapAssetExists(string mapPath)
    {
        if (PreviewMapOverrides.Values.Any(entry => entry.PreviewAssetPath.Equals(mapPath, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (Helper is null)
        {
            return false;
        }

        try
        {
            return Helper.GameContent.DoesAssetExist<Map>(Helper.GameContent.ParseAssetName(mapPath));
        }
        catch
        {
            return false;
        }
    }

    private static string FormatException(Exception ex)
    {
        return string.IsNullOrWhiteSpace(ex.Message)
            ? ex.GetType().Name
            : $"{ex.GetType().Name}: {ex.Message}";
    }

    private static IEnumerable<string> GetMapPathCandidates(string locationName)
    {
        yield return NormalizeMapPath(locationName);

        string? farmAlias = locationName switch
        {
            "Farm" => "Farm",
            "Farm_Riverland" => "Farm_Fishing",
            "Farm_Forest" => "Farm_Foraging",
            "Farm_Hilltop" => "Farm_Mining",
            "Farm_HillTop" => "Farm_Mining",
            "Farm_Wilderness" => "Farm_Combat",
            "Farm_Beach" => "Farm_Island",
            "Farm_Meadowlands" => "Farm_Ranching",
            "Farm_FourCorners" => "Farm_FourCorners",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(farmAlias))
        {
            yield return NormalizeMapPath(farmAlias);
        }

        if (FarmTypeMapNames.TryGetValue(locationName, out string? farmTypeMapName))
        {
            yield return NormalizeMapPath(farmTypeMapName);
        }
    }

    private static string NormalizeMapPath(string mapNameOrPath)
    {
        string normalized = mapNameOrPath.Replace('\\', '/');
        return normalized.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "Maps/" + normalized;
    }

    private static string ResolvePreviewMapPath(string mapPath)
    {
        return PreviewMapOverrides.TryGetValue(mapPath, out PreviewMapOverride? preview)
            ? preview.PreviewAssetPath
            : mapPath;
    }

    private static bool IsVanillaFarmVariantLocationName(string locationName)
    {
        return locationName is
            "Farm_Riverland"
            or "Farm_Forest"
            or "Farm_Hilltop"
            or "Farm_HillTop"
            or "Farm_Wilderness"
            or "Farm_Beach"
            or "Farm_Meadowlands"
            or "Farm_FourCorners";
    }
}

public sealed record LocationCatalogEntry(
    string Id,
    string DisplayName,
    string EventLocationName,
    string? PreviewMapPath,
    LocationData? LocationData,
    bool IsFarmVariant,
    string Source
)
{
    public string SearchText => $"{this.DisplayName} {this.Id} {this.EventLocationName} {this.PreviewMapPath} {this.Source}";
}

public sealed record PreviewMapOverride(
    string TargetMapPath,
    string PreviewAssetPath,
    string SourceFilePath,
    string SourceRootPath,
    string SourceName
);

public readonly record struct LocationLoadResult(GameLocation? Location, string? FailureReason)
{
    public bool Loaded => this.Location is not null;

    public static LocationLoadResult Success(GameLocation location)
    {
        return new LocationLoadResult(location, null);
    }

    public static LocationLoadResult Fail(string reason)
    {
        return new LocationLoadResult(null, reason);
    }
}
