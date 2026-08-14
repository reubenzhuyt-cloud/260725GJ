using System.Collections.Generic;
using Hotel.Runtime;
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
    public TextMeshProUGUI eventKindText;

    [Header("Confirm Mode")]
    public GameObject confirmButton;

    [Header("Choice Mode")]
    public GameObject choiceButtonContainer;
    public GameObject choiceButtonPrefab;
    public string optionTextPath = "ChoiceButton/Text";
    public string effectTextPath = "EffectText";

    [Header("Tag Requirements")]
    public GameObject tagPanel;
    public GameObject tagPrefab;
    public string tagTextPath = "Text (TMP)";

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
        if (!string.IsNullOrEmpty(currentEventId))
        {
            Debug.LogError($"[EventUI] Rejecting popup '{data.eventId}' because popup '{currentEventId}' is still active; prevented silent overwrite.");
            return;
        }
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
        if (eventKindText != null)
            eventKindText.text = GetKindLabel(data.eventKind);

        if (data.eventType == GameEventType.Confirm)
            ShowConfirm(data);
        else if (data.eventType == GameEventType.Choice)
            ShowChoice(data);
    }

    private void ShowConfirm(PopupData data)
    {
        if (confirmButton != null) confirmButton.SetActive(true);
        if (choiceButtonContainer != null) choiceButtonContainer.SetActive(false);
        if (tagPanel != null) tagPanel.SetActive(false);
        currentConfirmEffects = data.confirmEffects;
    }

    private void ShowChoice(PopupData data)
    {
        if (confirmButton != null) confirmButton.SetActive(false);
        if (choiceButtonContainer != null) choiceButtonContainer.SetActive(true);

        RefreshTagPanel(data);

        // Clear old clones (skip the template prefab)
        if (choiceButtonContainer != null)
        {
            for (int i = choiceButtonContainer.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = choiceButtonContainer.transform.GetChild(i);
                if (choiceButtonPrefab != null && child.gameObject == choiceButtonPrefab)
                    continue;
                Destroy(child.gameObject);
            }
        }

        if (data.choiceTexts == null || data.choiceTexts.Length == 0
            || choiceButtonPrefab == null || choiceButtonContainer == null)
        {
            Debug.LogWarning("[EventUI] No usable choices; falling back to confirm-style dismissal.");
            if (confirmButton != null) confirmButton.SetActive(true);
            if (choiceButtonContainer != null) choiceButtonContainer.SetActive(false);
            if (tagPanel != null) tagPanel.SetActive(false);
            currentConfirmEffects = null;
            return;
        }

        SettlementBridge bridge = SettlementBridge.Instance;
        GameRunState runState = bridge != null ? bridge.RunState : null;
        HashSet<TenantAbility> ownedAbilities = TenantAbilityResolver.GetOwnedAbilities(
            runState,
            TenantReviewCoordinator.Instance != null ? TenantReviewCoordinator.Instance.candidates : null);

        for (int i = 0; i < data.choiceTexts.Length; i++)
        {
            GameObject root = Instantiate(choiceButtonPrefab, choiceButtonContainer.transform);
            root.gameObject.SetActive(true);
            root.gameObject.name = $"ChoiceButton_{i}";

            Button btn = root.GetComponentInChildren<Button>(true);
            if (btn == null)
            {
                Debug.LogWarning($"[EventUI] Choice prefab has no Button at index {i}.");
                Destroy(root.gameObject);
                continue;
            }

            TextMeshProUGUI optionText = FindTMP(root, optionTextPath);
            if (optionText != null) optionText.text = data.choiceTexts[i];

            TextMeshProUGUI effectText = FindTMP(root, effectTextPath);
            if (effectText != null)
                effectText.text = (data.choiceEffectTexts != null && i < data.choiceEffectTexts.Length)
                    ? (data.choiceEffectTexts[i] ?? string.Empty)
                    : string.Empty;

            TenantAbility[] required = (data.choiceRequiredTags != null && i < data.choiceRequiredTags.Length)
                ? data.choiceRequiredTags[i]
                : null;
            EventEffect[] effects = (data.choiceEffects != null && i < data.choiceEffects.Length)
                ? data.choiceEffects[i]
                : null;
            bool affordable = EventAffordability.CanAfford(effects, runState);
            btn.interactable = TenantAbilityResolver.HasAllRequiredTags(required, ownedAbilities) && affordable;

            int choiceIndex = i;
            PopupData capturedData = data;
            btn.onClick.AddListener(() => OnChoiceSelected(choiceIndex, capturedData));
        }

        currentConfirmEffects = null;
    }

    private void RefreshTagPanel(PopupData data)
    {
        if (tagPanel == null || tagPrefab == null)
        {
            if (tagPanel != null) tagPanel.SetActive(false);
            return;
        }

        // Clear old generated items (never delete the template)
        for (int i = tagPanel.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = tagPanel.transform.GetChild(i);
            if (child.gameObject == tagPrefab)
                continue;
            Destroy(child.gameObject);
        }

        int generated = 0;
        if (data.choiceRequiredTags != null)
        {
            for (int o = 0; o < data.choiceRequiredTags.Length; o++)
            {
                TenantAbility[] tags = data.choiceRequiredTags[o];
                if (tags == null || tags.Length == 0) continue;

                foreach (TenantAbility tag in tags)
                {
                    GameObject clone = Instantiate(tagPrefab, tagPanel.transform);
                    clone.gameObject.SetActive(true);

                    TextMeshProUGUI label = FindTMP(clone, tagTextPath);
                    if (label != null)
                        label.text = $"{o + 1}·{AbilityDisplayName.Get(tag)}";
                    generated++;
                }
            }
        }

        tagPanel.SetActive(generated > 0);
    }

    private static TextMeshProUGUI FindTMP(GameObject root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        Transform target = root.transform.Find(path);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private static string GetKindLabel(EventKind kind)
    {
        switch (kind)
        {
            case EventKind.ChainStep: return "故事";
            case EventKind.Personal: return "个人";
            case EventKind.SpecialVisitor: return "特殊";
            default: return "普通";
        }
    }

    private void OnConfirmClicked()
    {
        if (string.IsNullOrEmpty(currentEventId))
        {
            Debug.LogWarning("[EventUI] Confirm ignored: no active popup.");
            return;
        }

        string eventId = currentEventId;
        EventEffect[] effects = currentConfirmEffects;
        ResetPopupState();
        Close();
        if (onEventProcessed != null)
            onEventProcessed.RaiseProcessed(new EventProcessedData
            {
                eventId = eventId,
                optionId = string.Empty,
                effects = effects
            });
    }

    private void OnChoiceSelected(int index, PopupData data)
    {
        if (string.IsNullOrEmpty(currentEventId))
        {
            Debug.LogWarning("[EventUI] Choice ignored: no active popup.");
            return;
        }

        if (data.choiceTexts == null || index < 0 || index >= data.choiceTexts.Length)
        {
            Debug.LogWarning($"[EventUI] Choice index {index} is invalid; falling back to confirm-style dismissal.");
            string fallbackEventId = currentEventId;
            ResetPopupState();
            Close();
            if (onEventProcessed != null)
                onEventProcessed.RaiseProcessed(new EventProcessedData
                {
                    eventId = fallbackEventId,
                    optionId = string.Empty,
                    effects = null
                });
            return;
        }

        string choiceText = data.choiceTexts[index] ?? string.Empty;
        string choiceResult = (data.choiceResults != null && index < data.choiceResults.Length)
            ? (data.choiceResults[index] ?? string.Empty)
            : string.Empty;
        Debug.Log($"[EventUI] Choice: {choiceText} → {choiceResult}");

        string optionId = (data.choiceIds != null && index >= 0 && index < data.choiceIds.Length)
            ? data.choiceIds[index]
            : string.Empty;
        EventEffect[] effects = (data.choiceEffects != null && index >= 0 && index < data.choiceEffects.Length)
            ? data.choiceEffects[index]
            : null;
        TenantAbility[] requiredTags = (data.choiceRequiredTags != null && index >= 0 && index < data.choiceRequiredTags.Length)
            ? data.choiceRequiredTags[index]
            : null;

        string eventId = currentEventId;
        ResetPopupState();
        Close();
        if (onEventProcessed != null)
            onEventProcessed.RaiseProcessed(new EventProcessedData
            {
                eventId = eventId,
                optionId = optionId,
                effects = effects,
                requiredTags = requiredTags
            });
    }

    private void ResetPopupState()
    {
        currentEventId = null;
        currentConfirmEffects = null;
    }

    private void Close()
    {
        if (eventPanel != null) eventPanel.SetActive(false);
        if (eventOverlay != null) eventOverlay.SetActive(false);
    }
}
