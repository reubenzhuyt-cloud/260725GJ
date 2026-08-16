using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class TenantLogPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private TMPro.TextMeshProUGUI rowTemplate;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMPro.TextMeshProUGUI emptyStateLabel;

    private string _currentTenantId;
    private bool _runStateRestoredSubscribed;

    private void OnEnable()
    {
        SubscribeRunStateRestored();
    }

    private void OnDisable()
    {
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

    private void OnRunStateRestored(GameRunState state)
    {
        if (state == null)
            return;
        if (string.IsNullOrEmpty(_currentTenantId))
            return;
        RefreshForTenant(_currentTenantId);
    }

    public void RefreshForTenant(string tenantId)
    {
        GameRunState state = SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
        if (string.IsNullOrEmpty(tenantId) || state == null ||
            state.Tenants == null || !state.Tenants.ContainsKey(tenantId))
        {
            ClearLog();
            return;
        }

        _currentTenantId = tenantId;

        IReadOnlyList<TenantLogEntry> all = TenantLogManager.Query(state, tenantId).All();
        int count = all != null ? all.Count : 0;

        var lines = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            string line = FormatLine(all[i]);
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }

        if (lines.Count == 0)
        {
            ClearLog();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        if (rowTemplate != null)
        {
            rowTemplate.text = string.Join("\n", lines);
            rowTemplate.gameObject.SetActive(true);
        }

        if (emptyStateLabel != null)
            emptyStateLabel.gameObject.SetActive(false);

        if (contentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    public void ClearLog()
    {
        _currentTenantId = null;
        if (rowTemplate != null)
        {
            rowTemplate.text = string.Empty;
            rowTemplate.gameObject.SetActive(false);
        }
        if (emptyStateLabel != null)
            emptyStateLabel.gameObject.SetActive(true);
        if (contentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private static string FormatLine(TenantLogEntry entry)
    {
        if (entry == null)
            return string.Empty;
        string summary = entry.Summary ?? string.Empty;
        return $"第 {entry.Day} 天 · {PhaseText(entry.Phase)} · {CategoryText(entry.Category)} · {summary}";
    }

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

    private static string CategoryText(TenantLogCategory category)
    {
        return category switch
        {
            TenantLogCategory.Recruit => "招募",
            TenantLogCategory.RoomAssignment => "入住",
            TenantLogCategory.RoomMove => "换房",
            TenantLogCategory.WorkAssignment => "工作",
            TenantLogCategory.Behavior => "行为",
            _ => "其他",
        };
    }
}
