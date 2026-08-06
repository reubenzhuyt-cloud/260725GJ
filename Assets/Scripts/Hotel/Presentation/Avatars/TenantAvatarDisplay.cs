using System;
using UnityEngine;

public class TenantAvatarDisplay : MonoBehaviour
{
    [Header("Display")]
    public SpriteRenderer spriteRenderer;
    public Color color = Color.white;

    [Header("Hover")]
    public TenantInfoHoverTrigger hoverTrigger;

    private string _boundTenantId;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider();

        if (hoverTrigger == null)
            hoverTrigger = GetComponent<TenantInfoHoverTrigger>();
        if (hoverTrigger == null)
            hoverTrigger = gameObject.AddComponent<TenantInfoHoverTrigger>();
        hoverTrigger.useWorldHitTest = true;
        hoverTrigger.tenantIdProvider = () => _boundTenantId;

        ApplyColor();
    }

    public TenantInfoHoverTrigger GetOrCreateTrigger()
    {
        if (hoverTrigger == null)
        {
            hoverTrigger = GetComponent<TenantInfoHoverTrigger>();
            if (hoverTrigger == null)
                hoverTrigger = gameObject.AddComponent<TenantInfoHoverTrigger>();
            hoverTrigger.useWorldHitTest = true;
            hoverTrigger.tenantIdProvider = () => _boundTenantId;
        }
        return hoverTrigger;
    }

    public void SetOccupant(string tenantId)
    {
        _boundTenantId = tenantId;
        EnsureCollider();
    }

    public void ClearOccupant()
    {
        _boundTenantId = null;
    }

    public void SetColor(Color color)
    {
        this.color = color;
        ApplyColor();
    }

    public void SetVisible(bool value)
    {
        gameObject.SetActive(value);
    }

    private void EnsureCollider()
    {
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Vector2 extents = spriteRenderer.sprite.bounds.extents;
            circle.radius = Mathf.Max(extents.x, extents.y, 0.01f);
        }
    }

    private void ApplyColor()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }
}
