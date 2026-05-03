using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace IncreasedPettingArea;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";

    internal static ModEntry Instance { get; private set; } = null!;

    internal ModConfig Config { get; private set; } = new();

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
            name: () => "Enable mod",
            tooltip: () => "Pets all animals within radius when you pet one."
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.Config.PetRadius,
            setValue: value => this.Config.PetRadius = value,
            name: () => "Pet radius",
            tooltip: () => "Tile radius around the clicked animal to also pet.",
            min: 3,
            max: 20,
            interval: 1
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
