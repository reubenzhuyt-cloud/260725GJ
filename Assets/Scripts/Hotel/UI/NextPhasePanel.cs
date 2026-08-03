using UnityEngine;

public class NextPhasePanel : MonoBehaviour
{
    [Header("Event Listener")]
    public EventQueueEmptyEvent onEventQueueEmpty;
    public GamePopupEvent onPopupEvent;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

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
    }

    private void Start()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.PhaseProcessingStarted += OnPhaseProcessingStarted;
    }

    private void OnDisable()
    {
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Unregister(OnQueueEmpty);
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnEventTriggered);
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

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}
