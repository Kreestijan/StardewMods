using StardewValley.Objects;

namespace CustomChestSize;

internal readonly struct StorageInfo
{
    public StorageInfo(StorageKind kind, Chest chest, ChestGridLayout layout, object? owner = null)
    {
        this.Kind = kind;
        this.Chest = chest;
        this.Layout = layout;
        this.Owner = owner;
    }

    public StorageKind Kind { get; }

    public Chest Chest { get; }

    public ChestGridLayout Layout { get; }

    public object? Owner { get; }
}
