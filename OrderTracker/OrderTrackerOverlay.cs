using System.Collections.Generic;
using System.Text;
using DigDeliver;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OrderTracker;

internal sealed class OrderTrackerOverlay : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;
    private const float PanelWidth = 344f;
    private const float ExpandedHeight = 552f;
    private const float MinimizedHeight = 62f;

    private readonly Dictionary<int, OrderStatus> _previousOrderStatuses = new Dictionary<int, OrderStatus>();
    private readonly List<OrderRecord> _visibleOrders = new List<OrderRecord>();
    private GameObject _canvasObject;
    private GameObject _buttonLayerObject;
    private GameObject _contentObject;
    private GameObject _scrollObject;
    private RectTransform _panelRect;
    private RectTransform _buttonLayerRect;
    private Image _headerImage;
    private TMP_Text _title;
    private TMP_Text _toggleHint;
    private TMP_Text _minimizeLabel;
    private string _snapshot;
    private string _lastToggleHint;
    private float _nextRefresh;
    private bool _minimized;

    internal static OrderTrackerOverlay Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_canvasObject != null)
        {
            Destroy(_canvasObject);
        }

        if (_buttonLayerObject != null)
        {
            Destroy(_buttonLayerObject);
        }
    }

    private void Update()
    {
        // Reattach the tracker to the active game canvas, handle input, and refresh changed orders.
        GameUI gameUi = GameUI.Instance;
        if (gameUi == null || gameUi.CanvasRoot == null)
        {
            if (_canvasObject != null)
            {
                Destroy(_canvasObject);
                _canvasObject = null;
            }
            if (_buttonLayerObject != null)
            {
                Destroy(_buttonLayerObject);
                _buttonLayerObject = null;
            }

            return;
        }

        if (_canvasObject == null || _canvasObject.transform.parent != gameUi.CanvasRoot)
        {
            if (_canvasObject != null)
            {
                Destroy(_canvasObject);
            }
            if (_buttonLayerObject != null)
            {
                Destroy(_buttonLayerObject);
                _buttonLayerObject = null;
            }

            BuildUi(gameUi.CanvasRoot);
        }

        if (!SettingsPanel.IsRebinding
            && !OrderTrackerControls.IsRebinding
            && Plugin.ToggleAction.WasPressedThisFrame())
        {
            ToggleMinimized();
        }

        UpdateToggleHint();

        OrderState state = OrderState.Instance;
        Transform crateWindow = gameUi.CanvasRoot.Find("CrateChecklist");
        bool crateWindowOpen = CrateChecklist.ActiveCrate != null
            || (crateWindow != null && crateWindow.gameObject.activeInHierarchy);
        if (crateWindowOpen && crateWindow != null)
        {
            crateWindow.SetAsLastSibling();
        }

        bool showTracker = state != null
            && state.isSpawned
            && !crateWindowOpen;
        if (_canvasObject.activeSelf != showTracker || _buttonLayerObject.activeSelf != showTracker)
        {
            _canvasObject.SetActive(showTracker);
            _buttonLayerObject.SetActive(showTracker);
        }

        if (showTracker && !_minimized && !OrderTrackerControls.IsRebinding)
        {
            if (Plugin.ScrollUpAction.WasPressedThisFrame())
            {
                ScrollOrders(1);
            }
            else if (Plugin.ScrollDownAction.WasPressedThisFrame())
            {
                ScrollOrders(-1);
            }
        }

        if (Time.unscaledTime < _nextRefresh)
        {
            return;
        }

        _nextRefresh = Time.unscaledTime + RefreshInterval;
        if (state == null || !state.isSpawned)
        {
            _snapshot = null;
            _previousOrderStatuses.Clear();
            return;
        }

        string snapshot = CreateSnapshot(state);
        if (snapshot == _snapshot)
        {
            return;
        }

        _snapshot = snapshot;
        RefreshOrders(state);
    }

    private void BuildUi(RectTransform canvasRoot)
    {
        // Build the transparent panel, header, minimize control, and clipped scrolling order list.
        UITheme theme = UITheme.Instance;
        Color panelColor = theme.panelColor;
        panelColor.a = 0f;
        Image panel = CreateImage(canvasRoot, "OrderTrackerPanel", theme.panel, panelColor);
        _canvasObject = panel.gameObject;
        _panelRect = panel.rectTransform;
        _panelRect.anchorMin = new Vector2(1f, 0.5f);
        _panelRect.anchorMax = new Vector2(1f, 0.5f);
        _panelRect.pivot = new Vector2(1f, 0.5f);
        _panelRect.sizeDelta = new Vector2(PanelWidth, _minimized ? MinimizedHeight : ExpandedHeight);
        _panelRect.anchoredPosition = GetPanelPosition();
        CreateButtonLayer(canvasRoot, theme);

        _headerImage = CreateImage(panel.transform, "Header", theme.panel, GetHeaderColor(theme));
        Anchor(_headerImage.rectTransform, new Vector2(0f, -MinimizedHeight), Vector2.zero, new Vector2(0f, 1f), Vector2.one);
        _title = CreateText(
            panel.transform,
            "Title",
            "Order Tracker",
            20f,
            theme.textColor,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        Anchor(_title.rectTransform, new Vector2(22f, -34f), new Vector2(-68f, -6f), new Vector2(0f, 1f), Vector2.one);

        _toggleHint = CreateText(
            panel.transform,
            "ToggleHint",
            string.Empty,
            11f,
            theme.mutedTextColor,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        Anchor(_toggleHint.rectTransform, new Vector2(22f, -58f), new Vector2(-68f, -34f), new Vector2(0f, 1f), Vector2.one);

        Button minimize = CreateButton(_buttonLayerRect, theme);
        RectTransform minimizeRect = minimize.GetComponent<RectTransform>();
        minimizeRect.anchorMin = Vector2.one;
        minimizeRect.anchorMax = Vector2.one;
        minimizeRect.pivot = Vector2.one;
        minimizeRect.sizeDelta = new Vector2(46f, 42f);
        minimizeRect.anchoredPosition = new Vector2(-12f, -10f);
        _minimizeLabel = minimize.GetComponentInChildren<TMP_Text>();
        _minimizeLabel.text = _minimized ? "+" : "-";
        minimize.onClick.AddListener(ToggleMinimized);

        _scrollObject = new GameObject(
            "OrdersScroll",
            typeof(RectTransform),
            typeof(ScrollRect));
        _scrollObject.transform.SetParent(panel.transform, false);
        RectTransform scrollRect = _scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(12f, 12f);
        scrollRect.offsetMax = new Vector2(-12f, -MinimizedHeight);

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(RectMask2D));
        viewportObject.transform.SetParent(_scrollObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMax = new Vector2(-18f, 0f);

        GameObject scrollbarObject = new GameObject(
            "VerticalScrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.transform.SetParent(_scrollObject.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(8f, 0f);
        scrollbarRect.anchoredPosition = new Vector2(-3f, 0f);
        Image scrollbarTrack = scrollbarObject.GetComponent<Image>();
        scrollbarTrack.color = new Color(0.88f, 0.82f, 0.69f, 1f);
        scrollbarTrack.raycastTarget = true;

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(scrollbarObject.transform, false);
        Image scrollbarHandle = handleObject.GetComponent<Image>();
        scrollbarHandle.color = new Color(0.48f, 0.46f, 0.42f, 1f);
        scrollbarHandle.raycastTarget = true;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = scrollbarHandle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;
        ColorBlock scrollbarColors = scrollbar.colors;
        scrollbarColors.normalColor = Color.white;
        scrollbarColors.highlightedColor = Color.white;
        scrollbarColors.pressedColor = Color.white;
        scrollbarColors.selectedColor = Color.white;
        scrollbar.colors = scrollbarColors;

        _contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        _contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = _contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup listLayout = _contentObject.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 7f;
        listLayout.padding = new RectOffset(6, 6, 6, 6);
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = _contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = _scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = 3f;

        _canvasObject.SetActive(false);
        _buttonLayerObject.SetActive(false);
        _snapshot = null;
        PlaceAboveCrateBelowMenus(canvasRoot, panel.transform);
    }

    private void CreateButtonLayer(RectTransform canvasRoot, UITheme theme)
    {
        // Keep the minimize button on a separate top layer so it stays clickable over menus.
        _buttonLayerObject = new GameObject("OrderTrackerButtonLayer", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _buttonLayerObject.transform.SetParent(canvasRoot, false);
        _buttonLayerRect = _buttonLayerObject.GetComponent<RectTransform>();
        _buttonLayerRect.anchorMin = new Vector2(1f, 0.5f);
        _buttonLayerRect.anchorMax = new Vector2(1f, 0.5f);
        _buttonLayerRect.pivot = new Vector2(1f, 0.5f);
        _buttonLayerRect.sizeDelta = new Vector2(PanelWidth, _minimized ? MinimizedHeight : ExpandedHeight);
        _buttonLayerRect.anchoredPosition = GetPanelPosition();

        Canvas buttonCanvas = _buttonLayerObject.GetComponent<Canvas>();
        Canvas gameCanvas = canvasRoot.GetComponentInParent<Canvas>();
        buttonCanvas.overrideSorting = true;
        buttonCanvas.sortingLayerID = gameCanvas.sortingLayerID;
        buttonCanvas.sortingOrder = gameCanvas.sortingOrder + 1;
    }

    private Vector2 GetPanelPosition()
    {
        float height = _minimized ? MinimizedHeight : ExpandedHeight;
        return new Vector2(-30f, 40f + (ExpandedHeight - height) * 0.5f);
    }

    private Color GetHeaderColor(UITheme theme)
    {
        Color color = theme.panelColor;
        color.a = _minimized ? 0.34f : 0.68f;
        return color;
    }

    private static void PlaceAboveCrateBelowMenus(RectTransform canvasRoot, Transform panel)
    {
        // Order the tracker behind open menus but immediately above the crate checklist.
        string[] menuNames =
        {
            "Backpack",
            "Station",
            "Chest",
            "Trash",
            "Town",
            "TownFinale",
            "Terminal",
            "Mailbox",
            "SkillTree",
            "Pause",
            "Settings",
            "Wardrobe",
            "Loading"
        };

        int logicalChildCount = 0;
        int crateIndex = -1;
        int firstActiveMenuIndex = int.MaxValue;
        for (int childIndex = 0; childIndex < canvasRoot.childCount; childIndex++)
        {
            Transform child = canvasRoot.GetChild(childIndex);
            if (child == panel)
            {
                continue;
            }

            if (child.name == "CrateChecklist")
            {
                crateIndex = logicalChildCount;
            }

            if (child.gameObject.activeInHierarchy && System.Array.IndexOf(menuNames, child.name) >= 0)
            {
                firstActiveMenuIndex = Mathf.Min(firstActiveMenuIndex, logicalChildCount);
            }

            logicalChildCount++;
        }

        int targetIndex = crateIndex >= 0 ? crateIndex + 1 : logicalChildCount;
        targetIndex = Mathf.Min(targetIndex, firstActiveMenuIndex);
        targetIndex = Mathf.Clamp(targetIndex, 0, canvasRoot.childCount - 1);
        if (panel.GetSiblingIndex() != targetIndex)
        {
            panel.SetSiblingIndex(targetIndex);
        }
    }

    private string CreateSnapshot(OrderState state)
    {
        // Include order progress and stored item counts so the UI rebuilds only when data changes.
        StringBuilder builder = new StringBuilder();
        GameDatabase database = GameDatabase.Instance;
        PlayerInventory backpack = PlayerAvatar.Local != null ? PlayerAvatar.Local.Inventory : null;
        SharedChest chest = SharedChest.Instance;
        foreach (OrderRecord order in state.orders)
        {
            if (order.status != OrderStatus.Active && order.status != OrderStatus.Packed)
            {
                continue;
            }

            builder.Append(order.id).Append(':')
                .Append((byte)order.status).Append(':')
                .Append(order.crate).Append(':');
            for (int line = 0; line < order.LineCount; line++)
            {
                ushort itemId = order.Item(line);
                ItemDefinition item = database != null ? database.Item(itemId) : null;
                builder.Append(itemId).Append(',')
                    .Append(order.Need(line)).Append(',')
                    .Append(order.Have(line)).Append(',')
                    .Append(item != null && backpack != null ? backpack.Count(item) : 0).Append(',')
                    .Append(item != null && chest != null ? chest.Count(item) : 0).Append(';');
            }
            builder.Append('|');
        }

        return builder.ToString();
    }

    private void RefreshOrders(OrderState state)
    {
        // Keep unfinished orders first and packed crates last, then rebuild their cards.
        List<int> newlyPackedOrderIds = new List<int>();
        Dictionary<int, OrderStatus> currentStatuses = new Dictionary<int, OrderStatus>();
        foreach (OrderRecord order in state.orders)
        {
            if (order.status != OrderStatus.Active && order.status != OrderStatus.Packed)
            {
                continue;
            }

            if (order.status == OrderStatus.Packed
                && _previousOrderStatuses.TryGetValue(order.id, out OrderStatus previousStatus)
                && previousStatus == OrderStatus.Active)
            {
                newlyPackedOrderIds.Add(order.id);
            }

            currentStatuses[order.id] = order.status;
        }

        _previousOrderStatuses.Clear();
        foreach (KeyValuePair<int, OrderStatus> status in currentStatuses)
        {
            _previousOrderStatuses.Add(status.Key, status.Value);
        }

        _visibleOrders.Clear();
        foreach (OrderRecord order in state.orders)
        {
            if (order.status == OrderStatus.Active)
            {
                _visibleOrders.Add(order);
            }
        }
        foreach (OrderRecord order in state.orders)
        {
            if (order.status == OrderStatus.Packed)
            {
                _visibleOrders.Add(order);
            }
        }

        UITheme theme = UITheme.Instance;
        _title.text = _visibleOrders.Count > 0
            ? $"Order Tracker ({_visibleOrders.Count})"
            : "Order Tracker";

        ClearContent();
        if (_visibleOrders.Count == 0)
        {
            TMP_Text empty = CreateText(
                _contentObject.transform,
                "Empty",
                "Accept an order to track its crate.",
                15f,
                theme.mutedTextColor,
                FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetPreferredHeight(empty.gameObject, 44f);
            return;
        }

        GameDatabase database = GameDatabase.Instance;
        foreach (OrderRecord order in _visibleOrders)
        {
            BuildOrderCard(order, database, theme);
        }

        ScrollToHiddenUnfinishedOrder(newlyPackedOrderIds);
    }

    private void ScrollToHiddenUnfinishedOrder(List<int> newlyPackedOrderIds)
    {
        // When a crate becomes ready, reveal the next unfinished crate if it is below the viewport.
        if (newlyPackedOrderIds.Count == 0)
        {
            return;
        }

        ScrollRect scroll = _scrollObject.GetComponent<ScrollRect>();
        RectTransform viewport = scroll.viewport;
        RectTransform content = scroll.content;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        for (int readyIndex = 0; readyIndex < _visibleOrders.Count; readyIndex++)
        {
            if (!newlyPackedOrderIds.Contains(_visibleOrders[readyIndex].id))
            {
                continue;
            }

            for (int orderIndex = readyIndex + 1; orderIndex < _visibleOrders.Count; orderIndex++)
            {
                OrderRecord order = _visibleOrders[orderIndex];
                if (order.status != OrderStatus.Active)
                {
                    continue;
                }

                RectTransform card = content.Find($"Order_{order.id}") as RectTransform;
                if (card == null)
                {
                    break;
                }

                Vector3[] corners = new Vector3[4];
                card.GetWorldCorners(corners);
                float cardBottom = viewport.InverseTransformPoint(corners[0]).y;
                float viewportBottom = viewport.rect.yMin;
                if (cardBottom < viewportBottom)
                {
                    float hiddenLength = content.rect.height - viewport.rect.height;
                    if (hiddenLength > 0f)
                    {
                        float scrollDistance = viewportBottom - cardBottom;
                        scroll.verticalNormalizedPosition = Mathf.Clamp01(
                            scroll.verticalNormalizedPosition - scrollDistance / hiddenLength);
                        return;
                    }
                }
            }
        }
    }

    private void BuildOrderCard(OrderRecord order, GameDatabase database, UITheme theme)
    {
        // Render remaining requirements, or a ready-to-ship state when the order is complete.
        Image card = CreateImage(_contentObject.transform, $"Order_{order.id}", theme.slot, theme.panelColor);
        GameObject cardObject = card.gameObject;
        VerticalLayoutGroup layout = cardObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 7, 7);
        layout.spacing = 1f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        bool readyToShip = order.status == OrderStatus.Packed;
        Transform headingParent = cardObject.transform;
        if (readyToShip)
        {
            GameObject headingRow = new GameObject(
                "HeadingRow",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            headingRow.transform.SetParent(cardObject.transform, false);
            HorizontalLayoutGroup headingLayout = headingRow.GetComponent<HorizontalLayoutGroup>();
            headingLayout.spacing = 6f;
            headingLayout.childAlignment = TextAnchor.MiddleLeft;
            headingLayout.childControlWidth = true;
            headingLayout.childControlHeight = true;
            headingLayout.childForceExpandWidth = false;
            headingLayout.childForceExpandHeight = false;
            SetPreferredHeight(headingRow, 24f);
            headingParent = headingRow.transform;
        }

        TMP_Text heading = CreateText(
            headingParent,
            "CrateHeading",
            order.crate >= 0
                ? $"Crate {order.crate + 1}  ·  {CustomerBook.CustomerName(order.customer)}"
                : "Order",
            16f,
            theme.accentColor,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        SetPreferredHeight(heading.gameObject, 24f);
        if (readyToShip)
        {
            LayoutElement headingWidth = heading.gameObject.AddComponent<LayoutElement>();
            headingWidth.flexibleWidth = 1f;
            headingWidth.minWidth = 0f;
            heading.overflowMode = TextOverflowModes.Truncate;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 10f;
            heading.fontSizeMax = 16f;

            TMP_Text readyLabel = CreateText(
                headingParent,
                "Ready",
                "Ready",
                12f,
                theme.goodColor,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);
            SetPreferredHeight(readyLabel.gameObject, 24f);
        }

        int remainingLineCount = 0;
        for (int line = 0; line < order.LineCount; line++)
        {
            int remaining = order.Need(line) - order.Have(line);
            if (remaining > 0)
            {
                remainingLineCount++;
                ushort itemId = order.Item(line);
                ItemDefinition item = database != null ? database.Item(itemId) : null;
                string itemName = item != null ? item.Name : $"Item {itemId}";
                int backpackCount = item != null && PlayerAvatar.Local != null
                    ? PlayerAvatar.Local.Inventory.Count(item)
                    : 0;
                int chestCount = item != null && SharedChest.Instance != null
                    ? SharedChest.Instance.Count(item)
                    : 0;
                BuildItemRow(
                    cardObject.transform,
                    item,
                    itemName,
                    order.Have(line),
                    order.Need(line),
                    remaining,
                    chestCount,
                    backpackCount,
                    theme);
            }
        }

        if (remainingLineCount == 0 && !readyToShip)
        {
            TMP_Text ready = CreateText(
                cardObject.transform,
                "Ready",
                "Items complete",
                15f,
                theme.goodColor,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            SetPreferredHeight(ready.gameObject, 22f);
        }

        Color cardColor = theme.panelInnerColor;
        cardColor.a = 0.68f;
        card.color = cardColor;
        card.raycastTarget = false;
        int rowCount = remainingLineCount > 0 ? remainingLineCount : 1;
        int childCount = readyToShip ? 1 : 1 + rowCount;
        float cardHeight = readyToShip
            ? 24f + 14f
            : 24f
                + (remainingLineCount > 0 ? remainingLineCount * 62f : 22f)
                + layout.spacing * Mathf.Max(0, childCount - 1)
                + 14f;
        SetPreferredHeight(cardObject, cardHeight);
    }

    private void BuildItemRow(
        Transform parent,
        ItemDefinition item,
        string itemName,
        int have,
        int need,
        int remaining,
        int chestCount,
        int backpackCount,
        UITheme theme)
    {
        // Show the required progress and available backpack/chest quantities for one order item.
        GameObject rowObject = new GameObject(
            $"Item_{(item != null ? item.id : 0)}",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement rowSize = rowObject.GetComponent<LayoutElement>();
        rowSize.minHeight = 62f;
        rowSize.preferredHeight = 62f;

        if (item != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(rowObject.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = item.icon;
            icon.color = item.icon != null ? Color.white : item.tint;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = item.icon != null;

            LayoutElement iconSize = iconObject.GetComponent<LayoutElement>();
            iconSize.minWidth = 64f;
            iconSize.preferredWidth = 64f;
            iconSize.minHeight = 60f;
            iconSize.preferredHeight = 60f;
        }

        GameObject detailsObject = new GameObject(
            "ItemDetails",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        detailsObject.transform.SetParent(rowObject.transform, false);
        VerticalLayoutGroup detailsLayout = detailsObject.GetComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 1f;
        detailsLayout.childAlignment = TextAnchor.MiddleLeft;
        detailsLayout.childControlWidth = true;
        detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        LayoutElement detailsSize = detailsObject.GetComponent<LayoutElement>();
        detailsSize.flexibleWidth = 1f;
        detailsSize.minHeight = 52f;
        detailsSize.preferredHeight = 52f;

        TMP_Text label = CreateText(
            detailsObject.transform,
            "ItemCount",
            $"{itemName}  {have}/{need}  ({backpackCount})",
            16f,
            theme.textColor,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        SetPreferredHeight(label.gameObject, 28f);
        label.overflowMode = TextOverflowModes.Truncate;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text storageCounts = CreateText(
            detailsObject.transform,
            "StorageCounts",
            $"Chest: {chestCount}",
            13f,
            theme.mutedTextColor,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetPreferredHeight(storageCounts.gameObject, 22f);
        storageCounts.overflowMode = TextOverflowModes.Truncate;
        storageCounts.textWrappingMode = TextWrappingModes.NoWrap;
    }

    internal void ToggleMinimized()
    {
        // Collapse to the header or restore the full panel while keeping its screen position stable.
        _minimized = !_minimized;
        _scrollObject.SetActive(!_minimized);
        _panelRect.sizeDelta = new Vector2(PanelWidth, _minimized ? MinimizedHeight : ExpandedHeight);
        _buttonLayerRect.sizeDelta = new Vector2(PanelWidth, _minimized ? MinimizedHeight : ExpandedHeight);
        _panelRect.anchoredPosition = GetPanelPosition();
        _buttonLayerRect.anchoredPosition = GetPanelPosition();
        _headerImage.color = GetHeaderColor(UITheme.Instance);
        _minimizeLabel.text = _minimized ? "+" : "-";
    }

    private void ScrollOrders(int direction)
    {
        // Move the list by one fixed step in response to the configured mouse buttons.
        const float scrollStep = 0.12f;
        ScrollRect scroll = _scrollObject.GetComponent<ScrollRect>();
        scroll.verticalNormalizedPosition = Mathf.Clamp01(
            scroll.verticalNormalizedPosition + direction * scrollStep);
    }

    private void UpdateToggleHint()
    {
        // Reflect the current key binding in the on-screen toggle prompt.
        string binding = Plugin.ToggleAction.GetBindingDisplayString(0);
        if (binding == _lastToggleHint)
        {
            return;
        }

        _lastToggleHint = binding;
        _toggleHint.text = $"Press {binding} to toggle";
    }

    private void ClearContent()
    {
        for (int index = _contentObject.transform.childCount - 1; index >= 0; index--)
        {
            Destroy(_contentObject.transform.GetChild(index).gameObject);
        }
    }

    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        string value,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = UITheme.Instance.font;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Button CreateButton(Transform parent, UITheme theme)
    {
        GameObject buttonObject = new GameObject(
            "MinimizeButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = theme.button;
        image.color = Color.clear;
        if (theme.button != null)
        {
            image.type = Image.Type.Sliced;
        }

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.2f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = Color.clear;
        button.colors = colors;

        TMP_Text label = CreateText(
            buttonObject.transform,
            "Label",
            "-",
            20f,
            theme.textColor,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    private static void SetPreferredHeight(GameObject gameObject, float height)
    {
        LayoutElement layout = gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static void Anchor(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
