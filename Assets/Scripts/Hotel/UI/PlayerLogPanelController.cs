using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public class PlayerLogPanelController : MonoBehaviour
{
    [Header("Event Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    [Header("Card List")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject cardTemplate;
    [SerializeField] private TMPro.TextMeshProUGUI emptyStateLabel;

    private readonly HashSet<PlayerLogCategory> _categoryFilter = new HashSet<PlayerLogCategory>();
    private readonly List<PlayerLogCardView> _visibleCards = new List<PlayerLogCardView>();
    private readonly List<GameObject> _generatedCards = new List<GameObject>();
    private IPlayerLogQuery _query;
    private int _lastSeenSequence;
    private bool _runStateRestoredSubscribed;
#if UNITY_EDITOR
    private int _lastRefreshedDay = int.MinValue;
    private HotelPhase _lastRefreshedPhase = HotelPhase.Dawn;
#endif

    public IReadOnlyList<PlayerLogCardView> VisibleCards => _visibleCards;

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
        SubscribeRunStateRestored();
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
        UnsubscribeRunStateRestored();
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

    private void Start()
    {
        RefreshTimeline();
    }

    private void Update()
    {
        PollIncremental();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        RefreshTimeline();
    }

    private void OnRunStateRestored(GameRunState state)
    {
        if (state == null)
            return;
        RefreshTimeline();
    }

    public void SetCategoryFilter(PlayerLogCategory category, bool active)
    {
        if (active)
            _categoryFilter.Add(category);
        else
            _categoryFilter.Remove(category);
        RefreshTimeline();
    }

    public void ClearCategoryFilter()
    {
        _categoryFilter.Clear();
        RefreshTimeline();
    }

    public void RefreshTimeline()
    {
        _query = PlayerLogManager.Query(SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null);
        _visibleCards.Clear();

        if (_query != null)
        {
            var all = _query.All();
            if (all.Count > 0)
                _lastSeenSequence = all[all.Count - 1].Sequence;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                PlayerLogEntry entry = all[i];
                if (!MatchesFilter(entry.Category))
                    continue;
                _visibleCards.Add(new PlayerLogCardView
                {
                    Sequence = entry.Sequence,
                    Day = entry.Day,
                    PhaseText = PhaseText(entry.Phase),
                    Category = entry.Category,
                    Title = entry.Title,
                    Summary = entry.Summary
                });
            }
        }

        RebuildCardInstances();

#if UNITY_EDITOR
        if (SettlementBridge.Instance != null && SettlementBridge.Instance.RunState != null)
        {
            _lastRefreshedDay = SettlementBridge.Instance.RunState.Day;
            _lastRefreshedPhase = SettlementBridge.Instance.RunState.Phase.Current;
        }

        LogVisibleCards();
#endif
    }

    private void RebuildCardInstances()
    {
        for (int i = 0; i < _generatedCards.Count; i++)
        {
            if (_generatedCards[i] != null)
                Destroy(_generatedCards[i]);
        }
        _generatedCards.Clear();

        if (emptyStateLabel != null)
            emptyStateLabel.gameObject.SetActive(_visibleCards.Count == 0);

        if (contentRoot == null || cardTemplate == null)
            return;

        cardTemplate.SetActive(false);

        List<PlayerLogDayGroup> groups = BuildDayGroups();
        for (int g = 0; g < groups.Count; g++)
        {
            List<PlayerLogCardView> cards = groups[g].Cards;
            for (int c = 0; c < cards.Count; c++)
            {
                GameObject instance = Instantiate(cardTemplate, contentRoot);
                if (instance == null)
                    continue;
                _generatedCards.Add(instance);
                instance.SetActive(true);
                PlayerLogCardItem item = instance.GetComponentInChildren<PlayerLogCardItem>(true);
                if (item != null)
                    item.Bind(cards[c]);
            }
        }
    }

    public List<PlayerLogDayGroup> BuildDayGroups()
    {
        var groups = new List<PlayerLogDayGroup>();
        PlayerLogDayGroup current = null;
        for (int i = 0; i < _visibleCards.Count; i++)
        {
            PlayerLogCardView card = _visibleCards[i];
            if (current == null || current.Day != card.Day)
            {
                current = new PlayerLogDayGroup { Day = card.Day };
                groups.Add(current);
            }
            current.Cards.Add(card);
        }
        return groups;
    }

    private void PollIncremental()
    {
        if (_query == null)
            return;
        IReadOnlyList<PlayerLogEntry> newer = _query.Since(_lastSeenSequence);
        if (newer == null || newer.Count == 0)
            return;
        _lastSeenSequence = newer[newer.Count - 1].Sequence;
        RefreshTimeline();
    }

    private bool MatchesFilter(PlayerLogCategory category)
    {
        return _categoryFilter.Count == 0 || _categoryFilter.Contains(category);
    }

#if UNITY_EDITOR
    private void LogVisibleCards()
    {
        var parts = new List<string>(_visibleCards.Count);
        for (int i = 0; i < _visibleCards.Count; i++)
        {
            PlayerLogCardView card = _visibleCards[i];
            parts.Add($"#{card.Sequence}[{card.Category}]D{card.Day}{card.PhaseText}「{card.Title}」{card.Summary}");
        }
        Debug.Log($"[PlayerLogUI] day={_lastRefreshedDay} phase={_lastRefreshedPhase} cards={_visibleCards.Count} " + string.Join(" | ", parts));
    }
#endif

    private static string PhaseText(HotelPhase phase)
    {
        switch (phase)
        {
            case HotelPhase.Dawn: return "黎明";
            case HotelPhase.Day: return "白天";
            case HotelPhase.Dusk: return "黄昏";
            default: return "黑夜";
        }
    }
}
