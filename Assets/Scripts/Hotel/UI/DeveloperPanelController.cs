using Hotel.Audio;
using UnityEngine;

namespace Hotel.UI
{
    public class DeveloperPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        public void Open()
        {
            if (UIManager.Instance != null)
            {
                if (!UIManager.Instance.CanOpenButtonPanel())
                    return;
                UIManager.Instance.CloseOtherButtonPanels(panel != null ? panel : gameObject);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUISound(UISoundType.PanelOpen);
                AudioManager.Instance.OpenCreditsBgm();
            }

            if (panel != null)
                panel.SetActive(true);
        }

        public void Close()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUISound(UISoundType.PanelClose);
                AudioManager.Instance.CloseCreditsBgm();
            }

            if (panel != null)
                panel.SetActive(false);
        }
    }
}
