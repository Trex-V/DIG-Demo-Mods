using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace DigFurnaceQueue;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private Harmony _harmony;

    private void Awake()
    {
        // Patch station recipe rows to add bulk-crafting buttons.
        Log = base.Logger;

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo("Furnace and saw bulk crafting enabled with the game's original six-job queue limit.");
    }

    private void OnDestroy()
    {
        // Remove the station UI patch if the plugin is unloaded.
        _harmony?.UnpatchSelf();
    }
}
