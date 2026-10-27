using DigDeliver;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ExtendedStats;

internal sealed class ExtendedStatsWindow : MonoBehaviour
{
    private enum SortColumn
    {
        Item,
        Broken,
        PickedUp,
        Crafted,
        Placed,
        Sold
    }

    private static ExtendedStatsWindow _instance;
    private Transform _content;
    private TMP_Text _totalLabel;
    private Button _playerScopeButton;
    private Button _worldScopeButton;
    private readonly Dictionary<SortColumn, TMP_Text> _headerLabels = new Dictionary<SortColumn, TMP_Text>();
    private StatsScope _scope = StatsScope.Player;
    private SortColumn _sortColumn = SortColumn.Broken;
    private bool _sortDescending = true;

    internal static void Open()
    {
        // Reopen and refresh the existing viewer instead of creating duplicate windows.
        if (_instance != null)
        {
            _instance.gameObject.SetActive(true);
            _instance.Refresh();
            return;
        }

        GameObject root = new GameObject("ExtendedStatsWindow");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<ExtendedStatsWindow>();
        _instance.Build();
    }

    internal static void Close()
    {
        // Destroy the viewer and clear its singleton reference.
        if (_instance == null)
        {
            return;
        }

        Destroy(_instance.gameObject);
        _instance = null;
    }

    internal static void RefreshIfOpen()
    {
        if (_instance != null && _instance.gameObject.activeInHierarchy)
        {
            _instance.Refresh();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        // Escape is the keyboard shortcut for closing the statistics viewer.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void Build()
    {
        // Create the modal canvas, scope selector, sortable headings, and scrollable data list.
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Image backdrop = UIFactory.Image(canvasObject.transform, "Backdrop", null, new Color(0f, 0f, 0f, 0.68f));
        backdrop.rectTransform.Stretch();

        UITheme theme = UITheme.Instance;
        Image panel = UIFactory.Panel(canvasObject.transform, "Panel");
        RectTransform panelRect = panel.rectTransform;
        panelRect.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160f, 670f));
        UIFactory.VLayout(panel.gameObject, 6f, new RectOffset(26, 26, 20, 20));

        RectTransform titleBar = UIFactory.Rect(panel.transform, "TitleBar");
        UIFactory.Size(titleBar, -1f, 42f);
        TMP_Text title = UIFactory.Text(titleBar, "Stats", 32f, null, TextAlignmentOptions.Center, FontStyles.Bold);
        title.rectTransform.Stretch();
        Button closeButton = UIFactory.Button(titleBar, "Close", Close, theme.warnColor, 18f);
        RectTransform closeRect = (RectTransform)closeButton.transform;
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(104f, 38f);
        closeRect.anchoredPosition = Vector2.zero;

        BuildScopeSelector(titleBar, theme);

        _totalLabel = UIFactory.Text(panel.transform, string.Empty, 20f, theme.accentColor, TextAlignmentOptions.Center, FontStyles.Bold);
        UIFactory.Size(_totalLabel, -1f, 36f);

        BuildHeader(panel.transform, theme);

        RectTransform listRect = UIFactory.Rect(panel.transform, "StatsList");
        UIFactory.Size(listRect, -1f, 430f);
        RectTransform content = UIFactory.Scroll(listRect, "List");
        content.parent.GetComponent<RectTransform>().Stretch();
        ScrollRect scroll = content.parent.GetComponent<ScrollRect>();
        scroll.vertical = true;
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;
        _content = content;

        Refresh();
    }

    private void BuildScopeSelector(Transform parent, UITheme theme)
    {
        // Add buttons for switching between local-player and combined world totals.
        RectTransform selector = UIFactory.Rect(parent, "StatsScopeSelector");
        selector.anchorMin = new Vector2(0f, 0.5f);
        selector.anchorMax = new Vector2(0f, 0.5f);
        selector.pivot = new Vector2(0f, 0.5f);
        selector.anchoredPosition = new Vector2(26f, 0f);

        HorizontalLayoutGroup layout = selector.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        _playerScopeButton = UIFactory.Button(
            selector,
            "Player",
            () => SelectScope(StatsScope.Player),
            theme.panelInnerColor,
            20f);
        Vector2 playerButtonSize = FitScopeButtonToLabel(_playerScopeButton);
        _worldScopeButton = UIFactory.Button(
            selector,
            "World",
            () => SelectScope(StatsScope.World),
            theme.panelInnerColor,
            20f);
        Vector2 worldButtonSize = FitScopeButtonToLabel(_worldScopeButton);
        selector.sizeDelta = new Vector2(playerButtonSize.x + layout.spacing + worldButtonSize.x, Mathf.Max(playerButtonSize.y, worldButtonSize.y));
        RefreshScopeSelector();
    }

