using BepInEx;
using BepInEx.Unity.Mono;
using DigDeliver;
using HarmonyLib;
using UnityEngine.InputSystem;
using UnityEngine;

namespace OrderTracker;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string ToggleBindingPrefsKey = "OrderTracker.ToggleBindingPath";
    private const string ScrollUpBindingPrefsKey = "OrderTracker.ScrollUpBindingPath";
    private const string ScrollDownBindingPrefsKey = "OrderTracker.ScrollDownBindingPath";
    internal static InputAction ToggleAction { get; private set; }
    internal static InputAction ScrollUpAction { get; private set; }
    internal static InputAction ScrollDownAction { get; private set; }

    private GameObject _overlayObject;
    private Harmony _harmony;
    private bool _inputActionsEnabled;

    private void Awake()
    {
        // Restore saved bindings, install game patches, and create the persistent overlay.
        ToggleAction = new InputAction("OrderTrackerToggle", InputActionType.Button, "<Keyboard>/t");
        string savedBinding = PlayerPrefs.GetString(ToggleBindingPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(savedBinding))
        {
            ToggleAction.ApplyBindingOverride(0, savedBinding);
        }

        ScrollUpAction = CreateBindingAction(
            "OrderTrackerScrollUp",
            "<Mouse>/forwardButton",
            ScrollUpBindingPrefsKey);
        ScrollDownAction = CreateBindingAction(
            "OrderTrackerScrollDown",
            "<Mouse>/backButton",
            ScrollDownBindingPrefsKey);
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        _overlayObject = new GameObject("DigOrderTracker");
        DontDestroyOnLoad(_overlayObject);
        _overlayObject.AddComponent<OrderTrackerOverlay>();
        Logger.LogInfo("Order Tracker loaded.");
    }

    private void Update()
    {
        // Enable actions on the first Unity update, after the input system has initialized.
        if (_inputActionsEnabled)
        {
            return;
        }

        ToggleAction.Enable();
        ScrollUpAction.Enable();
        ScrollDownAction.Enable();
        _inputActionsEnabled = true;
    }

    internal static void SaveToggleBinding()
    {
        SaveBinding(ToggleAction, ToggleBindingPrefsKey);
    }

    internal static void SaveScrollUpBinding()
    {
        SaveBinding(ScrollUpAction, ScrollUpBindingPrefsKey);
    }

    internal static void SaveScrollDownBinding()
    {
        SaveBinding(ScrollDownAction, ScrollDownBindingPrefsKey);
    }

    private static InputAction CreateBindingAction(string name, string defaultPath, string preferenceKey)
    {
        // Build a button action and replace its default path when the player saved an override.
        InputAction action = new InputAction(name, InputActionType.Button, defaultPath);
        string savedBinding = PlayerPrefs.GetString(preferenceKey, string.Empty);
        if (!string.IsNullOrEmpty(savedBinding))
        {
            action.ApplyBindingOverride(0, savedBinding);
        }

        return action;
    }

    private static void SaveBinding(InputAction action, string preferenceKey)
    {
        // Store the effective control path so the chosen binding survives the next launch.
        PlayerPrefs.SetString(preferenceKey, action.bindings[0].effectivePath);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        // Release input actions, Harmony patches, and the persistent tracker object on unload.
        _harmony?.UnpatchSelf();
        if (ToggleAction != null)
        {
            ToggleAction.Disable();
            ToggleAction.Dispose();
            ToggleAction = null;
        }
        DisposeAction(ScrollUpAction);
        ScrollUpAction = null;
        DisposeAction(ScrollDownAction);
        ScrollDownAction = null;

        if (_overlayObject != null)
        {
            Destroy(_overlayObject);
            _overlayObject = null;
        }
    }

    private static void DisposeAction(InputAction action)
    {
        // Disable before disposal so no input callback remains active.
        if (action != null)
        {
            action.Disable();
            action.Dispose();
        }
    }
}
