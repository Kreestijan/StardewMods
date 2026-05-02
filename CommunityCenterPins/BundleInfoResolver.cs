using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.Menus;

namespace CommunityCenterPins;

internal sealed class BundleInfoResolver
{
    public bool TryCreateSnapshot(JunimoNoteMenu menu, int? forcedCompletedIngredientIndex, out BundleSnapshot snapshot)
    {
        if (menu.currentPageBundle is null)
        {
            snapshot = default!;
            return false;
        }

        List<BundleRequirementLine> requirements = new();
        int completedCount = 0;
        for (int i = 0; i < menu.currentPageBundle.ingredients.Count; i++)
        {
            BundleIngredientDescription ingredient = menu.currentPageBundle.ingredients[i];
            bool isCompleted = ingredient.completed || forcedCompletedIngredientIndex == i;
            if (isCompleted)
            {
                completedCount++;
                continue;
            }

            requirements.Add(this.CreateRequirementLine(menu, ingredient, i));
        }

        snapshot = new BundleSnapshot(
            menu.currentPageBundle.bundleIndex,
            this.GetBundleDisplayName(menu.currentPageBundle.label),
            Math.Max(0, menu.currentPageBundle.numberOfIngredientSlots - completedCount),
            requirements
        );
        return true;
    }

    public bool TryCreateSnapshot(int bundleIndex, out BundleSnapshot snapshot)
    {
        snapshot = default!;
        CommunityCenter communityCenter = Game1.RequireLocation<CommunityCenter>("CommunityCenter");
        Dictionary<int, bool[]> completedByBundle = communityCenter.bundlesDict();
        if (!completedByBundle.TryGetValue(bundleIndex, out bool[]? completionState))
        {
            return false;
        }

        KeyValuePair<string, string>? bundleEntry = Game1.netWorldState.Value.BundleData
            .FirstOrDefault(pair => this.TryParseBundleIndex(pair.Key, out int parsedIndex) && parsedIndex == bundleIndex);

        if (bundleEntry is null || string.IsNullOrWhiteSpace(bundleEntry.Value.Value))
        {
            return false;
        }

        string[] fields = bundleEntry.Value.Value.Split('/');
        if (fields.Length < 3)
        {
            return false;
        }

        string displayName = fields.Length > 5 && !string.IsNullOrWhiteSpace(fields[5])
            ? this.GetBundleDisplayName(fields[5])
            : this.GetBundleDisplayName(fields[0]);

        string[] requirementTokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requirementTokens.Length % 3 != 0)
        {
            return false;
        }

        List<BundleRequirementLine> requirements = new();
        int numberOfIngredientSlots = fields.Length > 4 && int.TryParse(fields[4], out int parsedSlotCount)
            ? parsedSlotCount
            : requirementTokens.Length / 3;
        int completedCount = 0;
        for (int i = 0; i < requirementTokens.Length / 3; i++)
        {
            string idOrCategory = requirementTokens[i * 3];
            if (!int.TryParse(requirementTokens[i * 3 + 1], out int stack))
            {
                return false;
            }

            if (!int.TryParse(requirementTokens[i * 3 + 2], out int quality))
            {
                return false;
            }

            bool completed = i < completionState.Length && completionState[i];
            BundleIngredientDescription ingredient = new(idOrCategory, stack, quality, completed);
            if (ingredient.completed)
            {
                completedCount++;
                continue;
            }

            requirements.Add(this.CreateRequirementLine(ingredient));
        }

        snapshot = new BundleSnapshot(bundleIndex, displayName, Math.Max(0, numberOfIngredientSlots - completedCount), requirements);
        return true;
    }

    public bool TryCreateItem(BundleRequirementLine line, out Item? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(line.QualifiedItemId))
        {
            return false;
        }

        item = line.PreservesId is not null
            ? Utility.CreateFlavoredItem(line.QualifiedItemId, line.PreservesId, line.Quality, line.Required)
            : ItemRegistry.Create(line.QualifiedItemId, line.Required, line.Quality);

        return item is not null;
    }

    private BundleRequirementLine CreateRequirementLine(JunimoNoteMenu menu, BundleIngredientDescription ingredient, int index)
    {
        string qualifiedItemId = JunimoNoteMenu.GetRepresentativeItemId(ingredient);
        string name = this.GetLiveIngredientName(menu, index, ingredient, qualifiedItemId);
        return new BundleRequirementLine(name, ingredient.stack, qualifiedItemId, ingredient.quality, ingredient.preservesId);
    }

    private BundleRequirementLine CreateRequirementLine(BundleIngredientDescription ingredient)
    {
        string qualifiedItemId = JunimoNoteMenu.GetRepresentativeItemId(ingredient);
        return new BundleRequirementLine(
            this.GetResolvedIngredientName(ingredient, qualifiedItemId),
            ingredient.stack,
            qualifiedItemId,
            ingredient.quality,
            ingredient.preservesId
        );
    }

    private string GetLiveIngredientName(JunimoNoteMenu menu, int index, BundleIngredientDescription ingredient, string qualifiedItemId)
    {
        if (index < menu.ingredientList.Count)
        {
            ClickableTextureComponent component = menu.ingredientList[index];
            if (!string.IsNullOrWhiteSpace(component.hoverText))
            {
                return component.hoverText;
            }

            if (component.item is not null)
            {
                return component.item.DisplayName;
            }
        }

        return this.GetResolvedIngredientName(ingredient, qualifiedItemId);
    }

    private string GetResolvedIngredientName(BundleIngredientDescription ingredient, string qualifiedItemId)
    {
        if (ingredient.category.HasValue)
        {
            return ingredient.category.Value switch
            {
                -2 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.569"),
                -75 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.570"),
                -79 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.571"),
                -80 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.572"),
                -4 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.573"),
                -6 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.574"),
                _ => ItemRegistry.GetDataOrErrorItem(qualifiedItemId).DisplayName
            };
        }

        ParsedItemData data = ItemRegistry.GetDataOrErrorItem(qualifiedItemId);
        return data.DisplayName;
    }

    private string GetBundleDisplayName(string? bundleLabel)
    {
        if (string.IsNullOrWhiteSpace(bundleLabel))
        {
            return "Bundle";
        }

        return Game1.content.LoadString("Strings\\UI:JunimoNote_BundleName", bundleLabel);
    }

    private bool TryParseBundleIndex(string key, out int bundleIndex)
    {
        bundleIndex = -1;
        string[] parts = key.Split('/');
        return parts.Length >= 2 && int.TryParse(parts[1], out bundleIndex);
    }
}
