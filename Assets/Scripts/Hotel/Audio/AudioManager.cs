using System;
using UnityEngine;

namespace Hotel.Audio
{
    /// <summary>
    /// Scene-scoped audio manager. Owns exactly two AudioSource channels:
    /// "BGM Audio" (looping background music) and "SFX Audio" (one-shot effects).
    /// Does not persist across scene loads.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        public static AudioManager Instance => instance;

        [SerializeField] private AudioClip defaultBgm;
        [SerializeField] private SoundEffectEvent playSoundEffectEvent;

        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float soundEffectVolume = 1f;

        private AudioSource bgmSource;
        private AudioSource sfxSource;

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

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
                bgmSource.volume = bgmVolume;
        }

        public void SetSoundEffectVolume(float volume)
        {
            // Applied as the volume scale of each new PlayOneShot call
            // (AudioSource.volume does not affect PlayOneShot).
            soundEffectVolume = Mathf.Clamp01(volume);
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
