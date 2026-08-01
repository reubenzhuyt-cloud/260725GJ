using UnityEngine;

public class TenantAvatarLod : MonoBehaviour
{
    [SerializeField] private SpriteRenderer detailBackground;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float detailThreshold = 80f;
    [SerializeField] private float closestDetailZoom = 40f;
    [SerializeField] private float maximumDetailScale = 2f;

    private Vector3 baseScale;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        baseScale = transform.localScale;
        ApplyVisibility();
    }

    private void LateUpdate()
    {
        ApplyVisibility();
        ApplyScale();
    }

    private void ApplyVisibility()
    {
        if (targetCamera == null || detailBackground == null)
            return;

        detailBackground.gameObject.SetActive(targetCamera.orthographicSize <= detailThreshold);
    }

    private void ApplyScale()
    {
        if (targetCamera == null)
            return;

        float zoom = targetCamera.orthographicSize;
        float range = detailThreshold - closestDetailZoom;

        if (range <= 0f)
        {
            transform.localScale = new Vector3(baseScale.x, baseScale.y, baseScale.z);
            return;
        }

        float t = Mathf.InverseLerp(detailThreshold, closestDetailZoom, zoom);
        float multiplier = Mathf.Lerp(1f, maximumDetailScale, t);
        transform.localScale = new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
    }
}
