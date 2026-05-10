using CutsceneMaker.Commands;
using CutsceneMaker.Models;

namespace CutsceneMaker.Compiler;

public static class CutsceneValidator
{
    public static List<string> Validate(CutsceneData cutscene, EventCommandCatalog commandCatalog, EventPreconditionCatalog preconditionCatalog, bool forPreview)
    {
        List<string> errors = new();
        if (!cutscene.IncludeFarmer && cutscene.Actors.Count == 0)
        {
            errors.Add("At least one actor must be included.");
        }

        foreach (object command in cutscene.Commands)
        {
            if (command is RawCommandBlock raw && string.IsNullOrWhiteSpace(raw.RawText))
            {
                errors.Add("Raw command cannot be empty.");
                continue;
            }

            if (command is not EventCommandBlock block)
            {
                continue;
            }

            if (!commandCatalog.TryGetById(block.CommandId, out EventCommandDefinition? definition))
            {
                errors.Add($"Command '{block.DisplayName}' is unavailable.");
                continue;
            }

            if (forPreview && definition.UnsafeForPreview)
            {
                errors.Add($"'{definition.DisplayName}' cannot be previewed safely.");
            }

            if (block.CommandId.Equals("vanilla.question", StringComparison.Ordinal))
            {
                ValidateQuestion(block, errors);
            }

            foreach (EventCommandParameter parameter in definition.Parameters)
            {
                string value = block.Values.TryGetValue(parameter.Key, out string? configured) ? configured : parameter.DefaultValue;
                if (!parameter.Optional
                    && parameter.Type is not EventCommandParameterType.RawArguments and not EventCommandParameterType.AnswerList
                    && string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"'{definition.DisplayName}' requires {parameter.Label}.");
                }

                if (parameter.Type is EventCommandParameterType.Actor or EventCommandParameterType.OptionalActor && !string.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        string? slotId = block.ActorSlotIds.TryGetValue(parameter.Key, out string? configuredSlot) ? configuredSlot : null;
                        EventScriptBuilder.ResolveActorName(cutscene, slotId, value.TrimEnd('?'));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex.Message);
                    }
                }
            }
        }

        foreach (EventPreconditionBlock trigger in cutscene.Triggers)
        {
            if (!preconditionCatalog.TryGetById(trigger.PreconditionId, out EventPreconditionDefinition? definition))
            {
                if (string.IsNullOrWhiteSpace(trigger.Verb))
                {
                    errors.Add("Raw precondition cannot be empty.");
                }

                continue;
            }

            foreach (EventCommandParameter parameter in definition.Parameters)
            {
                string value = trigger.Values.TryGetValue(parameter.Key, out string? configured) ? configured : parameter.DefaultValue;
                if (!parameter.Optional && string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"'{definition.DisplayName}' requires {parameter.Label}.");
                }
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void ValidateQuestion(EventCommandBlock command, List<string> errors)
    {
        if (ContainsQuestionDelimiter(command.Values.TryGetValue("question", out string? question) ? question : string.Empty))
        {
            errors.Add("'Question' text cannot contain # because vanilla uses it to separate answers.");
        }

        int count = command.Values.TryGetValue("answers.count", out string? configuredCount) && int.TryParse(configuredCount, out int parsedCount)
            ? Math.Max(0, parsedCount)
            : 0;
        int nonEmptyAnswers = 0;
        for (int index = 0; index < count; index++)
        {
            if (!command.Values.TryGetValue($"answers.{index}", out string? answer) || string.IsNullOrWhiteSpace(answer))
            {
                continue;
            }

            nonEmptyAnswers++;
            if (ContainsQuestionDelimiter(answer))
            {
                errors.Add($"Question answer {index + 1} cannot contain # because vanilla uses it to separate answers.");
            }
        }

        if (nonEmptyAnswers == 0)
        {
            errors.Add("'Question' requires at least one answer.");
        }
    }

    private static bool ContainsQuestionDelimiter(string value)
    {
        return value.Contains('#', StringComparison.Ordinal);
    }
}
