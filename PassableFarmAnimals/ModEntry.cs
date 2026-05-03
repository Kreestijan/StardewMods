using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PassableFarmAnimals;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";

    internal static ModEntry Instance { get; private set; } = null!;

    internal ModConfig config = new();

    internal string Translate(string key) =>
        this.Helper.Translation.Get(key);
    private Harmony? harmony;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.config = helper.ReadConfig<ModConfig>();

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        ApplyHarmonyPatches(this.harmony);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void ApplyHarmonyPatches(Harmony harmony)
    {
        var paramTypes = new[] {
            typeof(Rectangle),
            typeof(xTile.Dimensions.Rectangle),
            typeof(bool),
            typeof(int),
            typeof(bool),
            typeof(Character),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool)
        };

        var glTarget = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isCollidingPosition), paramTypes);
        if (glTarget is not null)
        {
            harmony.Patch(glTarget, transpiler: new HarmonyMethod(typeof(FarmAnimalCollisionPatch), nameof(FarmAnimalCollisionPatch.ApplyTranspiler)));
        }
        else
        {
            this.Monitor.Log("Could not find GameLocation.isCollidingPosition to patch.", LogLevel.Error);
        }

        var farmTarget = AccessTools.Method(typeof(Farm), nameof(Farm.isCollidingPosition), paramTypes);
        if (farmTarget is not null)
        {
            harmony.Patch(farmTarget, transpiler: new HarmonyMethod(typeof(FarmAnimalCollisionPatch), nameof(FarmAnimalCollisionPatch.ApplyTranspiler)));
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            reset: () => this.config = new ModConfig(),
            save: () => this.Helper.WriteConfig(this.config),
            titleScreenOnly: false
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.EnableMod,
            setValue: value => this.config.EnableMod = value,
            name: () => this.Translate("gmcm.enable-mod.name"),
            tooltip: () => this.Translate("gmcm.enable-mod.tooltip")
        );
    }
}
