using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private const float NoticeStartInterval = 0.75f;

    [SerializeField] private MonoBehaviour[] managedPanels;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private GuidePanelController guidePanelController;
    [SerializeField] private GameSettingController gameSettingController;
    [SerializeField] private NoticePanel noticePanelTemplate;

    private UnityEngine.UI.Button _guideButton;
    private readonly Queue<string> _noticeQueue = new Queue<string>();
    private readonly List<NoticePanel> _activeNotices = new List<NoticePanel>();
    private bool _noticeConsuming;

    public bool IsPauseOverlayVisible => pauseOverlay != null && pauseOverlay.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoBindGuideUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

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

        if (SettlementBridge.Instance != null && SettlementBridge.Instance.IsFreshStart && SettlementBridge.Instance.RunState != null && SettlementBridge.Instance.RunState.Day == 1)
        {
            OpenGuidePanel();
        }
    }

    private void AutoBindGuideUI()
    {
        if (guidePanel == null)
        {
            GameObject found = GameObject.Find("GuidePanel");
            if (found != null)
            {
                guidePanel = found;
            }
        }

        if (guidePanelController == null && guidePanel != null)
        {
            guidePanelController = guidePanel.GetComponent<GuidePanelController>();
            if (guidePanelController == null)
            {
                guidePanelController = guidePanel.GetComponentInChildren<GuidePanelController>(true);
            }
        }

        if (guidePanelController == null)
        {
            guidePanelController = FindObjectOfType<GuidePanelController>(true);
            if (guidePanelController != null && guidePanel == null)
            {
                guidePanel = guidePanelController.gameObject;
            }
        }

        if (guidePanel != null && guidePanelController == null)
        {
            guidePanelController = guidePanel.AddComponent<GuidePanelController>();
        }

        GameObject guideBtnObj = GameObject.Find("GuideButton");
        if (guideBtnObj != null)
        {
            _guideButton = guideBtnObj.GetComponent<UnityEngine.UI.Button>();
            if (_guideButton != null)
            {
                _guideButton.onClick.RemoveListener(ToggleGuidePanel);
                _guideButton.onClick.AddListener(ToggleGuidePanel);
            }
        }

        if (guidePanel != null && guidePanel.activeSelf)
        {
            guidePanel.SetActive(false);
        }
    }

    public bool IsBlockingModalActive()
    {
        if (IsPauseOverlayVisible)
            return true;

        if (EventUI.Instance != null && EventUI.Instance.IsEventActive)
            return true;

        if (TenantReviewCoordinator.Instance != null && TenantReviewCoordinator.Instance.IsReviewActive)
            return true;

        if (RunSettlementController.Instance != null && RunSettlementController.Instance.IsSettlementActive)
            return true;

        return false;
    }

    public bool CanOpenButtonPanel()
    {
        return !IsBlockingModalActive();
    }

    public void CloseOtherButtonPanels(GameObject exceptPanel = null)
    {
        CloseAllButtonPanels(exceptPanel);
    }

    public void CloseAllButtonPanels(GameObject exceptPanel = null)
    {
        if (guidePanel != null && guidePanel != exceptPanel)
        {
            CloseGuidePanel();
        }

        if (inventoryPanel != null && inventoryPanel != exceptPanel)
        {
            SetInventoryPanelVisible(false);
        }

        PlayerLogOverlayController[] logControllers = FindObjectsOfType<PlayerLogOverlayController>(true);
        for (int i = 0; i < logControllers.Length; i++)
        {
            if (logControllers[i] != null && logControllers[i].gameObject != exceptPanel)
            {
                logControllers[i].Close();
            }
        }

        Hotel.UI.DeveloperPanelController[] devControllers = FindObjectsOfType<Hotel.UI.DeveloperPanelController>(true);
        for (int i = 0; i < devControllers.Length; i++)
        {
            if (devControllers[i] != null && devControllers[i].gameObject != exceptPanel)
            {
                devControllers[i].Close();
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
        if (inventoryPanel == null)
            return;

        if (inventoryPanel.activeSelf)
        {
            SetInventoryPanelVisible(false);
        }
        else
        {
            SetInventoryPanelVisible(true);
        }
    }

    public void SetInventoryPanelVisible(bool visible)
    {
        if (inventoryPanel == null)
            return;

        if (visible)
        {
            if (!CanOpenButtonPanel())
                return;

            CloseOtherButtonPanels(inventoryPanel);
            inventoryPanel.SetActive(true);
        }
        else
        {
            inventoryPanel.SetActive(false);
        }
    }

    public void OpenGuidePanel()
    {
        if (!CanOpenButtonPanel())
            return;

        CloseOtherButtonPanels(guidePanel);

        if (guidePanelController != null)
        {
            guidePanelController.Open();
        }
        else if (guidePanel != null)
        {
            guidePanel.SetActive(true);
        }
    }

    public void CloseGuidePanel()
    {
        if (guidePanelController != null)
        {
            guidePanelController.Close();
        }
        else if (guidePanel != null)
        {
            guidePanel.SetActive(false);
        }
    }

    public void ToggleGuidePanel()
    {
        bool isOpen = (guidePanelController != null && guidePanelController.IsOpen)
            || (guidePanel != null && guidePanel.activeSelf);

        if (isOpen)
        {
            CloseGuidePanel();
        }
        else
        {
            OpenGuidePanel();
        }
    }

    public void SetGuidePanelVisible(bool visible)
    {
        if (visible)
            OpenGuidePanel();
        else
            CloseGuidePanel();
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
        if (_guideButton != null)
        {
            _guideButton.onClick.RemoveListener(ToggleGuidePanel);
        }

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
