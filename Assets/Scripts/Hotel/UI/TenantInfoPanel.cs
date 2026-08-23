using System.Collections.Generic;
using Hotel.Audio;
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

    [Header("Eviction")]
    public Button letGoButton;
    public GameObject letGoConfirmPanel;
    public TextMeshProUGUI letGoConfirmText;
    public Button letGoConfirmAcceptButton;
    public Button letGoConfirmRefuseButton;

    [Header("Work Assignment")]
    public TMP_Dropdown workDropdown;

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
    private string _currentTenantId;
    private bool _suppressFlagWrite;
    private bool _suppressWorkWrite;
    private Color _defaultFlagBackgroundColor;
    private Sprite _defaultAvatarSprite;
    private readonly List<string> _visibleJobIds = new List<string>();

    private void Awake()
    {
        if (workDropdown == null)
        {
            Transform workDropdownTransform = transform.Find("LeftPanel/Dropdown");
            if (workDropdownTransform != null)
                workDropdown = workDropdownTransform.GetComponent<TMP_Dropdown>();
        }
        if (flagBackground != null)
            _defaultFlagBackgroundColor = flagBackground.color;
        if (tenantImage != null)
            _defaultAvatarSprite = tenantImage.sprite;
        if (flagDropdown != null)
            flagDropdown.onValueChanged.AddListener(OnFlagChanged);
        if (workDropdown != null)
            workDropdown.onValueChanged.AddListener(OnWorkChanged);
        if (letGoButton == null)
        {
            Transform letGoTransform = transform.Find("RightPanel/Panel/LetGoButton");
            if (letGoTransform != null)
                letGoButton = letGoTransform.GetComponent<Button>();
        }
        if (letGoButton != null)
        {
            letGoButton.onClick.AddListener(OnLetGoPressed);

            if (letGoConfirmPanel == null && transform.parent != null)
            {
                Transform confirmTransform = transform.parent.Find("LetGoConfirmPanel");
                if (confirmTransform != null)
                    letGoConfirmPanel = confirmTransform.gameObject;
            }
            if (letGoConfirmPanel != null)
            {
                if (letGoConfirmText == null)
                {
                    Transform textTransform = letGoConfirmPanel.transform.Find("Text (TMP)");
                    if (textTransform != null)
                        letGoConfirmText = textTransform.GetComponent<TextMeshProUGUI>();
                }
                if (letGoConfirmAcceptButton == null)
                {
                    Transform acceptTransform = letGoConfirmPanel.transform.Find("AcceptButton");
                    if (acceptTransform != null)
                        letGoConfirmAcceptButton = acceptTransform.GetComponent<Button>();
                }
                if (letGoConfirmRefuseButton == null)
                {
                    Transform refuseTransform = letGoConfirmPanel.transform.Find("RefuseButton");
                    if (refuseTransform != null)
                        letGoConfirmRefuseButton = refuseTransform.GetComponent<Button>();
                }
            }
            if (letGoConfirmAcceptButton != null)
                letGoConfirmAcceptButton.onClick.AddListener(OnLetGoAccept);
            if (letGoConfirmRefuseButton != null)
                letGoConfirmRefuseButton.onClick.AddListener(OnLetGoRefuse);
        }
    }

    private void OnDestroy()
    {
        if (flagDropdown != null)
            flagDropdown.onValueChanged.RemoveListener(OnFlagChanged);
        if (workDropdown != null)
            workDropdown.onValueChanged.RemoveListener(OnWorkChanged);
        if (letGoButton != null)
            letGoButton.onClick.RemoveListener(OnLetGoPressed);
        if (letGoConfirmAcceptButton != null)
            letGoConfirmAcceptButton.onClick.RemoveListener(OnLetGoAccept);
        if (letGoConfirmRefuseButton != null)
            letGoConfirmRefuseButton.onClick.RemoveListener(OnLetGoRefuse);
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
        ConfigureWorkDropdown();
        ApplyFlagToPanel(tenantId);
        ApplyWorkToPanel(tenantId);
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
        ConfigureWorkDropdown();
        ApplyFlagToPanel(tenantId);
        ApplyWorkToPanel(tenantId);
        PositionAt(screenPoint, false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelOpen);
    }

    public void Hide()
    {
        bool wasPinned = Mode == PanelMode.Pinned;
        Mode = PanelMode.Hidden;
        Source = DisplaySource.None;
        if (tenantLogPanel != null)
            tenantLogPanel.ClearLog();
        _currentTenantId = null;
        IsPointerOver = false;
        ApplyInteractionMode();
        gameObject.SetActive(false);
        if (wasPinned && AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelClose);
    }

    public void OnLetGoPressed()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.Click);

        if (string.IsNullOrEmpty(_currentTenantId))
            return;

        if (letGoConfirmPanel == null)
        {
            EvictCurrentTenant();
            return;
        }

        string displayName = ResolveDisplayName(_currentTenantId);
        if (letGoConfirmText != null)
            letGoConfirmText.text = $"确定要让 {displayName} 离开旅馆吗？";
        letGoConfirmPanel.SetActive(true);
    }

    public void OnLetGoAccept()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.Click);

        if (letGoConfirmPanel != null)
            letGoConfirmPanel.SetActive(false);
        EvictCurrentTenant();
    }

    public void OnLetGoRefuse()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.Click);

        if (letGoConfirmPanel != null)
            letGoConfirmPanel.SetActive(false);
    }

    private void EvictCurrentTenant()
    {
        if (string.IsNullOrEmpty(_currentTenantId))
            return;
        TenantAssignmentCoordinator coordinator = TenantAssignmentCoordinator.Instance;
        if (coordinator == null)
            return;
        coordinator.TryEvict(_currentTenantId);
        Hide();
    }

    private string ResolveDisplayName(string tenantId)
    {
        TenantReviewCandidateSO candidate = FindCandidate(tenantId);
        return candidate != null ? candidate.displayName : tenantId;
    }

    public bool IsInternalHit(GameObject hitObject)
    {
        if (hitObject == null)
            return false;
        if (hitObject.transform.IsChildOf(transform))
            return true;
        if (letGoConfirmPanel != null && letGoConfirmPanel.activeSelf
            && hitObject.transform.IsChildOf(letGoConfirmPanel.transform))
            return true;
        if (IsHitInExpandedDropdown(hitObject, flagDropdown)
            || IsHitInExpandedDropdown(hitObject, workDropdown))
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.Click);
        WriteFlag(value);
        if (flagLabel != null)
            flagLabel.text = GetFlagText(value);
        ApplyFlagColor(value);
    }

    private void ConfigureWorkDropdown()
    {
        if (workDropdown == null)
            return;

        _visibleJobIds.Clear();
        var options = new List<TMP_Dropdown.OptionData>
        {
            new("未安排")
        };

        IReadOnlyList<JobDefinition> jobs = JobCatalog.All;
        for (int i = 0; i < jobs.Count; i++)
        {
            JobDefinition job = jobs[i];
            if (job == null)
                continue;

            bool isVisible = false;
            switch (job.Id)
            {
                case JobCatalog.Cooking:
                case JobCatalog.NightWatch:
                case JobCatalog.Farming:
                case JobCatalog.Chores:
                    isVisible = true;
                    break;
                default:
                    isVisible = false;
                    break;
            }

            if (isVisible)
            {
                _visibleJobIds.Add(job.Id);
                options.Add(new TMP_Dropdown.OptionData(job.DisplayName));
            }
        }

        workDropdown.options = options;
        workDropdown.RefreshShownValue();
    }

    private void ApplyWorkToPanel(string tenantId)
    {
        if (workDropdown == null)
            return;

        int selectedIndex = 0;
        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge != null && bridge.RunState != null
            && bridge.RunState.Tenants.TryGetValue(tenantId, out TenantRunState tenant)
            && !string.IsNullOrEmpty(tenant.JobId))
        {
            for (int i = 0; i < _visibleJobIds.Count; i++)
            {
                if (string.Equals(_visibleJobIds[i], tenant.JobId, System.StringComparison.Ordinal))
                {
                    selectedIndex = i + 1;
                    break;
                }
            }
        }

        _suppressWorkWrite = true;
        workDropdown.SetValueWithoutNotify(selectedIndex);
        workDropdown.RefreshShownValue();
        _suppressWorkWrite = false;
    }

    private void OnWorkChanged(int value)
    {
        if (_suppressWorkWrite || string.IsNullOrEmpty(_currentTenantId))
            return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.Click);

        string jobId = value > 0 && value <= _visibleJobIds.Count
            ? _visibleJobIds[value - 1]
            : string.Empty;

        TenantAssignmentCoordinator coordinator = TenantAssignmentCoordinator.Instance;
        if (coordinator != null && coordinator.TryAssignJob(_currentTenantId, jobId))
        {
            if (tenantLogPanel != null && Mode == PanelMode.Pinned)
                tenantLogPanel.RefreshForTenant(_currentTenantId);
            return;
        }

        ApplyWorkToPanel(_currentTenantId);
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

    private static bool IsHitInExpandedDropdown(GameObject hitObject, TMP_Dropdown dropdown)
    {
        if (hitObject == null || dropdown == null || !dropdown.IsExpanded)
            return false;
        try
        {
            var field = typeof(TMP_Dropdown).GetField("m_Dropdown",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
                return false;
            GameObject list = field.GetValue(dropdown) as GameObject;
            return list != null && hitObject.transform.IsChildOf(list.transform);
        }
        catch
        {
            return false;
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

        float leftExtent = pivot.x * size.x;
        float rightExtent = (1f - pivot.x) * size.x;
        float bottomExtent = pivot.y * size.y;
        float topExtent = (1f - pivot.y) * size.y;

        bool overRight = local.x + rightExtent > half.x;
        bool overLeft = local.x - leftExtent < -half.x;
        bool overBottom = local.y - bottomExtent < -half.y;
        bool overTop = local.y + topExtent > half.y;

        bool placeLeft = preferLeft ? !overLeft : overRight;
        bool placeUp = overBottom && !overTop;

        Vector2 target = new Vector2(
            placeLeft
                ? local.x - rightExtent
                : local.x + leftExtent,
            placeUp
                ? local.y + bottomExtent
                : local.y - topExtent);

        float minX = -half.x + leftExtent;
        float maxX = half.x - rightExtent;
        float minY = -half.y + bottomExtent;
        float maxY = half.y - topExtent;

        target.x = minX < maxX ? Mathf.Clamp(target.x, minX, maxX) : 0f;
        target.y = minY < maxY ? Mathf.Clamp(target.y, minY, maxY) : 0f;

        _selfRect.anchoredPosition = target;
    }
}
