using System;
using System.IO;
using Hotel.Audio;
using Hotel.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameSettingController : MonoBehaviour
{
    private const string SettingsFileName = "hotel-settings.json";

    private static readonly FullScreenMode[] DisplayModes =
    {
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };

    private static readonly int[] ResolutionWidths = { 1280, 1600, 1920, 2560 };
    private static readonly int[] ResolutionHeights = { 720, 900, 1080, 1440 };

    [SerializeField] private UIManager uiManager;
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private GameSettingsData settings;
    private float timeScaleBeforePause = 1f;
    private bool isPauseMenuOpen;

    private void Awake()
    {
        settings = LoadSettings();
    }

    private void Start()
    {
        ApplySettings(settings);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (PlayerLogOverlayController.IsAnyLogOverlayOpen || PlayerLogOverlayController.WasEscapeConsumedThisFrame)
                return;
            TogglePauseMenu();
        }
    }

    private void OnDisable()
    {
        RestoreTimeScaleOnTeardown();
    }

    private void OnDestroy()
    {
        RestoreTimeScaleOnTeardown();
    }

    public void TogglePauseMenu()
    {
        if (uiManager == null) return;
        if (uiManager.IsPauseOverlayVisible)
            ClosePauseMenu();
        else
            OpenPauseMenu();
    }

    public void OpenPauseMenu()
    {
        if (uiManager == null) return;
        if (isPauseMenuOpen)
        {
            if (uiManager.IsPauseOverlayVisible)
                return;

            Time.timeScale = timeScaleBeforePause;
            isPauseMenuOpen = false;
        }

        timeScaleBeforePause = Time.timeScale;
        isPauseMenuOpen = true;
        uiManager.ShowPauseOverlay();
        Time.timeScale = 0f;
        RefreshUiFromRuntimeState();
    }

    public void ClosePauseMenu()
    {
        if (uiManager == null || !isPauseMenuOpen) return;
        isPauseMenuOpen = false;
        uiManager.HidePauseOverlay();
        Time.timeScale = timeScaleBeforePause;
    }

    public void SaveAndQuit()
    {
        SaveAndQuitFlow.Execute(
            isPauseMenuOpen,
            SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null,
            SaveGameService.TrySave,
            ClosePauseMenu,
            ReturnToMainMenu,
            message => Debug.LogError(message));
    }

    private static void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void OnDisplayModeChanged(int value)
    {
        if (!isPauseMenuOpen) return;
        int index = Mathf.Clamp(value, 0, DisplayModes.Length - 1);
        settings.FullScreen = DisplayModes[index] == FullScreenMode.FullScreenWindow;
        ApplySettings(settings);
        SaveSettings();
    }

    public void ResetToDefaults()
    {
        if (!isPauseMenuOpen) return;
        settings = GameSettingsCodec.Default();
        ApplySettings(settings);
        RefreshUiFromRuntimeState();
        SaveSettings();
    }

    public void OnResolutionChanged(int value)
    {
        if (!isPauseMenuOpen) return;
        int index = Mathf.Clamp(value, 0, ResolutionWidths.Length - 1);
        settings.ResolutionWidth = ResolutionWidths[index];
        settings.ResolutionHeight = ResolutionHeights[index];
        ApplySettings(settings);
        SaveSettings();
    }

    public void OnFrameRateChanged(int value)
    {
        if (!isPauseMenuOpen) return;
        int[] frameRates = GameSettingsCodec.SupportedFrameRates;
        int index = Mathf.Clamp(value, 0, frameRates.Length - 1);
        settings.TargetFrameRate = frameRates[index];
        ApplySettings(settings);
        SaveSettings();
    }

    public void OnBgmVolumeChanged(float value)
    {
        if (!isPauseMenuOpen) return;
        settings.BgmVolume = Mathf.Clamp01(value);
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetBgmVolume(settings.BgmVolume);
        SaveSettings();
    }

    public void OnSfxVolumeChanged(float value)
    {
        if (!isPauseMenuOpen) return;
        settings.SfxVolume = Mathf.Clamp01(value);
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSoundEffectVolume(settings.SfxVolume);
        SaveSettings();
    }

    private void RefreshUiFromRuntimeState()
    {
        if (displayModeDropdown != null)
            displayModeDropdown.SetValueWithoutNotify(Screen.fullScreenMode == FullScreenMode.FullScreenWindow ? 0 : 1);

        if (resolutionDropdown != null)
            resolutionDropdown.SetValueWithoutNotify(ClosestResolutionIndex(Screen.width, Screen.height));

        if (frameRateDropdown != null)
            frameRateDropdown.SetValueWithoutNotify(IndexOfFrameRate(Application.targetFrameRate));

        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.BgmVolume : settings.BgmVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : settings.SfxVolume);
    }

    private int ClosestResolutionIndex(int width, int height)
    {
        int best = 2;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < ResolutionWidths.Length; i++)
        {
            int distance = Mathf.Abs(ResolutionWidths[i] - width) + Mathf.Abs(ResolutionHeights[i] - height);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private static int IndexOfFrameRate(int frameRate)
    {
        int[] frameRates = GameSettingsCodec.SupportedFrameRates;
        for (int i = 0; i < frameRates.Length; i++)
        {
            if (frameRates[i] == frameRate) return i;
        }
        return 0;
    }

    private void ApplySettings(GameSettingsData data)
    {
        FullScreenMode mode = data.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.fullScreenMode = mode;
        Screen.SetResolution(data.ResolutionWidth, data.ResolutionHeight, mode);

        Application.targetFrameRate = data.TargetFrameRate;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBgmVolume(data.BgmVolume);
            AudioManager.Instance.SetSoundEffectVolume(data.SfxVolume);
        }
    }

    private GameSettingsData LoadSettings()
    {
        try
        {
            string path = GetSettingsPath();
            if (!File.Exists(path)) return GameSettingsCodec.Default();
            return GameSettingsCodec.FromJson(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to load settings: {e.Message}");
            return GameSettingsCodec.Default();
        }
    }

    private void SaveSettings()
    {
        try
        {
            File.WriteAllText(GetSettingsPath(), GameSettingsCodec.ToJson(settings));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to save settings: {e.Message}");
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(Application.persistentDataPath, SettingsFileName);
    }

    private void RestoreTimeScaleOnTeardown()
    {
        if (!isPauseMenuOpen) return;
        if (uiManager != null)
            uiManager.HidePauseOverlay();
        isPauseMenuOpen = false;
        Time.timeScale = timeScaleBeforePause;
    }
}
