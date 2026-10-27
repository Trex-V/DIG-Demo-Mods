using DigDeliver;
using HarmonyLib;

namespace DigAutoTool;

[HarmonyPatch(typeof(WorldState), "RequestHit")]
internal static class WorldStateRequestHitPatch
{
    [HarmonyPrefix]
    private static void Prefix(WorldState __instance, int cellIndex, ref ToolType tool)
    {
        // Ignore invalid cells before looking up their block or node definitions.
        if (__instance.Grid == null || cellIndex < 0 || cellIndex >= __instance.Grid.CellCount)
        {
            return;
        }

        ToolType requiredTool;
        BlockId block = __instance.Grid.Get(cellIndex);
        if (block == BlockId.Node)
        {
            // Blueprint and gold-bag nodes are not mined with the normal pickaxe flow.
            NodeDefinition node = GameDatabase.Instance?.Node(__instance.Grid.GetAux(cellIndex));
            if (node == null || node.blueprint != null || node.IsGoldBag)
            {
                return;
            }

            requiredTool = ToolType.Pickaxe;
        }
        else
        {
            // Air and bedrock have no selectable mining tool.
            if (block == BlockId.Air || block == BlockId.Bedrock)
            {
                return;
            }

            BlockDefinition definition = GameDatabase.Instance?.Block(block);
            if (definition == null)
            {
                return;
            }

            requiredTool = definition.tool;
        }

        Plugin.SelectTool(requiredTool);
        tool = requiredTool;
    }
}
