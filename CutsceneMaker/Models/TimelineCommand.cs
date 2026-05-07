namespace CutsceneMaker.Models;

public enum CommandType
{
    Move,
    Speak,
    Emote,
    Pause,
    FadeOut,
    FadeIn,
    Reward,
    End
}

public enum RewardType
{
    Item,
    Gold,
    Friendship,
    MailFlag,
    Quest,
    CookingRecipe,
    CraftingRecipe
}

public sealed class TimelineCommand
{
    public CommandType Type { get; set; }

    public string? ActorSlotId { get; set; }

    public string? ActorName { get; set; }

    public int? TileX { get; set; }

    public int? TileY { get; set; }

    public int? Facing { get; set; }

    public string? DialogueText { get; set; }

    public int? EmoteId { get; set; }

    public int? DurationMs { get; set; }

    public RewardType? RewardType { get; set; }

    public string? ItemId { get; set; }

    public int? Quantity { get; set; }

    public int? GoldAmount { get; set; }

    public string? RewardNpcName { get; set; }

    public int? FriendshipAmount { get; set; }

    public static TimelineCommand CreateEnd()
    {
        return new TimelineCommand
        {
            Type = CommandType.End
        };
    }
}
