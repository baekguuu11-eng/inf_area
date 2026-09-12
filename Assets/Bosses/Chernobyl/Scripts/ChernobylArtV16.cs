using UnityEngine;

/// <summary>Three independent generated layers, normalized once to Executor world-pixel density.</summary>
public static class ChernobylArtV16
{
    public const float PixelsPerUnit = 64f / 1.31f;
    private static Sprite body, ring, core;
    public static bool TryLoad(out Sprite bodySprite, out Sprite ringSprite, out Sprite coreSprite)
    {
        if (body == null) body = Build("Body", 112);
        if (ring == null) ring = Build("Ring", 52);
        if (core == null) core = Build("Core", 32);
        bodySprite = body; ringSprite = ring; coreSprite = core;
        return body != null && ring != null && core != null;
    }

    private static Sprite Build(string name, int longestSide)
    {
        Texture2D source = Resources.Load<Texture2D>("Bosses/Chernobyl/V16/" + name);
        if (source == null || !source.isReadable) return null;
        Color32[] pixels = source.GetPixels32();
        int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
        for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
        {
            if (pixels[y * source.width + x].a < 128) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < minX) return null;
        int sw = maxX - minX + 1, sh = maxY - minY + 1;
        float ratio = longestSide / (float)Mathf.Max(sw, sh);
        int w = Mathf.Max(1, Mathf.RoundToInt(sw * ratio));
        int h = Mathf.Max(1, Mathf.RoundToInt(sh * ratio));
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.name = "CHN_V16_" + name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] result = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int sx = minX + Mathf.Min(sw - 1, Mathf.FloorToInt((x + 0.5f) * sw / w));
            int sy = minY + Mathf.Min(sh - 1, Mathf.FloorToInt((y + 0.5f) * sh / h));
            Color32 c = pixels[sy * source.width + sx];
            c.a = c.a >= 128 ? (byte)255 : (byte)0;
            result[y * w + x] = c;
        }
        texture.SetPixels32(result); texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }
}
