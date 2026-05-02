using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PassableFarmAnimals;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";

    internal static ModEntry Instance { get; private set; } = null!;

    internal ModConfig config = new();
    private Harmony? harmony;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.config = helper.ReadConfig<ModConfig>();

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
            reset: () => this.config = new ModConfig(),
            save: () => this.Helper.WriteConfig(this.config),
            titleScreenOnly: false
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.EnableMod,
            setValue: value => this.config.EnableMod = value,
            name: () => "Enable mod",
            tooltip: () => "If enabled, farm animals are passable and the farmer can walk through them."
        );
    }
}
