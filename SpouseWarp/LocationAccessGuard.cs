using StardewValley;

namespace SpouseWarp;

internal sealed class LocationAccessGuard
{
    public WarpValidationResult ValidateTarget(WarpTarget target, ModConfig config)
    {
        if (config.IgnoreLocationUnlocks)
        {
            return WarpValidationResult.Pass();
        }

        GameLocation? location = target.Kind == WarpTargetKind.Player
            ? target.Farmer?.currentLocation
            : target.Npc?.currentLocation;

        if (location is null)
        {
            return WarpValidationResult.Pass();
        }

        foreach (string locationName in this.GetCandidateLocationNames(location))
        {
            if (!Game1.isLocationAccessible(locationName))
            {
                string areaName = location.GetRootLocation().DisplayName;
                return WarpValidationResult.Fail($"{target.DisplayName} is in {areaName}, which you haven't unlocked yet.");
            }
        }

        return WarpValidationResult.Pass();
    }

    private IEnumerable<string> GetCandidateLocationNames(GameLocation location)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string? name in new[]
        {
            location.Name,
            location.NameOrUniqueName,
            location.GetRootLocation().Name,
            location.GetRootLocation().NameOrUniqueName
        })
        {
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
            {
                yield return name;
            }
        }
    }
}
