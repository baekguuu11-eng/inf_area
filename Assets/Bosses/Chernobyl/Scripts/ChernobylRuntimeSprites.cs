using UnityEngine;

public static class ChernobylRuntimeSprites
{
    private static Sprite whitePixel;
    private static Sprite filledCircle;
    private static Sprite ring;
    private static Sprite diamond;

    public static Sprite WhitePixel
    {
        get
        {
            if (whitePixel == null)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "CHN_WhitePixel_Texture";
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                Color[] pixels = { Color.white, Color.white, Color.white, Color.white };
                texture.SetPixels(pixels);
                texture.Apply();
                whitePixel = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                whitePixel.name = "CHN_WhitePixel";
            }
            return whitePixel;
        }
    }

    public static Sprite FilledCircle
    {
        get
        {
            if (filledCircle == null)
                filledCircle = CreateCircleSprite("CHN_FilledCircle", 48, false);
            return filledCircle;
        }
    }

    public static Sprite Ring
    {
        get
        {
            if (ring == null)
                ring = CreateCircleSprite("CHN_Ring", 48, true);
            return ring;
        }
    }

    public static Sprite Diamond
    {
        get
        {
            if (diamond == null)
            {
                const int size = 24;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "CHN_Diamond_Texture";
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                Color clear = new Color(0f, 0f, 0f, 0f);
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Abs((x + 0.5f) - size * 0.5f);
                        float dy = Mathf.Abs((y + 0.5f) - size * 0.5f);
                        pixels[y * size + x] = dx + dy <= size * 0.42f ? Color.white : clear;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                diamond = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                diamond.name = "CHN_Diamond";
            }
            return diamond;
        }
    }

    private static Sprite CreateCircleSprite(string name, int size, bool hollow)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name + "_Texture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.45f;
        float inner = size * 0.34f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                bool on = hollow ? d <= outer && d >= inner : d <= outer;
                pixels[y * size + x] = on ? Color.white : clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = name;
        return sprite;
    }
}
