using UnityEngine;

/// <summary>
/// 외부 임시 리소스가 비어 있어도 핵심 전투 오브젝트가 보이도록 만드는 작은 런타임 픽셀 스프라이트 모음.
/// 실제 아트가 연결되어 있으면 이 스프라이트는 사용되지 않는다.
/// </summary>
public static class RuntimePixelSpriteFactory
{
    private static Sprite playerProjectile;
    private static Sprite enemyProjectile;
    private static Sprite bossProjectile;
    private static Sprite warningZone;
    private static Sprite hitExpression;
    private static Sprite whitePixel;
    private static Sprite enemyShard;
    private static readonly Sprite[] enemyShardVariants = new Sprite[5];
    private static Sprite casing;
    private static Sprite shotgunCasing;
    private static Sprite muzzleFlash;
    private static Sprite energySpark;
    private static Sprite deathStain;
    private static Sprite portal;

    public static Sprite GetPlayerProjectileSprite()
    {
        if (playerProjectile == null)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 edge = new Color32(65, 31, 160, 255);
            Color32 main = new Color32(232, 86, 255, 255);
            Color32 core = new Color32(188, 246, 255, 255);
            playerProjectile = CreateSprite("PlayerProjectile_Runtime", 16, 8, 16f, (x, y) =>
            {
                int cy = 3;
                if (x <= 1 || x >= 15 || Mathf.Abs(y - cy) > 2) return clear;
                if (Mathf.Abs(y - cy) == 2 || x == 2 || x == 14) return edge;
                if (Mathf.Abs(y - cy) == 0 && x >= 6 && x <= 12) return core;
                return main;
            });
        }
        return playerProjectile;
    }

    public static Sprite GetEnemyProjectileSprite()
    {
        if (enemyProjectile == null)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 edge = new Color32(120, 22, 12, 255);
            Color32 main = new Color32(255, 91, 35, 255);
            Color32 core = new Color32(255, 231, 92, 255);
            enemyProjectile = CreateRadialSprite("EnemyProjectile_Runtime", 14, 14, 14f, edge, main, core, clear);
        }
        return enemyProjectile;
    }

    public static Sprite GetBossProjectileSprite()
    {
        if (bossProjectile == null)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 edge = new Color32(42, 12, 91, 255);
            Color32 main = new Color32(145, 47, 255, 255);
            Color32 core = new Color32(160, 248, 255, 255);
            bossProjectile = CreateRadialSprite("MachineWormProjectile_Runtime", 18, 18, 18f, edge, main, core, clear);
        }
        return bossProjectile;
    }

    public static Sprite GetWarningZoneSprite()
    {
        if (warningZone == null)
        {
            const int size = 64;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 red = new Color32(255, 30, 30, 210);
            Color32 bright = new Color32(255, 180, 160, 235);
            float center = (size - 1) * 0.5f;
            warningZone = CreateSprite("MachineWormWarning_Runtime", size, size, size, (x, y) =>
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool outer = d >= 27f && d <= 30f;
                bool inner = d >= 17f && d <= 19f;
                bool cross = (Mathf.Abs(dx) <= 1f || Mathf.Abs(dy) <= 1f) && d <= 25f;
                bool diagonalCrack = (Mathf.Abs(dx - dy) <= 0.8f || Mathf.Abs(dx + dy) <= 0.8f) && d >= 7f && d <= 15f;
                if (outer || inner) return bright;
                if (cross || diagonalCrack) return red;
                return clear;
            });
        }
        return warningZone;
    }

    public static Sprite GetHitExpressionSprite()
    {
        if (hitExpression == null)
        {
            const int width = 24;
            const int height = 10;
            Color32 blueEdge = new Color32(18, 34, 116, 255);
            Color32 blue = new Color32(65, 102, 245, 255);
            Color32 white = new Color32(235, 248, 255, 255);
            hitExpression = CreateSprite("MachineWormHitFace_Runtime", width, height, 24f, (x, y) =>
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1) return blueEdge;
                bool leftEye = (x == 5 && y == 6) || (x == 6 && y == 5) || (x == 7 && y == 4);
                bool rightEye = (x == 16 && y == 4) || (x == 17 && y == 5) || (x == 18 && y == 6);
                bool mouth = (y == 3 && x >= 10 && x <= 13) || (y == 2 && (x == 9 || x == 14));
                if (leftEye || rightEye || mouth) return white;
                return blue;
            });
        }
        return hitExpression;
    }

    public static Sprite GetEnemyShardSprite()
    {
        return GetEnemyShardSprite(0);
    }

    public static Sprite GetEnemyShardSprite(int variant)
    {
        variant = Mathf.Abs(variant) % enemyShardVariants.Length;
        if (enemyShardVariants[variant] != null)
            return enemyShardVariants[variant];

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        int width = variant == 1 ? 5 : variant == 2 ? 8 : variant == 3 ? 6 : variant == 4 ? 3 : 7;
        int height = variant == 1 ? 5 : variant == 2 ? 3 : variant == 3 ? 6 : variant == 4 ? 3 : 5;

        enemyShardVariants[variant] = CreateSprite("EnemyShard_V61_" + variant, width, height, 16f, (x, y) =>
        {
            bool body;
            switch (variant)
            {
                case 1:
                    body = (x == 2 && y >= 0 && y <= 4) || (y == 2 && x >= 1 && x <= 3);
                    break;
                case 2:
                    body = (y == 1 && x >= 0 && x <= 7) || (y == 2 && x >= 2 && x <= 6);
                    break;
                case 3:
                    body = x >= Mathf.Max(0, y - 1) && x <= Mathf.Min(width - 1, y + 1);
                    break;
                case 4:
                    body = (x == 1 && y == 1) || (x == 1 && y == 2) || (x == 2 && y == 1);
                    break;
                default:
                    body = (y == 1 && x >= 1 && x <= 5) ||
                           (y == 2 && x >= 0 && x <= 5) ||
                           (y == 3 && x >= 2 && x <= 6);
                    break;
            }
            return body ? white : clear;
        });

        if (variant == 0)
            enemyShard = enemyShardVariants[0];
        return enemyShardVariants[variant];
    }

    public static Sprite GetCasingSprite(bool shotgun)
    {
        if (shotgun)
        {
            if (shotgunCasing == null)
            {
                Color32 clear = new Color32(0, 0, 0, 0);
                Color32 white = new Color32(255, 255, 255, 255);
                shotgunCasing = CreateSprite("ShotgunCasing_V61", 6, 3, 16f, (x, y) =>
                {
                    bool body = y == 1 || (y == 0 && x >= 1 && x <= 4) || (y == 2 && x >= 1 && x <= 4);
                    return body ? white : clear;
                });
            }
            return shotgunCasing;
        }

        if (casing == null)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            casing = CreateSprite("Casing_V61", 5, 2, 16f, (x, y) =>
            {
                bool body = (y == 0 && x >= 1 && x <= 4) || (y == 1 && x >= 0 && x <= 3);
                return body ? white : clear;
            });
        }
        return casing;
    }

    public static Sprite GetMuzzleFlashSprite()
    {
        if (muzzleFlash == null)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            muzzleFlash = CreateSprite("MuzzleFlash_V61", 12, 9, 16f, (x, y) =>
            {
                int cy = 4;
                bool core = x >= 0 && x <= 8 && Mathf.Abs(y - cy) <= 1;
                bool upper = x >= 4 && x <= 9 && y == cy + 2;
                bool lower = x >= 4 && x <= 9 && y == cy - 2;
                bool tip = (x == 10 && Mathf.Abs(y - cy) <= 1) || (x == 11 && y == cy);
                return core || upper || lower || tip ? white : clear;
            });
        }
        return muzzleFlash;
    }

    public static Sprite GetEnergySparkSprite()
    {
        if (energySpark == null)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            energySpark = CreateSprite("EnergySpark_V61", 5, 5, 16f, (x, y) =>
            {
                bool body = (x == 2) || (y == 2) || (x == y && x >= 1 && x <= 3);
                return body ? white : clear;
            });
        }
        return energySpark;
    }

    public static Sprite GetDeathStainSprite()
    {
        if (deathStain == null)
        {
            const int width = 18;
            const int height = 9;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 solid = new Color32(255, 255, 255, 210);
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            deathStain = CreateSprite("DeathStain_Runtime", width, height, 24f, (x, y) =>
            {
                float dx = (x - cx) / 8f;
                float dy = (y - cy) / 3.5f;
                float distance = dx * dx + dy * dy;
                if (distance > 1f) return clear;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(70f, solid.a, 1f - distance));
                return new Color32(solid.r, solid.g, solid.b, alpha);
            });
        }
        return deathStain;
    }

    public static Sprite GetPortalSprite()
    {
        if (portal == null)
        {
            const int width = 32;
            const int height = 48;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 edge = new Color32(25, 80, 120, 255);
            Color32 glow = new Color32(62, 235, 255, 255);
            Color32 core = new Color32(205, 255, 255, 255);
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            portal = CreateSprite("Portal_Runtime", width, height, 32f, (x, y) =>
            {
                float dx = (x - cx) / 12.5f;
                float dy = (y - cy) / 20f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1.05f || d < 0.68f) return clear;
                if (d > 0.96f || d < 0.73f) return edge;
                if ((x + y) % 7 == 0) return core;
                return glow;
            });
        }
        return portal;
    }

    public static Sprite GetWhitePixelSprite()
    {
        if (whitePixel == null)
        {
            whitePixel = CreateSprite("WhitePixel_Runtime", 1, 1, 1f, (x, y) => new Color32(255, 255, 255, 255));
        }
        return whitePixel;
    }

    private static Sprite CreateRadialSprite(string name, int width, int height, float ppu,
        Color32 edge, Color32 main, Color32 core, Color32 clear)
    {
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;
        float radius = Mathf.Min(width, height) * 0.44f;
        return CreateSprite(name, width, height, ppu, (x, y) =>
        {
            float dx = x - cx;
            float dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > radius) return clear;
            if (d > radius - 1.6f) return edge;
            if (dx < -1f && dy > 1f && d < radius * 0.55f) return core;
            return main;
        });
    }

    private static Sprite CreateSprite(string name, int width, int height, float ppu, System.Func<int, int, Color32> pixel)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name + "_Texture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] colors = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                colors[y * width + x] = pixel(x, y);
            }
        }

        texture.SetPixels32(colors);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
