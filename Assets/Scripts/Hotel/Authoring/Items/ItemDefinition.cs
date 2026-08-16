using UnityEngine;

namespace Hotel.Authoring.Items
{
    public enum ItemAcquisition
    {
        Merchant,
        EngineerEvent,
        MerchantAndEngineerEvent
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
        EngineerBoost
    }

    [CreateAssetMenu(menuName = "Hotel/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea] public string description;
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
