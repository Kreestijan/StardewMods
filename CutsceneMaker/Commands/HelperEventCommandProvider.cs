namespace CutsceneMaker.Commands;

public static class HelperEventCommandProvider
{
    public const string ModId = "Kree.CutsceneMaker.Helper";
    public const string ProviderName = "Helper";

    public static IEnumerable<EventCommandDefinition> GetDefinitions()
    {
        yield return new EventCommandDefinition
        {
            Id = "helper.reward",
            ProviderModId = ModId,
            ProviderName = ProviderName,
            DisplayName = "Reward",
            Verb = "reward",
            Badge = "Helper",
            Parameters = new[]
            {
                new EventCommandParameter
                {
                    Key = "kind",
                    Label = "Reward Type",
                    Type = EventCommandParameterType.RewardKind,
                    DefaultValue = "Item",
                    Choices = new[] { "Item", "Gold", "Friendship", "Mail", "Quest", "Cooking Recipe", "Crafting Recipe" }
                },
                new EventCommandParameter
                {
                    Key = "target",
                    Label = "Item / NPC / ID",
                    Type = EventCommandParameterType.Text,
                    DefaultValue = "(O)74",
                    Hint = "Item uses item ID. Friendship uses NPC name. Mail/Quest/Recipe use their ID/name."
                },
                new EventCommandParameter
                {
                    Key = "amount",
                    Label = "Amount",
                    Type = EventCommandParameterType.OptionalInteger,
                    DefaultValue = "1",
                    Optional = true,
                    Hint = "Item count, gold amount, or friendship points. Ignored for mail, quest, and recipes."
                },
                new EventCommandParameter
                {
                    Key = "quality",
                    Label = "Quality",
                    Type = EventCommandParameterType.OptionalInteger,
                    DefaultValue = "",
                    Optional = true,
                    Hint = "Item quality only. Common values: 0 normal, 1 silver, 2 gold, 4 iridium."
                }
            }
        };
    }
}
