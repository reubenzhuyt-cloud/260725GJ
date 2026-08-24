using UnityEngine;

namespace Hotel.Authoring.Items
{
    public enum ItemAcquisition
    {
        Merchant,
        EngineerEvent,
        MerchantAndEngineerEvent,
        TruthChain
    }

    public enum ItemTargeting
    {
        None,
        SingleTenant,
        EngineerTenant
    }

    public enum ItemEffectType
    {
        None,
        ErosionSingle,
        ErosionAll,
        NightLoss,
        ExtraClue,
        EngineerBoost,
        TruthClue
    }

    [CreateAssetMenu(menuName = "Hotel/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea(1, 3)] public string hoverDescription;
        [TextArea] public string description;
        [TextArea(4, 12)] public string readableContent;
        [TextArea(3, 8)] public string discoveryScene;
        public Sprite icon;
        public int maxStack = 1;

        public ItemAcquisition acquisition = ItemAcquisition.Merchant;
        public int merchantPrice;
        public ItemTargeting targeting = ItemTargeting.None;
        public ItemEffectType effectType = ItemEffectType.None;
        public float effectValue;
        public string effectFlag;
    }
}
