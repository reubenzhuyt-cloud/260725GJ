using UnityEngine;

namespace Hotel.Audio
{
    public class MainMenuAudioManager : BaseAudioManager
    {
        public new static MainMenuAudioManager Instance => BaseAudioManager.Instance as MainMenuAudioManager;
    }
}
