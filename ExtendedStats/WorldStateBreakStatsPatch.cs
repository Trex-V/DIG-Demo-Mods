using DigDeliver;
using HarmonyLib;
using PurrNet;

namespace ExtendedStats;

[HarmonyPatch(typeof(WorldState), "BroadcastHit")]
internal static class WorldStateBreakStatsPatch
{
    [HarmonyPostfix]
    private static void Postfix(WorldState __instance, BlockId block, bool broken, int cellIndex, PlayerID hitter)
    {
        // Count only completed server-side breaks, where the final cell data is authoritative.
        if (!broken || !__instance.isServer || __instance.Grid == null
            || cellIndex < 0 || cellIndex >= __instance.Grid.CellCount)
        {
            return;
        }

        BlockStatsStore.RecordBroken(
            block,
            __instance.Grid.GetAux(cellIndex),
            PlayerStatsAttribution.IsLocalPlayer(hitter));
    }
}
