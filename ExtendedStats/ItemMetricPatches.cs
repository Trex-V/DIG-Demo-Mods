using System.Collections.Generic;
using DigDeliver;
using HarmonyLib;
using PurrNet;
using UnityEngine;

namespace ExtendedStats;

[HarmonyPatch(typeof(DropState), "RequestCollect_Original_0")]
internal static class DropPickupStatsPatch
{
    private sealed class InventorySnapshot
    {
        internal PlayerAvatar Avatar;
        internal Dictionary<ushort, int> Counts;
        internal bool IsLocalPlayer;
    }

    [HarmonyPrefix]
    private static void Prefix(DropState __instance, RPCInfo info, out InventorySnapshot __state)
    {
        // Snapshot the server inventory before collection so the postfix can measure actual gains.
        __state = null;
        if (!__instance.isServer || !PlayerAvatar.TryGet(info.sender, out PlayerAvatar avatar)
            || GameDatabase.Instance == null)
        {
            return;
        }

        var counts = new Dictionary<ushort, int>();
        foreach (ItemDefinition item in GameDatabase.Instance.items)
        {
            if (item != null)
            {
                counts[item.id] = avatar.Inventory.Count(item);
            }
        }

        __state = new InventorySnapshot
        {
            Avatar = avatar,
            Counts = counts,
            IsLocalPlayer = PlayerStatsAttribution.IsLocalPlayer(info.sender)
        };
    }

    [HarmonyPostfix]
    private static void Postfix(InventorySnapshot __state)
    {
        // Count only positive inventory deltas; the drop may be partly or fully rejected.
        if (__state?.Avatar == null || GameDatabase.Instance == null)
        {
            return;
        }

        foreach (ItemDefinition item in GameDatabase.Instance.items)
        {
            if (item == null)
            {
                continue;
            }

            __state.Counts.TryGetValue(item.id, out int before);
            int pickedUp = __state.Avatar.Inventory.Count(item) - before;
            if (pickedUp > 0)
            {
                BlockStatsStore.RecordItem(item.id, ItemMetric.PickedUp, __state.IsLocalPlayer, pickedUp);
            }
        }
    }
}

[HarmonyPatch(typeof(CraftingService), "Finish")]
internal static class CraftingStatsPatch
{
    [HarmonyPostfix]
    private static void Postfix(CraftingService __instance, CraftJob job)
    {
        // Completed normal craft jobs record output quantity against the original requester.
        if (!__instance.isServer || GameDatabase.Instance == null)
        {
            return;
        }

        RecipeDefinition recipe = GameDatabase.Instance.Recipe(job.recipe);
        if (recipe != null && recipe.output != null && recipe.outputCount > 0 && job.count > 0)
        {
            BlockStatsStore.RecordItem(
                recipe.output.id,
                ItemMetric.Crafted,
                PlayerStatsAttribution.IsLocalPlayer(job.requester),
                (long)recipe.outputCount * job.count);
        }
    }
}

[HarmonyPatch(typeof(CraftingService), "FinishAssembly")]
internal static class AssemblyCraftingStatsPatch
{
    [HarmonyPostfix]
    private static void Postfix(CraftingService __instance, AssemblySite site)
    {
        // Attribute an assembly's completed output to the player who started that site.
        if (!__instance.isServer || GameDatabase.Instance == null)
        {
            return;
        }

        RecipeDefinition recipe = GameDatabase.Instance.Recipe(site.recipe);
        if (recipe != null && recipe.output != null && recipe.outputCount > 0)
        {
            BlockStatsStore.RecordItem(
                recipe.output.id,
                ItemMetric.Crafted,
                PlayerStatsAttribution.IsLocalPlayer(site.starter),
                recipe.outputCount);
        }
    }
}

[HarmonyPatch(typeof(CraftingService), "RequestPlacePiece_Original_1")]
internal static class AssemblyPlacementStatsPatch
{
    private sealed class PlacementSnapshot
    {
        internal uint SiteId;
        internal ushort[] ItemIds;
        internal int[] Counts;
        internal bool IsLocalPlayer;
    }

    [HarmonyPrefix]
    private static void Prefix(CraftingService __instance, uint siteId, RPCInfo info, out PlacementSnapshot __state)
    {
        // Capture each ingredient's placed count before the server processes this request.
        __state = null;
        if (!__instance.isServer || !__instance.FindSite(siteId, out AssemblySite site)
            || GameDatabase.Instance == null)
        {
            return;
        }

        RecipeDefinition recipe = GameDatabase.Instance.Recipe(site.recipe);
        if (recipe == null)
        {
            return;
        }

        List<ItemStack> inputs = CraftingService.Inputs(recipe);
        var ids = new ushort[inputs.Count];
        var counts = new int[inputs.Count];
        for (int index = 0; index < inputs.Count; index++)
        {
            ids[index] = inputs[index].item.id;
            counts[index] = site.Placed(index);
        }

        __state = new PlacementSnapshot
        {
            SiteId = siteId,
            ItemIds = ids,
            Counts = counts,
            IsLocalPlayer = PlayerStatsAttribution.IsLocalPlayer(info.sender)
        };
    }

