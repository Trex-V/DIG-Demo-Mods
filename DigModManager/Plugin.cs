using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using DigDeliver.Lobby;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Events;

namespace DigModManager;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private Harmony _harmony;

    private void Awake()
    {
        // Register menu patches and close the manager when the game changes scenes.
        Log = base.Logger;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Log.LogInfo("Mod Manager loaded.");
    }

    private void OnDestroy()
    {
        // Detach scene notifications and clean up before removing Harmony patches.
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        ModsMenuWindow.Close();
        _harmony?.UnpatchSelf();
    }

    private static void OnActiveSceneChanged(Scene previous, Scene current)
    {
        // Do not leave a menu canvas open across scene transitions.
        ModsMenuWindow.Close();
    }

    internal static void OpenModsMenu()
    {
        ModsMenuWindow.Open();
    }
}

[HarmonyPatch(typeof(MainMenuSettings), "AddButton")]
internal static class MainMenuSettingsAddButtonPatch
{
    [HarmonyPostfix]
    private static void Postfix(MainMenuSettings __instance)
    {
        // Avoid adding a duplicate if the settings panel is rebuilt.
        Component existingButton = AccessTools.Method(typeof(MainMenuSettings), "FindButton")
            .Invoke(__instance, new object[] { "DigModManagerButton" }) as Component;
        if (existingButton != null)
        {
            return;
        }

        Component wishlistButton = AccessTools.Method(typeof(MainMenuSettings), "FindButton")
            .Invoke(__instance, new object[] { "Wishlist" }) as Component;
        if (wishlistButton == null || wishlistButton.transform.parent == null)
        {
            Plugin.Log.LogWarning("Could not add the Mod Manager button because the Wishlist button was not found.");
            return;
        }

        // Clone the neighboring menu button to inherit the game's styling and layout.
        Transform menuButtons = wishlistButton.transform.parent;
        GameObject managerButton = UnityEngine.Object.Instantiate(wishlistButton.gameObject, menuButtons);
        managerButton.name = "DigModManagerButton";
        managerButton.transform.SetSiblingIndex(wishlistButton.transform.GetSiblingIndex() + 1);

        Component shake = managerButton.GetComponent("DigDeliver.AttentionShake");
        if (shake != null)
        {
            UnityEngine.Object.Destroy(shake);
        }

        foreach (TMP_Text label in managerButton.GetComponentsInChildren<TMP_Text>(true))
        {
            label.text = "Mod Manager";
        }

        Component buttonElement = managerButton.GetComponent("PurrNet.UI.ButtonElement");
        if (buttonElement == null)
        {
            UnityEngine.Object.Destroy(managerButton);
            Plugin.Log.LogWarning("Could not add the Mod Manager button because the Wishlist button has no ButtonElement component.");
            return;
        }

        var clickEvent = new UnityEvent();
        clickEvent.AddListener(Plugin.OpenModsMenu);
        AccessTools.Field(buttonElement.GetType(), "onClickUnity").SetValue(buttonElement, clickEvent);
        Plugin.Log.LogInfo("Added the stationary Mod Manager button below Add to Wishlist.");
    }
}
