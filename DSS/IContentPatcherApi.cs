using StardewModdingAPI;

namespace DSS;

public interface IContentPatcherApi
{
    void RegisterToken(IManifest mod, string name, object token);
}
