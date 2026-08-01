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
        {
            Debug.LogWarning("[NextPhasePanel] No CanvasGroup found on this GameObject! Adding one via AddComponent. " +
                "This may NOT match the CanvasGroup configured in the scene.");
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        else
        {
            Debug.Log("[NextPhasePanel] Found existing CanvasGroup in scene. alpha=" + canvasGroup.alpha +
                " interactable=" + canvasGroup.interactable + " blocksRaycasts=" + canvasGroup.blocksRaycasts);
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        Debug.Log("[NextPhasePanel] Awake complete. canvasGroup=" + canvasGroup +
            " GameObject active=" + gameObject.activeSelf + " activeInHierarchy=" + gameObject.activeInHierarchy);
    }

    private void OnEnable()
    {
        Debug.Log("[NextPhasePanel] OnEnable called. canvasGroup=" + canvasGroup +
            " alpha=" + (canvasGroup != null ? canvasGroup.alpha.ToString() : "NULL"));

        if (onEventQueueEmpty != null)
        {
            onEventQueueEmpty.Register(OnQueueEmpty);
            Debug.Log("[NextPhasePanel] Registered onEventQueueEmpty: " + onEventQueueEmpty.name);
        }
        else
            Debug.LogWarning("[NextPhasePanel] onEventQueueEmpty is NULL — drag the SO asset in inspector!");

        if (onPopupEvent != null)
            onPopupEvent.Register(OnEventTriggered);
    }

    private void OnDisable()
    {
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Unregister(OnQueueEmpty);
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnEventTriggered);
    }

    private void OnEventTriggered(PopupData data)
    {
        Debug.Log("[NextPhasePanel] OnEventTriggered — hiding panel.");
        SetVisible(false);
    }

    private void OnQueueEmpty(int data)
    {
        Debug.Log("[NextPhasePanel] OnQueueEmpty received! Showing button.");
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("[NextPhasePanel] SetVisible(" + visible + ") — canvasGroup is NULL!");
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;

        Debug.Log("[NextPhasePanel] SetVisible(" + visible + ") — " +
            "alpha=" + canvasGroup.alpha +
            " interactable=" + canvasGroup.interactable +
            " blocksRaycasts=" + canvasGroup.blocksRaycasts +
            " gameObject.activeSelf=" + gameObject.activeSelf +
            " activeInHierarchy=" + gameObject.activeInHierarchy);

        if (rectTransform != null)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Debug.Log("[NextPhasePanel] RectTransform — " +
                "anchoredPosition=" + rectTransform.anchoredPosition +
                " sizeDelta=" + rectTransform.sizeDelta +
                " rect=" + rectTransform.rect +
                " worldCorners[0]=" + corners[0] +
                " worldCorners[2]=" + corners[2]);

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            Debug.Log("[NextPhasePanel] Parent Canvas=" + (parentCanvas != null ? parentCanvas.name : "NULL") +
                " canvas.enabled=" + (parentCanvas != null ? parentCanvas.enabled.ToString() : "N/A") +
                " renderMode=" + (parentCanvas != null ? parentCanvas.renderMode.ToString() : "N/A"));

            CanvasGroup[] parentGroups = GetComponentsInParent<CanvasGroup>();
            if (parentGroups.Length > 0)
            {
                foreach (var pg in parentGroups)
                {
                    Debug.Log("[NextPhasePanel] Parent CanvasGroup on '" + pg.gameObject.name +
                        "' alpha=" + pg.alpha + " interactable=" + pg.interactable +
                        " blocksRaycasts=" + pg.blocksRaycasts);
                }
            }
        }
    }
}
