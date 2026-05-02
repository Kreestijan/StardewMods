namespace CutsceneMaker.Models;

public sealed class NpcPlacement
{
    public string ActorName { get; set; } = string.Empty;

    public int TileX { get; set; }

    public int TileY { get; set; }

    public int Facing { get; set; } = 2;

    public static NpcPlacement CreateFarmerDefault()
    {
        return new NpcPlacement
        {
            ActorName = "farmer",
            TileX = 0,
            TileY = 0,
            Facing = 2
        };
    }
}
