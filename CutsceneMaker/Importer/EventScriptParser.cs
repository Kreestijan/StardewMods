using CutsceneMaker.Commands;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;

namespace CutsceneMaker.Importer;

public static class EventScriptParser
{
    public static List<object> Parse(string script, CutsceneData cutscene, EventCommandCatalog? commandCatalog = null, bool preserveActors = false)
    {
        ArgumentNullException.ThrowIfNull(cutscene);
        commandCatalog ??= EventCommandCatalog.Empty;

        List<string> tokens = QuoteAwareSplit.Split(script, '/');
        List<object> commands = new();
        int index = ParseHeader(tokens, cutscene, commandCatalog, preserveActors);
        Dictionary<string, Point> actorPositions = BuildInitialActorPositions(cutscene);

        for (; index < tokens.Count; index++)
        {
            string token = tokens[index].Trim();
            if (token.Length == 0)
            {
                continue;
            }

            commands.Add(ParseCommand(token, cutscene, commandCatalog, actorPositions));
        }

        if (commands.Count == 0 || commands[^1] is not EventCommandBlock { CommandId: "vanilla.end" })
        {
            commands.Add(CreateVanillaEnd(commandCatalog));
        }

        return commands;
    }

    private static int ParseHeader(List<string> tokens, CutsceneData cutscene, EventCommandCatalog commandCatalog, bool preserveActors = false)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        cutscene.MusicTrack = tokens[0].Trim();
        if (!preserveActors)
        {
            cutscene.IncludeFarmer = false;
            cutscene.Skippable = false;
            cutscene.Actors.Clear();
        }

        // Viewport is optional in the SDV event format. If tokens[1] is a pair of
        // integers (e.g. "0 0") it's the viewport; otherwise tokens[1] is already
        // the placement header (actor tiles) and there is no viewport token.
        bool hasViewport = tokens.Count >= 2 && TryParseViewport(tokens[1], cutscene);
        int index = hasViewport ? 2 : 1;

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
            if (token.Equals("skippable", StringComparison.Ordinal))
            {
                cutscene.Skippable = true;
                continue;
            }

            string firstWord = GetFirstWord(token);
            if (commandCatalog.TryGetByVerb(firstWord, out _))
            {
                break;
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
            cutscene.IncludeFarmer = true;
            return;
        }

