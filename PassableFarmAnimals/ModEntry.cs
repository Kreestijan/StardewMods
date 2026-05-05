using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PassableFarmAnimals;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";

    internal static ModEntry Instance { get; private set; } = null!;

    internal ModConfig config = new();
    internal NudgeManager Nudges { get; private set; } = null!;

    internal string Translate(string key) =>
        this.Helper.Translation.Get(key);
    private Harmony? harmony;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.config = helper.ReadConfig<ModConfig>();
        this.ClampConfig();
        this.Nudges = new NudgeManager(this);

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        ApplyHarmonyPatches(this.harmony);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += this.Nudges.OnUpdateTicked;
        helper.Events.Multiplayer.ModMessageReceived += this.Nudges.OnModMessageReceived;
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

        var farmTarget = AccessTools.DeclaredMethod(typeof(Farm), nameof(Farm.isCollidingPosition), paramTypes);
        if (farmTarget is not null)
        {
            harmony.Patch(farmTarget, transpiler: new HarmonyMethod(typeof(FarmAnimalCollisionPatch), nameof(FarmAnimalCollisionPatch.ApplyTranspiler)));
        }
        else
        {
            this.Monitor.Log("Farm does not declare its own isCollidingPosition implementation; GameLocation patch covers Farm collisions.", LogLevel.Trace);
        }

        var drawTarget = AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.draw), new[] { typeof(SpriteBatch) });
        if (drawTarget is not null)
        {
            harmony.Patch(
                drawTarget,
                prefix: new HarmonyMethod(typeof(FarmAnimalDrawPatch), nameof(FarmAnimalDrawPatch.Prefix)),
                postfix: new HarmonyMethod(typeof(FarmAnimalDrawPatch), nameof(FarmAnimalDrawPatch.Postfix)),
                finalizer: new HarmonyMethod(typeof(FarmAnimalDrawPatch), nameof(FarmAnimalDrawPatch.Finalizer))
            );
        }
        else
        {
            this.Monitor.Log("Could not find FarmAnimal.draw to patch for visual nudges.", LogLevel.Warn);
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
            save: () =>
            {
                this.ClampConfig();
                this.Helper.WriteConfig(this.config);
            },
            titleScreenOnly: false
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.EnableMod,
            setValue: value => this.config.EnableMod = value,
            name: () => this.Translate("gmcm.enable-mod.name"),
            tooltip: () => this.Translate("gmcm.enable-mod.tooltip")
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.EnableNudge,
            setValue: value => this.config.EnableNudge = value,
            name: () => this.Translate("gmcm.enable-nudge.name"),
            tooltip: () => this.Translate("gmcm.enable-nudge.tooltip")
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.NudgeStrengthPixels,
            setValue: value => this.config.NudgeStrengthPixels = Math.Clamp(value, 0, 128),
            name: () => this.Translate("gmcm.nudge-strength.name"),
            tooltip: () => this.Translate("gmcm.nudge-strength.tooltip"),
            min: 0,
            max: 128,
            interval: 4,
            formatValue: value => $"{value}px"
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.NudgeDurationMs,
            setValue: value => this.config.NudgeDurationMs = Math.Clamp(value, 50, 1000),
            name: () => this.Translate("gmcm.nudge-duration.name"),
            tooltip: () => this.Translate("gmcm.nudge-duration.tooltip"),
            min: 50,
            max: 1000,
            interval: 10,
            formatValue: value => $"{value}ms"
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.NudgeCooldownMs,
            setValue: value => this.config.NudgeCooldownMs = Math.Clamp(value, 0, 2000),
            name: () => this.Translate("gmcm.nudge-cooldown.name"),
            tooltip: () => this.Translate("gmcm.nudge-cooldown.tooltip"),
            min: 0,
            max: 2000,
            interval: 25,
            formatValue: value => $"{value}ms"
        );
    }

    internal void ClampConfig()
    {
        this.config.NudgeStrengthPixels = Math.Clamp(this.config.NudgeStrengthPixels, 0, 128);
        this.config.NudgeDurationMs = Math.Clamp(this.config.NudgeDurationMs, 50, 1000);
        this.config.NudgeCooldownMs = Math.Clamp(this.config.NudgeCooldownMs, 0, 2000);
    }
}
