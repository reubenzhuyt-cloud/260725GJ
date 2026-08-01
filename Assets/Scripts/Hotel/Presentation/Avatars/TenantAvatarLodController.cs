using System.Collections.Generic;
using UnityEngine;

public class TenantAvatarLodController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float detailThreshold = 80f;
    [SerializeField] private float closestDetailZoom = 40f;
    [SerializeField] private float maximumDetailScale = 2f;
    [SerializeField] private List<TenantAvatarLodTarget> targets = new List<TenantAvatarLodTarget>();

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                targets[i].CaptureBaseScale();
        }

        ApplyAll();
    }

    private void LateUpdate()
    {
        ApplyAll();
    }

    private void ApplyAll()
    {
        if (targetCamera == null)
            return;

        float zoom = targetCamera.orthographicSize;
        float range = detailThreshold - closestDetailZoom;
        float multiplier;

        if (range <= 0f)
            multiplier = 1f;
        else
        {
            float t = Mathf.InverseLerp(detailThreshold, closestDetailZoom, zoom);
            multiplier = Mathf.Lerp(1f, maximumDetailScale, t);
        }

        bool showBackground = zoom <= detailThreshold;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                targets[i].Apply(showBackground, multiplier);
        }
    }
}

[System.Serializable]
public sealed class TenantAvatarLodTarget
{
    [SerializeField] private Transform coloredCircle;
    [SerializeField] private GameObject detailBackground;

    private Vector3 baseScale;
    private Vector3 backgroundBaseScale;

    public void CaptureBaseScale()
    {
        if (coloredCircle != null)
            baseScale = coloredCircle.localScale;

        if (detailBackground != null)
            backgroundBaseScale = detailBackground.transform.localScale;
    }

    public void Apply(bool showBackground, float multiplier)
    {
        if (detailBackground != null)
        {
            detailBackground.SetActive(showBackground);
            detailBackground.transform.localScale = new Vector3(backgroundBaseScale.x * multiplier, backgroundBaseScale.y * multiplier, backgroundBaseScale.z);
        }

        if (coloredCircle != null)
            coloredCircle.localScale = new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
    }
}
