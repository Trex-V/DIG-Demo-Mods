using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace ExtendedStats;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private Harmony _harmony;

    private void Awake()
    {
        // Load saved counts before installing the patches that record new activity.
        Log = Logger;
        BlockStatsStore.Load(Log);

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo("Extended Stats loaded.");
    }

    private void OnDestroy()
    {
        // Close UI and remove patches so no callbacks outlive the plugin.
        ExtendedStatsWindow.Close();
        _harmony?.UnpatchSelf();
    }
}
