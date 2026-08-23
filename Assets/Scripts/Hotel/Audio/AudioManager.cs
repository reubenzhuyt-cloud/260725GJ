using System;
using System.Collections;
using UnityEngine;

namespace Hotel.Audio
{
    public enum UISoundType
    {
        Click,
        PanelOpen,
        PanelClose,
        NextPhaseButtonSE
    }

    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        public static AudioManager Instance => instance;

        [SerializeField] private AudioClip defaultBgm;
        [SerializeField] private AudioClip creditsBgm;
        [SerializeField] private SoundEffectEvent playSoundEffectEvent;

        [Header("UI Sound Clips")]
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip uiPanelOpenClip;
        [SerializeField] private AudioClip uiPanelCloseClip;
        [SerializeField] private AudioClip uiNextPhaseButtonSEClip;

        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float soundEffectVolume = 1f;

        [Header("UI Equalizer (dB)")]
        [Range(-12f, 12f)] [SerializeField] private float uiEqLowGain = 0f;
        [Range(-12f, 12f)] [SerializeField] private float uiEqMidGain = 0f;
        [Range(-12f, 12f)] [SerializeField] private float uiEqHighGain = 0f;

        private AudioSource bgmSource;
        private AudioSource bgmSecondarySource;
        private AudioSource sfxSource;
        private AudioSource uiSource;
        private UIEqualizerFilter uiEqFilter;

        private Coroutine bgmFadeCoroutine;
        private AudioClip savedBgmBeforeCredits;
        private bool isCreditsBgmActive;

        private const float DefaultBgmCrossFadeDuration = 1f;
        private const float CreditsBgmCrossFadeDuration = 2f;

        private const float UiCooldownInterval = 0.05f;
        private readonly float[] lastUiPlayTimes = new float[Enum.GetValues(typeof(UISoundType)).Length];

        private void Awake()
        {
            instance = this;

            bgmSource = GetOrCreateSource("BGM Audio");
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = bgmVolume;

            bgmSecondarySource = GetOrCreateSource("BGM Audio Secondary");
            bgmSecondarySource.loop = true;
            bgmSecondarySource.playOnAwake = false;
            bgmSecondarySource.volume = 0f;

            sfxSource = GetOrCreateSource("SFX Audio");
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;

            uiSource = GetOrCreateSource("UI Audio");
            uiSource.loop = false;
            uiSource.playOnAwake = false;
            uiSource.volume = soundEffectVolume;

            uiEqFilter = uiSource.GetComponent<UIEqualizerFilter>();
            if (uiEqFilter == null)
                uiEqFilter = uiSource.gameObject.AddComponent<UIEqualizerFilter>();

            UpdateUIEqualizerGains();
        }

        private void Update()
        {
            UpdateUIEqualizerGains();
        }

        private void Start()
        {
            PlayBgm(defaultBgm);
        }

        private void OnEnable()
        {
            if (playSoundEffectEvent != null)
                playSoundEffectEvent.Register(PlaySoundEffect);
        }

        private void OnDisable()
        {
            if (playSoundEffectEvent != null)
                playSoundEffectEvent.Unregister(PlaySoundEffect);
        }

