using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public class TenantReviewCoordinator : MonoBehaviour
{
    public static TenantReviewCoordinator Instance { get; private set; }
    public event Action ReviewBatchCompleted;
    public bool IsReviewActive => _panelActive;

    [Header("Event Channel")]
    public PhaseEnteredEvent onPhaseEntered;

    [Header("UI")]
    public TenantReviewPanel reviewPanel;
    [SerializeField] private UIManager uiManager;

    [Header("Candidates")]
    public List<TenantReviewCandidateSO> candidates = new List<TenantReviewCandidateSO>();

    [Header("Pool")]
    [Tooltip("启用 4.2.5 访客生成池（每局从名字×文案×头像池确定性组合 40 人，物化为 SO 填入 candidates）。关闭则使用场景中序列化的旧候选列表。")]
    [System.NonSerialized]
    public bool usePool = true;

    private StateReducer _reducer;
    private GameRunState _runState;
    private TenantReviewCandidateSO[] _shuffledOrder;
    private Dictionary<string, TenantReviewCandidateSO> _candidateLookup;
    private Dictionary<string, TenantErosionTier> _tierByCandidateId;
    private IReadOnlyList<VisitorArrival> _arrivalSchedule;
    private readonly List<TenantReviewCandidateSO> _activeBatch = new List<TenantReviewCandidateSO>();
    private int _activeBatchIndex;
    private int _activeDay;
    private HotelPhase _activePhase;
    private bool _panelActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (SettlementBridge.Instance == null)
        {
            Debug.LogError("[TenantReviewCoordinator] SettlementBridge.Instance is null!");
            return;
        }

        _reducer = SettlementBridge.Instance.Reducer;
        _runState = SettlementBridge.Instance.RunState;

        var seed = _runState.Seed;
        if (usePool && TenantPoolManager.BuildRun(seed, ErosionWeightProfile.Default, 40))
        {
            // 生成池模式：40 个池档案物化为 SO 填入 candidates，下游（UI/派遣/事件）全部按 SO 读取，行为不变。
            candidates.Clear();
            _tierByCandidateId = new Dictionary<string, TenantErosionTier>();
            for (int i = 0; i < TenantPoolManager.NormalProfiles.Count; i++)
            {
                var p = TenantPoolManager.NormalProfiles[i];
                var so = ScriptableObject.CreateInstance<TenantReviewCandidateSO>();
                so.candidateId = p.candidateId;
                so.displayName = p.displayName;
                so.avatarKey = p.avatarKey;
                so.portrait = p.ResolvePortrait();
                so.avatarColor = p.avatarColor;
                so.ability = p.ability;
                so.activityType = p.activityType;
                so.shortDescription = p.shortDescription;
                // 注意：审查面板的 shortDescriptionLabel 与 detailedDescriptionLabel 在场景中绑定到同一个 TMP（Content），
                // Show() 先写短描述、再写详细描述覆盖；详细区域是玩家实际看到的描述，因此池文案同时填入两处。
                so.detailedDescription = p.shortDescription;
                candidates.Add(so);
                _tierByCandidateId[p.candidateId] = p.tier;
            }
            BuildLookup();
            _shuffledOrder = GetShuffledOrder(seed);
            var visitCount = new System.Random(seed ^ 0x50524F).Next(20, Math.Min(41, _shuffledOrder.Length + 1));
            _arrivalSchedule = VisitorArrivalScheduler.CreateSchedule(seed, visitCount);
            Debug.Log($"[TenantReviewCoordinator] 生成池模式：{_shuffledOrder.Length} 档案，本局到访约 {visitCount} 人");
        }
        else
        {
            _tierByCandidateId = null;
            BuildLookup();
            _shuffledOrder = GetShuffledOrder(seed);
            _arrivalSchedule = VisitorArrivalScheduler.CreateSchedule(seed, _shuffledOrder.Length);
        }
    }

    private void BuildLookup()
    {
        _candidateLookup = new Dictionary<string, TenantReviewCandidateSO>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;
            if (string.IsNullOrEmpty(candidates[i].candidateId)) continue;
            _candidateLookup[candidates[i].candidateId] = candidates[i];
        }
    }

    public bool TryGetCandidatePresentation(string candidateId, out string displayName, out Color color, out string avatarKey)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || candidate.candidateId != candidateId) continue;
            displayName = candidate.displayName;
            color = candidate.avatarColor;
            avatarKey = candidate.avatarKey;
            return true;
        }

        displayName = candidateId;
        color = Color.white;
        avatarKey = null;
        return false;
    }

    private TenantReviewCandidateSO[] GetShuffledOrder(int seed)
    {
        var valid = new List<TenantReviewCandidateSO>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null && !string.IsNullOrEmpty(candidates[i].candidateId))
                valid.Add(candidates[i]);
        }

        var result = valid.ToArray();
        var rng = new System.Random(seed);
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = result[i];
            result[i] = result[j];
            result[j] = temp;
        }
        return result;
    }

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (_runState == null || _shuffledOrder == null || _panelActive) return;
        if (!TryBuildBatch(data.day, data.phase)) return;

        _activeDay = data.day;
        _activePhase = ToHotelPhase(data.phase);
        _activeBatchIndex = 0;
        ShowCurrentReview();
    }

    public bool HasPendingReview()
    {
        if (_panelActive) return true;
        if (GamePhaseManager.Instance == null) return false;
        return HasScheduledReview(GamePhaseManager.Instance.currentDay, GamePhaseManager.Instance.currentPhase);
    }

    public bool HasScheduledReview(int day, GamePhase phase)
    {
        if (_runState == null || _shuffledOrder == null || _arrivalSchedule == null)
            return false;

        if (!TryGetArrival(day, phase, out var arrival, out var startIndex))
            return false;

        var endIndex = Math.Min(startIndex + arrival.VisitorCount, _shuffledOrder.Length);
        for (var index = startIndex; index < endIndex; index++)
        {
            if (!_runState.ResolvedReviewCandidateIds.Contains(_shuffledOrder[index].candidateId))
                return true;
        }

        return false;
    }

    private bool TryBuildBatch(int day, GamePhase phase)
    {
        _activeBatch.Clear();
        if (!TryGetArrival(day, phase, out var arrival, out var startIndex))
            return false;

        var endIndex = Math.Min(startIndex + arrival.VisitorCount, _shuffledOrder.Length);
        for (var index = startIndex; index < endIndex; index++)
        {
            var candidate = _shuffledOrder[index];
            if (!_runState.ResolvedReviewCandidateIds.Contains(candidate.candidateId))
                _activeBatch.Add(candidate);
        }

        return _activeBatch.Count > 0;
    }

    private bool TryGetArrival(int day, GamePhase phase, out VisitorArrival arrival, out int startIndex)
    {
        startIndex = 0;
        var runtimePhase = ToHotelPhase(phase);
        for (var index = 0; index < _arrivalSchedule.Count; index++)
        {
            var scheduled = _arrivalSchedule[index];
            if (scheduled.Day == day && scheduled.Phase == runtimePhase)
            {
                arrival = scheduled;
                return true;
            }
            startIndex += scheduled.VisitorCount;
        }

        arrival = default;
        return false;
    }

    private static HotelPhase ToHotelPhase(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Dawn => HotelPhase.Dawn,
            GamePhase.Dusk => HotelPhase.Dusk,
            GamePhase.Night => HotelPhase.Night,
            _ => HotelPhase.Day,
        };
    }

    private void ShowCurrentReview()
    {
        if (_activeBatchIndex >= _activeBatch.Count)
        {
            CompleteBatch();
            return;
        }
        if (reviewPanel == null)
        {
            Debug.LogError("[TenantReviewCoordinator] Review panel is not assigned.");
            CompleteBatch();
            return;
        }

        var candidate = _activeBatch[_activeBatchIndex];
        var canRecruit = HasRecruitmentCapacity();
        _panelActive = true;
        // External activation control (Event popup pattern): the controller lives
        // outside the panel hierarchy and activates it before populating content.
        reviewPanel.gameObject.SetActive(true);
        reviewPanel.Show(
            candidate.displayName,
            candidate.portrait,
            candidate.avatarColor,
            candidate.ability,
            candidate.activityType,
            candidate.shortDescription,
            candidate.detailedDescription,
            canRecruit,
            canRecruit ? null : "旅馆没有空房，无法招募。",
            OnConfirm,
            OnReject);
    }

    private bool HasRecruitmentCapacity()
    {
        if (_runState == null) return false;
        return _runState.Tenants.Count < _runState.Rooms.Count;
    }

    private void HideReview()
    {
        _panelActive = false;
        if (reviewPanel != null)
        {
            reviewPanel.Hide();
            // External deactivation (Event popup pattern) — restore the default
            // inactive state so the panel is hidden until the next review batch.
            reviewPanel.gameObject.SetActive(false);
        }
    }

    private TenantErosionTier GetTier(TenantReviewCandidateSO candidate)
    {
        if (_tierByCandidateId == null) return TenantErosionTier.Any;
        return _tierByCandidateId.TryGetValue(candidate.candidateId, out var tier) ? tier : TenantErosionTier.Any;
    }

    private void OnConfirm()
    {
        if (!_panelActive) return;
        if (!HasRecruitmentCapacity())
        {
            ShowCurrentReview();
            return;
        }

        var candidate = _activeBatch[_activeBatchIndex];
        var initialErosion = VisitorArrivalScheduler.GetInitialErosion(_runState.Seed, candidate.candidateId, GetTier(candidate));

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantReviewCoordinator",
            "ConfirmCandidate");
        changeSet.Add(new AddTenantChange(candidate.candidateId, candidate.candidateId, initialErosion, candidate.avatarKey));
        changeSet.Add(new ResolveCandidateChange(new ReviewDecisionRecord
        {
            CandidateId = candidate.candidateId,
            Decision = ReviewDecision.Recruit,
            Day = _activeDay,
            Phase = _activePhase,
            InitialErosion = initialErosion
        }));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);
        if (result.Succeeded)
        {
            uiManager?.ShowNotice($"已招募 {candidate.displayName}");
            if (TenantAssignmentCoordinator.Instance != null)
                TenantAssignmentCoordinator.Instance.RegisterTenant(candidate.candidateId, candidate.displayName, candidate.avatarColor, candidate.avatarKey);

            PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
                PlayerLogCategory.TenantRecruit,
                _activeDay,
                _activePhase,
                "租客招募",
                $"已招募 {candidate.displayName}",
                candidate.candidateId,
                candidate.candidateId));

            TenantLogManager.Record(_runState, new TenantLogWriteDto(
                candidate.candidateId,
                TenantLogCategory.Recruit,
                _activeDay,
                _activePhase,
                $"已招募 {candidate.displayName}",
                candidate.candidateId));

            Debug.Log($"[TenantReviewCoordinator] Confirmed candidate: {candidate.displayName}");
        }
        else
        {
            Debug.LogError($"[TenantReviewCoordinator] Failed to commit confirm for: {candidate.displayName}");
        }

        if (result.Succeeded)
            AdvanceBatch();
    }

    private void OnReject()
    {
        if (!_panelActive) return;

        var candidate = _activeBatch[_activeBatchIndex];
        var initialErosion = VisitorArrivalScheduler.GetInitialErosion(_runState.Seed, candidate.candidateId, GetTier(candidate));

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantReviewCoordinator",
            "RejectCandidate");
        changeSet.Add(new ResolveCandidateChange(new ReviewDecisionRecord
        {
            CandidateId = candidate.candidateId,
            Decision = ReviewDecision.Reject,
            Day = _activeDay,
            Phase = _activePhase,
            InitialErosion = initialErosion
        }));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);
        if (result.Succeeded)
        {
            uiManager?.ShowNotice($"已拒绝 {candidate.displayName}");
            PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
                PlayerLogCategory.TenantReject,
                _activeDay,
                _activePhase,
                "租客拒绝",
                $"拒绝 {candidate.displayName}",
                candidate.candidateId));

            Debug.Log($"[TenantReviewCoordinator] Rejected candidate: {candidate.displayName}");
        }
        else
        {
            Debug.LogError($"[TenantReviewCoordinator] Failed to commit reject for: {candidate.displayName}");
        }

        if (result.Succeeded)
            AdvanceBatch();
    }

    private void AdvanceBatch()
    {
        _activeBatchIndex++;
        ShowCurrentReview();
    }

    private void CompleteBatch()
    {
        HideReview();
        _activeBatch.Clear();
        _activeBatchIndex = 0;
        ReviewBatchCompleted?.Invoke();
    }
}