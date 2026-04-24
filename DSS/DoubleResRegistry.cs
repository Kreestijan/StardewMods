using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace DSS;

internal sealed class DoubleResRegistry
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly Dictionary<string, DoubleResAssetDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProxyTexture> proxyTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ignoredAssetNames = new(StringComparer.OrdinalIgnoreCase);

    public DoubleResRegistry(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public void Reload()
    {
        foreach (ProxyTexture proxyTexture in this.proxyTextures.Values)
        {
            proxyTexture.Dispose();
        }

        this.definitions.Clear();
        this.proxyTextures.Clear();
        this.ignoredAssetNames.Clear();

        Dictionary<string, List<DoubleResAssetDefinition>>? data = this.helper.GameContent.Load<Dictionary<string, List<DoubleResAssetDefinition>>>(ModEntry.AssetMapName);
        if (data is null)
        {
            return;
        }

        foreach ((string entryKey, List<DoubleResAssetDefinition> value) in data)
        {
            foreach (DoubleResAssetDefinition definition in value)
            {
                int scale = Math.Max(1, definition.Scale);
                if (scale <= 1)
                {
                    this.monitor.Log($"{entryKey} uses a scale of {definition.Scale}, so it was ignored.", LogLevel.Warn);
                    continue;
                }

                bool foundAsset = false;
                foreach (string assetName in definition.ExpandAssetNames())
                {
                    foundAsset = true;
                    this.definitions[assetName] = definition;
                }

                if (!foundAsset)
                {
                    this.monitor.Log($"{entryKey} does not define Asset or Assets, so it was ignored.", LogLevel.Warn);
                }
            }
        }
    }

    public bool TryGetDefinition(Texture2D texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture)
    {
        scaledTexture = texture is ProxyTexture proxyTexture
            ? proxyTexture.ScaledTexture
            : texture;

        string? assetName = scaledTexture.Name;
        if (string.IsNullOrWhiteSpace(assetName) || this.ignoredAssetNames.Contains(assetName))
        {
            definition = null;
            return false;
        }

        if (this.definitions.TryGetValue(assetName, out definition))
        {
            return true;
        }

        this.ignoredAssetNames.Add(assetName);
        definition = null;
        return false;
    }

    public bool TryGetProxyTexture(Texture2D texture, out Texture2D proxyTexture)
    {
        proxyTexture = texture;
        if (!this.TryGetDefinition(texture, out DoubleResAssetDefinition? definition, out Texture2D scaledTexture) || definition is null)
        {
            return false;
        }

        int scale = Math.Max(1, definition.Scale);
        if (scale <= 1 || scaledTexture.Width % scale != 0 || scaledTexture.Height % scale != 0)
        {
            return false;
        }

        string key = $"{scaledTexture.Name}|{scaledTexture.Width}|{scaledTexture.Height}|{scale}";
        if (!this.proxyTextures.TryGetValue(key, out ProxyTexture? cached))
        {
            cached = new ProxyTexture(scaledTexture, scaledTexture.Width / scale, scaledTexture.Height / scale);
            this.proxyTextures[key] = cached;
        }

        proxyTexture = cached;
        return true;
    }

    public Rectangle ScaleSourceRect(DoubleResAssetDefinition definition, Rectangle sourceRect)
    {
        int scale = Math.Max(1, definition.Scale);
        return new Rectangle(sourceRect.X * scale, sourceRect.Y * scale, sourceRect.Width * scale, sourceRect.Height * scale);
    }

    public Vector2 ScaleOrigin(DoubleResAssetDefinition definition, Vector2 origin)
    {
        return origin * Math.Max(1, definition.Scale);
    }

    public Vector2 ScaleDrawScale(DoubleResAssetDefinition definition, Vector2 scale)
    {
        int factor = Math.Max(1, definition.Scale);
        return new Vector2(scale.X / factor, scale.Y / factor);
    }
}
