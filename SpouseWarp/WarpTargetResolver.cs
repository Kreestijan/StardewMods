using StardewValley;

namespace SpouseWarp;

internal sealed class WarpTargetResolver
{
    public List<WarpTarget> BuildTargets(ModConfig config)
    {
        List<WarpTarget> results = new();
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        HashSet<long> onlineFarmerIds = Game1.getOnlineFarmers()
            .Select(static farmer => farmer.UniqueMultiplayerID)
            .ToHashSet();

        Farmer? playerSpouse = this.GetPlayerSpouse();
        if (playerSpouse is not null)
        {
            this.AddPlayerTarget(results, seenKeys, onlineFarmerIds, playerSpouse);
        }

        NPC? npcSpouse = Game1.player.getSpouse();
        if (npcSpouse is not null)
        {
            this.AddNpcTarget(results, seenKeys, npcSpouse);
        }

        if (!config.RequiresMarriage)
        {
            foreach (Farmer farmer in Game1.getAllFarmers().OrderBy(static farmer => farmer.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (farmer.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID)
                {
                    continue;
                }

                this.AddPlayerTarget(results, seenKeys, onlineFarmerIds, farmer);
            }
        }

        foreach (string npcName in config.ShowNPCs
            .Where(static pair => pair.Value)
            .Select(static pair => pair.Key)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
        {
            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc is not null)
            {
                this.AddNpcTarget(results, seenKeys, npc, isSelectable: this.IsConfiguredNpcSelectable(config, npc));
            }
        }

        return results;
    }

    private Farmer? GetPlayerSpouse()
    {
        long? spouseId = Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID);
        return spouseId.HasValue
            ? Game1.GetPlayer(spouseId.Value)
            : null;
    }

    private void AddPlayerTarget(List<WarpTarget> results, HashSet<string> seenKeys, HashSet<long> onlineFarmerIds, Farmer farmer)
    {
        string key = farmer.UniqueMultiplayerID.ToString();
        if (!seenKeys.Add(key))
        {
            return;
        }

        bool isOnline = onlineFarmerIds.Contains(farmer.UniqueMultiplayerID);

        results.Add(new WarpTarget(
            WarpTargetKind.Player,
            key,
            farmer.Name,
            farmer: farmer,
            isOnline: isOnline,
            isSelectable: isOnline
        ));
    }

    private void AddNpcTarget(List<WarpTarget> results, HashSet<string> seenKeys, NPC npc, bool isSelectable = true)
    {
        string key = $"NPC_{npc.Name}";
        if (!seenKeys.Add(key))
        {
            return;
        }

        results.Add(new WarpTarget(
            WarpTargetKind.Npc,
            key,
            npc.displayName,
            npc: npc,
            isSelectable: isSelectable
        ));
    }

    private bool IsConfiguredNpcSelectable(ModConfig config, NPC npc)
    {
        if (!config.RequiresMarriage)
        {
            return true;
        }

        return Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship)
            && (friendship.IsMarried() || friendship.IsRoommate());
    }
}
