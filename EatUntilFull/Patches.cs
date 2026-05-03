using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace EatUntilFull;

[HarmonyPatch]
internal static class Patches
{
    private static Object? _pendingFood;

    /// <summary>
    /// Intercept food interaction from the active slot or world.
    /// Replace Yes/No with a 3-option dialogue (EatUntilFull / Yes / No).
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Object), nameof(Object.checkForAction))]
    private static bool Object_checkForAction_Prefix(Object __instance, Farmer who, bool justCheckingForActivity)
    {
        if (!ModEntry.Instance.Config.EnableMod || justCheckingForActivity)
            return true;

        if (__instance.Edibility <= 0 || __instance.Stack < 2)
            return true;

        _pendingFood = __instance;
        ShowEatDialogue(who, __instance);
        return false;
    }

    /// <summary>
    /// Adds "Eat until full" to any &quot;Eat&quot;-keyed question dialogue.
    /// Covers food eaten from the inventory menu.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.createQuestionDialogue), typeof(string), typeof(Response[]), typeof(string))]
    private static void CreateQuestionDialogue_Prefix(string question, ref Response[] answerChoices, string dialogKey)
    {
        if (!ModEntry.Instance.Config.EnableMod || dialogKey != "Eat")
            return;

        if (_pendingFood is null)
            _pendingFood = DetectFoodFromGameState();

        if (_pendingFood is null || _pendingFood.Edibility <= 0 || _pendingFood.Stack < 2)
            return;

        var list = answerChoices.ToList();
        list.Insert(list.Count - 1, new Response("EatUntilFull", ModEntry.Instance.Translate("dialogue.eat-until-full")));
        answerChoices = list.ToArray();
    }

    /// <summary>
    /// Handle the &quot;EatUntilFull&quot; response.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.answerDialogue))]
    private static bool GameLocation_answerDialogue_Prefix(Response response)
    {
        if (response.responseKey != "EatUntilFull")
            return true;

        HandleEatUntilFull();
        return false;
    }

    private static Object? DetectFoodFromGameState()
    {
        // If the active item is edible, use it
        if (Game1.player?.ActiveItem is Object obj && obj.Edibility > 0)
            return obj;

        // Check the inventory-page hovered item
        if (Game1.activeClickableMenu is GameMenu menu && menu.currentTab == 0)
        {
            var invPage = menu.pages[0] as InventoryPage;
            if (invPage?.hoveredItem is Object hovered && hovered.Edibility > 0)
                return hovered;
        }

        return null;
    }

    private static void ShowEatDialogue(Farmer who, Object food)
    {
        string question;
        try { question = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12962", food.DisplayName); }
        catch { question = $"Eat {food.DisplayName}?"; }

        string yesText, noText;
        try { yesText = Game1.content.LoadString("Strings\\Lexicon:QuestionYes"); }
        catch { yesText = "Yes"; }
        try { noText = Game1.content.LoadString("Strings\\Lexicon:QuestionNo"); }
        catch { noText = "No"; }

        who.currentLocation?.createQuestionDialogue(
            question,
            [
                new("EatUntilFull", ModEntry.Instance.Translate("dialogue.eat-until-full")),
                new("Yes", yesText),
                new("No", noText)
            ],
            "Eat"
        );
    }

    private static void HandleEatUntilFull()
    {
        if (_pendingFood is null || _pendingFood.Edibility <= 0)
        {
            ShowTransientMessage("Nothing to eat.");
            return;
        }

        Object food = _pendingFood;
        _pendingFood = null;

        // SDV recovery formulas (mirrors Object.staminaRecovered / healthRecovered)
        int staminaPerItem = Math.Max(1, (int)(food.Edibility * 2.5f + 30f));
        int healthPerItem = Math.Max(1, staminaPerItem / 2);
        int available = food.Stack;

        if (available <= 0)
            return;

        var config = ModEntry.Instance.Config;
        int needed;

        if (config.FillTarget == FillTarget.Energy)
        {
            float deficit = Game1.player.maxStamina.Value - Game1.player.stamina;
            if (deficit <= 0f)
            {
                ShowTransientMessage(ModEntry.Instance.Translate("message.already-full"));
                return;
            }
            needed = (int)Math.Ceiling(deficit / staminaPerItem);
        }
        else
        {
            float deficit = Game1.player.maxHealth - Game1.player.health;
            if (deficit <= 0f)
            {
                ShowTransientMessage(ModEntry.Instance.Translate("message.already-full"));
                return;
            }
            needed = (int)Math.Ceiling(deficit / healthPerItem);
        }

        if (needed <= 0)
            return;

        int toEat = Math.Min(needed, available);

        // Silently consume all but the last item (just stats, no animation)
        for (int i = 1; i < toEat; i++)
        {
            Game1.player.stamina = Math.Min(
                Game1.player.maxStamina.Value,
                Game1.player.stamina + staminaPerItem
            );
            Game1.player.health = Math.Min(
                Game1.player.maxHealth,
                Game1.player.health + healthPerItem
            );
            food.Stack--;
        }

        // Eat the last one with full animation, sound, and inventory removal
        Game1.player.eatObject(food);

        Game1.addHUDMessage(new HUDMessage(
            string.Format(ModEntry.Instance.Translate("message.eaten-count"), toEat, food.DisplayName),
            HUDMessage.achievement_type
        ));
    }

    private static void ShowTransientMessage(string message)
    {
        Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
    }
}
