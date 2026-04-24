using Microsoft.Xna.Framework.Graphics;

namespace DSS;

internal sealed class ProxyTexture : Texture2D
{
    public ProxyTexture(Texture2D scaledTexture, int logicalWidth, int logicalHeight)
        : base(scaledTexture.GraphicsDevice, logicalWidth, logicalHeight)
    {
        this.ScaledTexture = scaledTexture;
        this.Name = scaledTexture.Name;
    }

    public Texture2D ScaledTexture { get; }
}
