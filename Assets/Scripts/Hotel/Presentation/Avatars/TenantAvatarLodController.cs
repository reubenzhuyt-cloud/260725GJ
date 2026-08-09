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

        bool showAvatarLayer = zoom <= detailThreshold;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                targets[i].Apply(showAvatarLayer, multiplier);
        }
    }
}

[System.Serializable]
public sealed class TenantAvatarLodTarget
{
    [SerializeField] private Transform avatarLayer;

    private Vector3 baseScale;

    public void CaptureBaseScale()
    {
        if (avatarLayer != null)
            baseScale = avatarLayer.localScale;
    }

    public void Apply(bool showAvatarLayer, float multiplier)
    {
        if (avatarLayer == null)
            return;

        avatarLayer.gameObject.SetActive(showAvatarLayer);
        avatarLayer.localScale = new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z);
    }
}
