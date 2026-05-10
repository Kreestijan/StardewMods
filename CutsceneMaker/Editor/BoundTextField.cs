using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CutsceneMaker.Editor;

internal sealed class BoundTextField
{
    private readonly TextBox textBox;
    private readonly Func<string> getValue;
    private readonly Action<string> setValue;
    private string lastCommittedValue = string.Empty;

    public BoundTextField(Func<string> getValue, Action<string> setValue, bool numbersOnly = false, int textLimit = -1)
    {
        this.getValue = getValue;
        this.setValue = setValue;
        this.textBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
        {
            numbersOnly = numbersOnly,
            textLimit = textLimit,
            Width = 220,
            Height = 48
        };
    }

    public bool Selected
    {
        get => this.textBox.Selected;
        set => this.textBox.Selected = value;
    }

    public void SetBounds(Rectangle bounds)
    {
        this.textBox.X = bounds.X;
        this.textBox.Y = bounds.Y;
        this.textBox.Width = bounds.Width;
        this.textBox.Height = bounds.Height;
    }

    public bool Contains(int x, int y)
    {
        return new Rectangle(this.textBox.X, this.textBox.Y, this.textBox.Width, this.textBox.Height).Contains(x, y);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.textBox.Selected)
        {
            string value = this.getValue();
            if (!string.Equals(this.textBox.Text, value, StringComparison.Ordinal))
            {
                this.textBox.Text = value;
            }
        }

        this.textBox.Draw(spriteBatch);
    }

    public void ReceiveKeyPress(Keys key)
    {
        if (!this.textBox.Selected)
        {
            return;
        }

        if (key == Keys.Enter || key == Keys.Tab || key == Keys.Escape)
        {
            this.Commit();
            this.textBox.Selected = false;
            return;
        }

        this.textBox.RecieveSpecialInput(key);
    }

    public void Update()
    {
        if (this.textBox.Selected && !string.Equals(this.lastCommittedValue, this.textBox.Text, StringComparison.Ordinal))
        {
            this.Commit();
        }
    }

    public void Select()
    {
        this.textBox.Text = this.getValue();
        this.lastCommittedValue = this.textBox.Text;
        this.textBox.Selected = true;
    }

    public void CommitAndDeselect()
    {
        this.Commit();
        this.textBox.Selected = false;
    }

    private void Commit()
    {
        this.lastCommittedValue = this.textBox.Text;
        this.setValue(this.textBox.Text);
    }
}
