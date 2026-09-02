using UnityEngine;

/// <summary>
/// 집행자 보스의 임시 도형 리소스 모음.
/// 실제 스프라이트가 Resources/Bosses/Executor에 있으면 그것을 우선 사용한다.
/// </summary>
public static class ExecutorRuntimeSprites
{
    private static Sprite shockwaveRing;
    private static Sprite explosion;
    private static Sprite byteReward;
    private static Sprite ammoReward;
    private static Sprite glow;
    private static Sprite dustCloud;
    private static Sprite rockChunk;
    private static Sprite groundCrack;
    private static Sprite groundSocketBack;
    private static Sprite groundSocketFront;
    private static Sprite undergroundMound;
    private static Sprite groundRevealMask;

    public static Sprite LoadBody()
    {
        Sprite sprite = Resources.Load<Sprite>("Bosses/Executor/ExecutorBody");
        return sprite != null ? sprite : RuntimePixelSpriteFactory.GetWhitePixelSprite();
    }

    public static Sprite LoadBodyPhase2()
    {
        Sprite sprite = Resources.Load<Sprite>("Bosses/Executor/ExecutorBody_Phase2");
        return sprite != null ? sprite : LoadBody();
    }

    public static Sprite LoadBodyFinal()
    {
        Sprite sprite = Resources.Load<Sprite>("Bosses/Executor/ExecutorBody_Final");
        return sprite != null ? sprite : LoadBodyPhase2();
    }

    public static Sprite LoadEmergeImpact()
    {
        return Resources.Load<Sprite>("Bosses/Executor/ExecutorEmergeImpact");
    }

    public static Sprite LoadDeath()
    {
        return Resources.Load<Sprite>("Bosses/Executor/ExecutorDeath");
    }

    public static Sprite GetProjectile()
    {
        return GetProjectile(1);
    }

    public static Sprite GetProjectile(int stage)
    {
        string path = stage >= 3
            ? "Bosses/Executor/ExecutorProjectile_Final"
            : stage == 2
                ? "Bosses/Executor/ExecutorProjectile_Phase2"
                : "Bosses/Executor/ExecutorProjectile_Phase1";
        Sprite sprite = Resources.Load<Sprite>(path);
        return sprite != null ? sprite : RuntimePixelSpriteFactory.GetBossProjectileSprite();
    }

    public static Sprite GetWarning()
    {
        return RuntimePixelSpriteFactory.GetWarningZoneSprite();
    }

    public static Sprite GetWhitePixel()
    {
        return RuntimePixelSpriteFactory.GetWhitePixelSprite();
    }

    public static Sprite GetGroundRevealMask()
    {
        if (groundRevealMask == null)
        {
            // 1x1 world-unit 마스크. 기존 직선 바닥 경계를 1~3픽셀 정도 불규칙하게 잘라
            // 별도 타원/받침대 없이도 본체가 바닥 속에 박혀 있는 것처럼 보이게 한다.
            const int size = 64;
            groundRevealMask = CreateSprite("ExecutorGroundRevealMask_Runtime", size, size, 64f, (x, y) =>
            {
                int wave = ((x * 17 + 5) % 11 == 0 ? 2 : 0)
                    + ((x * 7 + 3) % 13 == 0 ? 1 : 0)
                    - ((x * 5 + 1) % 17 == 0 ? 1 : 0);
                int threshold = Mathf.Clamp(1 + wave, 1, 3);
                if (y < threshold)
                    return new Color32(0, 0, 0, 0);
                return new Color32(255, 255, 255, 255);
            });
        }
        return groundRevealMask;
    }

    public static Sprite GetGlow()
    {
        if (glow == null)
        {
            glow = CreateRadialSprite(
                "ExecutorGlow_Runtime",
                32,
                32f,
                new Color32(255, 255, 255, 255),
                new Color32(110, 226, 255, 210),
                new Color32(20, 106, 190, 0));
        }
        return glow;
    }

