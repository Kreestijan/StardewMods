using System.Security.Cryptography;
using Newtonsoft.Json;

namespace CutsceneMaker.Models;

public sealed class CutsceneData
{
    private const string UniqueIdPrefix = "Kree_CM_";
    private const string UniqueIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int UniqueIdSuffixLength = 8;

    public string CutsceneName { get; set; } = string.Empty;

    public string UniqueId { get; set; } = GenerateUniqueId();

    public string LocationName { get; set; } = "Town";

    public string MusicTrack { get; set; } = "none";

    public int ViewportStartX { get; set; } = -100;

    public int ViewportStartY { get; set; } = -100;

    public bool Skippable { get; set; } = true;

    public bool IncludeFarmer { get; set; } = true;

    public NpcPlacement FarmerPlacement { get; set; } = NpcPlacement.CreateFarmerDefault();

    public List<NpcPlacement> Actors { get; set; } = new();

    public List<object> Commands { get; set; } = new()
    {
        new EventCommandBlock
        {
            ProviderModId = "StardewValley",
            ProviderName = "Vanilla",
            CommandId = "vanilla.end",
            DisplayName = "End",
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mode"] = string.Empty
            }
        }
    };

    public List<EventPreconditionBlock> Triggers { get; set; } = new();

    /// <summary>True if this cutscene was imported with unresolved CP tokens that could not be expanded.</summary>
    [JsonIgnore]
    public bool HasUnresolvedTokens { get; set; }

    /// <summary>The raw event key text when the key itself contains unresolved tokens.</summary>
    [JsonIgnore]
    public string? RawEventKey { get; set; }

    [JsonIgnore]
    public ImportedContentPackContext? ImportContext { get; set; }

    public static CutsceneData CreateBlank()
    {
        return new CutsceneData();
    }

    private static string GenerateUniqueId()
    {
        Span<byte> bytes = stackalloc byte[UniqueIdSuffixLength];
        RandomNumberGenerator.Fill(bytes);

        Span<char> suffix = stackalloc char[UniqueIdSuffixLength];
        for (int i = 0; i < bytes.Length; i++)
        {
            suffix[i] = UniqueIdAlphabet[bytes[i] % UniqueIdAlphabet.Length];
        }

        return UniqueIdPrefix + new string(suffix);
    }
}
