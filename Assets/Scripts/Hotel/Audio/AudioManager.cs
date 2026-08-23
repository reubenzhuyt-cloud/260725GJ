using System;
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

    /// <summary>
    /// Scene-scoped audio manager. Owns exactly three AudioSource channels:
    /// "BGM Audio" (looping background music), "SFX Audio" (one-shot effects),
    /// and "UI Audio" (UI sound effects with deduplication and override).
    /// Does not persist across scene loads.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        public static AudioManager Instance => instance;

        [SerializeField] private AudioClip defaultBgm;
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
        private AudioSource sfxSource;
        private AudioSource uiSource;
        private UIEqualizerFilter uiEqFilter;

        private const float UiCooldownInterval = 0.05f;
        private readonly float[] lastUiPlayTimes = new float[Enum.GetValues(typeof(UISoundType)).Length];

        private void Awake()
        {
            instance = this;

            bgmSource = GetOrCreateSource("BGM Audio");
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = bgmVolume;

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
            if (bgmSource != null)
                bgmSource.volume = bgmVolume;
        }

        public float BgmVolume => bgmVolume;

        public float SfxVolume => soundEffectVolume;

        public void SetSoundEffectVolume(float volume)
        {
            // Applied as the volume scale of each new PlayOneShot call
            // (AudioSource.volume does not affect PlayOneShot).
            soundEffectVolume = Mathf.Clamp01(volume);
            if (uiSource != null)
                uiSource.volume = soundEffectVolume;
        }

        private void UpdateUIEqualizerGains()
        {
            if (uiEqFilter != null)
                uiEqFilter.SetGains(uiEqLowGain, uiEqMidGain, uiEqHighGain);
        }

        private void PlayBgm(AudioClip clip)
        {
            if (clip == null)
                clip = LoadFallbackBgm();

            if (clip == null || bgmSource == null)
                return;

            if (bgmSource.clip == clip && bgmSource.isPlaying)
                return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        private static AudioClip LoadFallbackBgm()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>("BGM");
            if (clips == null || clips.Length == 0)
                return null;

            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            return clips[0];
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
