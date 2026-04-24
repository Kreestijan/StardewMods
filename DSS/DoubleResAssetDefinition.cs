namespace DSS;

internal sealed class DoubleResAssetDefinition
{
    public string? Target { get; set; }

    public string? Asset { get; set; }

    public string? Assets { get; set; }

    public int Scale { get; set; } = 2;

    public IEnumerable<string> ExpandAssetNames()
    {
        if (!string.IsNullOrWhiteSpace(this.Asset))
        {
            yield return this.ApplyTarget(this.Asset);
        }

        if (string.IsNullOrWhiteSpace(this.Assets))
        {
            yield break;
        }

        foreach (string asset in this.Assets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return this.ApplyTarget(asset);
        }
    }

    private string ApplyTarget(string asset)
    {
        if (string.IsNullOrWhiteSpace(this.Target))
        {
            return asset;
        }

        return $"{this.Target.TrimEnd('/')}/{asset.TrimStart('/')}";
    }
}
