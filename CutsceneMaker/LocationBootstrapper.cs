using StardewValley;
using StardewValley.GameData.Locations;

namespace CutsceneMaker;

public static class LocationBootstrapper
{
    public static List<string> SupportedLocations { get; } = new();

    public static Dictionary<string, GameLocation> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, LocationData> KnownLocationData = new(StringComparer.OrdinalIgnoreCase);

    public static void SetSupportedLocations(IEnumerable<string> locationNames)
    {
        SupportedLocations.Clear();
        SupportedLocations.AddRange(locationNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
        SupportedLocations.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public static void SetSupportedLocations(Dictionary<string, LocationData> locationData)
    {
        KnownLocationData.Clear();
        foreach (KeyValuePair<string, LocationData> pair in locationData)
        {
            KnownLocationData[pair.Key] = pair.Value;
        }

        SetSupportedLocations(locationData.Keys);
    }

    public static GameLocation? Load(string locationName)
    {
        return LoadDetailed(locationName).Location;
    }

    public static LocationLoadResult LoadDetailed(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return LocationLoadResult.Fail("No location name was provided.");
        }

        if (Cache.TryGetValue(locationName, out GameLocation? cached))
        {
            return LocationLoadResult.Success(cached);
        }

        GameLocation? location;
        try
        {
            location = Game1.getLocationFromName(locationName);
        }
        catch (Exception ex)
        {
            return LocationLoadResult.Fail($"Game lookup failed: {FormatException(ex)}");
        }

        if (location is null)
        {
            LocationData? data = KnownLocationData.GetValueOrDefault(locationName);
            if (data?.CreateOnLoad is not null)
            {
                try
                {
                    location = Game1.CreateGameLocation(locationName, data.CreateOnLoad);
                }
                catch (Exception ex)
                {
                    return LocationLoadResult.Fail(
                        $"CreateOnLoad failed for map '{data.CreateOnLoad.MapPath}': {FormatException(ex)}"
                    );
                }

                if (location is null)
                {
                    return LocationLoadResult.Fail($"CreateOnLoad returned no location for map '{data.CreateOnLoad.MapPath}'.");
                }
            }
            else
            {
                return LocationLoadResult.Fail("This Data/Locations entry has no CreateOnLoad map data, so it cannot be previewed from the title-screen editor.");
            }
        }

        try
        {
            location.updateMap();
        }
        catch (Exception ex)
        {
            return LocationLoadResult.Fail($"Map update failed: {FormatException(ex)}");
        }

        Cache[locationName] = location;
        return LocationLoadResult.Success(location);
    }

    public static IEnumerable<string> GetLocationNamesFromData(Dictionary<string, LocationData> locationData)
    {
        return locationData.Keys;
    }

    private static string FormatException(Exception ex)
    {
        return string.IsNullOrWhiteSpace(ex.Message)
            ? ex.GetType().Name
            : $"{ex.GetType().Name}: {ex.Message}";
    }
}

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
