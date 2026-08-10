using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotel.Runtime
{
    public readonly struct PlayerLogWriteDto
    {
        public readonly PlayerLogCategory Category;
        public readonly int Day;
        public readonly HotelPhase Phase;
        public readonly string Title;
        public readonly string Summary;
        public readonly string DetailKey;
        public readonly string TenantId;

        public PlayerLogWriteDto(PlayerLogCategory category, int day, HotelPhase phase, string title, string summary, string detailKey, string tenantId = null)
        {
            Category = category;
            Day = day;
            Phase = phase;
            Title = title;
            Summary = summary;
            DetailKey = detailKey;
            TenantId = tenantId;
        }
    }

    public interface IPlayerLogQuery
    {
        int Count { get; }
        IReadOnlyList<PlayerLogEntry> All();
        IReadOnlyList<PlayerLogEntry> ByDay(int day);
        IReadOnlyList<PlayerLogEntry> ByCategory(PlayerLogCategory category);
        IReadOnlyList<PlayerLogEntry> ByTenant(string tenantId);
        IReadOnlyList<PlayerLogEntry> Since(int lastSeenSequence);
        PlayerLogEntry Get(int sequence);
    }

    public static class PlayerLogManager
    {
        public static bool Record(GameRunState state, PlayerLogWriteDto dto)
        {
            if (state == null)
            {
                Debug.LogWarning("[PlayerLogManager] Record: state is null.");
                return false;
            }
            if (string.IsNullOrEmpty(dto.Summary))
            {
                Debug.LogWarning("[PlayerLogManager] Record: summary is empty; rejected.");
                return false;
            }
            try
            {
                if (state.PlayerLogs == null)
                    state.PlayerLogs = new List<PlayerLogEntry>();
                state.PlayerLogs.Add(new PlayerLogEntry
                {
                    Sequence = state.PlayerLogs.Count + 1,
                    Day = dto.Day,
                    Phase = dto.Phase,
                    Category = dto.Category,
                    Title = dto.Title ?? string.Empty,
                    Summary = dto.Summary,
                    DetailKey = dto.DetailKey,
                    TenantId = dto.TenantId
                });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerLogManager] Record failed: {exception}");
                return false;
            }
        }

        public static IPlayerLogQuery Query(GameRunState state)
        {
            return new PlayerLogQuery(state);
        }
    }

    internal sealed class PlayerLogQuery : IPlayerLogQuery
    {
        private readonly GameRunState _state;

        public PlayerLogQuery(GameRunState state)
        {
            _state = state;
        }

        public int Count
        {
            get
            {
                if (_state == null || _state.PlayerLogs == null)
                    return 0;
                return _state.PlayerLogs.Count;
            }
        }

        public IReadOnlyList<PlayerLogEntry> All()
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
                result.Add(Clone(_state.PlayerLogs[i]));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> ByDay(int day)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Day == day)
                    result.Add(Clone(entry));
            }
            result.Sort((a, b) => b.Sequence.CompareTo(a.Sequence));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> ByCategory(PlayerLogCategory category)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Category == category)
                    result.Add(Clone(entry));
            }
            result.Sort((a, b) => b.Sequence.CompareTo(a.Sequence));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> ByTenant(string tenantId)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null || string.IsNullOrEmpty(tenantId))
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.TenantId == tenantId)
                    result.Add(Clone(entry));
            }
            result.Sort((a, b) => b.Sequence.CompareTo(a.Sequence));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> Since(int lastSeenSequence)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Sequence > lastSeenSequence)
                    result.Add(Clone(entry));
            }
            return result;
        }

        public PlayerLogEntry Get(int sequence)
        {
            if (_state == null || _state.PlayerLogs == null)
                return null;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Sequence == sequence)
                    return Clone(entry);
            }
            return null;
        }

        private static PlayerLogEntry Clone(PlayerLogEntry entry)
        {
            if (entry == null)
                return null;
            return new PlayerLogEntry
            {
                Sequence = entry.Sequence,
                Day = entry.Day,
                Phase = entry.Phase,
                Category = entry.Category,
                Title = entry.Title,
                Summary = entry.Summary,
                DetailKey = entry.DetailKey,
                TenantId = entry.TenantId
            };
        }
    }
}