        private void OnDestroy()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (instance == this)
                instance = null;
        }

        public void PlaySoundEffect(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
                return;

            sfxSource.PlayOneShot(clip, soundEffectVolume);
        }

        public void PlayUISound(UISoundType type)
        {
            int index = (int)type;
            float now = Time.unscaledTime;
            if (index >= 0 && index < lastUiPlayTimes.Length)
            {
                if (now - lastUiPlayTimes[index] < UiCooldownInterval)
                    return;
                lastUiPlayTimes[index] = now;
            }

            AudioClip clip = GetUIClip(type);
            if (clip == null || uiSource == null)
                return;

            uiSource.clip = clip;
            uiSource.volume = soundEffectVolume;
            uiSource.Play();
        }

        private AudioClip GetUIClip(UISoundType type)
        {
            return type switch
            {
                UISoundType.Click => uiClickClip,
                UISoundType.PanelOpen => uiPanelOpenClip,
                UISoundType.PanelClose => uiPanelCloseClip,
                UISoundType.NextPhaseButtonSE => uiNextPhaseButtonSEClip,
                _ => null
            };
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmFadeCoroutine == null)
            {
                if (bgmSource != null)
                    bgmSource.volume = bgmVolume;
                if (bgmSecondarySource != null)
                    bgmSecondarySource.volume = 0f;
            }
        }

        public float BgmVolume => bgmVolume;

        public float SfxVolume => soundEffectVolume;

        public void SetSoundEffectVolume(float volume)
        {
            soundEffectVolume = Mathf.Clamp01(volume);
            if (uiSource != null)
                uiSource.volume = soundEffectVolume;
        }

        private void UpdateUIEqualizerGains()
        {
            if (uiEqFilter != null)
                uiEqFilter.SetGains(uiEqLowGain, uiEqMidGain, uiEqHighGain);
        }

        public void OpenCreditsBgm()
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

        public void CloseCreditsBgm()
        {
            if (!isCreditsBgmActive)
                return;

            if (savedBgmBeforeCredits == null)
                return;

            AudioClip targetClip = savedBgmBeforeCredits;
            savedBgmBeforeCredits = null;
            isCreditsBgmActive = false;

            if (GetCurrentBgmClip() == targetClip)
                return;

            CrossFadeBgm(targetClip, CreditsBgmCrossFadeDuration);
        }

        public void PlayBgm(AudioClip clip, float fadeDuration = DefaultBgmCrossFadeDuration)
        {
            if (clip == null)
                return;

            if (GetCurrentBgmClip() == clip && IsAnyBgmPlaying())
                return;

            CrossFadeBgm(clip, fadeDuration);
        }

        private AudioClip GetCurrentBgmClip()
        {
            if (bgmSource != null && bgmSource.isPlaying && bgmSource.clip != null)
                return bgmSource.clip;

            if (bgmSecondarySource != null && bgmSecondarySource.isPlaying && bgmSecondarySource.clip != null)
                return bgmSecondarySource.clip;

            return bgmSource != null ? bgmSource.clip : null;
        }

        private bool IsAnyBgmPlaying()
        {
            return (bgmSource != null && bgmSource.isPlaying) ||
                   (bgmSecondarySource != null && bgmSecondarySource.isPlaying);
        }

        private void CrossFadeBgm(AudioClip newClip, float duration)
        {
            if (newClip == null)
                return;

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (duration <= 0f)
            {
                if (bgmSecondarySource != null)
                {
                    bgmSecondarySource.Stop();
                    bgmSecondarySource.clip = null;
                    bgmSecondarySource.volume = 0f;
                }

                if (bgmSource != null)
                {
                    bgmSource.clip = newClip;
                    bgmSource.loop = true;
                    bgmSource.volume = bgmVolume;
                    bgmSource.Play();
                }

                return;
            }

            bgmFadeCoroutine = StartCoroutine(CrossFadeRoutine(newClip, duration));
        }

        private IEnumerator CrossFadeRoutine(AudioClip newClip, float duration)
        {
            AudioSource fadeOutSource = bgmSource;
            AudioSource fadeInSource = bgmSecondarySource;

            if (fadeOutSource == null || fadeInSource == null)
                yield break;

            fadeInSource.clip = newClip;
            fadeInSource.loop = true;
            fadeInSource.volume = 0f;
            fadeInSource.Play();

            float startFadeOutVolume = fadeOutSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (fadeOutSource != null)
                    fadeOutSource.volume = Mathf.Lerp(startFadeOutVolume, 0f, progress);

                if (fadeInSource != null)
                    fadeInSource.volume = Mathf.Lerp(0f, bgmVolume, progress);

                yield return null;
            }

            if (fadeOutSource != null)
            {
                fadeOutSource.Stop();
                fadeOutSource.clip = null;
                fadeOutSource.volume = 0f;
            }

            if (fadeInSource != null)
            {
                fadeInSource.volume = bgmVolume;
            }

            bgmSource = fadeInSource;
            bgmSecondarySource = fadeOutSource;
            bgmFadeCoroutine = null;
        }

        private AudioSource GetOrCreateSource(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null)
                source = child.gameObject.AddComponent<AudioSource>();

            return source;
        }
    }
}
