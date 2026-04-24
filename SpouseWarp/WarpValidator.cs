using StardewValley;

namespace SpouseWarp;

internal sealed class WarpValidator
{
    public WarpValidationResult ValidateInitiator(ModConfig config, DateTimeOffset now, DateTimeOffset? lastWarpAt)
    {
        if (Game1.CurrentEvent is not null)
        {
            return WarpValidationResult.Fail("You can't warp during a cutscene.");
        }

        if (Game1.player.currentLocation?.Name == "Temp")
        {
            return WarpValidationResult.Fail("You can't warp from a festival.");
        }

        if (config.WarpCostGold > Game1.player.Money)
        {
            return WarpValidationResult.Fail($"You need {config.WarpCostGold}g to warp.");
        }

        if (config.CooldownSeconds > 0 && lastWarpAt.HasValue)
        {
            double remainingSeconds = config.CooldownSeconds - (now - lastWarpAt.Value).TotalSeconds;
            if (remainingSeconds > 0)
            {
                return WarpValidationResult.Fail($"Warp on cooldown. {(int)Math.Ceiling(remainingSeconds)}s remaining.");
            }
        }

        return WarpValidationResult.Pass();
    }

    public WarpValidationResult ValidateTarget(WarpTarget target)
    {
        return target.Kind == WarpTargetKind.Player
            ? this.ValidatePlayerTarget(target)
            : this.ValidateNpcTarget(target);
    }

    private WarpValidationResult ValidatePlayerTarget(WarpTarget target)
    {
        Farmer? farmer = target.Farmer;
        if (farmer is null || !target.IsOnline)
        {
            return WarpValidationResult.Fail($"{target.DisplayName} is unavailable right now.");
        }

        if (farmer.currentLocation?.Name == "Temp")
        {
            return WarpValidationResult.Fail($"{target.DisplayName} is at a festival right now.");
        }

        if (farmer.currentLocation?.currentEvent is not null)
        {
            return WarpValidationResult.Fail($"{target.DisplayName} is in a cutscene right now.");
        }

        return WarpValidationResult.Pass();
    }

    private WarpValidationResult ValidateNpcTarget(WarpTarget target)
    {
        NPC? npc = target.Npc;
        if (npc is null || npc.IsInvisible)
        {
            return WarpValidationResult.Fail($"{target.DisplayName} is unavailable right now.");
        }

        if (npc.currentLocation?.Name == "Temp")
        {
            return WarpValidationResult.Fail($"{target.DisplayName} is at a festival right now.");
        }

        if (npc.isSleeping.Value)
        {
            return WarpValidationResult.Fail($"{target.DisplayName} is asleep right now.");
        }

        return WarpValidationResult.Pass();
    }
}
