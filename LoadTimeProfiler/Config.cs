using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace LoadTimeProfiler;

public sealed class ModConfig
{
    public int ThresholdMs { get; set; } = 100;

    public bool ShowAllMods { get; set; }

    public bool LogOnStartup { get; set; } = true;

    public SButton OverlayKey { get; set; } = SButton.K;

    public int OverlayTopN { get; set; } = 10;

    public int OverlaySampleWindow { get; set; } = 60;

    public void Clamp()
    {
        this.ThresholdMs = Math.Clamp(this.ThresholdMs, 10, 2000);
        this.OverlayTopN = Math.Clamp(this.OverlayTopN, 1, 30);
        this.OverlaySampleWindow = Math.Clamp(this.OverlaySampleWindow, 10, 300);
    }
}

public static class ConfigRegistrar
{
    public static void RegisterGmcm(Mod mod, ModConfig config)
    {
        if (!mod.Helper.ModRegistry.IsLoaded("spacechase0.GenericModConfigMenu"))
        {
            return;
        }

        try
        {
            IGenericModConfigMenuApi? api = mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api is null)
            {
                mod.Monitor.Log("Generic Mod Config Menu was detected, but its API could not be mapped.", LogLevel.Warn);
                return;
            }

            api.Register(
                mod.ModManifest,
                reset: () =>
                {
                    ModConfig defaults = new();
                    defaults.Clamp();
                    Copy(defaults, config);
                },
                save: () =>
                {
                    config.Clamp();
                    mod.Helper.WriteConfig(config);
                }
            );

            api.AddNumberOption(
                mod.ModManifest,
                getValue: () => config.ThresholdMs,
                setValue: value => config.ThresholdMs = value,
                name: () => "Threshold (ms)",
                tooltip: () => "Mods at or above this load time are flagged in the startup log.",
                min: 10,
                max: 2000,
                interval: 10
            );

            api.AddBoolOption(
                mod.ModManifest,
                getValue: () => config.ShowAllMods,
                setValue: value => config.ShowAllMods = value,
                name: () => "Show all mods",
                tooltip: () => "Show every mod in the startup load log instead of only flagged ones."
            );

            api.AddBoolOption(
                mod.ModManifest,
                getValue: () => config.LogOnStartup,
                setValue: value => config.LogOnStartup = value,
                name: () => "Log on startup",
                tooltip: () => "Print the startup load timing summary after launch."
            );

            api.AddKeybindList(
                mod.ModManifest,
                getValue: () => new KeybindList(config.OverlayKey),
                setValue: value =>
                {
                    SButton button = value.Keybinds
                        .SelectMany(keybind => keybind.Buttons)
                        .FirstOrDefault(candidate => candidate != SButton.None);

                    config.OverlayKey = button == SButton.None ? SButton.K : button;
                },
                name: () => "Overlay toggle key",
                tooltip: () => "Press this key to open or close the runtime profiler overlay."
            );

            api.AddNumberOption(
                mod.ModManifest,
                getValue: () => config.OverlayTopN,
                setValue: value => config.OverlayTopN = value,
                name: () => "Overlay top N",
                tooltip: () => "How many mods to show in the runtime overlay.",
                min: 1,
                max: 30,
                interval: 1
            );

            api.AddNumberOption(
                mod.ModManifest,
                getValue: () => config.OverlaySampleWindow,
                setValue: value => config.OverlaySampleWindow = value,
                name: () => "Overlay sample window",
                tooltip: () => "Rolling average window in frames for runtime profiling.",
                min: 10,
                max: 300,
                interval: 10
            );
        }
        catch (Exception ex)
        {
            mod.Monitor.Log($"Failed to register GMCM options. Technical details:\n{ex}", LogLevel.Warn);
        }
    }

    private static void Copy(ModConfig source, ModConfig destination)
    {
        destination.ThresholdMs = source.ThresholdMs;
        destination.ShowAllMods = source.ShowAllMods;
        destination.LogOnStartup = source.LogOnStartup;
        destination.OverlayKey = source.OverlayKey;
        destination.OverlayTopN = source.OverlayTopN;
        destination.OverlaySampleWindow = source.OverlaySampleWindow;
    }
}
