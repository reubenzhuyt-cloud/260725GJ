using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(22)]
public class RoomAvatarNamePanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Graphic panelGraphic;
    [SerializeField] private int occupantIndex;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private float fadeDuration = 0.3f;

    private RoomTenantAvatarSlot _slot;
    private bool _subscribed;
    private bool _runStateRestoredSubscribed;
    private bool _hasCapturedBaseScale;
    private Vector3 _baseScale = Vector3.one;

    private string _cachedOccupantId;
    private string _cachedDisplayName;
    private bool _isOccupied;

    private bool _hasCapturedOriginalAlphas;
    private float _originalNameLabelAlpha = 1f;
    private float _originalPanelGraphicAlpha = 1f;
    private Coroutine _fadeCoroutine;
    private bool _targetVisible;
    private bool _isFading;

    public int OccupantIndex
    {
        get => occupantIndex;
        set => occupantIndex = value;
    }

    private void Awake()
    {
        ResolveSlot();
        ResolveCameraReferences();
        CaptureBaseScale();
        CaptureOriginalAlphas();
    }

    private void CaptureOriginalAlphas()
    {
        if (_hasCapturedOriginalAlphas)
            return;

        if (nameLabel != null)
            _originalNameLabelAlpha = nameLabel.color.a;

        if (panelGraphic != null)
            _originalPanelGraphicAlpha = panelGraphic.color.a;

        _hasCapturedOriginalAlphas = true;
    }

    private void ResolveSlot()
    {
        _slot = null;
        Transform container = transform.parent;
        if (container == null)
            return;

        RoomTenantAvatarSlot[] slots = container.GetComponentsInChildren<RoomTenantAvatarSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].transform == container)
                continue;
            if (slots[i].OccupantIndex == occupantIndex)
            {
                _slot = slots[i];
                break;
            }
        }
    }

    private void ResolveCameraReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cameraController == null && targetCamera != null)
            cameraController = targetCamera.GetComponent<CameraController>();

        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
    }

    private void CaptureBaseScale()
    {
        if (_hasCapturedBaseScale)
            return;

        _baseScale = transform.localScale;
        _hasCapturedBaseScale = true;
    }

    private void OnEnable()
    {
        Subscribe();
        SubscribeRunStateRestored();
        Refresh();
    }

    private void Start()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnsubscribeRunStateRestored();
        StopFadeCoroutine();
        _targetVisible = false;
        ApplyVisibilityImmediate(false);
    }

    private void OnDestroy()
    {
        StopFadeCoroutine();
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
        {
            TenantAssignmentCoordinator.Instance.AssignmentChanged += OnAssignmentChanged;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= OnAssignmentChanged;
        _subscribed = false;
    }

    private void SubscribeRunStateRestored()
    {
        if (_runStateRestoredSubscribed)
            return;
        SettlementBridge.RunStateRestored += OnRunStateRestored;
        _runStateRestoredSubscribed = true;
    }

    private void UnsubscribeRunStateRestored()
    {
        if (!_runStateRestoredSubscribed)
            return;
        SettlementBridge.RunStateRestored -= OnRunStateRestored;
        _runStateRestoredSubscribed = false;
    }

    private void OnRunStateRestored(Hotel.Runtime.GameRunState state)
    {
        Refresh();
    }

    private void OnAssignmentChanged()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        FollowSlot();
        UpdateVisibilityByZoom();
    }

    private void FollowSlot()
    {
        if (_slot == null)
            ResolveSlot();

        if (_slot == null)
            return;

        RectTransform slotRect = _slot.transform as RectTransform;
        RectTransform selfRect = transform as RectTransform;
        if (slotRect == null || selfRect == null)
            return;

        Vector2 slotPos = slotRect.anchoredPosition;
        selfRect.anchoredPosition = new Vector2(slotPos.x, selfRect.anchoredPosition.y);
        transform.localScale = _baseScale;
    }

    private void UpdateVisibilityByZoom()
    {
        if (targetCamera == null)
            ResolveCameraReferences();

        if (targetCamera == null)
            return;

        float minZoom = 3f;
        float maxZoom = 30f;
        if (cameraController != null)
        {
            minZoom = cameraController.minZoom;
            maxZoom = cameraController.maxZoom;
        }

        float zoomMidpoint = (minZoom + maxZoom) * 0.5f;
        bool zoomedInEnough = targetCamera.orthographicSize <= zoomMidpoint;

        bool isVisible = _isOccupied && zoomedInEnough;
        SetVisibility(isVisible);
    }

    private void SetVisibility(bool targetVisible)
    {
        if (targetVisible == _targetVisible && (_isFading || IsVisualsEnabled() == targetVisible))
            return;

        _targetVisible = targetVisible;

        if (!gameObject.activeInHierarchy)
        {
            ApplyVisibilityImmediate(targetVisible);
            return;
        }

        StopFadeCoroutine();
        _fadeCoroutine = StartCoroutine(FadeRoutine(targetVisible));
    }

    private bool IsVisualsEnabled()
    {
        if (nameLabel != null && nameLabel.enabled)
            return true;
        if (panelGraphic != null && panelGraphic.enabled)
            return true;
        return false;
    }

    private void StopFadeCoroutine()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        _isFading = false;
    }

    private IEnumerator FadeRoutine(bool targetVisible)
    {
        _isFading = true;
        CaptureOriginalAlphas();

        if (targetVisible)
        {
            if (nameLabel != null)
                nameLabel.enabled = true;
            if (panelGraphic != null)
                panelGraphic.enabled = true;
        }

        float startLabelAlpha = nameLabel != null ? nameLabel.color.a : 0f;
        float startGraphicAlpha = panelGraphic != null ? panelGraphic.color.a : 0f;

        float endLabelAlpha = targetVisible ? _originalNameLabelAlpha : 0f;
        float endGraphicAlpha = targetVisible ? _originalPanelGraphicAlpha : 0f;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!isActiveAndEnabled)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            SetAlpha(Mathf.Lerp(startLabelAlpha, endLabelAlpha, t), Mathf.Lerp(startGraphicAlpha, endGraphicAlpha, t));
            yield return null;
        }

        SetAlpha(endLabelAlpha, endGraphicAlpha);

        if (!targetVisible)
        {
            if (nameLabel != null)
                nameLabel.enabled = false;
            if (panelGraphic != null)
                panelGraphic.enabled = false;
        }

        _fadeCoroutine = null;
        _isFading = false;
    }

    private void ApplyVisibilityImmediate(bool targetVisible)
    {
        CaptureOriginalAlphas();
        float labelAlpha = targetVisible ? _originalNameLabelAlpha : 0f;
        float graphicAlpha = targetVisible ? _originalPanelGraphicAlpha : 0f;

        SetAlpha(labelAlpha, graphicAlpha);

        if (nameLabel != null)
            nameLabel.enabled = targetVisible;
        if (panelGraphic != null)
            panelGraphic.enabled = targetVisible;

        _isFading = false;
    }

    private void SetAlpha(float labelAlpha, float graphicAlpha)
    {
        if (nameLabel != null)
        {
            Color c = nameLabel.color;
            c.a = labelAlpha;
            nameLabel.color = c;
        }

        if (panelGraphic != null)
        {
            Color c = panelGraphic.color;
            c.a = graphicAlpha;
            panelGraphic.color = c;
        }
    }

    public void Refresh()
    {
        if (_slot == null)
            ResolveSlot();

        _cachedOccupantId = _slot != null ? _slot.GetOccupantId() : null;
        _isOccupied = !string.IsNullOrEmpty(_cachedOccupantId);

        if (!_isOccupied)
        {
            _cachedDisplayName = string.Empty;
            if (nameLabel != null)
                nameLabel.text = string.Empty;
            UpdateVisibilityByZoom();
            return;
        }

        _cachedDisplayName = _cachedOccupantId;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.TryGetTenantDisplayName(_cachedOccupantId, out _cachedDisplayName);

        if (nameLabel != null)
            nameLabel.text = _cachedDisplayName ?? string.Empty;

        UpdateVisibilityByZoom();
    }
}
