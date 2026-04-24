using StardewValley;

namespace SpouseWarp;

internal enum WarpTargetKind
{
    Player,
    Npc
}

internal sealed class WarpTarget
{
    public WarpTarget(WarpTargetKind kind, string key, string displayName, Farmer? farmer = null, NPC? npc = null, bool isOnline = true, bool isSelectable = true)
    {
        this.Kind = kind;
        this.Key = key;
        this.DisplayName = displayName;
        this.Farmer = farmer;
        this.Npc = npc;
        this.IsOnline = isOnline;
        this.IsSelectable = isSelectable;
    }

    public WarpTargetKind Kind { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public Farmer? Farmer { get; }

    public NPC? Npc { get; }

    public bool IsOnline { get; }

    public bool IsSelectable { get; }
}
