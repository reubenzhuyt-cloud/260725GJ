using UnityEngine;

public static class TenantAvatarResolver
{
    private const string AvatarResourcesFolder = "CharacterPhoto/Characters/";

    public static bool TryResolve(string avatarKey, out Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(avatarKey))
        {
            sprite = null;
            return false;
        }

        sprite = Resources.Load<Sprite>(AvatarResourcesFolder + avatarKey);
        return sprite != null;
    }
}
