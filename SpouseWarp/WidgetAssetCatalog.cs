using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace SpouseWarp;

internal sealed class WidgetAssetCatalog
{
    private readonly IModHelper helper;

    public WidgetAssetCatalog(IModHelper helper)
    {
        this.helper = helper;
    }

    public Texture2D LoadSosButtonTexture()
    {
        return this.helper.ModContent.Load<Texture2D>("assets/sosbutton.png");
    }
}
