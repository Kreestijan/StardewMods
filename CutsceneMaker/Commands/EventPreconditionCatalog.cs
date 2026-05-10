namespace CutsceneMaker.Commands;

public sealed class EventPreconditionCatalog
{
    private readonly Dictionary<string, EventPreconditionDefinition> byId;
    private readonly Dictionary<string, EventPreconditionDefinition> byVerb;

    public EventPreconditionCatalog()
    {
        this.Definitions = GetDefinitions()
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        this.byId = this.Definitions.ToDictionary(definition => definition.Id, definition => definition, StringComparer.Ordinal);
        this.byVerb = this.Definitions.ToDictionary(definition => definition.Verb, definition => definition, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<EventPreconditionDefinition> Definitions { get; }

    public bool TryGetById(string id, out EventPreconditionDefinition definition)
    {
        return this.byId.TryGetValue(id, out definition!);
    }

    public bool TryGetByVerb(string verb, out EventPreconditionDefinition definition)
    {
        return this.byVerb.TryGetValue(verb, out definition!);
    }

    private static IEnumerable<EventPreconditionDefinition> GetDefinitions()
    {
        yield return Define("ActiveDialogueEvent", Text("id", "ID", "example"));
        yield return Define("ChoseDialogueAnswers", Raw("answers", "Answer IDs", "answerId"));
        yield return Define("CommunityCenterOrWarehouseDone");
        yield return Define("Dating", Actor("npc", "NPC"));
        yield return Define("DayOfMonth", Raw("days", "Days", "1"));
        yield return Define("DayOfWeek", Raw("days", "Days", "Monday"));
        yield return Define("DaysPlayed", Integer("days", "Days", "1"));
        yield return Define("EarnedMoney", Integer("amount", "Amount", "1000"));
        yield return Define("FestivalDay");
        yield return Define("FreeInventorySlots", Integer("slots", "Slots", "1"));
        yield return Define("Friendship", Actor("npc", "NPC"), Integer("points", "Points", "250"));
        yield return Define("GameStateQuery", Raw("query", "Query", "SEASON Spring"));
        yield return Define("Gender", Choice("gender", "Gender", "male", "male", "female"));
        yield return Define("GoldenWalnuts", Integer("count", "Count", "1"));
        yield return Define("HasItem", Text("itemId", "Item ID", "(O)74"));
        yield return Define("HasMoney", Integer("amount", "Amount", "500"));
        yield return Define("HostMail", Text("letter", "Letter ID", "exampleLetter"));
        yield return Define("HostOrLocalMail", Text("letter", "Letter ID", "exampleLetter"));
        yield return Define("InUpgradedHouse", OptionalInteger("level", "Level", "2"));
        yield return Define("IsHost");
        yield return Define("JojaBundlesDone");
        yield return Define("LocalMail", Text("letter", "Letter ID", "exampleLetter"));
        yield return Define("MissingPet", Text("pet", "Pet", "", optional: true));
        yield return Define("NPCVisible", Actor("npc", "NPC"));
        yield return Define("NpcVisibleHere", Actor("npc", "NPC"));
        yield return Define("Random", Float("chance", "Chance", "0.5"));
        yield return Define("ReachedMineBottom", OptionalInteger("count", "Count", "1"));
        yield return Define("Roommate");
        yield return Define("SawEvent", Raw("eventIds", "Event IDs", "Example.Event"));
        yield return Define("SawSecretNote", Integer("note", "Note", "1"));
        yield return Define("Season", Raw("seasons", "Seasons", "Spring"));
        yield return Define("SendMail", Text("letter", "Letter ID", "exampleLetter"), Bool("mailbox", "Direct To Mailbox", "", optional: true));
        yield return Define("Shipped", Raw("items", "Item / count", "(O)74 1"));
        yield return Define("Skill", Choice("skill", "Skill", "Farming", "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck"), Integer("level", "Level", "1"));
        yield return Define("Spouse", Actor("npc", "NPC"));
        yield return Define("SpouseBed");
        yield return Define("Tile", Raw("tiles", "Tiles", "0 0"));
        yield return Define("Time", Integer("start", "Start", "600"), Integer("end", "End", "2600"));
        yield return Define("UpcomingFestival", Integer("days", "Days", "1"));
        yield return Define("Weather", Text("weather", "Weather", "Sun"));
        yield return Define("WorldState", Text("id", "ID", "example"));
        yield return Define("Year", Integer("year", "Year", "1"));
    }

    private static EventPreconditionDefinition Define(string verb, params EventCommandParameter[] parameters)
    {
        return new EventPreconditionDefinition
        {
            Id = "vanilla." + verb,
            DisplayName = SplitWords(verb),
            Verb = verb,
            Parameters = parameters
        };
    }

    private static EventCommandParameter Text(string key, string label, string defaultValue, bool optional = false)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Text, DefaultValue = defaultValue, Optional = optional };
    }

    private static EventCommandParameter Integer(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Integer, DefaultValue = defaultValue };
    }

    private static EventCommandParameter OptionalInteger(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.OptionalInteger, DefaultValue = defaultValue, Optional = true };
    }

    private static EventCommandParameter Float(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Float, DefaultValue = defaultValue };
    }

    private static EventCommandParameter Choice(string key, string label, string defaultValue, params string[] choices)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Choice, DefaultValue = defaultValue, Choices = choices };
    }

    private static EventCommandParameter Bool(string key, string label, string defaultValue, bool optional = false)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Boolean, DefaultValue = defaultValue, Optional = optional };
    }

    private static EventCommandParameter Raw(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.RawArguments, DefaultValue = defaultValue, QuoteWhenNeeded = false, TextLimit = 300 };
    }

    private static EventCommandParameter Actor(string key, string label)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Actor, DefaultValue = "Lewis" };
    }

    private static string SplitWords(string value)
    {
        List<char> chars = new();
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(value[i]);
        }

        return new string(chars.ToArray());
    }
}
