using System;
using StardewModdingAPI;

namespace PassableFarmAnimals;

public interface IGenericModConfigMenuApi
{
    void Register(
        IManifest mod,
        Action reset,
        Action save,
        bool titleScreenOnly = true
    );

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string>? name = null,
        Func<string>? tooltip = null,
        string? fieldId = null
    );
}
