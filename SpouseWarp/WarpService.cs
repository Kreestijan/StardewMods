using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace SpouseWarp;

internal sealed class WarpService
{
    private const int MaxSearchRadius = 8;

    public bool TryWarpToTarget(WarpTarget target, int cost, out string? errorMessage)
    {
        if (!this.TryGetDestination(target, out GameLocation? location, out Point tile, out int facingDirection))
        {
            errorMessage = $"Couldn't find a safe tile near {target.DisplayName}.";
            return false;
        }

        if (cost > 0)
        {
            Game1.player.Money -= cost;
        }

        this.CloseMenusBeforeWarp();
        Game1.warpFarmer(location!.NameOrUniqueName, tile.X, tile.Y, facingDirection);
        errorMessage = null;
        return true;
    }

    public bool TryWarpHome(out string? errorMessage)
    {
        FarmHouse home = Utility.getHomeOfFarmer(Game1.player);
        Point? destination = this.FindNearbyTile(home, home.getEntryLocation())
            ?? this.FindNearbyTile(home, home.GetPlayerBedSpot());

        if (!destination.HasValue)
        {
            errorMessage = "Couldn't find a safe tile at home.";
            return false;
        }

        this.CloseMenusBeforeWarp();
        Game1.warpFarmer(home.NameOrUniqueName, destination.Value.X, destination.Value.Y, 2);
        errorMessage = null;
        return true;
    }

    private void CloseMenusBeforeWarp()
    {
        if (Game1.activeClickableMenu is null)
        {
            return;
        }

        Game1.exitActiveMenu();
        Game1.activeClickableMenu = null;
    }

    private bool TryGetDestination(WarpTarget target, out GameLocation? location, out Point tile, out int facingDirection)
    {
        location = null;
        tile = Point.Zero;
        facingDirection = Game1.player.FacingDirection;

        if (target.Kind == WarpTargetKind.Player)
        {
            Farmer? farmer = target.Farmer;
            if (farmer?.currentLocation is null)
            {
                return false;
            }

            location = farmer.currentLocation;
            Point farmerTile = farmer.TilePoint;
            Point? farmerDestination = this.FindNearbyTile(location, farmerTile);
            if (!farmerDestination.HasValue)
            {
                return false;
            }

            tile = farmerDestination.Value;
            facingDirection = farmer.FacingDirection;
            return true;
        }

        NPC? npc = target.Npc;
        if (npc?.currentLocation is null)
        {
            return false;
        }

        location = npc.currentLocation;
        Point npcTile = npc.TilePoint;
        Point? npcDestination = this.FindNearbyTile(location, npcTile);
        if (!npcDestination.HasValue)
        {
            return false;
        }

        tile = npcDestination.Value;
        facingDirection = npc.FacingDirection;
        return true;
    }

    private Point? FindNearbyTile(GameLocation location, Point targetTile)
    {
        foreach (Point candidate in this.GetCandidateTiles(targetTile))
        {
            if (this.IsValidWarpTile(location, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<Point> GetCandidateTiles(Point center)
    {
        yield return new Point(center.X, center.Y + 1);
        yield return new Point(center.X + 1, center.Y);
        yield return new Point(center.X - 1, center.Y);
        yield return new Point(center.X, center.Y - 1);

        for (int radius = 1; radius <= MaxSearchRadius; radius++)
        {
            List<Point> ring = new();
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                for (int y = center.Y - radius; y <= center.Y + radius; y++)
                {
                    if (Math.Max(Math.Abs(x - center.X), Math.Abs(y - center.Y)) != radius)
                    {
                        continue;
                    }

                    Point candidate = new(x, y);
                    if (candidate == new Point(center.X, center.Y + 1)
                        || candidate == new Point(center.X + 1, center.Y)
                        || candidate == new Point(center.X - 1, center.Y)
                        || candidate == new Point(center.X, center.Y - 1))
                    {
                        continue;
                    }

                    ring.Add(candidate);
                }
            }

            foreach (Point point in ring
                .OrderBy(point => Math.Abs(point.X - center.X) + Math.Abs(point.Y - center.Y))
                .ThenBy(point => Math.Abs(point.X - center.X))
                .ThenBy(point => Math.Abs(point.Y - center.Y)))
            {
                yield return point;
            }
        }
    }

    private bool IsValidWarpTile(GameLocation location, Point candidate)
    {
        if (!location.isTileOnMap(candidate))
        {
            return false;
        }

        Vector2 tile = candidate.ToVector2();
        return location.isTileLocationOpen(tile)
            && location.isTilePlaceable(tile)
            && !location.IsTileOccupiedBy(tile);
    }
}
