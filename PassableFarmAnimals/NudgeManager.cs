using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PassableFarmAnimals;

internal sealed class NudgeManager
{
    private const string NudgeMessageType = "AnimalNudge";

    private readonly ModEntry mod;
    private readonly Dictionary<NudgeKey, NudgeAnimation> activeNudges = new();
    private readonly Dictionary<NudgeKey, int> cooldowns = new();

    internal NudgeManager(ModEntry mod)
    {
        this.mod = mod;
    }

    internal void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        int elapsedMs = GetElapsedMilliseconds();
        this.UpdateAnimations(elapsedMs);

        if (!Context.IsWorldReady || !this.mod.config.EnableMod || !this.mod.config.EnableNudge)
        {
            return;
        }

        GameLocation? location = Game1.currentLocation;
        Farmer? player = Game1.player;
        if (location is null || player is null || !location.Animals.Pairs.Any())
        {
            return;
        }

        Rectangle farmerBounds = player.GetBoundingBox();
        foreach (var pair in location.Animals.Pairs)
        {
            FarmAnimal animal = pair.Value;
            if (!farmerBounds.Intersects(animal.GetBoundingBox()))
            {
                continue;
            }

            var key = new NudgeKey(location.NameOrUniqueName, animal.myID.Value);
            if (this.activeNudges.ContainsKey(key) || this.cooldowns.GetValueOrDefault(key) > 0)
            {
                continue;
            }

            int direction = player.FacingDirection;
            this.StartNudge(key, direction, this.mod.config.NudgeStrengthPixels, this.mod.config.NudgeDurationMs);
            this.cooldowns[key] = this.mod.config.NudgeCooldownMs;
            this.SendNudge(key, direction);
        }
    }

    internal void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != this.mod.ModManifest.UniqueID || e.Type != NudgeMessageType)
        {
            return;
        }

        if (Context.IsMultiplayer && e.FromPlayerID == Game1.player.UniqueMultiplayerID)
        {
            return;
        }

        NudgeMessage message = e.ReadAs<NudgeMessage>();
        if (string.IsNullOrWhiteSpace(message.LocationName) || message.DurationMs <= 0)
        {
            return;
        }

        this.StartNudge(
            new NudgeKey(message.LocationName, message.AnimalId),
            message.Direction,
            Math.Clamp(message.StrengthPixels, 0, 128),
            Math.Clamp(message.DurationMs, 50, 1000)
        );
    }

    internal bool TryGetDrawOffset(FarmAnimal animal, out Vector2 offset)
    {
        offset = Vector2.Zero;
        string? locationName = animal.currentLocation?.NameOrUniqueName;
        if (locationName is null)
        {
            return false;
        }

        var key = new NudgeKey(locationName, animal.myID.Value);
        if (!this.activeNudges.TryGetValue(key, out NudgeAnimation animation))
        {
            return false;
        }

        float progress = animation.ElapsedMs / (float)animation.DurationMs;
        float eased = 1f - MathHelper.Clamp(progress, 0f, 1f);
        offset = DirectionToVector(animation.Direction) * animation.StrengthPixels * eased;
        return offset != Vector2.Zero;
    }

    private void StartNudge(NudgeKey key, int direction, int strengthPixels, int durationMs)
    {
        if (strengthPixels <= 0)
        {
            return;
        }

        this.activeNudges[key] = new NudgeAnimation(
            Direction: NormalizeDirection(direction),
            StrengthPixels: strengthPixels,
            DurationMs: durationMs,
            ElapsedMs: 0
        );
    }

    private void SendNudge(NudgeKey key, int direction)
    {
        if (!Context.IsMultiplayer)
        {
            return;
        }

        this.mod.Helper.Multiplayer.SendMessage(
            new NudgeMessage
            {
                LocationName = key.LocationName,
                AnimalId = key.AnimalId,
                Direction = direction,
                StrengthPixels = this.mod.config.NudgeStrengthPixels,
                DurationMs = this.mod.config.NudgeDurationMs
            },
            NudgeMessageType,
            modIDs: new[] { this.mod.ModManifest.UniqueID }
        );
    }

    private void UpdateAnimations(int elapsedMs)
    {
        if (this.activeNudges.Count > 0)
        {
            foreach (NudgeKey key in this.activeNudges.Keys.ToList())
            {
                NudgeAnimation animation = this.activeNudges[key];
                animation = animation with { ElapsedMs = animation.ElapsedMs + elapsedMs };
                if (animation.ElapsedMs >= animation.DurationMs)
                {
                    this.activeNudges.Remove(key);
                }
                else
                {
                    this.activeNudges[key] = animation;
                }
            }
        }

        if (this.cooldowns.Count > 0)
        {
            foreach (NudgeKey key in this.cooldowns.Keys.ToList())
            {
                int remainingMs = this.cooldowns[key] - elapsedMs;
                if (remainingMs <= 0)
                {
                    this.cooldowns.Remove(key);
                }
                else
                {
                    this.cooldowns[key] = remainingMs;
                }
            }
        }
    }

    private static int GetElapsedMilliseconds()
    {
        int elapsedMs = (int)Math.Ceiling(Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? 16);
        return Math.Max(1, elapsedMs);
    }

    private static int NormalizeDirection(int direction)
    {
        return direction is >= 0 and <= 3 ? direction : Game1.down;
    }

    private static Vector2 DirectionToVector(int direction)
    {
        return NormalizeDirection(direction) switch
        {
            Game1.up => new Vector2(0f, -1f),
            Game1.right => new Vector2(1f, 0f),
            Game1.down => new Vector2(0f, 1f),
            Game1.left => new Vector2(-1f, 0f),
            _ => Vector2.Zero
        };
    }
}

internal readonly record struct NudgeKey(string LocationName, long AnimalId);

internal readonly record struct NudgeAnimation(int Direction, int StrengthPixels, int DurationMs, int ElapsedMs);

public sealed class NudgeMessage
{
    public string LocationName { get; set; } = string.Empty;
    public long AnimalId { get; set; }
    public int Direction { get; set; }
    public int StrengthPixels { get; set; }
    public int DurationMs { get; set; }
}
