using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace NoPopup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private Harmony _harmony;

    private void Awake()
    {
        // Intercept URL launches so the Google Forms patch can block matching links.
        Log = Logger;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo("No Popup loaded; Google Forms links will be blocked.");
    }

    private void OnDestroy()
    {
        // Restore normal URL handling when this plugin is unloaded.
        _harmony?.UnpatchSelf();
    }
}
