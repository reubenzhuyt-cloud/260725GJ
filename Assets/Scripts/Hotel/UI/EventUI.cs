using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventUI : MonoBehaviour
{
    [Header("Overlay")]
    public GameObject eventOverlay;

    [Header("Panel References")]
    public GameObject confirmPanel;
    public GameObject choicePanel;

    [Header("Confirm Panel Elements")]
    public Image confirmImage;
    public TextMeshProUGUI confirmTitle;
    public TextMeshProUGUI confirmDescription;
    public Button confirmButton;

    [Header("Choice Panel Elements")]
    public Image choiceImage;
    public TextMeshProUGUI choiceTitle;
    public TextMeshProUGUI choiceDescription;
    public Transform choiceButtonContainer;
    public Button choiceButtonPrefab;

    [Header("Event Listener")]
    public GamePopupEvent onPopupEvent;

    private EventEffect[] currentConfirmEffects;

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
        HideAll();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private void OnPopupReceived(PopupData data)
    {
        if (eventOverlay != null)
            eventOverlay.SetActive(true);
        HideAll();

        if (data.eventType == GameEventType.Confirm)
            ShowConfirm(data);
        else if (data.eventType == GameEventType.Choice)
            ShowChoice(data);
    }

    private void ShowConfirm(PopupData data)
    {
        if (confirmPanel != null) confirmPanel.SetActive(true);
        if (confirmImage != null && data.image != null) confirmImage.sprite = data.image;
        if (confirmTitle != null) confirmTitle.text = data.title;
        if (confirmDescription != null) confirmDescription.text = data.description;
        currentConfirmEffects = data.confirmEffects;
    }

    private void ShowChoice(PopupData data)
    {
        if (choicePanel != null) choicePanel.SetActive(true);
        if (choiceImage != null && data.image != null) choiceImage.sprite = data.image;
        if (choiceTitle != null) choiceTitle.text = data.title;
        if (choiceDescription != null) choiceDescription.text = data.description;

        if (choiceButtonContainer != null)
        {
            foreach (Transform child in choiceButtonContainer)
                Destroy(child.gameObject);
        }

        if (data.choiceTexts != null && choiceButtonPrefab != null && choiceButtonContainer != null)
        {
            for (int i = 0; i < data.choiceTexts.Length; i++)
            {
                Button btn = Instantiate(choiceButtonPrefab, choiceButtonContainer);
                btn.gameObject.SetActive(true);

                TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = data.choiceTexts[i];

                int choiceIndex = i;
                btn.onClick.AddListener(() => OnChoiceSelected(choiceIndex, data));
            }
        }
    }

    private void OnConfirmClicked()
    {
        ApplyEffects(currentConfirmEffects);
        HideAll();
        ResumeTime();
    }

    private void OnChoiceSelected(int index, PopupData data)
    {
        Debug.Log($"[EventUI] Choice: {data.choiceTexts[index]} → {data.choiceResults[index]}");

        if (data.choiceEffects != null && index < data.choiceEffects.Length)
            ApplyEffects(data.choiceEffects[index]);

        HideAll();
        ResumeTime();
    }

    private void ApplyEffects(EventEffect[] effects)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            switch (effect.effectType)
            {
                case EffectType.ModifyErosion:
                    if (ErosionManager.Instance != null)
                        ErosionManager.Instance.ModifyErosion(effect.floatValue);
                    Debug.Log($"[EventUI] Applied: ModifyErosion {effect.floatValue:+0.0;-0.0}");
                    break;
                case EffectType.None:
                    break;
            }
        }
    }

    private void HideAll()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    private void ResumeTime()
    {
        HideAll();
        if (eventOverlay != null)
            eventOverlay.SetActive(false);
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
    }
}
