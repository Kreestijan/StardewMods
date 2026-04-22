using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

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

        this.harmony = new Harmony(this.ModManifest.UniqueID);
        this.harmony.PatchAll();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    internal static bool ShouldHandleSleepingPet(FarmAnimal animal, Farmer who, bool isAutoPet)
    {
        if (!Instance.config.EnableMod || isAutoPet)
        {
            return false;
        }

        return Game1.timeOfDay >= 1900 && !animal.isMoving();
    }

    internal static void PetSleepingAnimal(FarmAnimal animal, Farmer who)
    {
        if (who.FarmerSprite.PauseForSingleAnimation)
        {
            return;
        }

        who.Halt();
        who.faceGeneralDirection(animal.Position, 0, opposite: false, useTileCalculations: false);

        animal.Halt();
        animal.Sprite.StopAnimation();
        animal.uniqueFrameAccumulator = -1;

        switch (Game1.player.FacingDirection)
        {
            case 0:
                animal.Sprite.currentFrame = 0;
                break;
            case 1:
                animal.Sprite.currentFrame = 12;
                break;
            case 2:
                animal.Sprite.currentFrame = 8;
                break;
            case 3:
                animal.Sprite.currentFrame = 4;
                break;
        }

        if (!animal.hasEatenAnimalCracker.Value && who.ActiveObject?.QualifiedItemId == "(O)GoldenAnimalCracker")
        {
            if ((!(animal.GetAnimalData()?.CanEatGoldenCrackers)) ?? false)
            {
                Game1.playSound("cancel");
                animal.doEmote(8);
                return;
            }

            animal.hasEatenAnimalCracker.Value = true;
            Game1.playSound("give_gift");
            animal.doEmote(56);
            Game1.player.reduceActiveItemByOne();
            return;
        }

        if (!animal.wasPet.Value)
        {
            bool shouldShowMessage = Instance.config.ShowSleepingPetMessage
                && who.ActiveObject?.QualifiedItemId != "(O)GoldenAnimalCracker";

            animal.wasPet.Value = true;

            int friendshipGainIfAutoPettedFirst = 7;
            if (animal.wasAutoPet.Value)
            {
                animal.friendshipTowardFarmer.Value = Math.Min(1000, animal.friendshipTowardFarmer.Value + friendshipGainIfAutoPettedFirst);
            }
            else
            {
                animal.friendshipTowardFarmer.Value = Math.Min(1000, animal.friendshipTowardFarmer.Value + 15);
            }

            var animalData = animal.GetAnimalData();
            int happinessGain = Math.Max(5, 30 + (animalData?.HappinessDrain ?? 0));

            if (animalData != null && animalData.ProfessionForHappinessBoost >= 0 && who.professions.Contains(animalData.ProfessionForHappinessBoost))
            {
                animal.friendshipTowardFarmer.Value = Math.Min(1000, animal.friendshipTowardFarmer.Value + 15);
                animal.happiness.Value = Math.Min(255, animal.happiness.Value + happinessGain);
            }

            int emote = animal.wasAutoPet.Value ? 32 : 20;
            animal.doEmote(animal.moodMessage.Value == 4 ? 12 : emote);
            animal.happiness.Value = Math.Min(255, animal.happiness.Value + happinessGain);
            animal.makeSound();
            who.gainExperience(0, 5);

            if (shouldShowMessage)
            {
                Game1.drawObjectDialogue($"{animal.displayName} is sleeping, but you pet them gently.");
            }

            return;
        }

        if (who.ActiveObject?.QualifiedItemId != "(O)178")
        {
            Game1.activeClickableMenu = new AnimalQueryMenu(animal);
        }
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
        gmcm.AddBoolOption(
            this.ModManifest,
            getValue: () => this.config.ShowSleepingPetMessage,
            setValue: value => this.config.ShowSleepingPetMessage = value,
            name: () => "Show sleep message",
            tooltip: () => "If enabled, sleeping animals show a custom message when you pet them at night."
        );
    }

    private void ResetConfig()
    {
        this.config = new ModConfig();
    }

    private void SaveConfig()
    {
        this.Helper.WriteConfig(this.config);
    }
}
