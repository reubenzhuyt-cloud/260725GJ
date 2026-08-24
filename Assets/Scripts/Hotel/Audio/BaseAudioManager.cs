using System;
using System.Collections;
using UnityEngine;
using Hotel.Runtime;

namespace Hotel.Audio
{
    public enum UISoundType
    {
        Click,
        PanelOpen,
        PanelClose,
        NextPhaseButtonSE
    }

    public abstract class BaseAudioManager : MonoBehaviour
    {
        private static BaseAudioManager instance;

        public static BaseAudioManager Instance => instance;

        [Header("BGM Settings")]
        [SerializeField] protected AudioClip defaultBgm;

        [Header("Sound Effect Events")]
        [SerializeField] protected SoundEffectEvent playSoundEffectEvent;

        [Header("UI Sound Clips")]
        [SerializeField] protected AudioClip uiClickClip;
        [SerializeField] protected AudioClip uiPanelOpenClip;
        [SerializeField] protected AudioClip uiPanelCloseClip;
        [SerializeField] protected AudioClip uiNextPhaseButtonSEClip;

        [Header("Volume Settings")]
        [Range(0f, 1f)] [SerializeField] protected float bgmVolume = 1f;
        [Range(0f, 1f)] [SerializeField] protected float soundEffectVolume = 1f;

        [Header("UI Equalizer (dB)")]
        [Range(-12f, 12f)] [SerializeField] protected float uiEqLowGain = 0f;
        [Range(-12f, 12f)] [SerializeField] protected float uiEqMidGain = 0f;
        [Range(-12f, 12f)] [SerializeField] protected float uiEqHighGain = 0f;

        protected AudioSource bgmSource;
        protected AudioSource bgmSecondarySource;
        protected AudioSource sfxSource;
        protected AudioSource uiSource;
        protected UIEqualizerFilter uiEqFilter;

        protected Coroutine bgmFadeCoroutine;

        protected const float DefaultBgmCrossFadeDuration = 1f;
        protected const float UiCooldownInterval = 0.05f;
        protected readonly float[] lastUiPlayTimes = new float[Enum.GetValues(typeof(UISoundType)).Length];

        public float BgmVolume => bgmVolume;
        public float SfxVolume => soundEffectVolume;

        public virtual void NotifyRunState(GameRunState state) { }

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

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

        protected virtual void OnValidate()
        {
            UpdateUIEqualizerGains();
        }

        protected virtual void Start()
        {
            if (defaultBgm != null)
            {
                PlayBgm(defaultBgm);
            }
        }

        protected virtual void OnEnable()
        {
            if (playSoundEffectEvent != null)
                playSoundEffectEvent.Register(PlaySoundEffect);
        }

        protected virtual void OnDisable()
        {
            if (playSoundEffectEvent != null)
                playSoundEffectEvent.Unregister(PlaySoundEffect);
        }

        protected virtual void OnDestroy()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (instance == this)
                instance = null;
        }

        public virtual void OpenCreditsBgm() { }
        public virtual void CloseCreditsBgm() { }

        public virtual void PlaySoundEffect(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
                return;

            sfxSource.PlayOneShot(clip, soundEffectVolume);
        }

        public virtual void PlayUISound(UISoundType type)
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

            uiSource.PlayOneShot(clip, soundEffectVolume);
        }

        protected virtual AudioClip GetUIClip(UISoundType type)
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

        public virtual void SetBgmVolume(float volume)
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

        public virtual void SetSoundEffectVolume(float volume)
        {
            soundEffectVolume = Mathf.Clamp01(volume);
            if (uiSource != null)
                uiSource.volume = soundEffectVolume;
        }

        public virtual void SetUIEqualizerGains(float lowDb, float midDb, float highDb)
        {
            uiEqLowGain = lowDb;
            uiEqMidGain = midDb;
            uiEqHighGain = highDb;
            UpdateUIEqualizerGains();
        }

        protected void UpdateUIEqualizerGains()
        {
            if (uiEqFilter != null)
                uiEqFilter.SetGains(uiEqLowGain, uiEqMidGain, uiEqHighGain);
        }

        public virtual void PlayBgm(AudioClip clip, float fadeDuration = DefaultBgmCrossFadeDuration, float startTime = 0f)
        {
            if (clip == null)
                return;

            if (GetCurrentBgmClip() == clip && IsAnyBgmPlaying())
                return;

            CrossFadeBgm(clip, fadeDuration, startTime);
        }

        public AudioClip GetCurrentBgmClip()
        {
            if (bgmFadeCoroutine != null && bgmSecondarySource != null && bgmSecondarySource.isPlaying && bgmSecondarySource.clip != null)
                return bgmSecondarySource.clip;

            if (bgmSource != null && bgmSource.isPlaying && bgmSource.clip != null)
                return bgmSource.clip;

            if (bgmSecondarySource != null && bgmSecondarySource.isPlaying && bgmSecondarySource.clip != null)
                return bgmSecondarySource.clip;

            return null;
        }

        public bool IsAnyBgmPlaying()
        {
            return (bgmSource != null && bgmSource.isPlaying) ||
                   (bgmSecondarySource != null && bgmSecondarySource.isPlaying);
        }

        public void CrossFadeBgm(AudioClip newClip, float duration, float startTime = 0f)
        {
            if (newClip == null)
                return;

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;

                // Sync sources to a consistent baseline before starting next crossfade
                if (bgmSecondarySource != null && bgmSecondarySource.isPlaying)
                {
                    // If secondary was playing, swap so bgmSource represents current playing source
                    AudioSource temp = bgmSource;
                    bgmSource = bgmSecondarySource;
                    bgmSecondarySource = temp;
                }

                if (bgmSecondarySource != null)
                {
                    bgmSecondarySource.Stop();
                    bgmSecondarySource.clip = null;
                    bgmSecondarySource.volume = 0f;
                }
            }

            float validStartTime = (startTime > 0f && startTime < newClip.length) ? startTime : 0f;

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
                    bgmSource.time = validStartTime;
                    bgmSource.volume = bgmVolume;
                    bgmSource.Play();
                }

                return;
            }

            bgmFadeCoroutine = StartCoroutine(CrossFadeRoutine(newClip, duration, validStartTime));
        }

        protected IEnumerator CrossFadeRoutine(AudioClip newClip, float duration, float startTime = 0f)
        {
            AudioSource fadeOutSource = bgmSource;
            AudioSource fadeInSource = bgmSecondarySource;

            if (fadeOutSource == null || fadeInSource == null)
                yield break;

            fadeInSource.clip = newClip;
            fadeInSource.loop = true;
            fadeInSource.time = startTime;
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

        protected AudioSource GetOrCreateSource(string childName)
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
