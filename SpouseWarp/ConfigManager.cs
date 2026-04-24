using StardewModdingAPI;
using StardewValley;

namespace SpouseWarp;

internal sealed class ConfigManager
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";
    private const string NpcSearchFieldId = "npc-search";
    private readonly IModHelper helper;
    private readonly IManifest manifest;
    private readonly IMonitor monitor;
    private ModConfig config;
    private string npcSearchText = "";

    public ConfigManager(IModHelper helper, IManifest manifest, IMonitor monitor)
    {
        this.helper = helper;
        this.manifest = manifest;
        this.monitor = monitor;
        this.config = helper.ReadConfig<ModConfig>();
        this.Sanitize();
    }

    public ModConfig Config => this.config;

    public void Save()
    {
        this.Sanitize();
        this.helper.WriteConfig(this.config);
    }

    public bool EnsureNpcEntries()
    {
        if (Game1.characterData is null)
        {
            return false;
        }

        bool changed = false;
        foreach (string npcName in Game1.characterData.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!this.config.ShowNPCs.ContainsKey(npcName))
            {
                this.config.ShowNPCs[npcName] = false;
                changed = true;
            }
        }

        if (changed)
        {
            this.helper.WriteConfig(this.config);
        }

        return changed;
    }

    public void RegisterGenericModConfigMenu()
    {
        IGenericModConfigMenuApi? gmcm = this.helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        gmcm.Unregister(this.manifest);
        gmcm.Register(
            this.manifest,
            reset: this.Reset,
            save: this.Save,
            titleScreenOnly: false
        );

        gmcm.AddBoolOption(
            this.manifest,
            getValue: () => this.config.RequiresMarriage,
            setValue: value => this.config.RequiresMarriage = value,
            name: () => "Restrict warp to spouse only",
            tooltip: () => "If enabled, only spouse targets stay warpable and other checked NPCs are shown greyed out."
        );
        gmcm.AddBoolOption(
            this.manifest,
            getValue: () => this.config.IgnoreLocationUnlocks,
            setValue: value => this.config.IgnoreLocationUnlocks = value,
            name: () => "Ignore locked areas",
            tooltip: () => "If enabled, warp targets are allowed even when Stardew reports their area as locked in this save."
        );
        gmcm.AddBoolOption(
            this.manifest,
            getValue: () => this.config.EnableSosButton,
            setValue: value => this.config.EnableSosButton = value,
            name: () => "S.O.S. button",
            tooltip: () => "Shows an emergency home-warp button above the widget."
        );
        gmcm.AddBoolOption(
            this.manifest,
            getValue: () => this.config.DebugHitboxes,
            setValue: value => this.config.DebugHitboxes = value,
            name: () => "Debug hitboxes",
            tooltip: () => "Draws the widget's active hitbox rectangles on top of the menu."
        );
        gmcm.AddNumberOption(
            this.manifest,
            getValue: () => this.config.WidgetScalePercent,
            setValue: value => this.config.WidgetScalePercent = value,
            name: () => "Widget scale",
            tooltip: () => "Scales the portrait and decoration icons together.",
            min: 50,
            max: 200,
            interval: 5
        );
        gmcm.AddNumberOption(
            this.manifest,
            getValue: () => this.config.WarpCostGold,
            setValue: value => this.config.WarpCostGold = value,
            name: () => "Warp cost",
            tooltip: () => "Gold spent every time a warp succeeds.",
            min: 0,
            max: 50000,
            interval: 50
        );
        gmcm.AddNumberOption(
            this.manifest,
            getValue: () => this.config.CooldownSeconds,
            setValue: value => this.config.CooldownSeconds = value,
            name: () => "Cooldown seconds",
            tooltip: () => "How long you must wait between successful warps.",
            min: 0,
            max: 20,
            interval: 1
        );
        gmcm.AddNumberOption(
            this.manifest,
            getValue: () => this.config.WidgetOffsetX,
            setValue: value => this.config.WidgetOffsetX = value,
            name: () => "Widget offset X",
            tooltip: () => "Moves the whole widget left or right.",
            min: -400,
            max: 400,
            interval: 4
        );
        gmcm.AddNumberOption(
            this.manifest,
            getValue: () => this.config.WidgetOffsetY,
            setValue: value => this.config.WidgetOffsetY = value,
            name: () => "Widget offset Y",
            tooltip: () => "Moves the whole widget up or down.",
            min: -400,
            max: 400,
            interval: 4
        );

        gmcm.AddSectionTitle(
            this.manifest,
            text: () => "NPC Targets",
            tooltip: () => "Enable any NPC you want to appear in the warp widget."
        );

        gmcm.AddTextOption(
            this.manifest,
            getValue: () => this.npcSearchText,
            setValue: value => this.npcSearchText = value?.Trim() ?? "",
            name: () => "Search NPCs",
            tooltip: () => "Filter the NPC checkbox list by name. Use Save after every search.",
            fieldId: NpcSearchFieldId
        );

        foreach (string npcName in this.GetFilteredNpcNames())
        {
            string capturedName = npcName;
            gmcm.AddBoolOption(
                this.manifest,
                getValue: () => this.config.ShowNPCs.TryGetValue(capturedName, out bool value) && value,
                setValue: value => this.config.ShowNPCs[capturedName] = value,
                name: () => capturedName,
                tooltip: () => $"Show {capturedName} as a warp target."
            );
        }

        gmcm.OnFieldChanged(this.manifest, this.OnFieldChanged);
    }

    private void Reset()
    {
        this.config = new ModConfig();
        this.npcSearchText = "";
        this.EnsureNpcEntries();
        this.Save();
    }

    private string[] GetFilteredNpcNames()
    {
        IEnumerable<string> names = this.config.ShowNPCs.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(this.npcSearchText))
        {
            names = names.Where(name => name.Contains(this.npcSearchText, StringComparison.OrdinalIgnoreCase));
        }

        return names.ToArray();
    }

    private void SetNpcSearchText(string value)
    {
        string normalized = value?.Trim() ?? "";
        if (string.Equals(this.npcSearchText, normalized, StringComparison.Ordinal))
        {
            return;
        }

        this.npcSearchText = normalized;
        this.RefreshOpenConfigMenu();
    }

    private void RefreshOpenConfigMenu()
    {
        IGenericModConfigMenuApi? gmcm = this.helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        this.RegisterGenericModConfigMenu();
        gmcm.OpenModMenu(this.manifest);
    }

    private void OnFieldChanged(string fieldId, object rawValue)
    {
        if (!string.Equals(fieldId, NpcSearchFieldId, StringComparison.Ordinal))
        {
            return;
        }

        this.SetNpcSearchText(rawValue as string ?? "");
    }

    private void Sanitize()
    {
        this.config.WidgetScalePercent = this.Clamp(this.config.WidgetScalePercent, 50, 200, nameof(this.config.WidgetScalePercent));
        this.config.WarpCostGold = this.Clamp(this.config.WarpCostGold, 0, 50000, nameof(this.config.WarpCostGold));
        this.config.CooldownSeconds = this.Clamp(this.config.CooldownSeconds, 0, 20, nameof(this.config.CooldownSeconds));
        this.config.ShowNPCs ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        this.config.Decorations ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private int Clamp(int value, int min, int max, string fieldName)
    {
        int clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            this.monitor.Log($"{fieldName} was set to {value}, so it was clamped to {clamped}.", LogLevel.Warn);
        }

        return clamped;
    }
}
