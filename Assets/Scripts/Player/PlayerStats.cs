using System;
using UnityEngine;

/// <summary>
/// 플레이어 성능 수치의 단일 기준점.
/// 이동/체력/근접/원거리/칩/상점 강화 값을 여기서 최종 계산한다.
/// 현재 체력, 공격 진행 상태 같은 런타임 상태는 각 담당 컴포넌트가 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    [Header("Base Survivability")]
    [SerializeField] private int baseMaxHealth = 5;
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float baseDefenseMultiplier = 1f;

    [Header("Base Melee")]
    [SerializeField] private int baseMeleeDamage = 1;
    [SerializeField] private float baseMeleeCooldown = 0.25f;
    [SerializeField] private float baseMeleeRange = 1.05f;
    [SerializeField] private float baseMeleeAngle = 120f;
    [SerializeField] private float baseMeleeKnockback = 2.2f;

    [Header("Base Ranged")]
    [SerializeField] private int baseRangedDamage = 1;
    [SerializeField] private float baseRangedCooldown = 0.15f;
    [SerializeField] private float baseProjectileSpeed = 12f;
    [SerializeField] private float baseProjectileScale = 1f;
    [SerializeField] private int baseProjectilePierce = 0;

    [Header("Other")]
    [SerializeField] private float baseSkillCooldownMultiplier = 1f;
    [SerializeField] private int overclockHealthBonus = 0;

    private int signature;

    public int MaxHealth { get; private set; }
    public float MoveSpeed { get; private set; }
    public float DefenseMultiplier { get; private set; }

    public int MeleeDamage { get; private set; }
    public float MeleeCooldown { get; private set; }
    public float MeleeRange { get; private set; }
    public float MeleeAngle { get; private set; }
    public float MeleeKnockback { get; private set; }

    public int RangedDamage { get; private set; }
    public float RangedCooldown { get; private set; }
    public float ProjectileSpeed { get; private set; }
    public float ProjectileScale { get; private set; }
    public int ProjectilePierce { get; private set; }
    public float SkillCooldownMultiplier { get; private set; }

    public event Action StatsChanged;

    private void Awake()
    {
        Recalculate(true);
    }

    private void Update()
    {
        Recalculate(false);
    }

    public void SetBaseValuesFromLegacy(int maxHealth, float moveSpeed, int meleeDamage, float meleeCooldown,
        float meleeRange, float meleeAngle, int rangedDamage, float rangedCooldown, float projectileSpeed)
    {
        baseMaxHealth = Mathf.Max(1, maxHealth);
        baseMoveSpeed = Mathf.Max(0.1f, moveSpeed);
        baseMeleeDamage = Mathf.Max(1, meleeDamage);
        baseMeleeCooldown = Mathf.Max(0.03f, meleeCooldown);
        baseMeleeRange = Mathf.Max(0.1f, meleeRange);
        baseMeleeAngle = Mathf.Clamp(meleeAngle, 10f, 360f);
        baseRangedDamage = Mathf.Max(1, rangedDamage);
        baseRangedCooldown = Mathf.Max(0.03f, rangedCooldown);
        baseProjectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        Recalculate(true);
    }

    public void SetOverclockHealthBonus(int amount)
    {
        overclockHealthBonus = Mathf.Max(0, amount);
        Recalculate(true);
    }

    public void ForceRefresh()
    {
        Recalculate(true);
    }

    private void Recalculate(bool force)
    {
        ChipSlotManager chips = ChipSlotManager.Instance;

        float meleeDamageMultiplier = chips != null ? chips.MeleeDamageMultiplier : 1f;
        float meleeSpeedMultiplier = chips != null ? chips.MeleeAttackSpeedMultiplier : 1f;
        float meleeRangeMultiplier = chips != null ? chips.MeleeRangeMultiplier : 1f;
        float rangedDamageMultiplier = chips != null ? chips.RangedDamageMultiplier : 1f;
        float rangedSpeedMultiplier = chips != null ? chips.RangedAttackSpeedMultiplier : 1f;
        float defenseMultiplier = chips != null ? chips.DefenseDamageMultiplier : 1f;
        float moveMultiplier = chips != null ? chips.MoveSpeedMultiplier : 1f;
        int pierceBonus = chips != null && chips.IsRangedPierceEnabled ? 1 : 0;

        int newSignature = 17;
        newSignature = newSignature * 31 + Quantize(meleeDamageMultiplier);
        newSignature = newSignature * 31 + Quantize(meleeSpeedMultiplier);
        newSignature = newSignature * 31 + Quantize(meleeRangeMultiplier);
        newSignature = newSignature * 31 + Quantize(rangedDamageMultiplier);
        newSignature = newSignature * 31 + Quantize(rangedSpeedMultiplier);
        newSignature = newSignature * 31 + Quantize(defenseMultiplier);
        newSignature = newSignature * 31 + Quantize(moveMultiplier);
        newSignature = newSignature * 31 + pierceBonus;
        newSignature = newSignature * 31 + Quantize(ShopRunUpgradeState.AttackDamageMultiplier);
        newSignature = newSignature * 31 + Quantize(ShopRunUpgradeState.WeaponDamageMultiplier);
        newSignature = newSignature * 31 + Quantize(ShopRunUpgradeState.SkillCooldownMultiplier);
        newSignature = newSignature * 31 + overclockHealthBonus;

        if (!force && newSignature == signature)
            return;

        signature = newSignature;

        MaxHealth = Mathf.Max(1, baseMaxHealth + overclockHealthBonus);
        MoveSpeed = Mathf.Max(0.1f, baseMoveSpeed * moveMultiplier);
        DefenseMultiplier = Mathf.Clamp(baseDefenseMultiplier * defenseMultiplier, 0.05f, 5f);

        float globalDamage = Mathf.Max(0.05f, ShopRunUpgradeState.AttackDamageMultiplier);
        float weaponDamage = Mathf.Max(0.05f, ShopRunUpgradeState.WeaponDamageMultiplier);

        MeleeDamage = Mathf.Max(1, Mathf.RoundToInt(baseMeleeDamage * meleeDamageMultiplier * globalDamage * weaponDamage));
        MeleeCooldown = Mathf.Max(0.03f, baseMeleeCooldown / Mathf.Max(0.05f, meleeSpeedMultiplier));
        MeleeRange = Mathf.Max(0.1f, baseMeleeRange * meleeRangeMultiplier);
        MeleeAngle = Mathf.Clamp(baseMeleeAngle, 10f, 360f);
        MeleeKnockback = Mathf.Max(0f, baseMeleeKnockback);

        RangedDamage = Mathf.Max(1, Mathf.RoundToInt(baseRangedDamage * rangedDamageMultiplier * globalDamage * weaponDamage));
        RangedCooldown = Mathf.Max(0.03f, baseRangedCooldown / Mathf.Max(0.05f, rangedSpeedMultiplier));
        ProjectileSpeed = Mathf.Max(0.1f, baseProjectileSpeed);
        ProjectileScale = Mathf.Max(0.1f, baseProjectileScale);
        ProjectilePierce = Mathf.Max(0, baseProjectilePierce + pierceBonus);
        SkillCooldownMultiplier = Mathf.Clamp(baseSkillCooldownMultiplier * ShopRunUpgradeState.SkillCooldownMultiplier, 0.1f, 5f);

        StatsChanged?.Invoke();
    }

    private int Quantize(float value)
    {
        return Mathf.RoundToInt(value * 10000f);
    }
}
