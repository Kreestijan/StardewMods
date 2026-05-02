using CutsceneMaker.Editor;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker;

internal sealed class TitleMenuButtonController
{
    private const int ButtonWidth = 260;
    private const int ButtonHeight = 64;
    private const int ScreenPadding = 32;
    private readonly IModHelper helper;
    private Rectangle buttonBounds;

    public TitleMenuButtonController(IModHelper helper)
    {
        this.helper = helper;
        helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Display.WindowResized += this.OnWindowResized;
        this.RecalculateBounds();
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not TitleMenu)
        {
            return;
        }

        this.RecalculateBounds();
        IClickableMenu.drawTextureBox(
            e.SpriteBatch,
            this.buttonBounds.X,
            this.buttonBounds.Y,
            this.buttonBounds.Width,
            this.buttonBounds.Height,
            Color.White
        );

        const string label = "Cutscene Maker";
        Vector2 size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(
            e.SpriteBatch,
            label,
            Game1.smallFont,
            new Vector2(
                this.buttonBounds.Center.X - size.X / 2f,
                this.buttonBounds.Center.Y - size.Y / 2f
            ),
            Game1.textColor
        );
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft || Game1.activeClickableMenu is not TitleMenu)
        {
            return;
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (!this.buttonBounds.Contains(cursor))
        {
            return;
        }

        this.helper.Input.Suppress(e.Button);
        Game1.playSound("bigSelect");
        Game1.activeClickableMenu = new CutsceneEditorMenu();
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        this.RecalculateBounds();
    }

    private void RecalculateBounds()
    {
        int x = Game1.uiViewport.Width - ButtonWidth - ScreenPadding;
        int y = Game1.uiViewport.Height - ButtonHeight - ScreenPadding;
        this.buttonBounds = new Rectangle(Math.Max(ScreenPadding, x), Math.Max(ScreenPadding, y), ButtonWidth, ButtonHeight);
    }
}
