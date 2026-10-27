using System;
using DigDeliver;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OrderTracker;

[HarmonyPatch(typeof(SettingsPanel), "BuildControls")]
internal static class OrderTrackerControls
{
    private static InputActionRebindingExtensions.RebindingOperation _rebinding;
    internal static bool IsRebinding { get; private set; }

    private static void Postfix(SettingsPanel __instance)
    {
        // Add three binding controls to the game's rebuilt Controls settings page.
        RectTransform content = (RectTransform)AccessTools.Field(typeof(SettingsPanel), "_content").GetValue(__instance);
        UITheme theme = UITheme.Instance;
        AddBindingRow(content, theme, __instance, "Order Tracker toggle", Plugin.ToggleAction, Plugin.SaveToggleBinding, false);
        AddBindingRow(content, theme, __instance, "Order Tracker scroll up", Plugin.ScrollUpAction, Plugin.SaveScrollUpBinding, true);
        AddBindingRow(content, theme, __instance, "Order Tracker scroll down", Plugin.ScrollDownAction, Plugin.SaveScrollDownBinding, true);
    }

    private static void AddBindingRow(
        RectTransform content,
        UITheme theme,
        SettingsPanel settingsPanel,
        string title,
        InputAction action,
        Action save,
        bool mouseOnly)
    {
        // Display the current binding and start capture when its button is pressed.
        Image row = UIFactory.Image(content, "OrderTrackerToggleBinding", null, theme.panelInnerColor);
        UIFactory.Size(row, -1f, 64f);

        TMP_Text name = UIFactory.Text(row.transform, title, 25f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        name.rectTransform.Anchor(
            new Vector2(0f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, 0f),
            new Vector2(-24f, 0f));

        Button button = UIFactory.Button(row.transform, BindingName(action), null, theme.buttonColor, 24f);
        button.onClick.AddListener(() =>
        {
            if (_rebinding == null)
            {
                settingsPanel.StartCoroutine(BeginRebind(button.GetComponentInChildren<TMP_Text>(), action, save, mouseOnly));
            }
        });
        RectTransform buttonRect = (RectTransform)button.transform;
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.sizeDelta = new Vector2(260f, 48f);
        buttonRect.anchoredPosition = new Vector2(-16f, 0f);
    }

    private static System.Collections.IEnumerator BeginRebind(
        TMP_Text label,
        InputAction action,
        Action save,
        bool mouseOnly)
    {
        // Restrict the capture to the intended device class and reserve Escape for cancellation.
        label.text = mouseOnly ? "Press a mouse button... (Esc to cancel)" : "Press a key... (Esc to cancel)";
        IsRebinding = true;
        yield return null;

        action.Disable();
        InputActionRebindingExtensions.RebindingOperation operation = action.PerformInteractiveRebinding(0);
        if (mouseOnly)
        {
            operation.WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll");
        }
        else
        {
            operation.WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Mouse>");
        }

        _rebinding = operation
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(completed => FinishRebind(completed, label, action, save, true))
            .OnCancel(cancelled => FinishRebind(cancelled, label, action, save, false))
            .Start();
    }

    private static void FinishRebind(
        InputActionRebindingExtensions.RebindingOperation operation,
        TMP_Text label,
        InputAction action,
        Action save,
        bool shouldSave)
    {
        // Restore the action after capture, persist successful changes, and refresh its label.
        operation.Dispose();
        _rebinding = null;
        IsRebinding = false;
        action.Enable();

        if (shouldSave)
        {
            save();
        }

        if (label != null)
        {
            label.text = BindingName(action);
        }
    }

    private static string BindingName(InputAction action)
    {
        return action.GetBindingDisplayString(0);
    }
}
