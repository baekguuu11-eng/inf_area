using UnityEngine;
public static class JHLArtV25
{
    private static Sprite[] sprites;
    private static Material lineMaterial;
    public static Material LineMaterial
    {
        get { if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default")); return lineMaterial; }
    }
    public static Sprite[] Load()
    {
        if (sprites != null) return sprites;
        Texture2D texture = Resources.Load<Texture2D>("Bosses/JHL/V25/Atlas");
        if (texture == null) throw new System.InvalidOperationException("JHL V25 Atlas missing");
        Rect[] rects = new Rect[] { new Rect(23,606,339,349),new Rect(414,635,309,324),new Rect(787,611,322,345),new Rect(1166,595,350,364),new Rect(31,154,328,309),new Rect(396,67,372,393),new Rect(768,66,380,435),new Rect(1160,64,368,437) };
        sprites = new Sprite[rects.Length];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = Sprite.Create(texture, rects[i], new Vector2(.5f,.5f), 100f, 0, SpriteMeshType.FullRect);
        return sprites;
    }
}
