using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace SpouseWarp;

public sealed class ModEntry : Mod
{
    private ConfigManager configManager = null!;
    private DecorationCatalog decorationCatalog = null!;
    private SpouseWarpWidget widget = null!;
    private WarpValidator validator = null!;
    private LocationAccessGuard locationAccessGuard = null!;
    private WarpService warpService = null!;
    private DateTimeOffset? lastWarpAt;

    internal static ModEntry Instance { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        this.configManager = new ConfigManager(helper, this.ModManifest, this.Monitor);
        this.decorationCatalog = new DecorationCatalog(helper);
        this.decorationCatalog.Reload();
        WidgetAssetCatalog widgetAssets = new WidgetAssetCatalog(helper);
        this.widget = new SpouseWarpWidget(this.decorationCatalog, new WarpTargetResolver(), this.configManager, widgetAssets.LoadSosButtonTexture());
        this.validator = new WarpValidator();
        this.locationAccessGuard = new LocationAccessGuard();
        this.warpService = new WarpService();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not GameMenu gameMenu
            || gameMenu.currentTab != GameMenu.inventoryTab
            || gameMenu.pages[gameMenu.currentTab] is not InventoryPage page)
        {
            return;
        }

        this.widget.DrawRows(page, e.SpriteBatch);
        this.DrawVanillaInventoryTooltip(page, e.SpriteBatch);
        this.widget.DrawTooltip(page, e.SpriteBatch);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.RefreshNpcConfig(registerGmcm: true);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.RefreshNpcConfig(registerGmcm: true);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is null)
        {
            return;
        }

        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);

        if (e.Button == SButton.MouseRight)
        {
            if ((this.Helper.Input.IsDown(SButton.LeftControl) || this.Helper.Input.IsDown(SButton.RightControl))
                && this.widget.TryCycleDecoration(mouseX, mouseY))
            {
                this.Helper.Input.Suppress(e.Button);
            }

            return;
        }

        if (e.Button != SButton.MouseLeft)
        {
            return;
        }

        if (this.widget.IsSosButtonClicked(mouseX, mouseY))
        {
            if (this.warpService.TryWarpHome(out string? homeWarpError))
            {
                Game1.playSound("wand");
            }
            else if (!string.IsNullOrWhiteSpace(homeWarpError))
            {
                this.ShowHudMessage(homeWarpError);
            }

            this.Helper.Input.Suppress(e.Button);
            return;
        }

        if (!this.widget.TryGetClickedPortrait(mouseX, mouseY, out WarpTarget? target) || target is null)
        {
            return;
        }

        if (!target.IsSelectable)
        {
            this.ShowHudMessage(this.GetDisabledTargetMessage(target));
            this.Helper.Input.Suppress(e.Button);
            return;
        }

        WarpValidationResult initiatorResult = this.validator.ValidateInitiator(this.configManager.Config, DateTimeOffset.UtcNow, this.lastWarpAt);
        if (!initiatorResult.Success)
        {
            this.ShowHudMessage(initiatorResult.Message!);
            this.Helper.Input.Suppress(e.Button);
            return;
        }

        WarpValidationResult targetResult = this.validator.ValidateTarget(target);
        if (!targetResult.Success)
        {
            this.ShowHudMessage(targetResult.Message!);
            this.Helper.Input.Suppress(e.Button);
            return;
        }

        WarpValidationResult locationAccessResult = this.locationAccessGuard.ValidateTarget(target, this.configManager.Config);
        if (!locationAccessResult.Success)
        {
            this.ShowHudMessage(locationAccessResult.Message!);
            this.Helper.Input.Suppress(e.Button);
            return;
        }

        if (this.warpService.TryWarpToTarget(target, this.configManager.Config.WarpCostGold, out string? warpError))
        {
            this.lastWarpAt = DateTimeOffset.UtcNow;
            Game1.playSound("wand");
        }
        else if (!string.IsNullOrWhiteSpace(warpError))
        {
            this.ShowHudMessage(warpError);
        }

        this.Helper.Input.Suppress(e.Button);
    }

    private void RefreshNpcConfig(bool registerGmcm)
    {
        this.configManager.EnsureNpcEntries();
        if (registerGmcm)
        {
            this.configManager.RegisterGenericModConfigMenu();
        }
    }

    private void ShowHudMessage(string message)
    {
        Game1.addHUDMessage(new HUDMessage(message));
    }

    private void DrawVanillaInventoryTooltip(InventoryPage page, SpriteBatch spriteBatch)
    {
        if (string.IsNullOrEmpty(page.hoverText))
        {
            return;
        }

        if (page.hoverAmount > 0)
        {
            IClickableMenu.drawToolTip(spriteBatch, page.hoverText, page.hoverTitle, null, heldItem: true, -1, 0, null, -1, null, page.hoverAmount);
            return;
        }

        IClickableMenu.drawToolTip(spriteBatch, page.hoverText, page.hoverTitle, page.hoveredItem);
    }

    private string GetDisabledTargetMessage(WarpTarget target)
    {
        if (target.Kind == WarpTargetKind.Player && !target.IsOnline)
        {
            return $"{target.DisplayName} is offline right now.";
        }

        if (target.Kind == WarpTargetKind.Npc && this.configManager.Config.RequiresMarriage)
        {
            return $"You can only warp to your spouse while 'Restrict warp to spouse only' is enabled.";
        }

        return $"{target.DisplayName} can't be warped to right now.";
    }
}
