using UnityEngine;

namespace Hotel.Audio
{
    [CreateAssetMenu(fileName = "PlaySoundEffectEvent", menuName = "Events/Play Sound Effect")]
    public class SoundEffectEvent : GameEvent<AudioClip>
    {
    }
}
