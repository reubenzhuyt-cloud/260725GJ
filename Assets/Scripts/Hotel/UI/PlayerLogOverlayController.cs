using Hotel.Audio;
using UnityEngine;

public class PlayerLogOverlayController : MonoBehaviour
{
    [SerializeField] private GameObject logOverlay;

    private static int _openLogOverlayCount;
    private static int _escapeConsumedFrame = -1;

    public static bool IsAnyLogOverlayOpen => _openLogOverlayCount > 0;
    public static bool WasEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;

    private bool _openedByController;
    private bool _started;
    private PlayerLogPanelController _panel;

    private void OnEnable()
    {
        _escapeConsumedFrame = -1;
        if (logOverlay == null || _started)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
    }

    private void Start()
    {
        _started = true;
    }

    private void Update()
    {
        if (logOverlay == null)
            return;

        if (Input.GetKeyDown(KeyCode.H))
            ToggleLogOverlay();

        if (Input.GetKeyDown(KeyCode.Escape) && _openedByController)
        {
            _escapeConsumedFrame = Time.frameCount;
            Close();
        }
    }

    public void ToggleLogOverlay()
    {
        if (logOverlay == null)
            return;
        if (_openedByController)
            Close();
        else if (!IsPauseOverlayVisible())
            Open();
    }

    private bool IsPauseOverlayVisible()
    {
        UIManager manager = FindObjectOfType<UIManager>();
        return manager != null && manager.IsPauseOverlayVisible;
    }

    private void Open()
    {
        if (_openedByController)
            return;
        logOverlay.SetActive(true);
        _openedByController = true;
        _openLogOverlayCount++;
        if (_panel == null)
            _panel = logOverlay.GetComponentInChildren<PlayerLogPanelController>(true);
        if (_panel != null)
            _panel.RefreshTimeline();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelOpen);
    }

    private void Close()
    {
        if (!_openedByController)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
        _openLogOverlayCount--;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUISound(UISoundType.PanelClose);
    }

    private void OnDisable()
    {
        _escapeConsumedFrame = -1;
        if (!_openedByController || logOverlay == null)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
        _openLogOverlayCount--;
    }

    private void OnDestroy()
    {
        _escapeConsumedFrame = -1;
        if (!_openedByController || logOverlay == null)
            return;
        logOverlay.SetActive(false);
        _openedByController = false;
        _openLogOverlayCount--;
    }
}
