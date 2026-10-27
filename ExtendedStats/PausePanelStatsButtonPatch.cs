using DigDeliver;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtendedStats;

[HarmonyPatch(typeof(PausePanel), "OnBuild")]
internal static class PausePanelStatsButtonPatch
{
    [HarmonyPostfix]
    private static void Postfix(PausePanel __instance)
    {
        // Add one Stats entry to the rebuilt pause menu, positioned alongside the built-in actions.
        RectTransform menu = AccessTools.Field(typeof(PausePanel), "_menu").GetValue(__instance) as RectTransform;
        if (menu == null)
        {
            Plugin.Log.LogWarning("Could not add the Stats button because the pause menu panel was not found.");
            return;
        }

        if (menu.Find("ExtendedStatsButton") != null)
        {
            return;
        }

        Component settingsButton = FindButtonByLabel(menu, "Ayarlar", "Settings");
        Component saveButton = FindButtonByLabel(menu, "Kaydet", "Save");
        if (settingsButton == null || saveButton == null)
        {
            Plugin.Log.LogWarning(
                $"Could not place the Stats button between Settings and Save (Settings found: {settingsButton != null}; Save found: {saveButton != null}).");
            return;
        }

        Button button = UIFactory.Button(menu, "Stats", ExtendedStatsWindow.Open, UITheme.Instance.accentColor);
        button.gameObject.name = "ExtendedStatsButton";
        UIFactory.Size(button, -1f, 64f);

        menu.sizeDelta = new Vector2(menu.sizeDelta.x, menu.sizeDelta.y + 72f);
        button.transform.SetSiblingIndex(settingsButton.transform.GetSiblingIndex() + 1);

        Plugin.Log.LogInfo("Added the Stats button between Settings and Save in the pause menu.");
    }

    private static Component FindButtonByLabel(Transform parent, params string[] labels)
    {
        // Support either localized label so the button can be anchored in both menu languages.
        foreach (Transform child in parent)
        {
            foreach (TMPro.TMP_Text text in child.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                foreach (string label in labels)
                {
                    if (text.text == label)
                    {
                        return child.GetComponent<Button>();
                    }
                }
            }
        }

        return null;
    }
}
