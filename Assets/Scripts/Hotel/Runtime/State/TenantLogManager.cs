using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotel.Runtime
{
    public readonly struct TenantLogWriteDto
    {
        public readonly string TenantId;
        public readonly TenantLogCategory Category;
        public readonly int Day;
        public readonly HotelPhase Phase;
        public readonly string Summary;
        public readonly string DetailKey;

        public TenantLogWriteDto(string tenantId, TenantLogCategory category, int day, HotelPhase phase, string summary, string detailKey)
        {
            TenantId = tenantId;
            Category = category;
            Day = day;
            Phase = phase;
            Summary = summary;
            DetailKey = detailKey;
        }
    }

    public interface ITenantLogQuery
    {
        int Count { get; }
        string TenantId { get; }
        IReadOnlyList<TenantLogEntry> All();
        IReadOnlyList<TenantLogEntry> ByCategory(TenantLogCategory category);
        IReadOnlyList<TenantLogEntry> Since(int lastSeenSequence);
        TenantLogEntry Get(int sequence);
    }

    public static class TenantLogManager
    {
        public static bool Record(GameRunState state, TenantLogWriteDto dto)
        {
            if (state == null)
            {
                Debug.LogWarning("[TenantLogManager] Record: state is null.");
                return false;
            }
            if (string.IsNullOrEmpty(dto.TenantId))
            {
                Debug.LogWarning("[TenantLogManager] Record: tenantId is empty; rejected.");
                return false;
            }
            if (state.Tenants == null || !state.Tenants.ContainsKey(dto.TenantId))
            {
                Debug.LogWarning($"[TenantLogManager] Record: tenant {dto.TenantId} is not recruited; rejected.");
                return false;
            }
            try
            {
                if (state.TenantLogs == null)
                    state.TenantLogs = new Dictionary<string, List<TenantLogEntry>>();
                if (!state.TenantLogs.TryGetValue(dto.TenantId, out List<TenantLogEntry> entries) || entries == null)
                {
                    entries = new List<TenantLogEntry>();
                    state.TenantLogs[dto.TenantId] = entries;
                }
                entries.Add(new TenantLogEntry
                {
                    Sequence = entries.Count + 1,
                    Day = dto.Day,
                    Phase = dto.Phase,
                    Category = dto.Category,
                    Summary = dto.Summary ?? string.Empty,
                    DetailKey = dto.DetailKey ?? string.Empty
                });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TenantLogManager] Record failed: {exception}");
                return false;
            }
        }

        public static ITenantLogQuery Query(GameRunState state, string tenantId)
        {
            return new TenantLogQuery(state, tenantId);
        }

        public static IReadOnlyList<PlayerLogEntry> QueryGlobalByTenant(GameRunState state, string tenantId)
        {
            return PlayerLogManager.Query(state).ByTenant(tenantId);
        }
    }

    internal sealed class TenantLogQuery : ITenantLogQuery
    {
        private readonly GameRunState _state;
        private readonly string _tenantId;

        public TenantLogQuery(GameRunState state, string tenantId)
        {
            _state = state;
            _tenantId = tenantId;
        }

        public int Count
        {
            get
            {
                if (!TryGetEntries(out List<TenantLogEntry> entries))
                    return 0;
                return entries.Count;
            }
        }

        public string TenantId => _tenantId;

        public IReadOnlyList<TenantLogEntry> All()
        {
            var result = new List<TenantLogEntry>();
            if (!TryGetEntries(out List<TenantLogEntry> entries))
                return result;
            for (int i = 0; i < entries.Count; i++)
                result.Add(Clone(entries[i]));
            return result;
        }

        public IReadOnlyList<TenantLogEntry> ByCategory(TenantLogCategory category)
        {
            var result = new List<TenantLogEntry>();
            if (!TryGetEntries(out List<TenantLogEntry> entries))
                return result;
            for (int i = 0; i < entries.Count; i++)
            {
                TenantLogEntry entry = entries[i];
                if (entry != null && entry.Category == category)
                    result.Add(Clone(entry));
            }
            result.Sort((a, b) => b.Sequence.CompareTo(a.Sequence));
            return result;
        }

        public IReadOnlyList<TenantLogEntry> Since(int lastSeenSequence)
        {
            var result = new List<TenantLogEntry>();
            if (!TryGetEntries(out List<TenantLogEntry> entries))
                return result;
            for (int i = 0; i < entries.Count; i++)
            {
                TenantLogEntry entry = entries[i];
                if (entry != null && entry.Sequence > lastSeenSequence)
                    result.Add(Clone(entry));
            }
            return result;
        }

        public TenantLogEntry Get(int sequence)
        {
            if (!TryGetEntries(out List<TenantLogEntry> entries))
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                TenantLogEntry entry = entries[i];
                if (entry != null && entry.Sequence == sequence)
                    return Clone(entry);
            }
            return null;
        }

        private bool TryGetEntries(out List<TenantLogEntry> entries)
        {
            entries = null;
            if (_state == null || string.IsNullOrEmpty(_tenantId) || _state.TenantLogs == null)
                return false;
            if (!_state.TenantLogs.TryGetValue(_tenantId, out List<TenantLogEntry> found) || found == null)
                return false;
            entries = found;
            return true;
        }

        private static TenantLogEntry Clone(TenantLogEntry entry)
        {
            if (entry == null)
                return null;
            return new TenantLogEntry
            {
                Sequence = entry.Sequence,
                Day = entry.Day,
                Phase = entry.Phase,
                Category = entry.Category,
                Summary = entry.Summary,
                DetailKey = entry.DetailKey
            };
        }
    }
}
