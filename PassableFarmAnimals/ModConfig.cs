namespace PassableFarmAnimals;

internal sealed class ModConfig
{
    public bool EnableMod { get; set; } = true;
    public bool EnableNudge { get; set; } = true;
    public int NudgeStrengthPixels { get; set; } = 8;
    public int NudgeDurationMs { get; set; } = 1000;
    public int NudgeCooldownMs { get; set; } = 2000;
}
