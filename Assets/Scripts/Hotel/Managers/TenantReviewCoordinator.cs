using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public class TenantReviewCoordinator : MonoBehaviour
{
    public static TenantReviewCoordinator Instance { get; private set; }

    [Header("Event Channel")]
    public PhaseEnteredEvent onPhaseEntered;

    [Header("UI")]
    public TenantReviewPanel reviewPanel;

    [Header("Candidates")]
    public List<TenantReviewCandidateSO> candidates = new List<TenantReviewCandidateSO>();

    private StateReducer _reducer;
    private GameRunState _runState;
    private TenantReviewCandidateSO[] _shuffledOrder;
    private Dictionary<string, TenantReviewCandidateSO> _candidateLookup;
    private Action _onReviewResolved;
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

        BuildLookup();
        _shuffledOrder = GetShuffledOrder(_runState.Seed);
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





private void OnPhaseEntered(PhaseEnterData data)
    {
        // Presentation is owned by EventManager's shared queue.
    }

    public bool TryBeginReview(Action onResolved)
    {
        if (_panelActive || _runState == null || _shuffledOrder == null)
            return false;

        if (!TryGetNextCandidate(out var candidate))
            return false;

        _onReviewResolved = onResolved;
        ShowReview(candidate);
        return true;
    }

    public bool HasPendingReview()
    {
        if (_runState == null || _shuffledOrder == null) return false;
        return TryGetNextCandidate(out _);
    }

    private bool TryGetNextCandidate(out TenantReviewCandidateSO candidate)
    {
        for (int i = 0; i < _shuffledOrder.Length; i++)
        {
            string id = _shuffledOrder[i].candidateId;
            bool resolved = false;
            for (int j = 0; j < _runState.ResolvedReviewCandidateIds.Count; j++)
            {
                if (_runState.ResolvedReviewCandidateIds[j] == id)
                {
                    resolved = true;
                    break;
                }
            }
            if (!resolved)
            {
                candidate = _shuffledOrder[i];
                return true;
            }
        }
        candidate = null;
        return false;
    }

    private void ShowReview(TenantReviewCandidateSO candidate)
    {
        if (reviewPanel == null) return;
        _panelActive = true;
        reviewPanel.Show(
            candidate.displayName,
            candidate.avatarColor,
            candidate.shortDescription,
            candidate.detailedDescription,
            OnConfirm,
            OnReject);
    }

private void HideReview()
    {
        _panelActive = false;
        if (reviewPanel != null)
            reviewPanel.Hide();
    }

private void OnConfirm()
    {
        if (!_panelActive) return;

        Action resolvedCallback = _onReviewResolved;
        _onReviewResolved = null;

        if (!TryGetNextCandidate(out var candidate))
        {
            HideReview();
            resolvedCallback?.Invoke();
            return;
        }

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantReviewCoordinator",
            "ConfirmCandidate");
        changeSet.Add(new AddTenantChange(candidate.candidateId, candidate.candidateId));
        changeSet.Add(new ResolveCandidateChange(candidate.candidateId));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);
        if (result.Succeeded)
        {
            if (TenantAssignmentCoordinator.Instance != null)
                TenantAssignmentCoordinator.Instance.RegisterTenant(candidate.candidateId, candidate.displayName, candidate.avatarColor);

            Debug.Log($"[TenantReviewCoordinator] Confirmed candidate: {candidate.displayName}");
        }
        else
        {
            Debug.LogError($"[TenantReviewCoordinator] Failed to commit confirm for: {candidate.displayName}");
        }

        HideReview();
        resolvedCallback?.Invoke();
    }

private void OnReject()
    {
        if (!_panelActive) return;

        Action resolvedCallback = _onReviewResolved;
        _onReviewResolved = null;

        if (!TryGetNextCandidate(out var candidate))
        {
            HideReview();
            resolvedCallback?.Invoke();
            return;
        }

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantReviewCoordinator",
            "RejectCandidate");
        changeSet.Add(new ResolveCandidateChange(candidate.candidateId));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);
        if (result.Succeeded)
        {
            Debug.Log($"[TenantReviewCoordinator] Rejected candidate: {candidate.displayName}");
        }
        else
        {
            Debug.LogError($"[TenantReviewCoordinator] Failed to commit reject for: {candidate.displayName}");
        }

        HideReview();
        resolvedCallback?.Invoke();
    }
}
