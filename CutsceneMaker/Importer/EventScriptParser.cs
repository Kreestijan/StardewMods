using CutsceneMaker.Models;

namespace CutsceneMaker.Importer;

public static class EventScriptParser
{
    private static readonly HashSet<string> KnownCommandVerbs = new(StringComparer.Ordinal)
    {
        "move",
        "speak",
        "message",
        "emote",
        "pause",
        "precisePause",
        "globalFade",
        "globalFadeIn",
        "globalFadeToClear",
        "addItem",
        "addMoney",
        "friendship",
        "mail",
        "addQuest",
        "learnRecipe",
        "learnCraftingRecipe",
        "end"
    };

    public static List<object> Parse(string script, CutsceneData cutscene)
    {
        ArgumentNullException.ThrowIfNull(cutscene);

        List<string> tokens = QuoteAwareSplit.Split(script, '/');
        List<object> commands = new();
        int index = ParseHeader(tokens, cutscene);

        Dictionary<string, (int X, int Y)> actorPositions = BuildInitialActorPositions(cutscene);
        for (; index < tokens.Count; index++)
        {
            string token = tokens[index].Trim();
            if (token.Length == 0)
            {
                continue;
            }

            commands.Add(ParseCommand(token, actorPositions));
        }

        if (commands.Count == 0 || commands[^1] is not TimelineCommand { Type: CommandType.End })
        {
            commands.Add(TimelineCommand.CreateEnd());
        }

        return commands;
    }

    private static int ParseHeader(List<string> tokens, CutsceneData cutscene)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        cutscene.MusicTrack = tokens[0].Trim();
        if (tokens.Count >= 2)
        {
            ParseViewport(tokens[1], cutscene);
        }

        cutscene.Skippable = false;
        cutscene.Actors.Clear();

        int index = 2;
        if (tokens.Count > index)
        {
            string placementHeader = tokens[index].Trim();
            if (TryParsePlacementHeader(placementHeader, out List<NpcPlacement> placements))
            {
                ApplyPlacements(cutscene, placements);
                index++;
            }
        }

        for (; index < tokens.Count; index++)
        {
            string token = tokens[index].Trim();
            string firstWord = GetFirstWord(token);
            if (KnownCommandVerbs.Contains(firstWord))
            {
                break;
            }

            if (token.Equals("skippable", StringComparison.Ordinal))
            {
                cutscene.Skippable = true;
                continue;
            }

            if (TryParsePlacement(token, out NpcPlacement placement))
            {
                ApplyPlacement(cutscene, placement);

                continue;
            }

            break;
        }

