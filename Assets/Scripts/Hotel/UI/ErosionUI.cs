using UnityEngine;
using TMPro;

public class ErosionUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI erosionText;

    [Header("Event Listener")]
    public ErosionChangedEvent onErosionChanged;

    private void OnEnable()
    {
        if (onErosionChanged != null)
            onErosionChanged.Register(OnErosionChanged);
    }

    private void OnDisable()
    {
        if (onErosionChanged != null)
            onErosionChanged.Unregister(OnErosionChanged);
    }

    private void Start()
    {
        if (ErosionManager.Instance != null)
        {
            UpdateDisplay(ErosionManager.Instance.GetErosion());
        }
    }

    private void OnErosionChanged(ErosionData data)
    {
        UpdateDisplay(data.newValue);
    }

    private void UpdateDisplay(float value)
    {
        if (erosionText != null)
            erosionText.text = $"侵蚀度: {value:F1}%";
    }
}