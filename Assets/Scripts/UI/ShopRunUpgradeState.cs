public static class ShopRunUpgradeState
{
    public static float AttackDamageMultiplier = 1f;
    public static float WeaponDamageMultiplier = 1f;
    public static float SkillCooldownMultiplier = 1f;

    public static void ResetRunUpgrades()
    {
        AttackDamageMultiplier = 1f;
        WeaponDamageMultiplier = 1f;
        SkillCooldownMultiplier = 1f;
    }
}