using System;
using DigDeliver;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DigFurnaceQueue;

[HarmonyPatch(typeof(StationPanel), "Row")]
internal static class StationPanelCraftButtonsPatch
{
    private static readonly AccessTools.FieldRef<StationPanel, RectTransform> RecipeList =
        AccessTools.FieldRefAccess<StationPanel, RectTransform>("_list");
    private static readonly AccessTools.FieldRef<StationPanel, PlayerInventory> Inventory =
        AccessTools.FieldRefAccess<StationPanel, PlayerInventory>("_inventory");
    private const int QueueLimit = 6;
    private static bool _warnedMissingButtons;

    [HarmonyPostfix]
    private static void Postfix(StationPanel __instance, RecipeDefinition recipe, bool unlocked)
    {
        // Assembly recipes use a separate interface; leave their buttons untouched.
        if (recipe.Assembled)
        {
            return;
        }

        RectTransform list = RecipeList(__instance);
        if (list == null || list.childCount == 0)
        {
            WarnMissingButtons();
            return;
        }

        Transform buttons = list.GetChild(list.childCount - 1).Find("Buttons");
        if (buttons == null)
        {
            WarnMissingButtons();
            return;
        }

        if (buttons.childCount < 2)
        {
            WarnMissingButtons();
            return;
        }

        Button craftButton = buttons.GetChild(0).GetComponent<Button>();
        Button x5Button = buttons.GetChild(1).GetComponent<Button>();
        if (craftButton == null || x5Button == null)
        {
            WarnMissingButtons();
            return;
        }

        var bulkButtonsObject = new GameObject(
            "BulkCraftButtons",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform bulkButtons = bulkButtonsObject.GetComponent<RectTransform>();
        bulkButtons.SetParent(buttons, false);
        HorizontalLayoutGroup layout = bulkButtonsObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        LayoutElement rowSize = bulkButtonsObject.GetComponent<LayoutElement>();
        rowSize.minWidth = 0f;
        rowSize.flexibleWidth = 1f;
        rowSize.preferredHeight = 36f;

        x5Button.transform.SetParent(bulkButtons, false);
        SetEqualButtonWidth(x5Button);

        Button x10Button = CreateBulkButton(buttons, __instance, recipe, 10);
        x10Button.transform.SetParent(bulkButtons, false);
        SetEqualButtonWidth(x10Button);

        Button x25Button = CreateBulkButton(buttons, __instance, recipe, 25);
        x25Button.transform.SetParent(bulkButtons, false);
        SetEqualButtonWidth(x25Button);

        // Match the game's queue limit and ingredient checks before enabling each batch size.
        PlayerInventory inventory = Inventory(__instance);
        CraftingService service = CraftingService.Instance;
        bool canQueue = unlocked && service != null && service.QueueLength(recipe.station) < QueueLimit;
        bool canFeed = SharedChest.Feeds(inventory.transform.position);
        var inputs = CraftingService.Inputs(recipe);
        x5Button.interactable = canQueue && SharedChest.Has(inventory, inputs, 5, canFeed);
        x10Button.interactable = canQueue && SharedChest.Has(inventory, inputs, 10, canFeed);
        x25Button.interactable = canQueue && SharedChest.Has(inventory, inputs, 25, canFeed);
    }

    private static Button CreateBulkButton(
        Transform parent,
        StationPanel panel,
        RecipeDefinition recipe,
        int count)
    {
        // Reuse the game's Craft method so the added buttons follow its normal queue flow.
        return UIFactory.Button(
            parent,
            $"x{count}",
            () => AccessTools.Method(typeof(StationPanel), "Craft").Invoke(
                panel,
                new object[] { recipe, count }),
            UITheme.Instance.accentColor,
            20f);
    }

    private static void SetEqualButtonWidth(Button button)
    {
        // Let the layout divide available row width evenly across the batch buttons.
        LayoutElement size = button.GetComponent<LayoutElement>();
        if (size == null)
        {
            size = button.gameObject.AddComponent<LayoutElement>();
        }

        size.minWidth = 0f;
        size.preferredWidth = 0f;
        size.flexibleWidth = 1f;
        size.preferredHeight = 36f;
    }

    private static void WarnMissingButtons()
    {
        // The game UI can change between versions; warn once instead of repeating each row.
        if (_warnedMissingButtons)
        {
            return;
        }

        _warnedMissingButtons = true;
        Plugin.Log.LogWarning("Could not arrange the bulk craft buttons because the station recipe button layout was not found.");
    }
}
