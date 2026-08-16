using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private const float NoticeStartInterval = 0.75f;

    [SerializeField] private MonoBehaviour[] managedPanels;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameSettingController gameSettingController;
    [SerializeField] private NoticePanel noticePanelTemplate;

    private readonly Queue<string> _noticeQueue = new Queue<string>();
    private readonly List<NoticePanel> _activeNotices = new List<NoticePanel>();
    private bool _noticeConsuming;

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

    public void ToggleInventoryPanel()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(!inventoryPanel.activeSelf);
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
        if (!isActiveAndEnabled)
            return;

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

            NoticePanel notice = Instantiate(noticePanelTemplate, noticePanelTemplate.transform.parent);
            if (notice != null)
            {
                _activeNotices.Add(notice);
                notice.gameObject.SetActive(true);
                notice.StartCoroutine(notice.Play(content, OnNoticeComplete));
            }

            if (_noticeQueue.Count == 0)
                break;

            float elapsed = 0f;
            while (elapsed < NoticeStartInterval)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        _noticeConsuming = false;
    }

    private void OnNoticeComplete(NoticePanel notice)
    {
        if (_activeNotices.Remove(notice))
            Destroy(notice.gameObject);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _noticeConsuming = false;
        _noticeQueue.Clear();

        for (int i = _activeNotices.Count - 1; i >= 0; i--)
        {
            NoticePanel notice = _activeNotices[i];
            if (notice != null)
                Destroy(notice.gameObject);
        }
        _activeNotices.Clear();
    }
}
