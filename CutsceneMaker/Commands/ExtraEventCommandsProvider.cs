namespace CutsceneMaker.Commands;

public static class ExtraEventCommandsProvider
{
    public const string ModId = "tsubasamoon.ExtraEventCommands";
    private const string ProviderName = "Extra Event Commands";
    private const string Badge = "EEC";

    public static IEnumerable<EventCommandDefinition> GetDefinitions()
    {
        yield return Define("stripClothing", "Strip Clothing", "stripClothing", ClothingItems("items"));
        yield return Define("restoreClothing", "Restore Clothing", "restoreClothing", ClothingItems("items"));
        yield return Define("strip", "Strip Clothing Alias", "strip", ClothingItems("items"));
        yield return Define("wear", "Wear Clothing Alias", "wear", ClothingItems("items"));
        yield return Define("stripAll", "Strip All", "stripAll");
        yield return Define("wearAll", "Wear All", "wearAll");
        yield return Define("rotateFarmer", "Rotate Farmer", "rotateFarmer", Integer("degrees", "Degrees", "180"));
        yield return Define("resetFarmerRotation", "Reset Farmer Rotation", "resetFarmerRotation");
        yield return Define("farmerAbove", "Farmer Above NPC", "farmerAbove", Actor("npc", "NPC"));
        yield return Define("farmerBelow", "Farmer Below NPC", "farmerBelow", Actor("npc", "NPC"));
        yield return Define("resetLayers", "Reset Layers", "resetLayers");
        yield return Define("tempActorAtTile", "Temp Actor At Tile", "tempActorAtTile", Actor("actor", "Actor"), Integer("tileX", "Tile X", "0"), Integer("tileY", "Tile Y", "0"));
        yield return Define("tempActorAtScreen", "Temp Actor At Screen", "tempActorAtScreen", Actor("actor", "Actor"), Integer("screenX", "Screen X", "640"), Integer("screenY", "Screen Y", "360"));
        yield return Define("resetTempActors", "Reset Temp Actors", "resetTempActors");
    }

    private static EventCommandDefinition Define(string id, string displayName, string verb, params EventCommandParameter[] parameters)
    {
        return new EventCommandDefinition
        {
            Id = id,
            ProviderModId = ModId,
            ProviderName = ProviderName,
            DisplayName = displayName,
            Verb = verb,
            Badge = Badge,
            Parameters = parameters
        };
    }

    private static EventCommandParameter ClothingItems(string key)
    {
        return new EventCommandParameter
        {
            Key = key,
            Label = "Items",
            Type = EventCommandParameterType.Text,
            DefaultValue = "shirt pants",
            TextLimit = 120,
            QuoteWhenNeeded = false
        };
    }

    private static EventCommandParameter Integer(string key, string label, string defaultValue)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Integer, DefaultValue = defaultValue };
    }

    private static EventCommandParameter Actor(string key, string label)
    {
        return new EventCommandParameter { Key = key, Label = label, Type = EventCommandParameterType.Actor, DefaultValue = "farmer" };
    }
}
