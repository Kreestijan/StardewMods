using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace DSS;

public sealed class ModEntry : Mod
{
    internal const string AssetMapName = "Kree.DSS/Assets";

    private DoubleResRegistry registry = null!;

    public override void Entry(IModHelper helper)
    {
        this.registry = new DoubleResRegistry(helper, this.Monitor);
        HarmonyPatches.Initialize(this.registry);
        HarmonyPatches.PatchAll(this.ModManifest.UniqueID);

        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.Content.AssetReady += this.OnAssetReady;
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Helper.ModRegistry.GetApi<IContentPatcherApi>("Pathoschild.ContentPatcher")
            ?.RegisterToken(this.ModManifest, "Assets", new AssetsToken());
        this.registry.Reload();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.registry.Reload();
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(AssetMapName))
        {
            e.LoadFrom(() => new Dictionary<string, List<DoubleResAssetDefinition>>(), AssetLoadPriority.Exclusive);
        }
    }

    private void OnAssetReady(object? sender, AssetReadyEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(AssetMapName))
        {
            this.registry.Reload();
        }
    }

    private sealed class AssetsToken
    {
        public bool IsMutable()
        {
            return false;
        }

        public IEnumerable<string> GetValues(string input)
        {
            return new[] { AssetMapName };
        }
    }
}
