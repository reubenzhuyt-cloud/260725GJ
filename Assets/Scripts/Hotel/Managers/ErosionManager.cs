using UnityEngine;

public class ErosionManager : MonoBehaviour
{
    public static ErosionManager Instance { get; private set; }

    [Header("Erosion State")]
    public ErosionState erosionState = new ErosionState();

    [Header("Event Channel")]
    public ErosionChangedEvent onErosionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Public API: modify erosion with delta (+/-)
    public void ModifyErosion(float delta)
    {
        float old = erosionState.erosionValue;
        erosionState.Add(delta);

        if (onErosionChanged != null)
        {
            onErosionChanged.Raise(new ErosionData
            {
                oldValue = old,
                newValue = erosionState.erosionValue,
                delta = delta
            });
        }

        Debug.Log($"[ErosionManager] Erosion: {old:F1} → {erosionState.erosionValue:F1} (Δ{delta:+0.0;-0.0})");
    }

    // Public API: set erosion to absolute value
    public void SetErosion(float value)
    {
        float old = erosionState.erosionValue;
        erosionState.Set(value);

        if (onErosionChanged != null)
        {
            onErosionChanged.Raise(new ErosionData
            {
                oldValue = old,
                newValue = erosionState.erosionValue,
                delta = erosionState.erosionValue - old
            });
        }
    }

    // Public API: get current value
    public float GetErosion()
    {
        return erosionState.erosionValue;
    }
}