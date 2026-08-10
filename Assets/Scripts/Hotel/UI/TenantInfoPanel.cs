using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class TenantInfoPanel : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    public enum PanelMode { Hidden, Hover, Pinned }

    public enum DisplaySource { None, ListItem, RoomSlot }

    public static event System.Action<string, int> TenantFlagChanged;

    [Header("UI References")]
    public TextMeshProUGUI nameLabel;
    public Image tenantImage;
    public GameObject tagPanel;
    public GameObject tagPrefab;
    public string tagTextPath = "Text (TMP)";
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI shortDescriptionLabel;
    public TextMeshProUGUI detailedDescriptionLabel;
    public TenantLogPanelController tenantLogPanel;

    [Header("Player Flag")]
    public TMP_Dropdown flagDropdown;
    public TextMeshProUGUI flagLabel;
    public Image flagBackground;
    public Color[] flagColors;
    public string[] flagLabels;

    public PanelMode Mode { get; private set; } = PanelMode.Hidden;
    public DisplaySource Source { get; private set; } = DisplaySource.None;
    public bool IsPointerOver { get; private set; }
    public bool IsShowing => gameObject.activeSelf;
    public string CurrentTenantId => _currentTenantId;

    private Canvas _canvas;
    private RectTransform _selfRect;
    private CanvasGroup _canvasGroup;
    private TMP_Dropdown _dropdown;
    private string _currentTenantId;
    private bool _suppressFlagWrite;
    private Color _defaultFlagBackgroundColor;
    private Sprite _defaultAvatarSprite;

    private void Awake()
    {
        _dropdown = flagDropdown;
        if (flagBackground != null)
            _defaultFlagBackgroundColor = flagBackground.color;
        if (tenantImage != null)
            _defaultAvatarSprite = tenantImage.sprite;
        if (flagDropdown != null)
            flagDropdown.onValueChanged.AddListener(OnFlagChanged);
    }

    private void OnDestroy()
    {
        if (flagDropdown != null)
            flagDropdown.onValueChanged.RemoveListener(OnFlagChanged);
    }

    private void Update()
    {
        if (Mode != PanelMode.Pinned)
            return;
        if (!Input.GetMouseButtonDown(0))
            return;

        List<RaycastResult> hits = RaycastAllUnderPointer();
        if (hits == null)
        {
            Hide();
            return;
        }
        for (int i = 0; i < hits.Count; i++)
        {
            GameObject hitObject = hits[i].gameObject;
            if (hitObject == null)
                continue;
            if (IsInternalHit(hitObject))
                return;
            if (hits[i].module is GraphicRaycaster)
            {
                Hide();
                return;
            }
        }
        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].module is Physics2DRaycaster)
                return;
        }
        Hide();
    }

    public void ShowHover(string tenantId, Vector2 screenPoint, bool preferLeft, DisplaySource source)
    {
        EnsureInitialized();
        if (!FillContent(tenantId))
        {
            Hide();
            return;
        }
        _currentTenantId = tenantId;
        Mode = PanelMode.Hover;
        Source = source;
        ApplyInteractionMode();
        gameObject.SetActive(true);
        ApplyFlagToPanel(tenantId);
        PositionAt(screenPoint, preferLeft);
    }

    public void ShowPinned(string tenantId, Vector2 screenPoint, DisplaySource source)
    {
        EnsureInitialized();
        if (!FillContent(tenantId))
        {
            Hide();
            return;
        }
        _currentTenantId = tenantId;
        Mode = PanelMode.Pinned;
        Source = source;
        if (tenantLogPanel != null)
            tenantLogPanel.RefreshForTenant(tenantId);
        ApplyInteractionMode();
        gameObject.SetActive(true);
        ApplyFlagToPanel(tenantId);
        PositionAt(screenPoint, false);
    }

    public void Hide()
    {
        Mode = PanelMode.Hidden;
        Source = DisplaySource.None;
        if (tenantLogPanel != null)
            tenantLogPanel.ClearLog();
        _currentTenantId = null;
        IsPointerOver = false;
        ApplyInteractionMode();
        gameObject.SetActive(false);
    }

    public bool IsInternalHit(GameObject hitObject)
    {
        if (hitObject == null)
            return false;
        if (hitObject.transform.IsChildOf(transform))
            return true;
        Transform listRoot = GetActiveDropdownListRoot();
        if (listRoot != null && hitObject.transform.IsChildOf(listRoot))
            return true;
        return false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsPointerOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsPointerOver = false;
    }

    private void EnsureInitialized()
    {
        if (_selfRect == null)
            _selfRect = GetComponent<RectTransform>();
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
    }

    private void ApplyInteractionMode()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = Mode == PanelMode.Pinned;
    }

    private bool FillContent(string tenantId)
    {
        TenantReviewCandidateSO candidate = FindCandidate(tenantId);
        if (candidate == null)
            return false;
        if (nameLabel != null)
            nameLabel.text = candidate.displayName;
        if (tenantImage != null)
        {
            Sprite resolved = ResolveAvatarByKey(tenantId);
            if (resolved == null && candidate.portrait != null)
                resolved = candidate.portrait;

            tenantImage.sprite = resolved != null ? resolved : _defaultAvatarSprite;
            tenantImage.color = resolved != null ? Color.white : candidate.avatarColor;
        }
        RefreshTagPanel(candidate.ability);
        if (titleLabel != null)
            titleLabel.text = GetActivityLabel(candidate.activityType);
        if (shortDescriptionLabel != null)
            shortDescriptionLabel.text = candidate.shortDescription ?? string.Empty;
        if (detailedDescriptionLabel != null)
            detailedDescriptionLabel.text = candidate.detailedDescription ?? string.Empty;
        return true;
    }

    private static List<RaycastResult> RaycastAllUnderPointer()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return null;
        PointerEventData ped = new PointerEventData(eventSystem);
        ped.position = Input.mousePosition;
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(ped, results);
        return results;
    }

    private void OnFlagChanged(int value)
    {
        if (_suppressFlagWrite)
            return;
        if (string.IsNullOrEmpty(_currentTenantId))
            return;
        WriteFlag(value);
        if (flagLabel != null)
            flagLabel.text = GetFlagText(value);
        ApplyFlagColor(value);
    }

    private void WriteFlag(int value)
    {
        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
            return;
        if (!bridge.RunState.Tenants.ContainsKey(_currentTenantId))
            return;
        var set = AuthorizedChangeSet.Domain(
            bridge.RunState.RunId,
            bridge.RunState.StateVersion,
            "TenantInfoPanel",
            "SetTenantFlag");
        set.Add(new SetTenantFlagChange(_currentTenantId, value));
        CommitResult result = bridge.Reducer.TryCommit(bridge.RunState, set);
        if (result.Succeeded)
        {
            TenantFlagChanged?.Invoke(_currentTenantId, value);
        }
        else
        {
            ApplyFlagToPanel(_currentTenantId);
        }
    }

    private void ApplyFlagToPanel(string tenantId)
    {
        int flag = ReadFlag(tenantId);
        if (flagDropdown != null)
        {
            int clamped = Mathf.Clamp(flag, 0, Mathf.Max(0, flagDropdown.options.Count - 1));
            if (flagDropdown.value != clamped)
            {
                _suppressFlagWrite = true;
                flagDropdown.value = clamped;
                _suppressFlagWrite = false;
            }
        }
        if (flagLabel != null)
            flagLabel.text = GetFlagText(flag);
        ApplyFlagColor(flag);
    }

    private static int ReadFlag(string tenantId)
    {
        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
            return 0;
        if (bridge.RunState.Tenants.TryGetValue(tenantId, out TenantRunState tenant))
            return tenant.PlayerFlag;
        return 0;
    }

    private string GetFlagText(int flag)
    {
        if (flagDropdown != null && flag >= 0 && flag < flagDropdown.options.Count)
            return flagDropdown.options[flag].text;
        if (flagLabels != null && flag >= 0 && flag < flagLabels.Length)
            return flagLabels[flag];
        return string.Empty;
    }

    private void ApplyFlagColor(int flag)
    {
        if (flagBackground == null)
            return;
        Color target = _defaultFlagBackgroundColor;
        int colorIndex = flag - 1;
        if (colorIndex >= 0 && flagColors != null && colorIndex < flagColors.Length)
            target = flagColors[colorIndex];
        flagBackground.color = target;
    }

    private Transform GetActiveDropdownListRoot()
    {
        if (_dropdown == null)
            return null;
        try
        {
            if (!_dropdown.IsExpanded)
                return null;
            var field = typeof(TMP_Dropdown).GetField("m_Dropdown",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
                return null;
            GameObject list = field.GetValue(_dropdown) as GameObject;
            return list != null ? list.transform : null;
        }
        catch
        {
            return null;
        }
    }

    private static TenantReviewCandidateSO FindCandidate(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            return null;
        TenantReviewCoordinator coordinator = TenantReviewCoordinator.Instance;
        if (coordinator == null)
            coordinator = FindObjectOfType<TenantReviewCoordinator>(true);
        if (coordinator == null || coordinator.candidates == null)
            return null;
        List<TenantReviewCandidateSO> list = coordinator.candidates;
        for (int i = 0; i < list.Count; i++)
        {
            TenantReviewCandidateSO c = list[i];
            if (c != null && c.candidateId == tenantId)
                return c;
        }
        return null;
    }

    private static Sprite ResolveAvatarByKey(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            return null;
        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
            return null;
        if (!bridge.RunState.Tenants.TryGetValue(tenantId, out TenantRunState tenant))
            return null;
        if (string.IsNullOrEmpty(tenant.AvatarKey))
            return null;
        TenantAvatarResolver.TryResolve(tenant.AvatarKey, out Sprite sprite);
        return sprite;
    }

    private void RefreshTagPanel(TenantAbility ability)
    {
        if (tagPanel == null || tagPrefab == null)
        {
            if (tagPanel != null) tagPanel.SetActive(false);
            return;
        }

        tagPrefab.SetActive(false);

        for (int i = tagPanel.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = tagPanel.transform.GetChild(i);
            if (child.gameObject == tagPrefab)
                continue;
            DestroyImmediate(child.gameObject);
        }

        int generated = 0;
        if (ability != TenantAbility.None)
        {
            GameObject clone = Instantiate(tagPrefab, tagPanel.transform);
            clone.gameObject.SetActive(true);
            TextMeshProUGUI label = FindTMP(clone, tagTextPath);
            if (label != null)
                label.text = AbilityDisplayName.Get(ability);
            generated++;
        }

        tagPanel.SetActive(generated > 0);
    }

    private static TextMeshProUGUI FindTMP(GameObject root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        Transform target = root.transform.Find(path);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private static string GetActivityLabel(TenantActivityType activityType)
    {
        switch (activityType)
        {
            case TenantActivityType.NightActive: return "夜行";
            case TenantActivityType.AllDay: return "全天";
            default: return "日行";
        }
    }

    private void PositionAt(Vector2 screenPoint, bool preferLeft)
    {
        if (_canvas == null || _selfRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, _canvas.worldCamera, out local))
            return;

        Rect panelRect = _selfRect.rect;
        Vector2 size = new Vector2(panelRect.width, panelRect.height);
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(400f, 300f);

        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 pivot = _selfRect.pivot;
        Vector2 half = canvasSize * 0.5f;

        bool overRight = local.x + size.x > half.x;
        bool overLeft = local.x - size.x < -half.x;
        bool overBottom = local.y - size.y < -half.y;

        bool placeLeft = preferLeft ? !overLeft : overRight;
        bool placeUp = overBottom;

        Vector2 target = new Vector2(
            placeLeft
                ? local.x - (1f - pivot.x) * size.x
                : local.x + pivot.x * size.x,
            placeUp
                ? local.y + pivot.y * size.y
                : local.y - (1f - pivot.y) * size.y);

        target.x = Mathf.Clamp(target.x, -half.x, half.x);
        target.y = Mathf.Clamp(target.y, -half.y, half.y);

        _selfRect.anchoredPosition = target;
    }
}
