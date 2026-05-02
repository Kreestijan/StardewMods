using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

public abstract class EditorPanel
{
    protected EditorPanel(string title)
    {
        this.Title = title;
    }

    public Rectangle Bounds { get; private set; }

    protected string Title { get; }

    public virtual void SetBounds(Rectangle bounds)
    {
        this.Bounds = bounds;
    }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        IClickableMenu.drawTextureBox(
            spriteBatch,
            this.Bounds.X,
            this.Bounds.Y,
            this.Bounds.Width,
            this.Bounds.Height,
            Color.White
        );

        Utility.drawTextWithShadow(
            spriteBatch,
            this.Title,
            Game1.smallFont,
            new Vector2(this.Bounds.X + 20, this.Bounds.Y + 16),
            Game1.textColor
        );
    }

    public virtual void ReceiveLeftClick(int x, int y)
    {
    }

    public virtual void LeftClickHeld(int x, int y)
    {
    }

    public virtual void ReleaseLeftClick(int x, int y)
    {
    }

    public virtual void ReceiveRightClick(int x, int y)
    {
    }

    public virtual void ReceiveScrollWheelAction(int direction)
    {
    }

    public virtual void Update()
    {
    }

    public virtual void ReceiveKeyPress(Keys key)
    {
    }
}
