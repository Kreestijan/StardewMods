using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace SpouseWarp;

internal sealed class DecorationCatalog
{
    private const string NoneDecorationId = "none";
    private readonly IModHelper helper;
    private readonly string directoryPath;
    private readonly Dictionary<string, Texture2D> textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> ids = new();

    public DecorationCatalog(IModHelper helper)
    {
        this.helper = helper;
        this.directoryPath = Path.Combine(helper.DirectoryPath, "assets", "decorations");
    }

    public IReadOnlyList<string> Ids => this.ids;

    public void Reload()
    {
        this.textures.Clear();
        this.ids.Clear();

        Directory.CreateDirectory(this.directoryPath);

        foreach (string filePath in Directory.EnumerateFiles(this.directoryPath, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            string id = Path.GetFileNameWithoutExtension(filePath);
            string relativePath = Path.GetRelativePath(this.helper.DirectoryPath, filePath).Replace('\\', '/');

            this.textures[id] = this.helper.ModContent.Load<Texture2D>(relativePath);
            this.ids.Add(id);
        }

        this.ids.Add(NoneDecorationId);
    }

    public string NormalizeSelection(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NoneDecorationId;
        }

        string? existingId = this.ids.FirstOrDefault(existing => existing.Equals(id, StringComparison.OrdinalIgnoreCase));
        return existingId ?? NoneDecorationId;
    }

    public string GetNext(string? currentId)
    {
        if (this.ids.Count == 0)
        {
            return NoneDecorationId;
        }

        string normalized = this.NormalizeSelection(currentId);
        int index = this.ids.FindIndex(existing => existing.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return this.ids[0];
        }

        return this.ids[(index + 1) % this.ids.Count];
    }

    public Texture2D? GetTexture(string? id)
    {
        string normalized = this.NormalizeSelection(id);
        return this.textures.TryGetValue(normalized, out Texture2D? texture)
            ? texture
            : null;
    }
}
