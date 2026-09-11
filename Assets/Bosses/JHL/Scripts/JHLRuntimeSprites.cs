using UnityEngine;

public static class JHLRuntimeSprites
{
    private static Sprite whitePixel;
    private static Sprite filledCircle;
    private static Sprite ring;
    private static Sprite diamond;

    public static Sprite WhitePixel => whitePixel != null ? whitePixel : (whitePixel = CreateWhitePixel());
    public static Sprite FilledCircle => filledCircle != null ? filledCircle : (filledCircle = CreateCircle("JHL_FilledCircle", false));
    public static Sprite Ring => ring != null ? ring : (ring = CreateCircle("JHL_Ring", true));
    public static Sprite Diamond => diamond != null ? diamond : (diamond = CreateDiamond());

    private static Sprite CreateWhitePixel()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "JHL_WhitePixelTex";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "JHL_WhitePixel";
        return sprite;
    }

    private static Sprite CreateCircle(string name, bool hollow)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name + "Tex";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.45f;
        float inner = size * 0.32f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                bool on = hollow ? d <= outer && d >= inner : d <= outer;
                pixels[y * size + x] = on ? Color.white : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = name;
        return sprite;
    }

    private static Sprite CreateDiamond()
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "JHL_DiamondTex";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        int center = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int manhattan = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                pixels[y * size + x] = manhattan <= center - 1 ? Color.white : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "JHL_Diamond";
        return sprite;
    }
}
