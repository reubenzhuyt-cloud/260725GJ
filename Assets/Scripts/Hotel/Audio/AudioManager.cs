using System;
using UnityEngine;
using Hotel.Runtime;

namespace Hotel.Audio
{
    public class AudioManager : BaseAudioManager
    {
        public new static BaseAudioManager Instance => BaseAudioManager.Instance;

        [Header("Erosion BGM Settings")]
        [SerializeField] private AudioClip lowErosionBgm;
        [SerializeField] private AudioClip highErosionBgm;
        [SerializeField] private float erosionCrossFadeDuration = 1f;

        [Header("Credits BGM")]
        [SerializeField] private AudioClip creditsBgm;

        private AudioClip savedBgmBeforeCredits;
        private bool isCreditsBgmActive;
        private bool isHighErosionBgmTriggered;

        private const float CreditsBgmCrossFadeDuration = 2f;

        protected override void Start()
        {
            CheckAndEvaluateBgm(playDirectIfInitial: true);
        }

        public override void NotifyRunState(GameRunState state)
        {
            if (state != null)
            {
                EvaluateTriggerCondition(state);
            }
            CheckAndEvaluateBgm();
        }

        public void UpdateGameState(int day, float averageErosion)
        {
            if (!isHighErosionBgmTriggered && (averageErosion > 50f || day >= 15))
            {
                isHighErosionBgmTriggered = true;
            }
            CheckAndEvaluateBgm();
        }

        public void CheckAndEvaluateBgm(bool playDirectIfInitial = false)
        {
            AudioClip activeNormalBgm = GetDesiredNormalBgm();
            if (activeNormalBgm == null)
                return;

            if (isCreditsBgmActive)
                return;

            AudioClip currentClip = GetCurrentBgmClip();
            if (playDirectIfInitial && currentClip == null)
            {
                PlayBgm(activeNormalBgm, 0f);
            }
            else if (currentClip != activeNormalBgm)
            {
                CrossFadeBgm(activeNormalBgm, erosionCrossFadeDuration);
            }
        }

        private void EvaluateTriggerCondition(GameRunState state)
        {
            if (isHighErosionBgmTriggered)
                return;

            float avgErosion = CalculateAverageErosion(state);
            if (avgErosion > 50f || state.Day >= 15)
            {
                isHighErosionBgmTriggered = true;
            }
        }

        private float CalculateAverageErosion(GameRunState state)
        {
            if (state == null || state.Tenants == null || state.Tenants.Count == 0)
                return 0f;

            float totalErosion = 0f;
            int count = 0;

            foreach (TenantRunState tenant in state.Tenants.Values)
            {
                if (tenant != null && !string.IsNullOrEmpty(tenant.RoomId))
                {
                    totalErosion += tenant.TrueErosion;
                    count++;
                }
            }

            return count > 0 ? totalErosion / count : 0f;
        }

        private AudioClip GetDesiredNormalBgm()
        {
            if (isHighErosionBgmTriggered && highErosionBgm != null)
            {
                return highErosionBgm;
            }

            if (lowErosionBgm != null)
            {
                return lowErosionBgm;
            }

            return defaultBgm;
        }

        public override void OpenCreditsBgm()
        {
            if (creditsBgm == null)
                return;

            if (isCreditsBgmActive)
                return;

            AudioClip currentClip = GetCurrentBgmClip();
            if (currentClip == creditsBgm)
                return;

            savedBgmBeforeCredits = currentClip;
            isCreditsBgmActive = true;
            CrossFadeBgm(creditsBgm, CreditsBgmCrossFadeDuration);
        }

        public override void CloseCreditsBgm()
        {
            if (!isCreditsBgmActive)
                return;

            isCreditsBgmActive = false;
            savedBgmBeforeCredits = null;

            AudioClip targetClip = GetDesiredNormalBgm();
            if (targetClip == null)
                return;

            if (GetCurrentBgmClip() == targetClip)
                return;

            CrossFadeBgm(targetClip, CreditsBgmCrossFadeDuration);
        }
    }
}