    [HarmonyPostfix]
    private static void Postfix(CraftingService __instance, PlacementSnapshot __state)
    {
        // Record only ingredients actually added by this request, not failed placement attempts.
        if (__state == null || !__instance.FindSite(__state.SiteId, out AssemblySite site))
        {
            return;
        }

        for (int index = 0; index < __state.ItemIds.Length; index++)
        {
            int placed = site.Placed(index) - __state.Counts[index];
            if (placed > 0)
            {
                BlockStatsStore.RecordItem(__state.ItemIds[index], ItemMetric.Placed, __state.IsLocalPlayer, placed);
            }
        }
    }
}

[HarmonyPatch(typeof(LampState), "RequestPlace_Original_0")]
internal static class LampPlacementStatsPatch
{
    private static readonly AccessTools.FieldRef<LampState, ItemDefinition> LampItem =
        AccessTools.FieldRefAccess<LampState, ItemDefinition>("lampItem");

    private struct LampActionSnapshot
    {
        internal int LampCount;
        internal bool IsLocalPlayer;
    }

    [HarmonyPrefix]
    private static void Prefix(LampState __instance, RPCInfo info, out LampActionSnapshot __state)
    {
        // Keep the lamp count and requester before placement to identify successful placement.
        __state = new LampActionSnapshot
        {
            LampCount = __instance.lamps.Count,
            IsLocalPlayer = PlayerStatsAttribution.IsLocalPlayer(info.sender)
        };
    }

    [HarmonyPostfix]
    private static void Postfix(LampState __instance, LampActionSnapshot __state)
    {
        // A lamp is placed only when the server-side lamp count increases.
        if (__instance.isServer && __instance.lamps.Count > __state.LampCount)
        {
            ItemDefinition item = LampItem(__instance);
            if (item != null)
            {
                BlockStatsStore.RecordItem(item.id, ItemMetric.Placed, __state.IsLocalPlayer);
            }
        }
    }
}

[HarmonyPatch(typeof(LampState), "RequestPickup_Original_1")]
internal static class LampPickupStatsPatch
{
    private static readonly AccessTools.FieldRef<LampState, ItemDefinition> LampItem =
        AccessTools.FieldRefAccess<LampState, ItemDefinition>("lampItem");

    private struct LampActionSnapshot
    {
        internal int LampCount;
        internal bool IsLocalPlayer;
    }

    [HarmonyPrefix]
    private static void Prefix(LampState __instance, RPCInfo info, out LampActionSnapshot __state)
    {
        // Keep the lamp count and requester before pickup to identify a successful removal.
        __state = new LampActionSnapshot
        {
            LampCount = __instance.lamps.Count,
            IsLocalPlayer = PlayerStatsAttribution.IsLocalPlayer(info.sender)
        };
    }

    [HarmonyPostfix]
    private static void Postfix(LampState __instance, LampActionSnapshot __state)
    {
        // A lamp is collected only when the server-side lamp count decreases.
        if (__instance.isServer && __instance.lamps.Count < __state.LampCount)
        {
            ItemDefinition item = LampItem(__instance);
            if (item != null)
            {
                BlockStatsStore.RecordItem(item.id, ItemMetric.PickedUp, __state.IsLocalPlayer);
            }
        }
    }
}

[HarmonyPatch(typeof(OrderState), "RequestShip_Original_4")]
internal static class ItemSalesStatsPatch
{
    private sealed class ShippedItem
    {
        internal ushort ItemId;
        internal int Count;
    }

    private sealed class ShipmentSnapshot
    {
        internal bool CanShip;
        internal List<ShippedItem> Items;
        internal bool IsLocalPlayer;
    }

    [HarmonyPrefix]
    private static void Prefix(OrderState __instance, RPCInfo info, out ShipmentSnapshot __state)
    {
        // Capture packed order contents before shipping changes their state or removes them.
        __state = null;
        if (!__instance.isServer || __instance.shipPhase.value != ShipPhase.Idle)
        {
            return;
        }

        var items = new List<ShippedItem>();
        foreach (OrderRecord order in __instance.orders)
        {
            if (order.status != OrderStatus.Packed)
            {
                continue;
            }

            for (int index = 0; index < order.LineCount; index++)
            {
                int count = order.Have(index);
                if (count > 0)
                {
                    items.Add(new ShippedItem { ItemId = order.Item(index), Count = count });
                }
            }
        }

        __state = new ShipmentSnapshot
        {
            CanShip = items.Count > 0,
            Items = items,
            IsLocalPlayer = PlayerStatsAttribution.IsLocalPlayer(info.sender)
        };
    }

    [HarmonyPostfix]
    private static void Postfix(OrderState __instance, ShipmentSnapshot __state)
    {
        // Record the captured items only after the server has actually begun shipping.
        if (__state == null || !__state.CanShip || __instance.shipPhase.value == ShipPhase.Idle)
        {
            return;
        }

        foreach (ShippedItem item in __state.Items)
        {
            BlockStatsStore.RecordItem(item.ItemId, ItemMetric.Sold, __state.IsLocalPlayer, item.Count);
        }
    }
}
