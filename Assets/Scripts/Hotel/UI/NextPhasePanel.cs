using Hotel.Runtime;
using UnityEngine;

public class NextPhasePanel : MonoBehaviour
{
    [Header("Event Listener")]
    public EventQueueEmptyEvent onEventQueueEmpty;
    public GamePopupEvent onPopupEvent;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private bool _runStateRestoredSubscribed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void OnEnable()
    {
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Register(OnQueueEmpty);

        if (onPopupEvent != null)
            onPopupEvent.Register(OnEventTriggered);

        if (!_runStateRestoredSubscribed)
        {
            SettlementBridge.RunStateRestored += OnRunStateRestored;
            _runStateRestoredSubscribed = true;
        }
    }

    private void Start()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.PhaseProcessingStarted += OnPhaseProcessingStarted;

        SyncFromAuthoritativeState();
    }

    private void OnDisable()
    {
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Unregister(OnQueueEmpty);
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnEventTriggered);

        if (_runStateRestoredSubscribed)
        {
            SettlementBridge.RunStateRestored -= OnRunStateRestored;
            _runStateRestoredSubscribed = false;
        }
    }

    private void OnDestroy()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.PhaseProcessingStarted -= OnPhaseProcessingStarted;
    }

    private void OnPhaseProcessingStarted()
    {
        SetVisible(false);
    }

    private void OnEventTriggered(PopupData data)
    {
        SetVisible(false);
    }

    private void OnQueueEmpty(int data)
    {
        SetVisible(true);
    }

    private void OnRunStateRestored(GameRunState state)
    {
        SyncFromAuthoritativeState();
    }

    private void SyncFromAuthoritativeState()
    {
        if (EventManager.Instance == null)
            return;

        SetVisible(EventManager.Instance.IsPhaseComplete);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}
