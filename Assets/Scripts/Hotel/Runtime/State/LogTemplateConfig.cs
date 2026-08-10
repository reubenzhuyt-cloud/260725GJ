using UnityEngine;

namespace Hotel.Runtime
{
    [CreateAssetMenu(fileName = "LogTemplate", menuName = "Configs/LogTemplate")]
    public class LogTemplateConfig : ScriptableObject
    {
        [Header("Identity")]
        public string templateId;

        [Header("Content")]
        public TenantLogCategory category = TenantLogCategory.Behavior;

        [TextArea(2, 4)]
        public string messageTemplate;
    }
}
