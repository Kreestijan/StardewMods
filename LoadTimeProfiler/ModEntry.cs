using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace LoadTimeProfiler;

public sealed class ModEntry : Mod
{
    private ModConfig config = null!;
    private Harmony harmony = null!;
    private LoadProfiler loadProfiler = null!;
    private RuntimeProfiler runtimeProfiler = null!;
    private Overlay overlay = null!;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();
        this.config.Clamp();
        helper.WriteConfig(this.config);

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.loadProfiler = new LoadProfiler(this, this.config);
        this.runtimeProfiler = new RuntimeProfiler(this, () => this.config);
        this.overlay = new Overlay(() => this.config, this.runtimeProfiler);

        this.runtimeProfiler.Enable(this.harmony);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.Display.RenderedHud += this.OnRenderedHud;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        ConfigRegistrar.RegisterGmcm(this, this.config);
        this.loadProfiler.LogResults();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (this.runtimeProfiler.IsAvailable)
        {
            this.runtimeProfiler.AdvanceUpdateFrame();
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.overlay.Close();
        this.runtimeProfiler.Clear();
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        this.overlay.Draw(e.SpriteBatch);

        if (this.runtimeProfiler.IsAvailable)
        {
            this.runtimeProfiler.AdvanceDrawFrame();
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == this.config.OverlayKey)
        {
            this.overlay.Toggle();
            return;
        }

        if (!this.overlay.IsOpen)
        {
            return;
        }

        if (e.Button == SButton.Escape)
        {
            this.overlay.Close();
            return;
        }

        if (e.Button == SButton.Tab)
        {
            this.overlay.SwitchTab();
            return;
        }

        if (e.Button == SButton.MouseLeft)
        {
            int mouseX = Game1.getMouseX(ui_scale: true);
            int mouseY = Game1.getMouseY(ui_scale: true);
            this.overlay.HandleLeftClick(mouseX, mouseY);
        }
    }
}
