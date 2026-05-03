namespace EatUntilFull;

internal enum FillTarget
{
    Energy,
    Health
}

internal sealed class ModConfig
{
    public bool EnableMod { get; set; } = true;
    public FillTarget FillTarget { get; set; } = FillTarget.Energy;
}
