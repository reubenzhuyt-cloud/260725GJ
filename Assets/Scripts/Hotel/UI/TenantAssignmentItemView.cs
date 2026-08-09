using UnityEngine;

public readonly struct TenantAssignmentItemView
{
    public string TenantId { get; }
    public string DisplayName { get; }
    public Color Color { get; }
    public string AvatarKey { get; }

    public TenantAssignmentItemView(string tenantId, string displayName, Color color, string avatarKey)
    {
        TenantId = tenantId;
        DisplayName = displayName;
        Color = color;
        AvatarKey = avatarKey;
    }
}
