using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] managedPanels;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private GameSettingController gameSettingController;
    [SerializeField] private NoticePanel noticePanelTemplate;

    private readonly Queue<string> _noticeQueue = new Queue<string>();
    private bool _noticeConsuming;
    private NoticePanel _activeNotice;

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

    public void SaveAndQuit()
    {
        if (gameSettingController != null)
            gameSettingController.SaveAndQuit();
    }

    public void ResetToDefaults()
    {
        if (gameSettingController != null)
            gameSettingController.ResetToDefaults();
    }

    public void ShowNotice(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        _noticeQueue.Enqueue(content);

        if (_noticeConsuming)
            return;

        StartCoroutine(ConsumeNotices());
    }

    private IEnumerator ConsumeNotices()
    {
        _noticeConsuming = true;

        if (noticePanelTemplate == null)
        {
            _noticeQueue.Clear();
            _noticeConsuming = false;
            yield break;
        }

        while (_noticeQueue.Count > 0)
        {
            string content = _noticeQueue.Dequeue();

            _activeNotice = Instantiate(noticePanelTemplate, noticePanelTemplate.transform.parent);
            if (_activeNotice == null)
                continue;

            _activeNotice.gameObject.SetActive(true);

            yield return _activeNotice.Play(content);

            if (_activeNotice != null)
            {
                Destroy(_activeNotice.gameObject);
                _activeNotice = null;
            }
        }

        _noticeConsuming = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _noticeConsuming = false;
        _noticeQueue.Clear();

        if (_activeNotice != null)
        {
            Destroy(_activeNotice.gameObject);
            _activeNotice = null;
        }
    }
}
