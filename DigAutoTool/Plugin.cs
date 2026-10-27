using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using DigDeliver;
using HarmonyLib;

namespace DigAutoTool;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony _harmony;

    private void Awake()
    {
        // Install all tool-selection patches when BepInEx loads the plugin.
        Logger = base.Logger;

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("Automatic tool switching enabled for block, cliff, and tree mining.");
    }

    private void OnDestroy()
    {
        // Remove this plugin's patches when it is unloaded.
        _harmony?.UnpatchSelf();
    }

    internal static void SelectTool(ToolType tool)
    {
        // Resolve the requested tool to the game's slot before changing selection.
        ToolController controller = ToolController.Local;
        if (controller == null)
        {
            Logger.LogWarning($"Could not switch to {tool}: the local tool controller is unavailable.");
            return;
        }

        int slot = Array.IndexOf(ToolController.Slots, tool);
        if (slot < 0)
        {
            Logger.LogWarning($"Could not switch to {tool}: no matching tool slot exists.");
            return;
        }

        controller.Select(slot);
    }
}
