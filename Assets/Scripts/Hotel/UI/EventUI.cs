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

        if (data.choiceTexts == null || choiceButtonPrefab == null || choiceButtonContainer == null)
        {
            Debug.LogWarning("[EventUI] Missing references for choice buttons!");
            return;
        }

        HashSet<TenantAbility> ownedAbilities = GetOwnedAbilities();

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
            btn.interactable = HasAllRequiredTags(required, ownedAbilities);

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

    private static HashSet<TenantAbility> GetOwnedAbilities()
    {
        var owned = new HashSet<TenantAbility>();

        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
            return owned;

        TenantReviewCoordinator coordinator = TenantReviewCoordinator.Instance;
        List<TenantReviewCandidateSO> candidates = coordinator != null ? coordinator.candidates : null;

        foreach (KeyValuePair<string, TenantRunState> pair in bridge.RunState.Tenants)
        {
            TenantAbility ability = TenantAbility.None;
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    TenantReviewCandidateSO candidate = candidates[i];
                    if (candidate == null || candidate.candidateId != pair.Key)
                        continue;
                    ability = candidate.ability;
                    break;
                }
            }

            if (ability != TenantAbility.None)
                owned.Add(ability);
        }

        return owned;
    }

    private static bool HasAllRequiredTags(TenantAbility[] required, HashSet<TenantAbility> owned)
    {
        if (required == null || required.Length == 0) return true;

        foreach (TenantAbility tag in required)
        {
            if (!owned.Contains(tag))
                return false;
        }

        return true;
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
