using DigDeliver;
using HarmonyLib;

namespace DigAutoTool;

[HarmonyPatch(typeof(CliffState), "RequestMine")]
internal static class CliffStateRequestMinePatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        // Cliff mining always uses the pickaxe, so select it before the game request runs.
        Plugin.SelectTool(ToolType.Pickaxe);
    }
}
