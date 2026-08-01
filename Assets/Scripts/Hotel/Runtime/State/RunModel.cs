
using UnityEngine;
using System;

namespace Hotel.Runtime
{
    [Serializable]
    public readonly struct RunId
    {
        [SerializeField] private readonly string value;

        public RunId(string value)
        {
            this.value = value;
        }

        public string Value => value;
    }

    public enum HotelPhase
    {
        Dawn,
        Day,
        Dusk,
        Night
    }

    [Serializable]
    public sealed class PhaseRunState
    {
        public HotelPhase Current = HotelPhase.Dawn;
    }

    [Serializable]
    public sealed class GameRunState
    {
        public RunId RunId;
        public int Day;
        public PhaseRunState Phase = new PhaseRunState();

        public static GameRunState New(RunId id)
        {
            return new GameRunState
            {
                RunId = id,
                Day = 1
            };
        }
    }
}