using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] managedPanels;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private GameSettingController gameSettingController;

    public bool IsPauseOverlayVisible => pauseOverlay != null && pauseOverlay.activeSelf;

    private void Start()
    {
        foreach (var panel in managedPanels)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                panel.enabled = true;
            }
        }
    }

    public void ShowPauseOverlay()
    {
        if (pauseOverlay != null)
            pauseOverlay.SetActive(true);
    }

    public void HidePauseOverlay()
    {
        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);
    }

    public void TogglePauseMenu()
    {
        if (gameSettingController != null)
            gameSettingController.TogglePauseMenu();
    }

    public void OpenPauseMenu()
    {
        if (gameSettingController != null)
            gameSettingController.OpenPauseMenu();
    }

    public void ClosePauseMenu()
    {
        if (gameSettingController != null)
            gameSettingController.ClosePauseMenu();
    }

    public void ResetToDefaults()
    {
        if (gameSettingController != null)
            gameSettingController.ResetToDefaults();
    }
}