    private static Vector2 FitScopeButtonToLabel(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        Vector2 textSize = label.GetPreferredValues();
        Vector2 buttonSize = textSize + new Vector2(16f, 8f);
        ((RectTransform)button.transform).sizeDelta = buttonSize;
        return buttonSize;
    }

    private void BuildHeader(Transform parent, UITheme theme)
    {
        // Create one sortable column heading for each recorded metric.
        Image header = UIFactory.Image(parent, "StatsHeader", null, theme.panelInnerColor);
        UIFactory.Size(header, -1f, 60f);
        HorizontalLayoutGroup layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 2, 2);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        AddSortHeader(header.transform, SortColumn.Item, "ITEM", 340f, 0f, TextAlignmentOptions.MidlineLeft, theme);
        AddSortHeader(header.transform, SortColumn.Broken, "BROKEN", 120f, 1f, TextAlignmentOptions.Center, theme);
        AddSortHeader(header.transform, SortColumn.PickedUp, "PICKED UP", 120f, 1f, TextAlignmentOptions.Center, theme);
        AddSortHeader(header.transform, SortColumn.Crafted, "CRAFTED", 120f, 1f, TextAlignmentOptions.Center, theme);
        AddSortHeader(header.transform, SortColumn.Placed, "PLACED", 120f, 1f, TextAlignmentOptions.Center, theme);
        AddSortHeader(header.transform, SortColumn.Sold, "SOLD", 120f, 1f, TextAlignmentOptions.Center, theme);
    }

    private void Refresh()
    {
        // Rebuild the table from the selected scope so live changes appear immediately.
        if (_content == null)
        {
            return;
        }

        _totalLabel.text = $"{(_scope == StatsScope.Player ? "Player" : "World")} total blocks broken: {BlockStatsStore.Total(_scope):N0}";
        for (int index = _content.childCount - 1; index >= 0; index--)
        {
            Destroy(_content.GetChild(index).gameObject);
        }

        UITheme theme = UITheme.Instance;
        List<BlockStatEntry> entries = BlockStatsStore.GetEntries(_scope);
        RefreshSortHeaders(entries);
        entries.Sort(CompareEntries);
        foreach (BlockStatEntry entry in entries)
        {
            Image row = UIFactory.Image(_content, $"Item_{entry.Key}", null, theme.panelInnerColor);
            UIFactory.Size(row, -1f, 48f);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 2, 2);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            AddItemColumn(row.transform, entry, theme);
            AddColumn(row.transform, entry.Broken.ToString("N0"), 120f, 1f, theme.textColor, TextAlignmentOptions.Center, false);
            AddColumn(row.transform, entry.PickedUp.ToString("N0"), 120f, 1f, theme.textColor, TextAlignmentOptions.Center, false);
            AddColumn(row.transform, entry.Crafted.ToString("N0"), 120f, 1f, theme.textColor, TextAlignmentOptions.Center, false);
            AddColumn(row.transform, entry.Placed.ToString("N0"), 120f, 1f, theme.textColor, TextAlignmentOptions.Center, false);
            AddColumn(row.transform, entry.Sold.ToString("N0"), 120f, 1f, theme.textColor, TextAlignmentOptions.Center, false);
        }
    }

    private void SelectScope(StatsScope scope)
    {
        // Update the active scope and redraw only when the selection actually changes.
        if (_scope == scope)
        {
            return;
        }

        _scope = scope;
        RefreshScopeSelector();
        Refresh();
    }

    private void RefreshScopeSelector()
    {
        if (_playerScopeButton == null || _worldScopeButton == null)
        {
            return;
        }

        Color selectedColor = new Color(0.66f, 0.28f, 0.08f, 1f);
        Color unselectedColor = new Color(0.94f, 0.86f, 0.72f, 1f);
        Color selectedTextColor = Color.white;
        Color unselectedTextColor = new Color(0.29f, 0.2f, 0.14f, 1f);
        SetScopeButtonStyle(_playerScopeButton, _scope == StatsScope.Player, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
        SetScopeButtonStyle(_worldScopeButton, _scope == StatsScope.World, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
    }

    private static void SetScopeButtonStyle(
        Button button,
        bool selected,
        Color selectedColor,
        Color unselectedColor,
        Color selectedTextColor,
        Color unselectedTextColor)
    {
        button.image.color = selected ? selectedColor : unselectedColor;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.color = selected ? selectedTextColor : unselectedTextColor;
        }
    }

    private void AddSortHeader(
        Transform parent,
        SortColumn column,
        string title,
        float width,
        float flexibleWidth,
        TextAlignmentOptions alignment,
        UITheme theme)
    {
        GameObject headerButton = new GameObject("Sort_" + column, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        headerButton.transform.SetParent(parent, false);

        LayoutElement element = headerButton.GetComponent<LayoutElement>();
        if (width > 0f)
        {
            element.minWidth = width;
            element.preferredWidth = width;
        }

        element.flexibleWidth = flexibleWidth;
        Image background = headerButton.GetComponent<Image>();
        background.color = new Color(theme.panelInnerColor.r, theme.panelInnerColor.g, theme.panelInnerColor.b, 0f);
        Button button = headerButton.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectSortColumn(column));

        TMP_Text label = UIFactory.Text(headerButton.transform, title, 20f, theme.accentColor, alignment, FontStyles.Bold);
        label.rectTransform.Stretch();
        label.raycastTarget = false;
        _headerLabels[column] = label;
    }

    private void SelectSortColumn(SortColumn column)
    {
        // A repeated click reverses direction; choosing a new column starts descending.
        if (_sortColumn == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumn = column;
            _sortDescending = true;
        }

        Refresh();
    }

    private void RefreshSortHeaders(List<BlockStatEntry> entries)
    {
        // Show the active sort arrow and metric totals directly in the column headings.
        foreach (KeyValuePair<SortColumn, TMP_Text> header in _headerLabels)
        {
            string title = header.Key switch
            {
                SortColumn.Item => "ITEM",
                SortColumn.Broken => "BROKEN",
                SortColumn.PickedUp => "PICKED UP",
                SortColumn.Crafted => "CRAFTED",
                SortColumn.Placed => "PLACED",
                SortColumn.Sold => "SOLD",
                _ => throw new ArgumentOutOfRangeException()
            };

            if (header.Key == _sortColumn)
            {
                title += _sortDescending ? " ↓" : " ↑";
            }

            long total = header.Key switch
            {
                SortColumn.Broken => entries.Sum(entry => entry.Broken),
                SortColumn.PickedUp => entries.Sum(entry => entry.PickedUp),
                SortColumn.Crafted => entries.Sum(entry => entry.Crafted),
                SortColumn.Placed => entries.Sum(entry => entry.Placed),
                SortColumn.Sold => entries.Sum(entry => entry.Sold),
                _ => -1
            };

            if (total >= 0)
            {
                string unit = header.Key == SortColumn.Broken ? "blocks broken" : header.Key switch
                {
                    SortColumn.PickedUp => "items picked up",
                    SortColumn.Crafted => "items crafted",
                    SortColumn.Placed => "items placed",
                    SortColumn.Sold => "items sold",
                    _ => string.Empty
                };
                title = $"<size=20>{title}</size>\n<size=16>({total:N0} {unit})</size>";
            }
            else
            {
                title = $"<size=20>{title}</size>";
            }

            header.Value.text = title;
        }
    }

    private int CompareEntries(BlockStatEntry left, BlockStatEntry right)
    {
        // Apply the selected column and direction, using the item key as a stable tie-breaker.
        int comparison = _sortColumn switch
        {
            SortColumn.Item => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name),
            SortColumn.Broken => left.Broken.CompareTo(right.Broken),
            SortColumn.PickedUp => left.PickedUp.CompareTo(right.PickedUp),
            SortColumn.Crafted => left.Crafted.CompareTo(right.Crafted),
            SortColumn.Placed => left.Placed.CompareTo(right.Placed),
            SortColumn.Sold => left.Sold.CompareTo(right.Sold),
            _ => 0
        };

        if (_sortDescending)
        {
            comparison = -comparison;
        }

        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(left.Key, right.Key);
    }

    private static void AddItemColumn(Transform parent, BlockStatEntry entry, UITheme theme)
    {
        GameObject column = new GameObject("ItemName", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        column.transform.SetParent(parent, false);

        LayoutElement element = column.GetComponent<LayoutElement>();
        element.minWidth = 340f;
        element.preferredWidth = 340f;

        HorizontalLayoutGroup layout = column.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (entry.Icon != null)
        {
            Image icon = UIFactory.Image(column.transform, "Icon", entry.Icon, Color.white);
            icon.preserveAspect = true;
            UIFactory.Size(icon, 32f, 32f);
        }

        AddColumn(column.transform, entry.Name, -1f, 1f, theme.textColor, TextAlignmentOptions.MidlineLeft, false);
    }

    private static void AddColumn(
        Transform parent,
        string value,
        float width,
        float flexibleWidth,
        Color color,
        TextAlignmentOptions alignment,
        bool bold)
    {
        GameObject column = new GameObject("Column", typeof(RectTransform), typeof(LayoutElement));
        column.transform.SetParent(parent, false);
        LayoutElement element = column.GetComponent<LayoutElement>();
        if (width > 0f)
        {
            element.minWidth = width;
            element.preferredWidth = width;
        }

        element.flexibleWidth = flexibleWidth;
        TMP_Text text = UIFactory.Text(column.transform, value, bold ? 16f : 17f, color, alignment, bold ? FontStyles.Bold : FontStyles.Normal);
        text.rectTransform.Stretch();
    }
}
