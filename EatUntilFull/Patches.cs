using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace EatUntilFull;

[HarmonyPatch]
internal static class Patches
{
    /// <summary>
    /// Replaces the Yes/No eat confirmation with a 3-option dialogue
    /// ("Eat until full", "Yes", "No") so the player can choose to eat
    /// multiple items at once.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Object), nameof(Object.checkForAction))]
    private static bool Object_checkForAction_Prefix(Object __instance, Farmer who, bool justCheckingForActivity)
    {
        if (!ModEntry.Instance.Config.EnableMod)
            return true;

        // Only intercept food items that the player can eat
        if (__instance.Edibility <= 0 || who?.canEat() != true || who.isRidingHorse())
            return true;

        if (justCheckingForActivity)
            return true;

        who.lastClickableObject = __instance;

        string question;
        try
        {
            question = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12962", __instance.DisplayName);
        }
        catch
        {
            question = $"Eat {__instance.DisplayName}?";
        }

        string yesText;
        string noText;
        try
        {
            yesText = Game1.content.LoadString("Strings\\Lexicon:QuestionYes");
            noText = Game1.content.LoadString("Strings\\Lexicon:QuestionNo");
        }
        catch
        {
            yesText = "Yes";
            noText = "No";
        }

        who.currentLocation?.createQuestionDialogue(
            question,
            new Response[]
            {
                new("EatUntilFull", ModEntry.Instance.Translate("dialogue.eat-until-full")),
                new("Yes", yesText),
                new("No", noText)
            },
            "Eat"
        );

        return false;
    }

    /// <summary>
    /// Handles the "Eat until full" response by eating enough of the
    /// selected food item to fill the player's energy or health bar.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.answerDialogue))]
    private static bool GameLocation_answerDialogue_Prefix(Response response)
    {
        if (response.responseKey != "EatUntilFull")
            return true;

        GameLocation? location = Game1.currentLocation;
        if (location is null || location.currentQuestionKeys.Count <= 0)
            return true;

        // Peek to verify this is an "Eat" dialogue, but only pop when we handle it
        if (location.currentQuestionKeys.Peek() != "Eat")
            return true;

        ModConfig config = ModEntry.Instance.Config;
        if (!config.EnableMod)
            return false;

        // Pop the question key — we're handling this response
        location.currentQuestionKeys.Pop();

        if (Game1.player.lastClickableObject is not Object food || food.Edibility <= 0)
            return false;

        // Guard: item gives neither energy nor health
        bool givesStamina = food.staminaRecovered > 0;
        bool givesHealth = food.healthRecovered > 0;

        if (!givesStamina && !givesHealth)
        {
            ShowTransientMessage(ModEntry.Instance.Translate("message.no-benefit"));
            return false;
        }

        // Only the stack that was right-clicked is eligible — don't pull from other slots
        int available = food.Stack;

        if (available <= 0)
            return false;

        // Calculate how many items to eat
        bool fillStamina = config.FillTarget == FillTarget.Energy;
        int needed;

        if (fillStamina)
        {
            if (!givesStamina)
            {
                ShowTransientMessage(ModEntry.Instance.Translate("message.no-energy"));
                return false;
            }

            float deficit = Game1.player.maxStamina - Game1.player.stamina;
            needed = (int)Math.Ceiling(deficit / food.staminaRecovered);
        }
        else
        {
            if (!givesHealth)
            {
                ShowTransientMessage(ModEntry.Instance.Translate("message.no-health"));
                return false;
            }

            int deficit = Game1.player.maxHealth - Game1.player.health;
            needed = (int)Math.Ceiling((double)deficit / food.healthRecovered);
        }

        if (needed <= 0)
        {
            ShowTransientMessage(ModEntry.Instance.Translate("message.already-full"));
            return false;
        }

        int toEat = Math.Min(needed, available);

        // Apply cumulative stats
        Game1.player.stamina = Math.Min(
            Game1.player.maxStamina,
            Game1.player.stamina + food.staminaRecovered * toEat
        );
        Game1.player.health = Math.Min(
            Game1.player.maxHealth,
            Game1.player.health + food.healthRecovered * toEat
        );

        // Remove consumed items from inventory
        RemoveItemsFromInventory(food, toEat);

        // Single eat animation and sound
        Game1.player.ShowItemEatAnimation();
        Game1.playSound("eat");

        // Show summary message
        string summary = string.Format(
            ModEntry.Instance.Translate("message.eaten-count"),
            toEat,
            food.DisplayName
        );
        Game1.addHUDMessage(new HUDMessage(summary, HUDMessage.achievement_type));

        return false;
    }

    private static void RemoveItemsFromInventory(Object food, int count)
    {
        food.Stack -= count;
        if (food.Stack <= 0)
        {
            Game1.player.removeItemFromInventory(food);
        }
    }

    /// <summary>
    /// Shows a brief top-left HUD message instead of a blocking dialogue box,
    /// so the player isn't interrupted mid-workflow.
    /// </summary>
    private static void ShowTransientMessage(string message)
    {
        Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
    }
}
