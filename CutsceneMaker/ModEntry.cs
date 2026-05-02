using StardewModdingAPI;
using StardewModdingAPI.Events;
using CutsceneMaker.Compiler;
using CutsceneMaker.Importer;
using CutsceneMaker.Models;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Locations;

namespace CutsceneMaker;

public sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;

    public static List<string> KnownNpcNames { get; } = new();
    public static Dictionary<string, string> KnownNpcSpriteAssets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, Point> KnownNpcSpriteSizes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string ModsDirectoryPath { get; private set; } = string.Empty;
    private TitleMenuButtonController titleMenuButton = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.ModsDirectoryPath = Path.Combine(Constants.GamePath, "Mods");
        this.titleMenuButton = new TitleMenuButtonController(helper);
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

#if DEBUG
        this.RunCompilerSmokeTest();
#endif
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Dictionary<string, LocationData> locationData = this.Helper.GameContent.Load<Dictionary<string, LocationData>>("Data/Locations");
        LocationBootstrapper.SetSupportedLocations(LocationBootstrapper.GetLocationNamesFromData(locationData));

        this.RefreshKnownNpcs();

        GameLocation? town = LocationBootstrapper.Load("Town");
        this.Monitor.Log(
            town is null
                ? "Cutscene Maker bootstrap check could not load Town."
                : "Cutscene Maker bootstrap check loaded Town.",
            town is null ? LogLevel.Warn : LogLevel.Trace
        );
    }

    public void RefreshKnownNpcs()
    {
        Dictionary<string, CharacterData> characters = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (KeyValuePair<string, CharacterData> pair in this.Helper.GameContent.Load<Dictionary<string, CharacterData>>("Data/Characters"))
            {
                characters[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Cutscene Maker could not read Data/Characters: {ex.Message}", LogLevel.Warn);
        }

        if (Game1.characterData is not null)
        {
            foreach (KeyValuePair<string, CharacterData> pair in Game1.characterData)
            {
                characters[pair.Key] = pair.Value;
            }
        }

        KnownNpcNames.Clear();
        KnownNpcSpriteAssets.Clear();
        KnownNpcSpriteSizes.Clear();

        foreach (KeyValuePair<string, CharacterData> pair in characters.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            KnownNpcNames.Add(pair.Key);

            CharacterData? data = pair.Value;
            string textureName = string.IsNullOrWhiteSpace(data?.TextureName)
                ? pair.Key
                : data.TextureName;
            KnownNpcSpriteAssets[pair.Key] = "Characters\\" + textureName;
            KnownNpcSpriteSizes[pair.Key] = data?.Size == Point.Zero ? new Point(16, 32) : data?.Size ?? new Point(16, 32);
        }
    }

#if DEBUG
    private void RunCompilerSmokeTest()
    {
        CutsceneData sample = CutsceneData.CreateBlank();
        sample.CutsceneName = "Debug Sample";
        sample.LocationName = "Town";
        sample.MusicTrack = "none";
        sample.ViewportStartX = -100;
        sample.ViewportStartY = -100;
        sample.FarmerPlacement = new NpcPlacement
        {
            ActorName = "farmer",
            TileX = 10,
            TileY = 12,
            Facing = 2
        };
        sample.Actors.Add(new NpcPlacement
        {
            ActorName = "Penny",
            TileX = 12,
            TileY = 12,
            Facing = 3
        });
        sample.Triggers.Add(new PreconditionData
        {
            Type = PreconditionType.Season,
            Season = "Spring"
        });
        sample.Commands.Insert(0, new TimelineCommand
        {
            Type = CommandType.Speak,
            ActorName = "Penny",
            DialogueText = "This is a Cutscene Maker compiler test.$h"
        });

        this.Monitor.Log("Cutscene Maker compiler smoke test:", LogLevel.Debug);
        this.Monitor.Log("Key: " + EventKeyBuilder.Build(sample), LogLevel.Debug);
        this.Monitor.Log("Script: " + EventScriptBuilder.Build(sample), LogLevel.Debug);

        List<string> split = QuoteAwareSplit.Split("none/-100 -100/speak Penny \"hello / still dialogue\"/end", '/');
        this.Monitor.Log("Quote-aware split sample parts: " + split.Count, LogLevel.Debug);
    }
#endif
}
