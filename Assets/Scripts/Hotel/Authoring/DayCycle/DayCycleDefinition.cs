using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

namespace Hotel.Authoring.DayCycle
{
    [CreateAssetMenu(menuName = "Hotel/Day Cycle")]
    public sealed class DayCycleDefinition : ScriptableObject, IPhaseCycle
    {
        [SerializeField] private HotelPhase[] ordered =
        {
            HotelPhase.Dawn,
            HotelPhase.Day,
            HotelPhase.Dusk,
            HotelPhase.Night
        };

        public IReadOnlyList<HotelPhase> OrderedPhases => Array.AsReadOnly(ordered);

        public static DayCycleDefinition CreateDefault()
        {
            return CreateInstance<DayCycleDefinition>();
        }

        public HotelPhase GetNext(HotelPhase phase)
        {
            if (!string.IsNullOrEmpty(Validate()))
            {
                throw new InvalidOperationException(Validate());
            }

            var index = Array.IndexOf(ordered, phase);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            return ordered[(index + 1) % ordered.Length];
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(Validate()))
            {
                Debug.LogError(Validate(), this);
            }
        }

        public string Validate()
        {
            if (ordered == null || ordered.Length != 4)
            {
                return "Cycle must contain exactly four phases.";
            }

            var expected = new[]
            {
                HotelPhase.Dawn,
                HotelPhase.Day,
                HotelPhase.Dusk,
                HotelPhase.Night
            };
            var seen = new HashSet<HotelPhase>();

            for (var index = 0; index < expected.Length; index++)
            {
                if (ordered[index] != expected[index] || !seen.Add(ordered[index]))
                {
                    return "Cycle must be Dawn,Day,Dusk,Night.";
                }
            }

            return string.Empty;
        }
    }
}