using UnityEngine;

namespace Hotel.Authoring.Resources
{
    [CreateAssetMenu(menuName = "Hotel/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        public string resourceId;
        public string displayName;
        public int initialAmount;
        public Sprite icon;
    }
}
