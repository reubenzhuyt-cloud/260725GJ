using Hotel.Runtime;

public static class AbilityDisplayName
{
    public static string Get(TenantAbility ability)
    {
        switch (ability)
        {
            case TenantAbility.Doctor: return "医生";
            case TenantAbility.Cook: return "厨师";
            case TenantAbility.Engineer: return "工程师";
            case TenantAbility.NightWatch: return "守夜人";
            case TenantAbility.FormerEmployee: return "前员工";
            case TenantAbility.Merchant: return "商贩";
            case TenantAbility.Carpenter: return "木工";
            case TenantAbility.Farmer: return "农民";
            default: return "无标签";
        }
    }
}
