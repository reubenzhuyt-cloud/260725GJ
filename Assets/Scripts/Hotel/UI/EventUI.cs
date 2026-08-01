using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventUI : MonoBehaviour
{
    [Header("Overlay")]
    public GameObject eventOverlay;

    [Header("Panel")]
    public GameObject eventPanel;

    [Header("Shared Elements")]
    public Image eventImage;
    public TextMeshProUGUI eventTitle;
    public TextMeshProUGUI eventDescription;

    [Header("Confirm Mode")]
    public GameObject confirmButton;

    [Header("Choice Mode")]
    public GameObject choiceButtonContainer;
    public Button choiceButtonPrefab;

    [Header("Event Listener")]
    public GamePopupEvent onPopupEvent;

    [Header("Event Channel")]
    public EventProcessedEvent onEventProcessed;

    private EventEffect[] currentConfirmEffects;
    private string currentEventId;

    private void OnEnable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Register(OnPopupReceived);
    }

    private void OnDisable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnPopupReceived);
    }

    private void Start()
    {
        if (eventOverlay != null)
            eventOverlay.SetActive(false);

        if (eventPanel != null)
            eventPanel.SetActive(false);

        if (confirmButton != null)
        {
            Button btn = confirmButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnConfirmClicked);
            }
        }
    }

    private void OnPopupReceived(PopupData data)
    {
        currentEventId = data.eventId;

        if (eventOverlay != null)
            eventOverlay.SetActive(true);

        if (eventPanel != null)
            eventPanel.SetActive(true);

        // Set shared content
        if (eventImage != null && data.image != null)
            eventImage.sprite = data.image;
        if (eventTitle != null)
            eventTitle.text = data.title;
        if (eventDescription != null)
            eventDescription.text = data.description;

        if (data.eventType == GameEventType.Confirm)
            ShowConfirm(data);
        else if (data.eventType == GameEventType.Choice)
            ShowChoice(data);
    }

    private void ShowConfirm(PopupData data)
    {
        if (confirmButton != null) confirmButton.SetActive(true);
        if (choiceButtonContainer != null) choiceButtonContainer.SetActive(false);
        currentConfirmEffects = data.confirmEffects;
    }

    private void ShowChoice(PopupData data)
    {
        if (confirmButton != null) confirmButton.SetActive(false);
        if (choiceButtonContainer != null) choiceButtonContainer.SetActive(true);

        // Clear old clones (skip the template prefab)
        if (choiceButtonContainer != null)
        {
            for (int i = choiceButtonContainer.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = choiceButtonContainer.transform.GetChild(i);
                if (child.gameObject == choiceButtonPrefab?.gameObject)
                    continue;
                Destroy(child.gameObject);
            }
        }

        // Create choice buttons
        if (data.choiceTexts == null || choiceButtonPrefab == null || choiceButtonContainer == null)
        {
            Debug.LogWarning("[EventUI] Missing references for choice buttons!");
            return;
        }

        for (int i = 0; i < data.choiceTexts.Length; i++)
        {
            Button btn = Instantiate(choiceButtonPrefab, choiceButtonContainer.transform);
            btn.gameObject.SetActive(true);
            btn.gameObject.name = $"ChoiceButton_{i}";
            btn.interactable = true;

            TextMeshProUGUI btnText = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (btnText != null) btnText.text = data.choiceTexts[i];

            int choiceIndex = i;
            PopupData capturedData = data;
            btn.onClick.AddListener(() => OnChoiceSelected(choiceIndex, capturedData));
        }

        currentConfirmEffects = null;
    }

    private void OnConfirmClicked()
    {
        ApplyEffects(currentConfirmEffects);
        Close();
    }

    private void OnChoiceSelected(int index, PopupData data)
    {
        Debug.Log($"[EventUI] Choice: {data.choiceTexts[index]} → {data.choiceResults[index]}");

        if (data.choiceEffects != null && index < data.choiceEffects.Length)
            ApplyEffects(data.choiceEffects[index]);

        Close();
    }

    private void Close()
    {
        if (eventPanel != null) eventPanel.SetActive(false);
        if (eventOverlay != null) eventOverlay.SetActive(false);

        if (onEventProcessed != null && currentEventId != null)
            onEventProcessed.Raise(currentEventId);
    }

    private void ApplyEffects(EventEffect[] effects)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            switch (effect.effectType)
            {
                case EffectType.ModifyTenantErosion:
                    Debug.LogWarning("[EventUI] ModifyTenantErosion effect requires tenant context — deferred");
                    break;
            }
        }
    }
}
