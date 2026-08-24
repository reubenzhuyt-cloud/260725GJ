using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hotel.Runtime;

namespace Hotel.Audio
{
    public class AudioManager : BaseAudioManager
    {
        public new static AudioManager Instance => BaseAudioManager.Instance as AudioManager;

        [Header("Main Menu Settings")]
        [SerializeField] private bool isMainMenu = false;
        [SerializeField] private float mainMenuBgmStartTime = 30f;

        [Header("Erosion BGM Settings")]
        [SerializeField] private AudioClip lowErosionBgm;
        [SerializeField] private AudioClip highErosionBgm;
        [SerializeField] private float erosionCrossFadeDuration = 1f;

        [Header("Credits BGM")]
        [SerializeField] private AudioClip creditsBgm;

        private bool isCreditsBgmActive;
        private bool isHighErosionBgmTriggered;
        private AudioClip savedBgmBeforeCredits;

        private const float CreditsBgmCrossFadeDuration = 2f;

        private bool IsMainMenuMode => isMainMenu || SceneManager.GetActiveScene().name == "MainMenu";

        protected override void Start()
        {
            if (IsMainMenuMode)
            {
                if (defaultBgm != null)
                {
                    PlayBgm(defaultBgm, 0f, mainMenuBgmStartTime);
                }
                return;
            }

            CheckAndEvaluateBgm(playDirectIfInitial: true);
        }

        public override void NotifyRunState(GameRunState state)
        {
            if (IsMainMenuMode)
                return;

            if (state != null)
            {
                EvaluateTriggerCondition(state);
            }
            CheckAndEvaluateBgm();
        }

        public void UpdateGameState(int day, float averageErosion)
        {
            if (IsMainMenuMode)
                return;

            if (!isHighErosionBgmTriggered && (averageErosion > 50f || day >= 15))
            {
                isHighErosionBgmTriggered = true;
            }
            CheckAndEvaluateBgm();
        }

        public void CheckAndEvaluateBgm(bool playDirectIfInitial = false)
        {
            if (IsMainMenuMode)
                return;

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
            if (creditsBgm == null || isCreditsBgmActive)
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

            AudioClip targetClip = GetDesiredNormalBgm() ?? savedBgmBeforeCredits;
            savedBgmBeforeCredits = null;

            if (targetClip == null)
                return;

            CrossFadeBgm(targetClip, CreditsBgmCrossFadeDuration);
        }
    }
}
