using CutsceneMaker.Commands;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;

namespace CutsceneMaker.Compiler;

public static class EventScriptBuilder
{
    public static string Build(CutsceneData cutscene, EventCommandCatalog? commandCatalog = null, int startCommandIndex = -1, Dictionary<string, Point>? initialPositions = null)
    {
        ArgumentNullException.ThrowIfNull(cutscene);
        commandCatalog ??= EventCommandCatalog.Empty;

        List<string> parts = new()
        {
            RequireValue(cutscene.MusicTrack, nameof(cutscene.MusicTrack)),
            CompileInitialViewport(cutscene),
            CompilePlacementHeader(cutscene, initialPositions)
        };

        if (cutscene.Skippable)
        {
            parts.Add("skippable");
        }

        Dictionary<string, Point> actorPositions = BuildInitialActorPositions(cutscene);
        if (initialPositions is not null)
        {
            foreach (var kvp in initialPositions)
            {
                actorPositions[kvp.Key] = kvp.Value;
            }
        }

        int startIndex = Math.Max(0, startCommandIndex);
        for (int i = startIndex; i < cutscene.Commands.Count; i++)
        {
            object command = cutscene.Commands[i];
            switch (command)
            {
                case EventCommandBlock eventCommand:
                    parts.AddRange(CompileEventCommand(cutscene, commandCatalog, eventCommand, actorPositions));
                    break;

                case RawCommandBlock rawCommand:
                    parts.Add(RequireValue(rawCommand.RawText, nameof(rawCommand.RawText)));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported command object '{command.GetType().FullName}'.");
            }
        }

        if (parts.Count == 0 || !IsEndCommand(parts[^1]))
        {
            parts.Add("end");
        }

        return string.Join("/", parts);
    }

    public static string ResolveActorName(CutsceneData cutscene, string? actorSlotId, string? actorName)
    {
        if (!string.IsNullOrWhiteSpace(actorSlotId))
        {
            if (cutscene.FarmerPlacement.ActorSlotId.Equals(actorSlotId, StringComparison.Ordinal))
            {
                if (!cutscene.IncludeFarmer)
                {
                    throw new InvalidOperationException("This cutscene has farmer disabled but a command still references farmer.");
                }

                return "farmer";
            }

            NpcPlacement? actor = cutscene.Actors.FirstOrDefault(actor =>
                actor.ActorSlotId.Equals(actorSlotId, StringComparison.Ordinal));
            if (actor is not null)
            {
                return RequireValue(actor.ActorName, nameof(actor.ActorName));
            }
        }

        string resolvedActorName = RequireValue(actorName, nameof(actorName));
        if (resolvedActorName.Equals("farmer", StringComparison.OrdinalIgnoreCase) && !cutscene.IncludeFarmer)
        {
            throw new InvalidOperationException("This cutscene has farmer disabled but a command still references farmer.");
        }

        return resolvedActorName;
    }

    private static IEnumerable<string> CompileEventCommand(CutsceneData cutscene, EventCommandCatalog commandCatalog, EventCommandBlock command, Dictionary<string, Point> actorPositions)
    {
        if (!commandCatalog.TryGetById(command.CommandId, out EventCommandDefinition? definition))
        {
            throw new InvalidOperationException($"Command '{command.CommandId}' from '{command.ProviderModId}' is unavailable.");
        }

        if (command.CommandId.Equals("vanilla.speak", StringComparison.Ordinal))
        {
            yield return CompileSpeakCommand(cutscene, command);
            yield break;
        }

        if (command.CommandId.Equals("helper.reward", StringComparison.Ordinal))
        {
            yield return CompileRewardCommand(command);
            yield break;
        }

        if (command.CommandId.Equals("vanilla.question", StringComparison.Ordinal))
        {
            yield return CompileQuestionCommand(command);
            yield break;
        }

        if (command.CommandId.Equals("vanilla.move", StringComparison.Ordinal))
        {
            foreach (string moveCommand in CompileMoveCommand(cutscene, command, actorPositions))
            {
                yield return moveCommand;
            }

            yield break;
        }

        string compiled = definition.Compile(command, (slotId, actorName) => ResolveActorName(cutscene, slotId, actorName));
        UpdateKnownActorPosition(cutscene, command, actorPositions);
        yield return compiled;
    }