        return index;
    }

    private static void ApplyPlacements(CutsceneData cutscene, IEnumerable<NpcPlacement> placements)
    {
        foreach (NpcPlacement placement in placements)
        {
            ApplyPlacement(cutscene, placement);
        }
    }

    private static void ApplyPlacement(CutsceneData cutscene, NpcPlacement placement)
    {
        if (placement.ActorName.Equals("farmer", StringComparison.OrdinalIgnoreCase))
        {
            placement.ActorName = "farmer";
            cutscene.FarmerPlacement = placement;
            return;
        }

        cutscene.Actors.Add(placement);
    }

    private static Dictionary<string, (int X, int Y)> BuildInitialActorPositions(CutsceneData cutscene)
    {
        Dictionary<string, (int X, int Y)> actorPositions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["farmer"] = (cutscene.FarmerPlacement.TileX, cutscene.FarmerPlacement.TileY)
        };

        foreach (NpcPlacement actor in cutscene.Actors)
        {
            if (!string.IsNullOrWhiteSpace(actor.ActorName))
            {
                actorPositions[actor.ActorName] = (actor.TileX, actor.TileY);
            }
        }

        return actorPositions;
    }

    private static object ParseCommand(string token, Dictionary<string, (int X, int Y)> actorPositions)
    {
        string verb = GetFirstWord(token);
        string[] parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (verb)
        {
            case "move" when parts.Length >= 5:
                return ParseMoveCommand(parts, actorPositions);

            case "speak" when parts.Length >= 3:
                return new TimelineCommand
                {
                    Type = CommandType.Speak,
                    ActorName = parts[1],
                    DialogueText = Unquote(token[(verb.Length + parts[1].Length + 2)..].Trim())
                };

            case "message" when parts.Length >= 2:
                return new TimelineCommand
                {
                    Type = CommandType.Speak,
                    ActorName = "farmer",
                    DialogueText = Unquote(token[verb.Length..].Trim())
                };

            case "emote" when parts.Length >= 3:
                return new TimelineCommand
                {
                    Type = CommandType.Emote,
                    ActorName = parts[1],
                    EmoteId = TryParseInt(parts[2])
                };

            case "pause" when parts.Length >= 2:
            case "precisePause" when parts.Length >= 2:
                return new TimelineCommand
                {
                    Type = CommandType.Pause,
                    DurationMs = TryParseInt(parts[1])
                };

            case "globalFade":
                return new TimelineCommand { Type = CommandType.FadeOut };

            case "globalFadeIn":
            case "globalFadeToClear":
                return new TimelineCommand { Type = CommandType.FadeIn };

            case "addItem" when parts.Length >= 2:
                return new TimelineCommand
                {
                    Type = CommandType.Reward,
                    RewardType = RewardType.Item,
                    ItemId = parts[1],
                    Quantity = parts.Length >= 3 ? TryParseInt(parts[2]) : 1
                };

            case "addMoney" when parts.Length >= 2:
                return new TimelineCommand
                {
                    Type = CommandType.Reward,
                    RewardType = RewardType.Gold,
                    GoldAmount = TryParseInt(parts[1])
                };

            case "friendship" when parts.Length >= 3:
                return new TimelineCommand
                {
                    Type = CommandType.Reward,
                    RewardType = RewardType.Friendship,
                    RewardNpcName = parts[1],
                    FriendshipAmount = TryParseInt(parts[2])
                };

            case "mail" when parts.Length >= 2:
                return CreateIdReward(RewardType.MailFlag, parts[1]);

            case "addQuest" when parts.Length >= 2:
                return CreateIdReward(RewardType.Quest, parts[1]);

            case "learnRecipe" when parts.Length >= 2:
                return CreateIdReward(RewardType.CookingRecipe, parts[1]);

            case "learnCraftingRecipe" when parts.Length >= 2:
                return CreateIdReward(RewardType.CraftingRecipe, parts[1]);

            case "end":
                return TimelineCommand.CreateEnd();

            default:
                return new RawCommandBlock
                {
                    RawText = token
                };
        }
    }

    private static TimelineCommand ParseMoveCommand(string[] parts, Dictionary<string, (int X, int Y)> actorPositions)
    {
        string actorName = parts[1];
        int? deltaX = TryParseInt(parts[2]);
        int? deltaY = TryParseInt(parts[3]);
        int? facing = TryParseInt(parts[4]);
        int? targetX = deltaX;
        int? targetY = deltaY;

        if (deltaX.HasValue && deltaY.HasValue && actorPositions.TryGetValue(actorName, out (int X, int Y) currentPosition))
        {
            targetX = currentPosition.X + deltaX.Value;
            targetY = currentPosition.Y + deltaY.Value;
            actorPositions[actorName] = (targetX.Value, targetY.Value);
        }

        return new TimelineCommand
        {
            Type = CommandType.Move,
            ActorName = actorName,
            TileX = targetX,
            TileY = targetY,
            Facing = facing
        };
    }

    private static TimelineCommand CreateIdReward(RewardType rewardType, string id)
    {
        return new TimelineCommand
        {
            Type = CommandType.Reward,
            RewardType = rewardType,
            ItemId = id
        };
    }

    private static void ParseViewport(string token, CutsceneData cutscene)
    {
        string[] parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
        {
            cutscene.ViewportStartX = x;
            cutscene.ViewportStartY = y;
        }
    }

    private static bool TryParsePlacement(string token, out NpcPlacement placement)
    {
        placement = null!;
        string[] parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4
            || !int.TryParse(parts[1], out int x)
            || !int.TryParse(parts[2], out int y)
            || !int.TryParse(parts[3], out int facing))
        {
            return false;
        }

        placement = new NpcPlacement
        {
            ActorName = parts[0],
            TileX = x,
            TileY = y,
            Facing = facing
        };
        return true;
    }

    private static bool TryParsePlacementHeader(string token, out List<NpcPlacement> placements)
    {
        placements = new List<NpcPlacement>();
        string[] parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || parts.Length % 4 != 0)
        {
            return false;
        }

        for (int i = 0; i < parts.Length; i += 4)
        {
            if (!int.TryParse(parts[i + 1], out int x)
                || !int.TryParse(parts[i + 2], out int y)
                || !int.TryParse(parts[i + 3], out int facing))
            {
                placements.Clear();
                return false;
            }

            placements.Add(new NpcPlacement
            {
                ActorName = parts[i],
                TileX = x,
                TileY = y,
                Facing = facing
            });
        }

        return true;
    }

    private static string GetFirstWord(string token)
    {
        int separatorIndex = token.IndexOf(' ', StringComparison.Ordinal);
        return separatorIndex < 0
            ? token
            : token[..separatorIndex];
    }

    private static int? TryParseInt(string value)
    {
        return int.TryParse(value, out int result)
            ? result
            : null;
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
