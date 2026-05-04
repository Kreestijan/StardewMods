using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace EatUntilFull;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";

    internal static ModEntry Instance { get; private set; } = null!;

    internal ModConfig Config { get; private set; } = new();

    internal string Translate(string key) =>
        this.Helper.Translation.Get(key);

    private Harmony? harmony;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.Config = helper.ReadConfig<ModConfig>();

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.harmony.PatchAll();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
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
            reset: this.ResetConfig,
            save: this.SaveConfig,
            titleScreenOnly: false
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.Config.EnableMod,
            setValue: value => this.Config.EnableMod = value,
            name: () => this.Translate("gmcm.enable-mod.name"),
            tooltip: () => this.Translate("gmcm.enable-mod.tooltip")
        );

        string[] fillTargets = Enum.GetNames<FillTarget>();
        gmcm.AddTextOption(
            this.ModManifest,
            getValue: () => this.Config.FillTarget.ToString(),
            setValue: value =>
            {
                if (System.Enum.TryParse<FillTarget>(value, out var target))
                    this.Config.FillTarget = target;
            },
            name: () => this.Translate("gmcm.fill-target.name"),
            tooltip: () => this.Translate("gmcm.fill-target.tooltip"),
            allowedValues: fillTargets,
            formatValue: value => this.Translate($"gmcm.fill-target.{value.ToLowerInvariant()}")
        );
    }

    private void ResetConfig()
    {
        this.Config = new ModConfig();
    }

    private void SaveConfig()
    {
        this.Helper.WriteConfig(this.Config);
    }
}
