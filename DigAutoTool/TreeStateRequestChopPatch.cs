using DigDeliver;
using HarmonyLib;

namespace DigAutoTool;

[HarmonyPatch(typeof(TreeState), "RequestChop")]
internal static class TreeStateRequestChopPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        // Tree chopping always uses the axe, so select it before the game request runs.
        Plugin.SelectTool(ToolType.Axe);
    }
}
