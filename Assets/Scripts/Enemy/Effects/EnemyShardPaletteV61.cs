using UnityEngine;

/// <summary>
/// 적 유형별 대표 색을 파편용 팔레트로 제공한다.
/// 별도 파편 아트가 없어도 적의 정체성이 사망 연출에 남도록 한다.
/// </summary>
public static class EnemyShardPaletteV61
{
    public struct Palette
    {
        public Color main;
        public Color secondary;
        public Color dark;
        public Color highlight;
        public Color accent;

        public Color Pick(float roll, out bool emissive)
        {
            emissive = false;
            if (roll < 0.50f) return main;
            if (roll < 0.72f) return secondary;
            if (roll < 0.88f) return dark;
            if (roll < 0.96f) return highlight;
            emissive = true;
            return accent * 1.65f;
        }
    }

    public static Palette Resolve(EnemyRole role, SpriteRenderer source)
    {
        EnemyRole.Role resolvedRole = role != null ? role.CurrentRole : EnemyRole.Role.Melee;
        Palette palette;

        switch (resolvedRole)
        {
            case EnemyRole.Role.Ranged:
                palette = new Palette
                {
                    main = From255(80, 72, 158),
                    secondary = From255(61, 31, 111),
                    dark = From255(25, 8, 54),
                    highlight = From255(225, 218, 255),
                    accent = From255(255, 44, 164)
                };
                break;
            case EnemyRole.Role.Tank:
                palette = new Palette
                {
                    main = From255(246, 131, 18),
                    secondary = From255(177, 75, 9),
                    dark = From255(69, 15, 8),
                    highlight = From255(255, 213, 112),
                    accent = From255(255, 43, 31)
                };
                break;
            case EnemyRole.Role.Bomber:
                palette = new Palette
                {
                    main = From255(77, 77, 102),
                    secondary = From255(105, 127, 132),
                    dark = From255(19, 19, 24),
                    highlight = From255(239, 239, 245),
                    accent = From255(255, 42, 36)
                };
                break;
            case EnemyRole.Role.Boss:
                palette = new Palette
                {
                    main = From255(87, 54, 137),
                    secondary = From255(37, 24, 67),
                    dark = From255(12, 10, 22),
                    highlight = From255(222, 232, 255),
                    accent = From255(86, 239, 255)
                };
                break;
            default:
                palette = new Palette
                {
                    main = From255(245, 79, 126),
                    secondary = From255(196, 33, 82),
                    dark = From255(66, 10, 18),
                    highlight = From255(255, 147, 172),
                    accent = From255(255, 80, 36)
                };
                break;
        }

        if (source != null && source.color != Color.white)
        {
            Color tint = source.color;
            palette.main *= tint;
            palette.secondary *= tint;
            palette.highlight *= Color.Lerp(Color.white, tint, 0.35f);
            palette.accent *= Color.Lerp(Color.white, tint, 0.25f);
        }

        return palette;
    }

    private static Color From255(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
