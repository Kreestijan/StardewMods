namespace CutsceneMaker.Models;

public enum PreconditionType
{
    Time,
    Season,
    Weather,
    Year,
    DaysPlayed,
    Friendship,
    HasSeenEvent,
    HasMailFlag,
    GameStateQuery
}

public sealed class PreconditionData
{
    public PreconditionType Type { get; set; }

    public bool Negated { get; set; }

    public string? NpcName { get; set; }

    public int? HeartLevel { get; set; }

    public int? TimeStart { get; set; }

    public int? TimeEnd { get; set; }

    public string? Season { get; set; }

    public string? Weather { get; set; }

    public int? MinYear { get; set; }

    public int? DaysPlayed { get; set; }

    public string? FlagOrEventId { get; set; }

    public string? QueryString { get; set; }
}