        cutscene.Actors.Add(placement);
    }

    private static object ParseCommand(string token, CutsceneData cutscene, EventCommandCatalog commandCatalog, Dictionary<string, Point> actorPositions)
    {
        string verb = GetFirstWord(token);
        string[] parts = QuoteAwareSplit.Split(token, ' ')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (commandCatalog.TryGetByVerb(verb, out EventCommandDefinition? definition))
        {
            return ParseEventCommand(definition, parts, token, cutscene, actorPositions);
        }

        return new RawCommandBlock
        {
            RawText = token
        };
    }

    private static EventCommandBlock ParseEventCommand(EventCommandDefinition definition, string[] parts, string token, CutsceneData cutscene, Dictionary<string, Point> actorPositions)
    {
        if (definition.Id.Equals("vanilla.move", StringComparison.Ordinal) && parts.Length >= 5)
        {
            return ParseMoveCommand(definition, parts, cutscene, actorPositions);
        }

        if (definition.Id.Equals("vanilla.question", StringComparison.Ordinal) && parts.Length >= 3)
        {
            return ParseQuestionCommand(definition, parts);
        }

        // Track warp destinations so subsequent move commands compute absolute targets
        // relative to the warp destination, not the initial placement position.
        if (definition.Id.Equals("vanilla.warp", StringComparison.Ordinal) && parts.Length >= 4)
        {
            string actorName = Unquote(parts[1]);
            int targetX = TryParseInt(parts[2]);
            int targetY = TryParseInt(parts[3]);
            actorPositions[actorName] = new Point(targetX, targetY);
        }

        EventCommandBlock block = definition.CreateDefaultBlock();
        int argumentIndex = 1;
        for (int parameterIndex = 0; parameterIndex < definition.Parameters.Count; parameterIndex++)
        {
            EventCommandParameter parameter = definition.Parameters[parameterIndex];
            if (argumentIndex >= parts.Length)
            {
                continue;
            }

            string value;
            if (parameter.Type == EventCommandParameterType.RawArguments)
            {
                value = string.Join(" ", parts.Skip(argumentIndex).Select(Unquote));
                argumentIndex = parts.Length;
            }
            else if (parameterIndex == definition.Parameters.Count - 1 && parameter.Type == EventCommandParameterType.Text)
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
            if (parameter.Type is EventCommandParameterType.Actor or EventCommandParameterType.OptionalActor)
            {
                string actorName = value.TrimEnd('?');
                string? slotId = ResolveActorSlotId(cutscene, actorName);
                if (!string.IsNullOrWhiteSpace(slotId))
                {
                    block.ActorSlotIds[parameter.Key] = slotId;
                }
            }
        }

        return block;
    }

    private static EventCommandBlock ParseQuestionCommand(EventCommandDefinition definition, string[] parts)
    {
        EventCommandBlock block = definition.CreateDefaultBlock();
        int payloadIndex = 2;
        string mode = Unquote(parts[1]);
        if (mode.Equals("fork", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
        {
            block.Values["forkAnswer"] = Unquote(parts[2]);
            payloadIndex = 3;
        }
        else if (mode.StartsWith("fork", StringComparison.OrdinalIgnoreCase) && mode.Length > "fork".Length)
        {
            block.Values["forkAnswer"] = mode["fork".Length..];
        }
        else
        {
            block.Values["forkAnswer"] = string.Empty;
        }

        string payload = string.Join(" ", parts.Skip(payloadIndex).Select(Unquote));
        string[] fields = payload.Split('#');
        block.Values["question"] = fields.Length > 0 ? fields[0] : string.Empty;
        int answerCount = Math.Max(1, fields.Length - 1);
        block.Values["answers.count"] = answerCount.ToString();
        for (int index = 0; index < answerCount; index++)
        {
            block.Values[$"answers.{index}"] = index + 1 < fields.Length ? fields[index + 1] : string.Empty;
        }

        return block;
    }

    private static EventCommandBlock ParseMoveCommand(EventCommandDefinition definition, string[] parts, CutsceneData cutscene, Dictionary<string, Point> actorPositions)
    {
        EventCommandBlock block = definition.CreateDefaultBlock();
        string actorName = Unquote(parts[1]);
        int deltaX = TryParseInt(parts[2]);
        int deltaY = TryParseInt(parts[3]);
        string direction = Unquote(parts[4]);

        Point currentPosition = actorPositions.TryGetValue(actorName, out Point knownPosition)
            ? knownPosition
            : Point.Zero;
        Point target = new(currentPosition.X + deltaX, currentPosition.Y + deltaY);
        actorPositions[actorName] = target;

        block.Values["actor"] = actorName;
        block.Values["targetX"] = target.X.ToString();
        block.Values["targetY"] = target.Y.ToString();
        block.Values["direction"] = direction;
        if (parts.Length >= 6)
        {
            block.Values["continue"] = Unquote(parts[5]);
        }

        string? slotId = ResolveActorSlotId(cutscene, actorName);
        if (!string.IsNullOrWhiteSpace(slotId))
        {
            block.ActorSlotIds["actor"] = slotId;
        }

        return block;
    }

    private static Dictionary<string, Point> BuildInitialActorPositions(CutsceneData cutscene)
    {
        Dictionary<string, Point> positions = new(StringComparer.OrdinalIgnoreCase);
        if (cutscene.IncludeFarmer)
        {
            positions["farmer"] = new Point(cutscene.FarmerPlacement.TileX, cutscene.FarmerPlacement.TileY);
        }

        foreach (NpcPlacement actor in cutscene.Actors)
        {
            if (!string.IsNullOrWhiteSpace(actor.ActorName))
            {
                positions[actor.ActorName] = new Point(actor.TileX, actor.TileY);
            }
        }

        return positions;
    }

    private static EventCommandBlock CreateVanillaEnd(EventCommandCatalog commandCatalog)
    {
        if (commandCatalog.TryGetById("vanilla.end", out EventCommandDefinition? definition))
        {
            return definition.CreateDefaultBlock();
        }

        return new EventCommandBlock
        {
            ProviderModId = VanillaEventCommandProvider.ModId,
            ProviderName = "Vanilla",
            CommandId = "vanilla.end",
            DisplayName = "End",
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mode"] = string.Empty
            }
        };
    }

    private static string? ResolveActorSlotId(CutsceneData cutscene, string actorName)
    {
        if (actorName.Equals("farmer", StringComparison.OrdinalIgnoreCase))
        {
            return cutscene.IncludeFarmer ? cutscene.FarmerPlacement.ActorSlotId : null;
        }

        List<NpcPlacement> matches = cutscene.Actors
            .Where(actor => actor.ActorName.Equals(actorName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0].ActorSlotId : null;
    }

    private static bool TryParseViewport(string token, CutsceneData cutscene)
    {
        string[] parts = token.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
        {
            cutscene.ViewportStartX = x;
            cutscene.ViewportStartY = y;
            return true;
        }
        return false;
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

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value.Replace("\\\"", "\"", StringComparison.Ordinal);
    }

    private static int TryParseInt(string value)
    {
        return int.TryParse(value, out int result)
            ? result
            : 0;
    }
}
