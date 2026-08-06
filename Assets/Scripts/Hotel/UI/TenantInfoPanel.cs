using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class TenantInfoPanel : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    private static TenantInfoPanel _instance;

    public static TenantInfoPanel Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<TenantInfoPanel>(true);
            return _instance;
        }
    }

    [Header("UI References")]
    public TextMeshProUGUI nameLabel;
    public Image tenantImage;
    public GameObject tagPanel;
    public GameObject tagPrefab;
    public string tagTextPath = "Text (TMP)";
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI shortDescriptionLabel;
    public TextMeshProUGUI detailedDescriptionLabel;

    private Canvas _canvas;
    private RectTransform _selfRect;
    private bool _triggerHovering;
    private float _hidePendingStart;
    private const float HideDelay = 0.15f;

    public bool OpenedByRightClick { get; private set; }
    public bool OpenedByHover { get; private set; }

    private TMP_Dropdown _dropdown;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _dropdown = GetComponentInChildren<TMP_Dropdown>(true);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public bool IsShowing => gameObject.activeSelf;

    private void EnsureInitialized()
    {
        if (_selfRect == null)
            _selfRect = GetComponent<RectTransform>();
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
    }

    public void ShowHover(string tenantId, Vector2 screenPoint)
    {
        EnsureInitialized();
        TenantReviewCandidateSO candidate = FindCandidate(tenantId);
        if (candidate == null)
        {
            Hide();
            return;
        }

        ApplyContent(candidate);
        OpenedByRightClick = false;
        OpenedByHover = true;
        gameObject.SetActive(true);
        CancelPendingHide();
        PositionAt(screenPoint);
    }

    public void ShowPinned(string tenantId, Vector2 screenPoint)
    {
        EnsureInitialized();
        TenantReviewCandidateSO candidate = FindCandidate(tenantId);
        if (candidate == null)
        {
            Hide();
            return;
        }

        ApplyContent(candidate);
        OpenedByRightClick = true;
        OpenedByHover = false;
        _hidePendingStart = 0f;
        gameObject.SetActive(true);
        PositionAt(screenPoint);
    }

    private void ApplyContent(TenantReviewCandidateSO candidate)
    {
        if (nameLabel != null)
            nameLabel.text = candidate.displayName;
        if (tenantImage != null)
            tenantImage.gameObject.SetActive(candidate.portrait != null);
        RefreshTagPanel(candidate.ability);
        if (titleLabel != null)
            titleLabel.text = GetActivityLabel(candidate.activityType);
        if (shortDescriptionLabel != null)
            shortDescriptionLabel.text = candidate.shortDescription ?? string.Empty;
        if (detailedDescriptionLabel != null)
            detailedDescriptionLabel.text = candidate.detailedDescription ?? string.Empty;
    }

    public void Hide()
    {
        _hidePendingStart = 0f;
        OpenedByRightClick = false;
        OpenedByHover = false;
        gameObject.SetActive(false);
    }

    public void SetTriggerHover(bool hovering)
    {
        _triggerHovering = hovering;
        if (OpenedByRightClick)
            return;
        if (hovering)
            CancelPendingHide();
        else
            ScheduleHideIfNeeded();
    }

    public void CancelPendingHide()
    {
        _hidePendingStart = 0f;
    }

    public void ScheduleHideIfNeeded()
    {
        if (!IsShowing)
            return;
        if (!OpenedByHover)
            return;
        if (_triggerHovering || IsPointerOver)
            return;
        if (_hidePendingStart <= 0f)
            _hidePendingStart = Time.unscaledTime;
    }

    public bool IsPointerOver { get; private set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsPointerOver = true;
        if (OpenedByRightClick)
            return;
        CancelPendingHide();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsPointerOver = false;
        if (OpenedByRightClick)
            return;
        ScheduleHideIfNeeded();
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

    private void Update()
    {
        if (_hidePendingStart > 0f && IsShowing && OpenedByHover)
        {
            if (Time.unscaledTime - _hidePendingStart >= HideDelay)
            {
                _hidePendingStart = 0f;
                gameObject.SetActive(false);
                OpenedByHover = false;
            }
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

    private void PositionAt(Vector2 screenPoint)
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
        bool overBottom = local.y - size.y < -half.y;

        Vector2 target;
        if (!overRight && !overBottom)
        {
            target = new Vector2(local.x + pivot.x * size.x, local.y - (1f - pivot.y) * size.y);
        }
        else if (overRight && !overBottom)
        {
            target = new Vector2(local.x - (1f - pivot.x) * size.x, local.y - (1f - pivot.y) * size.y);
        }
        else if (!overRight && overBottom)
        {
            target = new Vector2(local.x + pivot.x * size.x, local.y + pivot.y * size.y);
        }
        else
        {
            target = new Vector2(local.x - (1f - pivot.x) * size.x, local.y + pivot.y * size.y);
        }

        target.x = Mathf.Clamp(target.x, -half.x, half.x);
        target.y = Mathf.Clamp(target.y, -half.y, half.y);

        _selfRect.anchoredPosition = target;
    }
}
