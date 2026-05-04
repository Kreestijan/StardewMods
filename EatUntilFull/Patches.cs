using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using Object = StardewValley.Object;

namespace EatUntilFull;

[HarmonyPatch]
internal static class Patches
{
    private const string EatUntilFullResponseKey = "EatUntilFull";

    private static Object? _pendingFood;
    private static bool _pendingFoodFromObjectAction;

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

        ClearPendingFood();

        if (__instance.Edibility <= 0 || __instance.Stack < 2)
            return true;

        _pendingFood = __instance;
        _pendingFoodFromObjectAction = true;
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

        if (!_pendingFoodFromObjectAction)
            _pendingFood = DetectFoodFromGameState();

        _pendingFoodFromObjectAction = false;

        if (!IsEligibleFood(_pendingFood))
        {
            ClearPendingFood();
            return;
        }

        var list = answerChoices.ToList();
        list.Insert(list.Count - 1, new Response(EatUntilFullResponseKey, ModEntry.Instance.Translate("dialogue.eat-until-full")));
        answerChoices = list.ToArray();
    }

    /// <summary>
    /// Handle the &quot;EatUntilFull&quot; response.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.answerDialogue))]
    private static bool GameLocation_answerDialogue_Prefix(Response answer)
    {
        if (answer.responseKey != EatUntilFullResponseKey)
            return true;

        HandleEatUntilFull();
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.answerDialogue))]
    private static void GameLocation_answerDialogue_Postfix(Response answer)
    {
        if (answer.responseKey != EatUntilFullResponseKey)
            ClearPendingFood();
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
                new(EatUntilFullResponseKey, ModEntry.Instance.Translate("dialogue.eat-until-full")),
                new("Yes", yesText),
                new("No", noText)
            ],
            "Eat"
        );
    }

    private static void HandleEatUntilFull()
    {
        Object? food = _pendingFood;
        ClearPendingFood();

        if (food is null || food.Edibility <= 0)
        {
            ShowTransientMessage("Nothing to eat.");
            return;
        }

        // Use SDV's own recovery formulas which account for quality and special items
        int staminaPerItem = food.staminaRecoveredOnConsumption();
        int healthPerItem = food.healthRecoveredOnConsumption();

        if (staminaPerItem <= 0 && healthPerItem <= 0)
        {
            ShowTransientMessage(ModEntry.Instance.Translate("message.no-benefit"));
            return;
        }

        int available = food.Stack;
        if (available <= 0)
            return;

        var config = ModEntry.Instance.Config;
        int needed;

        if (config.FillTarget == FillTarget.Energy)
        {
            if (staminaPerItem <= 0)
            {
                ShowTransientMessage(ModEntry.Instance.Translate("message.no-energy"));
                return;
            }

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
            if (healthPerItem <= 0)
            {
                ShowTransientMessage(ModEntry.Instance.Translate("message.no-health"));
                return;
            }

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
        int silentEat = toEat - 1;

        // Apply stamina and health for silently consumed items
        // (the last item's recovery is applied by doneEating() after eatObject)
        if (silentEat > 0)
        {
            Game1.player.stamina = Math.Min(
                Game1.player.maxStamina.Value,
                Game1.player.stamina + staminaPerItem * silentEat
            );
            Game1.player.health = Math.Min(
                Game1.player.maxHealth,
                Game1.player.health + healthPerItem * silentEat
            );

            // Reduce item stack in inventory.
            // Items[slot] = null triggers a NetRef change that syncs in multiplayer.
            // Partial consumption (remaining stack > 0) is local-only, same as vanilla SDV.
            int slot = Game1.player.Items.IndexOf(food);
            if (slot >= 0)
            {
                food.Stack -= silentEat;
                if (food.Stack <= 0)
                    Game1.player.Items[slot] = null;
            }
        }

        // Eat the last one with full animation, sound, network event, and inventory removal
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

    private static bool IsEligibleFood(Object? food)
    {
        return food is { Edibility: > 0, Stack: >= 2 };
    }

    private static void ClearPendingFood()
    {
        _pendingFood = null;
        _pendingFoodFromObjectAction = false;
    }
}
