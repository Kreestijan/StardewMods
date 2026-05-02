using StardewValley;
using StardewValley.GameData.Locations;
using xTile;

namespace CutsceneMaker;

public static class LocationBootstrapper
{
    public static List<string> SupportedLocations { get; } = new();

    public static Dictionary<string, GameLocation> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static void SetSupportedLocations(IEnumerable<string> locationNames)
    {
        SupportedLocations.Clear();
        SupportedLocations.AddRange(locationNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
        SupportedLocations.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public static GameLocation? Load(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return null;
        }

        if (Cache.TryGetValue(locationName, out GameLocation? cached))
        {
            return cached;
        }

        GameLocation? location = Game1.getLocationFromName(locationName);
        if (location is null)
        {
            string mapPath = "Maps/" + locationName;
            try
            {
                Game1.content.Load<Map>(mapPath);
                location = new GameLocation(mapPath, locationName);
            }
            catch
            {
                return null;
            }
        }

        try
        {
            location.updateMap();
        }
        catch
        {
            return null;
        }

        Cache[locationName] = location;
        return location;
    }

    public static IEnumerable<string> GetLocationNamesFromData(Dictionary<string, LocationData> locationData)
    {
        return locationData.Keys;
    }
}
