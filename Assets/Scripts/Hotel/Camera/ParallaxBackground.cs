using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Header("Parallax")]
    public float parallaxFactor = 0.2f;

    [Header("Phase Backgrounds")]
    public Sprite dawnBackground;
    public Sprite daytimeBackground;
    public Sprite duskBackground;
    public Sprite nightBackground;

    [Header("Event Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private Vector3 lastCameraPos;
    private CameraController camController;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sortingOrder = -100;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        camController = mainCamera.GetComponent<CameraController>();
        lastCameraPos = mainCamera.transform.position;

        // Set initial background
        if (GamePhaseManager.Instance != null)
            SetBackgroundForPhase(GamePhaseManager.Instance.currentPhase);
    }

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        Vector3 cameraDelta = mainCamera.transform.position - lastCameraPos;
        transform.position += new Vector3(
            cameraDelta.x * parallaxFactor * -1f,
            cameraDelta.y * parallaxFactor * -1f,
            0f);

        lastCameraPos = mainCamera.transform.position;
        UpdateScale();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        SetBackgroundForPhase(data.phase);
    }

    private void SetBackgroundForPhase(GamePhase phase)
    {
        Sprite bg = null;
        switch (phase)
        {
            case GamePhase.Dawn:    bg = dawnBackground; break;
            case GamePhase.Day:     bg = daytimeBackground; break;
            case GamePhase.Dusk:    bg = duskBackground; break;
            case GamePhase.Night:   bg = nightBackground; break;
        }

        if (bg != null && spriteRenderer != null)
            spriteRenderer.sprite = bg;
    }

    private void UpdateScale()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || mainCamera == null)
            return;

        float currentHeight = 2f * mainCamera.orthographicSize;
        float currentWidth = currentHeight * mainCamera.aspect;

        Vector2 mapSize = Vector2.zero;
        if (camController != null)
            mapSize = camController.mapSize;

        float parallaxRangeX = mapSize.x * parallaxFactor;
        float parallaxRangeY = mapSize.y * parallaxFactor;

        float requiredWidth = currentWidth + parallaxRangeX;
        float requiredHeight = currentHeight + parallaxRangeY;

        float spriteW = spriteRenderer.sprite.bounds.size.x;
        float spriteH = spriteRenderer.sprite.bounds.size.y;

        if (spriteW <= 0 || spriteH <= 0) return;

        float scaleX = requiredWidth / spriteW;
        float scaleY = requiredHeight / spriteH;
        float scale = Mathf.Max(scaleX, scaleY);

        transform.localScale = Vector3.one * scale;
    }
}
