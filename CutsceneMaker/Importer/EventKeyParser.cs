using CutsceneMaker.Models;

namespace CutsceneMaker.Importer;

public static class EventKeyParser
{
    public static (string UniqueId, List<PreconditionData> Triggers) Parse(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Event key is required.", nameof(key));
        }

        string[] parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string uniqueId = parts[0];
        List<PreconditionData> triggers = new();

        for (int i = 1; i < parts.Length; i++)
        {
            triggers.Add(ParsePrecondition(parts[i]));
        }

        return (uniqueId, triggers);
    }

    private static PreconditionData ParsePrecondition(string token)
    {
        bool negated = token.StartsWith('!');
        if (negated)
        {
            token = token[1..].TrimStart();
        }

        string[] parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new PreconditionData
            {
                Type = PreconditionType.GameStateQuery,
                Negated = negated,
                QueryString = token
            };
        }

        PreconditionData data = new()
        {
            Negated = negated
        };

        switch (parts[0])
        {
            case "t" when parts.Length >= 3 && int.TryParse(parts[1], out int start) && int.TryParse(parts[2], out int end):
                data.Type = PreconditionType.Time;
                data.TimeStart = start;
                data.TimeEnd = end;
                break;

            case "z" when parts.Length >= 2:
                data.Type = PreconditionType.Season;
                data.Season = NormalizeTitleCase(parts[1]);
                break;

            case "w" when parts.Length >= 2:
                data.Type = PreconditionType.Weather;
                data.Weather = NormalizeTitleCase(parts[1]);
                break;

            case "y" when parts.Length >= 2 && int.TryParse(parts[1], out int minYear):
                data.Type = PreconditionType.Year;
                data.MinYear = minYear;
                break;

            case "j" when parts.Length >= 2 && int.TryParse(parts[1], out int daysPlayed):
                data.Type = PreconditionType.DaysPlayed;
                data.DaysPlayed = daysPlayed;
                break;

            case "f" when parts.Length >= 3 && int.TryParse(parts[2], out int friendshipPoints):
                data.Type = PreconditionType.Friendship;
                data.NpcName = parts[1];
                data.HeartLevel = friendshipPoints / 250;
                break;

            case "Hn" when parts.Length >= 2:
                data.Type = PreconditionType.HasSeenEvent;
                data.FlagOrEventId = parts[1];
                break;

            case "h" when parts.Length >= 2:
                data.Type = PreconditionType.HasMailFlag;
                data.FlagOrEventId = parts[1];
                break;

            case "GAME_STATE_QUERY":
                data.Type = PreconditionType.GameStateQuery;
                data.QueryString = token["GAME_STATE_QUERY".Length..].TrimStart();
                break;

            default:
                data.Type = PreconditionType.GameStateQuery;
                data.QueryString = token;
                break;
        }

        return data;
    }

    private static string NormalizeTitleCase(string value)
    {
        return value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
