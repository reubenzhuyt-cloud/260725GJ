using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class NextPhaseButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Settings")]
    public float holdDuration = 1f;

    [Header("Visual")]
    public Image fillImage;
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color pressColor = Color.black;

    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool triggered = false;

    private void Update()
    {
        if (!isHolding || triggered) return;

        holdTimer += Time.deltaTime;

        if (fillImage != null)
            fillImage.fillAmount = holdTimer / holdDuration;

        if (holdTimer >= holdDuration)
        {
            triggered = true;
            OnLongPress();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isHolding = true;
        holdTimer = 0f;
        triggered = false;

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.color = pressColor;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
        holdTimer = 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.color = normalColor;
        }
    }

    private void OnLongPress()
    {
        Debug.Log("[NextPhaseButton] Long press triggered — advancing phase");
        if (GamePhaseManager.Instance != null)
            GamePhaseManager.Instance.AdvancePhase();
    }
}
