using StardewModdingAPI;

namespace CutsceneMaker.Commands;

public sealed class EventCommandCatalog
{
    private readonly Dictionary<string, EventCommandDefinition> byId;
    private readonly Dictionary<string, EventCommandDefinition> byVerb;

    private EventCommandCatalog(IEnumerable<EventCommandDefinition> definitions)
    {
        this.Definitions = definitions
            .OrderBy(definition => definition.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        this.byId = this.Definitions.ToDictionary(definition => definition.Id, definition => definition, StringComparer.Ordinal);
        this.byVerb = this.Definitions
            .GroupBy(definition => definition.Verb, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public static EventCommandCatalog Empty { get; } = new(Array.Empty<EventCommandDefinition>());

    public IReadOnlyList<EventCommandDefinition> Definitions { get; }

    public static EventCommandCatalog Build(IModRegistry modRegistry)
    {
        ArgumentNullException.ThrowIfNull(modRegistry);

        List<EventCommandDefinition> definitions = new();
        definitions.AddRange(HelperEventCommandProvider.GetDefinitions());
        definitions.AddRange(VanillaEventCommandProvider.GetDefinitions());

        if (modRegistry.IsLoaded(BetterEventsCoreCommandProvider.ModId))
        {
            definitions.AddRange(BetterEventsCoreCommandProvider.GetDefinitions());
        }

        if (modRegistry.IsLoaded(ExtraEventCommandsProvider.ModId))
        {
            definitions.AddRange(ExtraEventCommandsProvider.GetDefinitions());
        }

        return new EventCommandCatalog(definitions);
    }

    public bool HasProvider(string providerModId)
    {
        return this.Definitions.Any(definition => definition.ProviderModId.Equals(providerModId, StringComparison.Ordinal));
    }

    public bool TryGetById(string id, out EventCommandDefinition definition)
    {
        return this.byId.TryGetValue(id, out definition!);
    }

    public bool TryGetByVerb(string verb, out EventCommandDefinition definition)
    {
        return this.byVerb.TryGetValue(verb, out definition!);
    }
}
