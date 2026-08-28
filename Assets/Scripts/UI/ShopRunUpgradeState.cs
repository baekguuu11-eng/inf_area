using System;

public static class ShopRunUpgradeState
{
    public static float AttackDamageMultiplier = 1f;
    public static float WeaponDamageMultiplier = 1f;
    public static float SkillCooldownMultiplier = 1f;

    public static event Action Changed;

    public static void AddAttackDamage(float amount)
    {
        AttackDamageMultiplier = Math.Max(0.1f, AttackDamageMultiplier + amount);
        Changed?.Invoke();
    }

    public static void AddWeaponDamage(float amount)
    {
        WeaponDamageMultiplier = Math.Max(0.1f, WeaponDamageMultiplier + amount);
        Changed?.Invoke();
    }

    public static void MultiplySkillCooldown(float multiplier)
    {
        SkillCooldownMultiplier = Math.Max(0.1f, SkillCooldownMultiplier * multiplier);
        Changed?.Invoke();
    }

    public static void ResetRunUpgrades()
    {
        AttackDamageMultiplier = 1f;
        WeaponDamageMultiplier = 1f;
        SkillCooldownMultiplier = 1f;
        Changed?.Invoke();
    }
}