    public static Sprite GetGroundSocketBack()
    {
        if (groundSocketBack == null)
        {
            const int width = 96;
            const int height = 40;
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            groundSocketBack = CreateSprite("ExecutorGroundSocketBack_Runtime", width, height, 32f, (x, y) =>
            {
                float nx = (x - cx) / 44f;
                float ny = (y - cy) / 15f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d > 1f) return new Color32(0, 0, 0, 0);
                if (d > 0.86f) return new Color32(82, 18, 20, 235);
                if (d > 0.70f) return new Color32(31, 8, 10, 245);
                float centerShade = Mathf.InverseLerp(0.70f, 0f, d);
                return new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Lerp(12f, 2f, centerShade)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(5f, 1f, centerShade)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(7f, 2f, centerShade)),
                    245);
            });
        }
        return groundSocketBack;
    }

    public static Sprite GetGroundSocketFront()
    {
        if (groundSocketFront == null)
        {
            // 얇은 전면 지면 립만 그린다. 하단 반원/고리처럼 보이는 픽셀은 완전히 제거한다.
            const int width = 96;
            const int height = 18;
            float cx = (width - 1) * 0.5f;
            groundSocketFront = CreateSprite("ExecutorGroundSocketFront_Runtime", width, height, 32f, (x, y) =>
            {
                float nx = Mathf.Abs((x - cx) / 45f);
                if (nx > 1f) return new Color32(0, 0, 0, 0);

                float arch = 7.0f + (1f - nx * nx) * 5.2f;
                float irregular = ((x * 17 + 11) % 7 == 0 ? 1f : 0f) - ((x * 13 + 3) % 11 == 0 ? 1f : 0f);
                float top = arch + irregular;
                float bottom = Mathf.Max(5f, top - 4.2f);
                if (y < bottom || y > top) return new Color32(0, 0, 0, 0);

                if (y >= top - 1f) return new Color32(92, 28, 30, 240);
                if (y >= top - 2.4f) return new Color32(42, 15, 17, 250);
                return new Color32(17, 8, 10, 252);
            });
        }
        return groundSocketFront;
    }

    public static Sprite GetUndergroundMound()
    {
        if (undergroundMound == null)
        {
            const int width = 72;
            const int height = 28;
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            undergroundMound = CreateSprite("ExecutorUndergroundMound_Runtime", width, height, 32f, (x, y) =>
            {
                float nx = (x - cx) / 32f;
                float ny = (y - cy) / 10f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d > 1f) return new Color32(0, 0, 0, 0);
                if (d > 0.82f) return new Color32(175, 166, 158, 70);
                return new Color32(38, 34, 34, (byte)Mathf.RoundToInt(Mathf.Lerp(90f, 22f, d)));
            });
        }
        return undergroundMound;
    }

    public static Sprite GetShockwaveRing()
    {
        if (shockwaveRing == null)
        {
            const int size = 128;
            float center = (size - 1) * 0.5f;
            shockwaveRing = CreateSprite("ExecutorShockwaveRing_Runtime", size, size, 64f, (x, y) =>
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance >= 57f && distance <= 63f)
                    return new Color32(255, 88, 72, 230);
                if (distance >= 53f && distance < 57f)
                    return new Color32(255, 30, 30, 105);
                return new Color32(0, 0, 0, 0);
            });
        }
        return shockwaveRing;
    }

    public static Sprite GetExplosion()
    {
        if (explosion == null)
        {
            explosion = CreateRadialSprite(
                "ExecutorExplosion_Runtime",
                64,
                32f,
                new Color32(255, 248, 205, 255),
                new Color32(255, 82, 30, 230),
                new Color32(100, 0, 0, 0));
        }
        return explosion;
    }

    public static Sprite GetByteReward()
    {
        if (byteReward == null)
        {
            byteReward = CreateSprite("ExecutorByteReward_Runtime", 12, 12, 16f, (x, y) =>
            {
                bool outer = x >= 2 && x <= 9 && y >= 2 && y <= 9;
                bool inner = x >= 4 && x <= 7 && y >= 4 && y <= 7;
                if (!outer) return new Color32(0, 0, 0, 0);
                if (inner) return new Color32(210, 255, 255, 255);
                return new Color32(40, 220, 245, 255);
            });
        }
        return byteReward;
    }

    public static Sprite GetAmmoReward()
    {
        if (ammoReward == null)
        {
            ammoReward = CreateSprite("ExecutorAmmoReward_Runtime", 12, 12, 16f, (x, y) =>
            {
                bool body = x >= 3 && x <= 8 && y >= 1 && y <= 10;
                bool cap = y >= 8 && y <= 10 && x >= 4 && x <= 7;
                if (!body) return new Color32(0, 0, 0, 0);
                if (cap) return new Color32(255, 250, 175, 255);
                return new Color32(255, 174, 42, 255);
            });
        }
        return ammoReward;
    }

    public static Sprite GetDustCloud()
    {
        if (dustCloud == null)
        {
            dustCloud = CreateRadialSprite(
                "ExecutorDustCloud_Runtime",
                32,
                16f,
                new Color32(255, 255, 255, 235),
                new Color32(205, 211, 216, 185),
                new Color32(90, 99, 108, 0));
        }
        return dustCloud;
    }

    public static Sprite GetRockChunk()
    {
        if (rockChunk == null)
        {
            rockChunk = CreateSprite("ExecutorRockChunk_Runtime", 10, 8, 16f, (x, y) =>
            {
                bool body = (y >= 1 && y <= 5 && x >= 2 && x <= 7)
                    || (y == 6 && x >= 3 && x <= 6)
                    || (y == 0 && x >= 4 && x <= 6)
                    || (x == 1 && y >= 2 && y <= 4)
                    || (x == 8 && y >= 2 && y <= 4);
                if (!body) return new Color32(0, 0, 0, 0);
                if ((x + y) % 5 == 0) return new Color32(166, 158, 150, 255);
                return new Color32(82, 82, 87, 255);
            });
        }
        return rockChunk;
    }

    public static Sprite GetGroundCrack()
    {
        if (groundCrack == null)
        {
            const int size = 64;
            float center = (size - 1) * 0.5f;
            groundCrack = CreateSprite("ExecutorGroundCrack_Runtime", size, size, 32f, (x, y) =>
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = distance >= 20f && distance <= 22f;
                bool branchA = Mathf.Abs(dy - dx * 0.36f) <= 0.8f && distance >= 5f && distance <= 23f;
                bool branchB = Mathf.Abs(dy + dx * 0.52f) <= 0.8f && distance >= 7f && distance <= 21f;
                bool branchC = Mathf.Abs(dx) <= 0.8f && dy >= -20f && dy <= -5f;
                if (ring || branchA || branchB || branchC)
                    return new Color32(83, 22, 20, 225);
                return new Color32(0, 0, 0, 0);
            });
        }
        return groundCrack;
    }

    private static Sprite CreateRadialSprite(string name, int size, float pixelsPerUnit,
        Color32 core, Color32 middle, Color32 edge)
    {
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;
        return CreateSprite(name, size, size, pixelsPerUnit, (x, y) =>
        {
            float dx = x - center;
            float dy = y - center;
            float normalized = Mathf.Sqrt(dx * dx + dy * dy) / radius;
            if (normalized > 1f) return new Color32(0, 0, 0, 0);
            if (normalized < 0.30f) return core;
            if (normalized < 0.68f) return middle;
            Color32 value = edge;
            value.a = (byte)Mathf.RoundToInt(edge.a * Mathf.InverseLerp(1f, 0.68f, normalized));
            return value;
        });
    }

    private static Sprite CreateSprite(string name, int width, int height, float pixelsPerUnit,
        System.Func<int, int, Color32> sampler)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name + "_Texture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = sampler(x, y);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.name = name;
        return sprite;
    }
}