    private static string CompileSpeakCommand(CutsceneData cutscene, EventCommandBlock command)
    {
        string actorName = ResolveActorName(
            cutscene,
            command.ActorSlotIds.TryGetValue("actor", out string? actorSlotId) ? actorSlotId : null,
            GetValue(command, "actor")
        );
        string text = QuoteArgumentIfNeeded(GetValue(command, "text"));

        return actorName.Equals("farmer", StringComparison.OrdinalIgnoreCase)
            ? $"message {text}"
            : $"speak {actorName} {text}";
    }

    private static string CompileQuestionCommand(EventCommandBlock command)
    {
        string forkAnswer = GetValue(command, "forkAnswer");
        string mode = string.IsNullOrWhiteSpace(forkAnswer) ? "null" : $"fork{RequireInt(forkAnswer, "forkAnswer")}";
        string payload = string.Join("#", new[] { GetValue(command, "question") }.Concat(GetQuestionAnswers(command)));
        return $"question {mode} {QuoteArgument(payload)}";
    }

    private static IEnumerable<string> CompileMoveCommand(CutsceneData cutscene, EventCommandBlock command, Dictionary<string, Point> actorPositions)
    {
        string actorName = ResolveActorName(
            cutscene,
            command.ActorSlotIds.TryGetValue("actor", out string? actorSlotId) ? actorSlotId : null,
            GetValue(command, "actor")
        );
        int targetX = RequireInt(GetValue(command, "targetX"), "targetX");
        int targetY = RequireInt(GetValue(command, "targetY"), "targetY");
        int facing = RequireInt(GetValue(command, "direction"), "direction");
        bool continueAfterMove = bool.TryParse(GetValue(command, "continue"), out bool parsedContinue) && parsedContinue;

        Point currentPosition = actorPositions.TryGetValue(actorName, out Point knownPosition)
            ? knownPosition
            : Point.Zero;
        Point target = new(targetX, targetY);
        int deltaX = target.X - currentPosition.X;
        int deltaY = target.Y - currentPosition.Y;
        actorPositions[actorName] = target;

        if (deltaX == 0 && deltaY == 0)
        {
            yield return $"faceDirection {actorName} {facing} true";
            yield break;
        }

        if (deltaX != 0)
        {
            int segmentFacing = deltaY == 0 ? facing : GetDirectionForDelta(deltaX, 0);
            string continueSuffix = deltaY == 0 && continueAfterMove ? " true" : string.Empty;
            yield return $"move {actorName} {deltaX} 0 {segmentFacing}{continueSuffix}";
        }

        if (deltaY != 0)
        {
            string continueSuffix = continueAfterMove ? " true" : string.Empty;
            yield return $"move {actorName} 0 {deltaY} {facing}{continueSuffix}";
        }
    }

    private static string CompileRewardCommand(EventCommandBlock command)
    {
        string kind = GetValue(command, "kind");
        string target = RequireValue(GetValue(command, "target"), "target");
        string amount = GetValue(command, "amount");
        string quality = GetValue(command, "quality");

        return kind switch
        {
            "Item" => CompileItemReward(target, amount, quality),
            "Gold" => $"money {target}",
            "Friendship" => $"friendship {target} {RequireValue(amount, "amount")}",
            "Mail" => $"mail {target}",
            "Quest" => $"addQuest {target}",
            "Cooking Recipe" => $"addCookingRecipe {QuoteArgumentIfNeeded(target)}",
            "Crafting Recipe" => $"addCraftingRecipe {QuoteArgumentIfNeeded(target)}",
            _ => throw new InvalidOperationException($"Unsupported reward type '{kind}'.")
        };
    }

    private static string CompileItemReward(string target, string amount, string quality)
    {
        if (string.IsNullOrWhiteSpace(amount))
        {
            return $"addItem {target}";
        }

        return string.IsNullOrWhiteSpace(quality)
            ? $"addItem {target} {amount}"
            : $"addItem {target} {amount} {quality}";
    }

