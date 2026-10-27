using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigModManager;

internal sealed class ModsMenuWindow : MonoBehaviour
{
    private const string ManagerGuid = "DigModManager";
    private static ModsMenuWindow _instance;
    private Transform _content;
    private TMP_Text _status;

    internal static void Open()
    {
        // Reuse the existing window when possible, otherwise build it once and keep it across scenes.
        if (_instance != null)
        {
            _instance.gameObject.SetActive(true);
            _instance.Refresh();
            return;
        }

        var root = new GameObject("DigModManagerWindow");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<ModsMenuWindow>();
        _instance.Build();
    }

    internal static void Close()
    {
        // Destroy the complete window so its canvas and controls cannot linger.
        if (_instance == null)
        {
            return;
        }

        Destroy(_instance.gameObject);
        _instance = null;
    }

    private void Build()
    {
        // Construct a standalone, scrollable overlay for installed BepInEx plugins.
        var canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Image backdrop = CreateImage(canvasObject.transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.68f));
        Stretch(backdrop.rectTransform);

        Image panel = CreateImage(canvasObject.transform, "Panel", DigDeliver.UITheme.Instance.panel, DigDeliver.UITheme.Instance.panelColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 600f);
        panelRect.anchoredPosition = Vector2.zero;

        CreateText(
            panel.transform,
            "Title",
            "Mod Manager",
            34f,
            new Vector2(32f, -84f),
            new Vector2(-200f, -20f),
            new Vector2(0f, 1f),
            Vector2.one,
            TextAlignmentOptions.Left);
        Button closeButton = CreateButton(
            panel.transform,
            "Close",
            "Close",
            new Vector2(-28f, -24f),
            Vector2.one,
            new Vector2(150f, 48f),
            () => gameObject.SetActive(false),
            DigDeliver.UITheme.Instance.warnColor,
            Vector2.one);
        closeButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;
        _status = CreateText(
            panel.transform,
            "Status",
            "Live toggles are best-effort. Restart the game if a mod does not fully stop or resume.",
            16f,
            new Vector2(32f, -132f),
            new Vector2(-32f, -92f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            TextAlignmentOptions.MidlineLeft);

        var scrollObject = new GameObject("ModsScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(panel.transform, false);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 28f);
        scrollRect.offsetMax = new Vector2(-28f, -158f);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportObject.GetComponent<Image>().color = DigDeliver.UITheme.Instance.panelInnerColor;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        var contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup listLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 8f;
        listLayout.padding = new RectOffset(4, 4, 4, 4);
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;

        _content = contentObject.transform;
        Refresh();
    }

    private void Refresh()
    {
        // Rebuild rows from the live chainloader list so displayed states stay current.
        if (_content == null)
        {
            return;
        }

        for (int index = _content.childCount - 1; index >= 0; index--)
        {
            Destroy(_content.GetChild(index).gameObject);
        }

        List<PluginInfo> plugins = UnityChainloader.Instance?.Plugins?.Values
            .OrderBy(plugin => plugin.Metadata.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (plugins == null || plugins.Count == 0)
        {
            CreateText(_content, "Empty", "No BepInEx plugins were found.", 20f, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            return;
        }

        foreach (PluginInfo plugin in plugins)
        {
            BuildPluginRow(plugin);
        }
    }

    private void BuildPluginRow(PluginInfo plugin)
    {
        // Show metadata and provide a live toggle only when the plugin has a usable instance.
        var rowObject = new GameObject("PluginRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(_content, false);
        Image rowBackground = rowObject.GetComponent<Image>();
        rowBackground.sprite = DigDeliver.UITheme.Instance.panelInner;
        rowBackground.color = DigDeliver.UITheme.Instance.panelInnerColor;

        LayoutElement rowSize = rowObject.GetComponent<LayoutElement>();
        rowSize.minHeight = 96f;
        rowSize.preferredHeight = 96f;

        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f;
        rowLayout.padding = new RectOffset(16, 16, 8, 8);
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        string name = string.IsNullOrWhiteSpace(plugin.Metadata.Name) ? plugin.Metadata.GUID : plugin.Metadata.Name;
        BaseUnityPlugin instance = plugin.Instance as BaseUnityPlugin;
        bool isManager = string.Equals(plugin.Metadata.GUID, ManagerGuid, StringComparison.OrdinalIgnoreCase);
        ModAbout about = ReadAbout(plugin, instance, isManager);
        TMP_Text details = CreateText(
            rowObject.transform,
            "Details",
            $"{name}\nVersion: {about.Version} | Game build last tested: {about.GameBuild}\nAuthor: {about.Author} | Live toggle supported: {about.LiveToggle}",
            15f,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.one,
            TextAlignmentOptions.Left);
        LayoutElement detailsSize = details.gameObject.AddComponent<LayoutElement>();
        detailsSize.flexibleWidth = 1f;

        string state = instance == null ? "Live toggle unavailable" : instance.enabled ? "Enabled" : "Disabled";
        CreateText(
            rowObject.transform,
            "State",
            state,
            16f,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            Vector2.one,
            TextAlignmentOptions.Center).gameObject.AddComponent<LayoutElement>().preferredWidth = 170f;

        string buttonLabel = isManager ? "Always on" : instance == null ? "Restart required" : instance.enabled ? "Disable" : "Enable";
        Button toggle = CreateButton(
            rowObject.transform,
            "Toggle",
            buttonLabel,
            Vector2.zero,
            Vector2.zero,
            new Vector2(150f, 44f),
            () => TogglePlugin(plugin));
        toggle.interactable = !isManager && instance != null;
    }

    private static ModAbout ReadAbout(PluginInfo plugin, BaseUnityPlugin instance, bool isManager)
    {
        // Start with safe defaults, then apply fields from the optional sidecar ABOUT file.
        var about = new ModAbout
        {
            Version = plugin.Metadata.Version?.ToString() ?? "Not specified",
            GameBuild = "Not specified",
            Author = "Not specified",
            LiveToggle = isManager
                ? "No (manager must stay enabled)"
                : instance == null ? "No" : "Best-effort (session only)"
        };

        if (string.IsNullOrWhiteSpace(plugin.Location))
        {
            return about;
        }

        string aboutPath = Path.ChangeExtension(plugin.Location, ".ABOUT.md");
        if (!File.Exists(aboutPath))
        {
            return about;
        }

        try
        {
            foreach (string sourceLine in File.ReadAllLines(aboutPath))
            {
                string line = sourceLine.Trim();
                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    line = line.Substring(2).Trim();
                }

                int separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim().Trim('`');
                if (value.Length == 0)
                {
                    continue;
                }

                if (key.Equals("Version", StringComparison.OrdinalIgnoreCase))
                {
                    about.Version = value;
                }
                else if (key.Equals("Game build last tested", StringComparison.OrdinalIgnoreCase))
                {
                    about.GameBuild = value;
                }
                else if (key.Equals("Author", StringComparison.OrdinalIgnoreCase))
                {
                    about.Author = value;
                }
                else if (key.Equals("Live toggle supported", StringComparison.OrdinalIgnoreCase))
                {
                    about.LiveToggle = value;
                }
            }
        }
        catch (IOException exception)
        {
            Plugin.Log.LogWarning($"Could not read mod information file '{aboutPath}': {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Plugin.Log.LogWarning($"Could not read mod information file '{aboutPath}': {exception.Message}");
        }

        return about;
    }

    private void TogglePlugin(PluginInfo info)
    {
        // Live toggling is best-effort: enable/disable the plugin and its Harmony patches for this session.
        try
        {
            if (string.Equals(info.Metadata.GUID, ManagerGuid, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Dig Mod Manager cannot disable itself.");
                return;
            }

            BaseUnityPlugin instance = info.Instance as BaseUnityPlugin;
            if (instance == null)
            {
                SetStatus($"{info.Metadata.Name} cannot be toggled live. Restart the game to change it.");
                return;
            }

            var harmony = new Harmony(info.Metadata.GUID);
            if (instance.enabled)
            {
                harmony.UnpatchSelf();
                instance.enabled = false;
                Plugin.Log.LogInfo($"Disabled plugin {info.Metadata.GUID} for this session.");
                SetStatus($"{info.Metadata.Name} disabled. Some plugin state may require a game restart to clear.");
            }
            else
            {
                instance.enabled = true;
                harmony.PatchAll(instance.GetType().Assembly);
                Plugin.Log.LogInfo($"Enabled plugin {info.Metadata.GUID} for this session.");
                SetStatus($"{info.Metadata.Name} enabled. Some plugins may require a game restart to fully resume.");
            }

            Refresh();
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Could not toggle plugin {info.Metadata.GUID}: {exception}");
            SetStatus($"Could not toggle {info.Metadata.Name}: {exception.Message}");
        }
    }

    private void SetStatus(string message)
    {
        if (_status != null)
        {
            _status.text = message;
        }
    }

    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = DigDeliver.UITheme.Instance.font;
        text.fontSize = fontSize;
        text.color = DigDeliver.UITheme.Instance.textColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        Vector2 anchor,
        Vector2 size,
        Action onClick,
        Color? color = null,
        Vector2? pivot = null)
    {
        Button button = DigDeliver.UIFactory.Button(
            parent,
            label,
            () => onClick(),
            color ?? DigDeliver.UITheme.Instance.accentColor,
            18f);
        GameObject buttonObject = button.gameObject;
        buttonObject.name = objectName;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = buttonObject.AddComponent<LayoutElement>();
        }

        layout.minWidth = size.x;
        layout.preferredWidth = size.x;
        layout.minHeight = size.y;
        layout.preferredHeight = size.y;

        return button;
    }

    private static Image CreateImage(Transform parent, string objectName, Sprite sprite, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite != null)
        {
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private sealed class ModAbout
    {
        internal string Version;
        internal string GameBuild;
        internal string Author;
        internal string LiveToggle;
    }
}
