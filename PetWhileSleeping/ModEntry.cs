using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PetWhileSleeping;

public sealed class ModEntry : Mod
{
    private const string GenericModConfigMenuId = "spacechase0.GenericModConfigMenu";

    internal static ModEntry Instance { get; private set; } = null!;

    private ModConfig config = new();
    private Harmony? harmony;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.config = helper.ReadConfig<ModConfig>();
        this.ClampConfig();

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.harmony.PatchAll();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    internal static int GetTimeOfDayForPetSleepCheck()
    {
        return Instance.config.EnableMod ? 0 : Game1.timeOfDay;
    }

    internal static SleepingPetContext? BeginSleepingPet(FarmAnimal animal, Farmer who, bool isAutoPet)
    {
        if (!Instance.config.EnableMod || isAutoPet || !IsSleepingPetAttempt(animal))
        {
            return null;
        }

        return new SleepingPetContext(
            WasPetBefore: animal.wasPet.Value,
            FriendshipBefore: animal.friendshipTowardFarmer.Value
        );
    }

    internal static void ApplySleepingFriendshipPenalty(FarmAnimal animal, SleepingPetContext? context)
    {
        int penaltyPercent = Instance.config.SleepingFriendshipPenaltyPercent;
        if (context is null || penaltyPercent <= 0 || context.WasPetBefore || !animal.wasPet.Value)
        {
            return;
        }

        int friendshipGain = animal.friendshipTowardFarmer.Value - context.FriendshipBefore;
        if (friendshipGain <= 0)
        {
            return;
        }

        int penalty = friendshipGain * penaltyPercent / 100;
        animal.friendshipTowardFarmer.Value = Math.Max(0, animal.friendshipTowardFarmer.Value - penalty);
    }

    private static bool IsSleepingPetAttempt(FarmAnimal animal)
    {
        return Game1.timeOfDay >= 1900 && !animal.isMoving();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GenericModConfigMenuId);
        if (gmcm is null)
        {
            return;
        }

        gmcm.Register(
            this.ModManifest,
            reset: this.ResetConfig,
            save: this.SaveConfig,
            titleScreenOnly: false
        );

        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.EnableMod,
            setValue: value => this.config.EnableMod = value,
            name: () => "Enable mod",
            tooltip: () => "If enabled, you can still pet sleeping farm animals and gain friendship at night."
        );

        gmcm.AddNumberOption(
            this.ModManifest,
            getValue: () => this.config.SleepingFriendshipPenaltyPercent,
            setValue: value => this.config.SleepingFriendshipPenaltyPercent = Math.Clamp(value, 0, 100),
            name: () => "Sleeping friendship penalty",
            tooltip: () => "Percent of vanilla friendship gain removed when petting an animal while it is sleeping.",
            min: 0,
            max: 100,
            interval: 5,
            formatValue: value => $"{value}%"
        );
    }

    private void ResetConfig()
    {
        this.config = new ModConfig();
    }

    private void SaveConfig()
    {
        this.ClampConfig();
        this.Helper.WriteConfig(this.config);
    }

    private void ClampConfig()
    {
        this.config.SleepingFriendshipPenaltyPercent = Math.Clamp(this.config.SleepingFriendshipPenaltyPercent, 0, 100);
    }
}

internal sealed record SleepingPetContext(bool WasPetBefore, int FriendshipBefore);
