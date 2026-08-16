using UnityEngine;
using System.Collections;

public class GamePhaseManager : MonoBehaviour
{
    public static GamePhaseManager Instance { get; private set; }

    [Header("State")]
    public int currentDay = 1;
    public GamePhase currentPhase = GamePhase.Dawn;

    [Header("Event Channel")]
    public PhaseEnteredEvent onPhaseEntered;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Force Day on every play — scene serialized value might still be Dawn
        if (SettlementBridge.Instance != null && SettlementBridge.Instance.RunState != null)
        {
            currentDay = Mathf.Max(1, SettlementBridge.Instance.RunState.Day);
            currentPhase = ToGamePhase(SettlementBridge.Instance.RunState.Phase.Current);
        }
        else
        {
            currentDay = 1;
            currentPhase = GamePhase.Dawn;
        }
    }

    private void Start()
    {
        // Delay 1 frame so all listeners (NextPhasePanel, EventManager, PhaseUI) register in their OnEnable
        StartCoroutine(InitPhase());
    }

    private IEnumerator InitPhase()
    {
        yield return null;
        NotifyPhaseEntered();
    }

    public void AdvancePhase()
    {
        if (!CanAdvancePhase())
        {
            Debug.Log("[GamePhaseManager] Advance blocked until review, assignment, and events are complete.");
            return;
        }

        if (SettlementBridge.Instance != null
            && !SettlementBridge.Instance.TrySettleJobs(currentDay, currentPhase))
        {
            Debug.LogError("[GamePhaseManager] Advance blocked because job settlement failed.");
            return;
        }

        GamePhase nextPhase = GetNextPhase();
        int targetDay = currentDay + (nextPhase == GamePhase.Dawn ? 1 : 0);

        if (IsHiddenPhase(nextPhase))
        {
            if ((EventManager.Instance != null && EventManager.Instance.HasPreGeneratedEvents(nextPhase))
                || ShouldForceEnterHiddenPhase(nextPhase, targetDay))
            {
                // Hidden phase has pre-generated events or pending review → enter it
                currentPhase = nextPhase;
                if (currentPhase == GamePhase.Dawn)
                    currentDay++;
            }
            else
            {
                // Hidden phase has no events → skip to the next normal phase
                Debug.Log($"[GamePhaseManager] Skipping hidden phase {nextPhase}");
                currentPhase = GetPhaseAfterHidden(nextPhase);

                if (nextPhase == GamePhase.Dawn)
                    currentDay++;
            }
        }
        else
        {
            currentPhase = nextPhase;

            if (currentPhase == GamePhase.Dawn)
                currentDay++;
        }

        Debug.Log($"[GamePhaseManager] Advanced to Day {currentDay} - {currentPhase}");
        NotifyPhaseEntered();
    }

    public bool CanAdvancePhase()
    {
        if (TenantReviewCoordinator.Instance != null && TenantReviewCoordinator.Instance.IsReviewActive)
            return false;
        if (TenantAssignmentCoordinator.Instance != null && TenantAssignmentCoordinator.Instance.HasUnassignedTenants)
            return false;
        return EventManager.Instance == null || EventManager.Instance.IsPhaseComplete;
    }

    private GamePhase GetNextPhase()
    {
        switch (currentPhase)
        {
            case GamePhase.Dawn:  return GamePhase.Day;
            case GamePhase.Day:   return GamePhase.Dusk;
            case GamePhase.Dusk:  return GamePhase.Night;
            case GamePhase.Night: return GamePhase.Dawn;
            default: return GamePhase.Day;
        }
    }

    private bool IsHiddenPhase(GamePhase phase)
    {
        return phase == GamePhase.Dawn || phase == GamePhase.Dusk;
    }

    private bool ShouldForceEnterHiddenPhase(GamePhase phase, int day)
    {
        return TenantReviewCoordinator.Instance != null
            && TenantReviewCoordinator.Instance.HasScheduledReview(day, phase);
    }

    private GamePhase GetPhaseAfterHidden(GamePhase hiddenPhase)
    {
        // Dusk → skip to Night (same day), Dawn → skip to Day (new day)
        switch (hiddenPhase)
        {
            case GamePhase.Dusk: return GamePhase.Night;
            case GamePhase.Dawn: return GamePhase.Day;
            default: return GamePhase.Day;
        }
    }

    private void NotifyPhaseEntered()
    {
        if (currentPhase == GamePhase.Day && EventManager.Instance != null)
        {
            EventManager.Instance.PreGenerateDayEvents(currentDay);
        }

        if (onPhaseEntered != null)
        {
            onPhaseEntered.Raise(new PhaseEnterData
            {
                day = currentDay,
                phase = currentPhase
            });
        }
    }

    private static GamePhase ToGamePhase(Hotel.Runtime.HotelPhase phase)
    {
        return phase switch
        {
            Hotel.Runtime.HotelPhase.Dawn => GamePhase.Dawn,
            Hotel.Runtime.HotelPhase.Dusk => GamePhase.Dusk,
            Hotel.Runtime.HotelPhase.Night => GamePhase.Night,
            _ => GamePhase.Day,
        };
    }
}
