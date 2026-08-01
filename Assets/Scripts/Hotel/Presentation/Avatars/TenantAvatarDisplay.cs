using UnityEngine;

public class TenantAvatarDisplay : MonoBehaviour
{
    [Header("Display")]
    public SpriteRenderer spriteRenderer;
    public Color color = Color.white;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyColor();
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

    private void ApplyColor()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }
}
