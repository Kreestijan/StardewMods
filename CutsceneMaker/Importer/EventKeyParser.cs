using CutsceneMaker.Commands;
using CutsceneMaker.Models;

namespace CutsceneMaker.Importer;

public static class EventKeyParser
{
    private static readonly Dictionary<string, string> LegacyAliases = new(StringComparer.Ordinal)
    {
        ["*"] = "WorldState",
        ["*l"] = "!HostOrLocalMail",
        ["*n"] = "HostOrLocalMail",
        ["A"] = "!ActiveDialogueEvent",
        ["a"] = "Tile",
        ["B"] = "SpouseBed",
        ["b"] = "ReachedMineBottom",
        ["C"] = "CommunityCenterOrWarehouseDone",
        ["c"] = "FreeInventorySlots",
        ["d"] = "!DayOfWeek",
        ["D"] = "Dating",
        ["g"] = "Gender",
        ["G"] = "GameStateQuery",
        ["F"] = "!FestivalDay",
        ["f"] = "Friendship",
        ["H"] = "IsHost",
        ["h"] = "MissingPet",
        ["Hl"] = "!HostMail",
        ["Hn"] = "HostMail",
        ["i"] = "HasItem",
        ["J"] = "JojaBundlesDone",
        ["j"] = "DaysPlayed",
        ["k"] = "!SawEvent",
        ["L"] = "InUpgradedHouse",
        ["l"] = "!LocalMail",
        ["m"] = "EarnedMoney",
        ["M"] = "HasMoney",
        ["N"] = "GoldenWalnuts",
        ["n"] = "LocalMail",
        ["O"] = "Spouse",
        ["o"] = "!Spouse",
        ["p"] = "NpcVisibleHere",
        ["q"] = "ChoseDialogueAnswers",
        ["R"] = "Roommate",
        ["Rf"] = "!Roommate",
        ["r"] = "Random",
        ["S"] = "SawSecretNote",
        ["s"] = "Shipped",
        ["t"] = "Time",
        ["U"] = "!UpcomingFestival",
        ["u"] = "DayOfMonth",
        ["v"] = "NPCVisible",
        ["w"] = "Weather",
        ["X"] = "!CommunityCenterOrWarehouseDone",
        ["y"] = "Year",
        ["z"] = "!Season"
    };

    public static (string UniqueId, List<EventPreconditionBlock> Triggers) Parse(string key, EventPreconditionCatalog? catalog = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Event key is required.", nameof(key));
        }

        catalog ??= new EventPreconditionCatalog();
        string[] parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string uniqueId = parts[0];
        List<EventPreconditionBlock> triggers = new();

        for (int i = 1; i < parts.Length; i++)
        {
            triggers.Add(ParsePrecondition(parts[i], catalog));
        }

        return (uniqueId, triggers);
    }

    private static EventPreconditionBlock ParsePrecondition(string token, EventPreconditionCatalog catalog)
    {
        bool negated = token.StartsWith('!');
        if (negated)
        {
            token = token[1..].TrimStart();
        }

        string[] parts = QuoteAwareSplit.Split(token, ' ')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length == 0)
        {
            return CreateRaw(token, negated);
        }

        string verb = parts[0];
        if (LegacyAliases.TryGetValue(verb, out string? modernVerb))
        {
            if (modernVerb.StartsWith('!'))
            {
                negated = !negated;
                modernVerb = modernVerb[1..];
            }

            verb = modernVerb;
        }

        if (!catalog.TryGetByVerb(verb, out EventPreconditionDefinition? definition))
        {
            return CreateRaw(token, negated);
        }

        EventPreconditionBlock block = definition.CreateDefaultBlock();
        block.Negated = negated;
        int argumentIndex = 1;
        foreach (EventCommandParameter parameter in definition.Parameters)
        {
            if (argumentIndex >= parts.Length)
            {
                continue;
            }

            string value;
            if (parameter.Type == EventCommandParameterType.RawArguments || parameter == definition.Parameters[^1])
            {
                value = string.Join(" ", parts.Skip(argumentIndex).Select(Unquote));
                argumentIndex = parts.Length;
            }
            else
            {
                value = Unquote(parts[argumentIndex]);
                argumentIndex++;
            }

            block.Values[parameter.Key] = value;
        }

        return block;
    }

    private static EventPreconditionBlock CreateRaw(string token, bool negated)
    {
        return new EventPreconditionBlock
        {
            PreconditionId = "raw",
            DisplayName = "Raw Precondition",
            Verb = token,
            Negated = negated
        };
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value.Replace("\\\"", "\"", StringComparison.Ordinal);
    }
}