    private static void UpdateKnownActorPosition(CutsceneData cutscene, EventCommandBlock command, Dictionary<string, Point> actorPositions)
    {
        if (!command.CommandId.Equals("vanilla.warp", StringComparison.Ordinal))
        {
            return;
        }

        string actorName = ResolveActorName(
            cutscene,
            command.ActorSlotIds.TryGetValue("actor", out string? actorSlotId) ? actorSlotId : null,
            GetValue(command, "actor")
        );
        actorPositions[actorName] = new Point(RequireInt(GetValue(command, "x"), "x"), RequireInt(GetValue(command, "y"), "y"));
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

    private static int GetDirectionForDelta(int deltaX, int deltaY)
    {
        if (deltaX < 0)
        {
            return 3;
        }

        if (deltaX > 0)
        {
            return 1;
        }

        return deltaY < 0 ? 0 : 2;
    }

    private static bool IsEndCommand(string command)
    {
        return command.Equals("end", StringComparison.Ordinal)
            || command.StartsWith("end ", StringComparison.Ordinal);
    }

    public static int GetHeaderEntryCount(CutsceneData cutscene)
    {
        return 3 + (cutscene.Skippable ? 1 : 0);
    }

    private static string CompilePlacementHeader(CutsceneData cutscene, Dictionary<string, Point>? initialPositions = null)
    {
        List<string> placements = new();
        if (cutscene.IncludeFarmer)
        {
            placements.Add(CompilePlacement(cutscene.FarmerPlacement, "farmer", initialPositions));
        }

        foreach (NpcPlacement actor in cutscene.Actors)
        {
            placements.Add(CompilePlacement(actor, actor.ActorName, initialPositions));
        }

        if (placements.Count == 0)
        {
            throw new InvalidOperationException("At least one actor must be included in the cutscene setup.");
        }

        return string.Join(" ", placements);
    }

    private static string CompileInitialViewport(CutsceneData cutscene)
    {
        if (cutscene.ViewportStartX >= 0 && cutscene.ViewportStartY >= 0)
        {
            return $"{cutscene.ViewportStartX} {cutscene.ViewportStartY}";
        }

        NpcPlacement? firstActor = cutscene.IncludeFarmer
            ? cutscene.FarmerPlacement
            : cutscene.Actors.FirstOrDefault();

        return firstActor is not null
            ? $"{firstActor.TileX} {firstActor.TileY}"
            : "0 0";
    }

    private static string CompilePlacement(NpcPlacement placement, string actorName, Dictionary<string, Point>? initialPositions = null)
    {
        if (initialPositions is not null && initialPositions.TryGetValue(actorName, out Point overridePos))
        {
            return $"{RequireValue(actorName, nameof(actorName))} {overridePos.X} {overridePos.Y} {placement.Facing}";
        }

        return $"{RequireValue(placement.ActorName, nameof(placement.ActorName))} {placement.TileX} {placement.TileY} {placement.Facing}";
    }

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }

    private static int RequireInt(string? value, string fieldName)
    {
        if (value is not null && value.Contains("{{", StringComparison.Ordinal))
        {
            return 0; // CP token — placeholder, CP resolves at runtime
        }

        if (!int.TryParse(value, out int result))
        {
            throw new InvalidOperationException($"{fieldName} must be an integer.");
        }

        return result;
    }

    private static string GetValue(EventCommandBlock command, string key)
    {
        return command.Values.TryGetValue(key, out string? value)
            ? value
            : string.Empty;
    }

    private static IEnumerable<string> GetQuestionAnswers(EventCommandBlock command)
    {
        int count = command.Values.TryGetValue("answers.count", out string? configuredCount) && int.TryParse(configuredCount, out int parsedCount)
            ? Math.Max(0, parsedCount)
            : 0;

        for (int index = 0; index < count; index++)
        {
            if (command.Values.TryGetValue($"answers.{index}", out string? answer) && !string.IsNullOrWhiteSpace(answer))
            {
                yield return answer;
            }
        }
    }

    private static string QuoteArgumentIfNeeded(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
        {
            return value; // CP token — output verbatim, CP resolves at runtime
        }

        if (value.Length == 0 || value.Any(character => char.IsWhiteSpace(character) || character == '/'))
        {
            return QuoteArgument(value);
        }

        return value;
    }

    private static string QuoteArgument(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
        {
            return value; // CP token — output verbatim
        }

        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
