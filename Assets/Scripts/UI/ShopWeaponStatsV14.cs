using System;

public static class ShopWeaponStatsV14
{
    public sealed class Profile
    {
        public readonly string[] Labels;
        public readonly int[] Values;
        public readonly string Trait;
        public Profile(string[] labels, int[] values, string trait)
        {
            Labels = labels;
            Values = values;
            Trait = trait;
        }
    }

    private static readonly string[] MeleeLabels = { "공격력", "공격속도", "사거리", "공격범위", "저지력" };
    private static readonly string[] RangedLabels = { "공격력", "연사력", "사거리", "정확도", "탄약효율" };

    public static bool TryGet(string weaponId, out Profile profile)
    {
        profile = null;
        if (string.IsNullOrWhiteSpace(weaponId)) return false;
        switch (weaponId.Trim().ToLowerInvariant())
        {
            case "debug_sword":
                profile = new Profile(MeleeLabels, new[] { 3, 4, 3, 3, 3 }, "균형형"); return true;
            case "debug_hammer":
                profile = new Profile(MeleeLabels, new[] { 5, 2, 2, 4, 5 }, "중량 충격"); return true;
            case "debug_whip":
                profile = new Profile(MeleeLabels, new[] { 2, 4, 5, 4, 2 }, "데이터 구속"); return true;
            case "firewall_sword":
                profile = new Profile(MeleeLabels, new[] { 4, 3, 3, 4, 3 }, "침식"); return true;
            case "pistol":
                profile = new Profile(RangedLabels, new[] { 3, 3, 4, 5, 5 }, "안정성"); return true;
            case "machine_gun":
                profile = new Profile(RangedLabels, new[] { 3, 5, 4, 3, 2 }, "지속 화력"); return true;
            case "shotgun":
                profile = new Profile(RangedLabels, new[] { 5, 2, 2, 2, 3 }, "근접 파쇄"); return true;
            case "laser_gun":
                profile = new Profile(RangedLabels, new[] { 4, 3, 5, 5, 3 }, "관통 정밀사격"); return true;
            default:
                return false;
        }
    }
}
