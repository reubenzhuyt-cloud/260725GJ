using System.Collections.Generic;
using Hotel.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerLogOverlayController : MonoBehaviour
{
    [SerializeField] private GameObject logOverlay;

    private static int _openLogOverlayCount;
    private static int _escapeConsumedFrame = -1;

    public static bool IsAnyLogOverlayOpen => _openLogOverlayCount > 0;
    public static bool WasEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private bool _openedByController;
    private bool _started;
    private int _openedFrame = -1;
    private PlayerLogPanelController _panel;
    private RectTransform _panelRect;

    private void OnEnable()
    {
        _escapeConsumedFrame = -1;
        if (logOverlay == null || _started)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
    }

    private void Start()
    {
        _started = true;
    }

    private void Update()
    {
        if (logOverlay == null)
            return;

        if (Input.GetKeyDown(KeyCode.H))
            ToggleLogOverlay();

        if (Input.GetKeyDown(KeyCode.Escape) && _openedByController)
        {
            _escapeConsumedFrame = Time.frameCount;
            Close();
            return;
        }

        if (_openedByController && logOverlay.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (Time.frameCount == _openedFrame)
                return;

            if (!IsPointerInsideLogPanel())
            {
                Close();
            }
        }
    }

    private bool IsPointerInsideLogPanel()
    {
        if (logOverlay == null)
            return false;

        if (_panel == null)
            _panel = logOverlay.GetComponentInChildren<PlayerLogPanelController>(true);

        Transform contentRoot = logOverlay.transform.Find("LogPanel");
        if (contentRoot == null && _panel != null && _panel.gameObject != logOverlay)
        {
            contentRoot = _panel.transform;
        }

        if (contentRoot == null)
        {
            for (int i = 0; i < logOverlay.transform.childCount; i++)
            {
                Transform child = logOverlay.transform.GetChild(i);
                if (child.GetComponent<RectTransform>() != null)
                {
                    contentRoot = child;
                    break;
                }
            }
        }

        if (contentRoot == null)
            return false;

        EventSystem current = EventSystem.current;
        if (current != null)
        {
            var pointerData = new PointerEventData(current)
            {
                position = Input.mousePosition
            };
            _raycastResults.Clear();
            current.RaycastAll(pointerData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                GameObject hit = _raycastResults[i].gameObject;
                if (hit == null || hit == logOverlay)
                    continue;

                if (hit == contentRoot.gameObject || hit.transform.IsChildOf(contentRoot))
                    return true;
            }
        }

        RectTransform innerPanelRect = contentRoot.GetComponent<RectTransform>();
        if (innerPanelRect != null)
        {
            Canvas canvas = logOverlay.GetComponentInParent<Canvas>();
            Camera eventCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera;

            if (RectTransformUtility.RectangleContainsScreenPoint(innerPanelRect, Input.mousePosition, eventCamera))
                return true;
        }

        return false;
    }

    public void ToggleLogOverlay()
    {
        if (logOverlay == null)
            return;
        if (_openedByController)
            Close();
        else if (!IsPauseOverlayVisible())
            Open();
    }

    private bool IsPauseOverlayVisible()
    {
        UIManager manager = FindObjectOfType<UIManager>();
        return manager != null && manager.IsPauseOverlayVisible;
    }

    public void Open()
    {
        if (_openedByController)
            return;
        if (UIManager.Instance != null)
        {
            if (!UIManager.Instance.CanOpenButtonPanel())
                return;
            UIManager.Instance.CloseOtherButtonPanels(logOverlay);
        }
        _openedFrame = Time.frameCount;
        logOverlay.SetActive(true);
        _openedByController = true;
        _openLogOverlayCount++;
        if (_panel == null)
            _panel = logOverlay.GetComponentInChildren<PlayerLogPanelController>(true);
        if (_panel != null)
            _panel.RefreshTimeline();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelOpen);
    }

    public void Close()
    {
        if (!_openedByController)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
        _openLogOverlayCount--;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelClose);
    }

    private void OnDisable()
    {
        _escapeConsumedFrame = -1;
        if (!_openedByController || logOverlay == null)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
        _openLogOverlayCount--;
    }

    private void OnDestroy()
    {
        _escapeConsumedFrame = -1;
        if (!_openedByController || logOverlay == null)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
        _openLogOverlayCount--;
    }
}
