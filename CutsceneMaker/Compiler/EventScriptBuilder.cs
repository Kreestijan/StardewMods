using CutsceneMaker.Models;

namespace CutsceneMaker.Compiler;

public static class EventScriptBuilder
{
    public static string Build(CutsceneData cutscene)
    {
        ArgumentNullException.ThrowIfNull(cutscene);

        List<string> parts = new()
        {
            RequireValue(cutscene.MusicTrack, nameof(cutscene.MusicTrack)),
            $"{cutscene.ViewportStartX} {cutscene.ViewportStartY}",
            CompilePlacementHeader(cutscene)
        };

        if (cutscene.Skippable)
        {
            parts.Add("skippable");
        }

        Dictionary<string, (int X, int Y)> actorPositions = BuildInitialActorPositions(cutscene);
        foreach (object command in cutscene.Commands)
        {
            switch (command)
            {
                case TimelineCommand { Type: CommandType.End }:
                    break;

                case TimelineCommand timelineCommand:
                    parts.AddRange(CompileCommand(timelineCommand, actorPositions));
                    break;

                case RawCommandBlock rawCommand:
                    parts.Add(RequireValue(rawCommand.RawText, nameof(rawCommand.RawText)));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported command object '{command.GetType().FullName}'.");
            }
        }

        parts.Add("end");
        return string.Join("/", parts);
    }

    private static string CompilePlacementHeader(CutsceneData cutscene)
    {
        List<string> placements = new()
        {
            CompilePlacement(cutscene.FarmerPlacement)
        };

        foreach (NpcPlacement actor in cutscene.Actors)
        {
            placements.Add(CompilePlacement(actor));
        }

        return string.Join(" ", placements);
    }

    private static string CompilePlacement(NpcPlacement placement)
    {
        return $"{RequireValue(placement.ActorName, nameof(placement.ActorName))} {placement.TileX} {placement.TileY} {placement.Facing}";
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

    private static IEnumerable<string> CompileCommand(TimelineCommand command, Dictionary<string, (int X, int Y)> actorPositions)
    {
        switch (command.Type)
        {
            case CommandType.Move:
                return CompileMove(command, actorPositions);

            case CommandType.Speak:
                return SingleCommand(CompileSpeak(command));

            case CommandType.Emote:
                return SingleCommand($"emote {RequireValue(command.ActorName, nameof(command.ActorName))} {RequireInt(command.EmoteId, nameof(command.EmoteId))} true");

            case CommandType.Pause:
                return SingleCommand($"precisePause {RequireInt(command.DurationMs, nameof(command.DurationMs))}");

            case CommandType.FadeOut:
                return SingleCommand("globalFade");

            case CommandType.FadeIn:
                return SingleCommand("globalFadeToClear");

            case CommandType.Reward:
                return SingleCommand(CompileReward(command));

            case CommandType.End:
                return SingleCommand("end");

            default:
                throw new InvalidOperationException($"Unsupported command type '{command.Type}'.");
        }
    }

    private static string CompileSpeak(TimelineCommand command)
    {
        string actorName = RequireValue(command.ActorName, nameof(command.ActorName));
        string dialogue = EscapeDialogue(RequireValue(command.DialogueText, nameof(command.DialogueText)));
        return actorName.Equals("farmer", StringComparison.OrdinalIgnoreCase)
            ? $"message \"{dialogue}\""
            : $"speak {actorName} \"{dialogue}\"";
    }

    private static IEnumerable<string> CompileMove(TimelineCommand command, Dictionary<string, (int X, int Y)> actorPositions)
    {
        string actorName = RequireValue(command.ActorName, nameof(command.ActorName));
        int targetX = RequireInt(command.TileX, nameof(command.TileX));
        int targetY = RequireInt(command.TileY, nameof(command.TileY));
        int facing = RequireInt(command.Facing, nameof(command.Facing));

        (int X, int Y) currentPosition = actorPositions.TryGetValue(actorName, out (int X, int Y) knownPosition)
            ? knownPosition
            : (0, 0);

        int deltaX = targetX - currentPosition.X;
        int deltaY = targetY - currentPosition.Y;
        actorPositions[actorName] = (targetX, targetY);

        List<string> commands = new();
        if (deltaX != 0)
        {
            int segmentFacing = deltaY == 0 ? facing : GetDirectionForDelta(deltaX, 0);
            commands.Add($"move {actorName} {deltaX} 0 {segmentFacing}");
        }

        if (deltaY != 0)
        {
            commands.Add($"move {actorName} 0 {deltaY} {facing}");
        }

        if (commands.Count == 0)
        {
            commands.Add($"faceDirection {actorName} {facing} true");
        }

        return commands;
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

    private static IEnumerable<string> SingleCommand(string command)
    {
        yield return command;
    }

    private static string CompileReward(TimelineCommand command)
    {
        RewardType rewardType = command.RewardType ?? throw new InvalidOperationException($"{nameof(command.RewardType)} is required.");
        return rewardType switch
        {
            RewardType.Item => $"addItem {RequireValue(command.ItemId, nameof(command.ItemId))} {command.Quantity ?? 1}",
            RewardType.Gold => $"addMoney {RequireInt(command.GoldAmount, nameof(command.GoldAmount))}",
            RewardType.Friendship => $"friendship {RequireValue(command.RewardNpcName, nameof(command.RewardNpcName))} {RequireInt(command.FriendshipAmount, nameof(command.FriendshipAmount))}",
            RewardType.MailFlag => $"mail {RequireValue(command.ItemId, nameof(command.ItemId))}",
            RewardType.Quest => $"addQuest {RequireValue(command.ItemId, nameof(command.ItemId))}",
            RewardType.CookingRecipe => $"learnRecipe {RequireValue(command.ItemId, nameof(command.ItemId))}",
            RewardType.CraftingRecipe => $"learnCraftingRecipe {RequireValue(command.ItemId, nameof(command.ItemId))}",
            _ => throw new InvalidOperationException($"Unsupported reward type '{rewardType}'.")
        };
    }

    private static string EscapeDialogue(string dialogue)
    {
        return dialogue.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }

    private static int RequireInt(int? value, string fieldName)
    {
        return value ?? throw new InvalidOperationException($"{fieldName} is required.");
    }
}
