using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v6.1.5: URP material errors cannot turn warnings magenta because every shape uses
/// SpriteRenderer with Unity's built-in sprite material. Warnings disappear as damage starts.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyTelegraph : MonoBehaviour
{
    private enum ShapeMode { None, Circle, Line, Sector }

    [Header("Warning Style")]
    [SerializeField] private SpriteRenderer rendererTarget;
    [SerializeField] private Color fillColor = new Color(1f, 0.04f, 0.04f, 0.16f);
    [SerializeField] private Color outlineColor = new Color(1f, 0.10f, 0.08f, 0.78f);
    [SerializeField] private Color scanColor = new Color(1f, 0.26f, 0.16f, 0.52f);
    [SerializeField, Min(0f)] private float pulseSpeed = 7.5f;
    [SerializeField] private int sortingOrder = 18;

    private static Sprite circleFillSprite;
    private static Sprite circleOutlineSprite;
    private static Sprite lineFillSprite;
    private static Sprite lineOutlineSprite;
    private static Sprite scanBarSprite;
    private static readonly Dictionary<int, Sprite> sectorFillCache = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Sprite> sectorOutlineCache = new Dictionary<int, Sprite>();

    private SpriteRenderer outlineRenderer;
    private SpriteRenderer scanRenderer;
    private ShapeMode shapeMode;
    private bool visible;
    private float progress01;

    public bool IsVisible => visible;

    private void Awake()
    {
        EnsureRenderers();
        Hide();
    }

    private void Update()
    {
        if (!visible) return;

        ApplyCurrentColors();
        AnimateScan();
    }

    public void SetProgress(float value) => progress01 = Mathf.Clamp01(value);

    public void ShowCircle(float radius)
    {
        EnsureRenderers();
        shapeMode = ShapeMode.Circle;
        rendererTarget.sprite = GetCircleSprite(false);
        outlineRenderer.sprite = GetCircleSprite(true);
        scanRenderer.sprite = GetCircleSprite(true);
        ResetRoot();
        SetRootWorldSize(new Vector2(Mathf.Max(0.05f, radius * 2f), Mathf.Max(0.05f, radius * 2f)));
        outlineRenderer.transform.localPosition = Vector3.zero;
        outlineRenderer.transform.localRotation = Quaternion.identity;
        outlineRenderer.transform.localScale = Vector3.one;
        scanRenderer.transform.localPosition = Vector3.zero;
        scanRenderer.transform.localRotation = Quaternion.identity;
        scanRenderer.transform.localScale = Vector3.one * 0.18f;
        ShowAll();
    }

    public void ShowLine(Vector2 direction, float length, float width)
    {
        EnsureRenderers();
        shapeMode = ShapeMode.Line;
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float safeLength = Mathf.Max(0.05f, length);
        float safeWidth = Mathf.Max(0.03f, width);
        rendererTarget.sprite = GetLineSprite(false);
        outlineRenderer.sprite = GetLineSprite(true);
        scanRenderer.sprite = GetScanSprite();
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        SetWorldLocalPosition(dir * safeLength * 0.5f);
        SetRootWorldSize(new Vector2(safeLength, safeWidth));
        outlineRenderer.transform.localPosition = Vector3.zero;
        outlineRenderer.transform.localRotation = Quaternion.identity;
        outlineRenderer.transform.localScale = Vector3.one;
        scanRenderer.transform.localPosition = new Vector3(-0.47f, 0f, 0f);
        scanRenderer.transform.localRotation = Quaternion.identity;
        scanRenderer.transform.localScale = new Vector3(0.045f, 0.82f, 1f);
        ShowAll();
    }

    public void ShowSector(Vector2 direction, float radius, float angleDegrees)
    {
        EnsureRenderers();
        shapeMode = ShapeMode.Sector;
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        int angleKey = Mathf.RoundToInt(Mathf.Clamp(angleDegrees, 5f, 360f));
        rendererTarget.sprite = GetSectorSprite(angleKey, false);
        outlineRenderer.sprite = GetSectorSprite(angleKey, true);
        scanRenderer.sprite = GetSectorSprite(angleKey, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        transform.localScale = Vector3.one;
        SetRootWorldSize(new Vector2(Mathf.Max(0.05f, radius * 2f), Mathf.Max(0.05f, radius * 2f)));
        outlineRenderer.transform.localPosition = Vector3.zero;
        outlineRenderer.transform.localRotation = Quaternion.identity;
        outlineRenderer.transform.localScale = Vector3.one;
        scanRenderer.transform.localPosition = Vector3.zero;
        scanRenderer.transform.localRotation = Quaternion.identity;
        scanRenderer.transform.localScale = Vector3.one * 0.20f;
        ShowAll();
    }

    public void FlashImpact(float duration = 0.12f) => Hide();

    public void Hide()
    {
        visible = false;
        progress01 = 0f;
        shapeMode = ShapeMode.None;
        ResetRoot();
        if (rendererTarget != null) rendererTarget.enabled = false;
        if (outlineRenderer != null) outlineRenderer.enabled = false;
        if (scanRenderer != null) scanRenderer.enabled = false;
    }

    private void ShowAll()
    {
        progress01 = 0f;
        visible = true;

        // Apply the red palette before enabling renderers. This removes the one-frame
        // white flash that previously appeared when a telegraph first opened.
        ApplyCurrentColors();
        rendererTarget.enabled = true;
        outlineRenderer.enabled = true;
        scanRenderer.enabled = true;
    }

    private void EnsureRenderers()
    {
        if (rendererTarget == null) rendererTarget = GetComponent<SpriteRenderer>();
        if (rendererTarget == null) rendererTarget = gameObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(rendererTarget, sortingOrder);

        outlineRenderer = GetOrCreateChildRenderer("TelegraphOutline", sortingOrder + 1);
        scanRenderer = GetOrCreateChildRenderer("TelegraphScan", sortingOrder + 2);
    }

    private SpriteRenderer GetOrCreateChildRenderer(string childName, int order)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject go = new GameObject(childName);
            child = go.transform;
            child.SetParent(transform, false);
        }
        SpriteRenderer result = child.GetComponent<SpriteRenderer>();
        if (result == null) result = child.gameObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(result, order);
        return result;
    }

    private void ConfigureRenderer(SpriteRenderer target, int order)
    {
        V620SpriteMaterialUtility.Apply(target);
        target.sortingLayerID = rendererTarget != null ? rendererTarget.sortingLayerID : 0;
        target.sortingOrder = order;
        target.maskInteraction = SpriteMaskInteraction.None;
        target.color = new Color(1f, 0f, 0f, 0f);
    }

    private void ApplyCurrentColors()
    {
        float time = Time.unscaledTime;
        float pulse = pulseSpeed <= 0f
            ? 1f
            : Mathf.Lerp(0.88f, 1f, (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f);
        float blink = progress01 < 0.82f
            ? 1f
            : (Mathf.Sin(time * Mathf.Lerp(16f, 31f, progress01)) > -0.10f ? 1f : 0.48f);

        Color fill = fillColor;
        fill.a *= Mathf.Lerp(0.55f, 1.15f, progress01) * pulse;
        Color outline = outlineColor;
        outline.a *= Mathf.Lerp(0.70f, 1.05f, progress01) * blink;
        Color scan = scanColor;
        scan.a *= Mathf.Lerp(0.52f, 1f, progress01);

        if (rendererTarget != null) rendererTarget.color = fill;
        if (outlineRenderer != null) outlineRenderer.color = outline;
        if (scanRenderer != null) scanRenderer.color = scan;
    }

    private void AnimateScan()
    {
        if (scanRenderer == null || !scanRenderer.enabled) return;
        if (shapeMode == ShapeMode.Line)
            scanRenderer.transform.localPosition = new Vector3(Mathf.Lerp(-0.47f, 0.47f, progress01), 0f, 0f);
        else if (shapeMode == ShapeMode.Circle || shapeMode == ShapeMode.Sector)
            scanRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.20f, 1.02f, progress01);
    }

    private void ResetRoot()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void SetWorldLocalPosition(Vector2 worldOffset)
    {
        transform.localPosition = transform.parent != null ? transform.parent.InverseTransformVector((Vector3)worldOffset) : (Vector3)worldOffset;
    }

    private void SetRootWorldSize(Vector2 worldSize)
    {
        Sprite sprite = rendererTarget != null ? rendererTarget.sprite : null;
        if (sprite == null) return;
        Vector2 spriteSize = sprite.bounds.size;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, spriteSize.x * Mathf.Abs(parentScale.x)),
            worldSize.y / Mathf.Max(0.0001f, spriteSize.y * Mathf.Abs(parentScale.y)), 1f);
    }

    private static Sprite GetCircleSprite(bool outline)
    {
        if (outline && circleOutlineSprite != null) return circleOutlineSprite;
        if (!outline && circleFillSprite != null) return circleFillSprite;
        const int size = 64;
        Texture2D texture = NewTexture(size, size, outline ? "TelegraphCircleOutlineTex" : "TelegraphCircleFillTex");
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = 29.5f, inner = 26.5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            bool on = outline ? d <= outer && d >= inner : d <= outer;
            texture.SetPixel(x, y, on ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        Sprite sprite = MakeSprite(texture, outline ? "TelegraphCircleOutline" : "TelegraphCircleFill", size);
        if (outline) circleOutlineSprite = sprite; else circleFillSprite = sprite;
        return sprite;
    }

    private static Sprite GetLineSprite(bool outline)
    {
        if (outline && lineOutlineSprite != null) return lineOutlineSprite;
        if (!outline && lineFillSprite != null) return lineFillSprite;
        const int width = 96, height = 24;
        Texture2D texture = NewTexture(width, height, outline ? "TelegraphLineOutlineTex" : "TelegraphLineFillTex");
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool border = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
            texture.SetPixel(x, y, (outline ? border : true) ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        Sprite sprite = MakeSprite(texture, outline ? "TelegraphLineOutline" : "TelegraphLineFill", width);
        if (outline) lineOutlineSprite = sprite; else lineFillSprite = sprite;
        return sprite;
    }

    private static Sprite GetScanSprite()
    {
        if (scanBarSprite != null) return scanBarSprite;
        Texture2D texture = NewTexture(8, 32, "TelegraphScanTex");
        for (int y = 0; y < 32; y++) for (int x = 0; x < 8; x++) texture.SetPixel(x, y, Color.white);
        texture.Apply(false, true);
        scanBarSprite = MakeSprite(texture, "TelegraphScan", 32);
        return scanBarSprite;
    }

    private static Sprite GetSectorSprite(int angle, bool outline)
    {
        Dictionary<int, Sprite> cache = outline ? sectorOutlineCache : sectorFillCache;
        if (cache.TryGetValue(angle, out Sprite sprite) && sprite != null) return sprite;
        const int size = 128;
        Texture2D texture = NewTexture(size, size, outline ? "TelegraphSectorOutlineTex" : "TelegraphSectorFillTex");
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = 58f;
        float half = angle * 0.5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 v = new Vector2(x, y) - center;
            float d = v.magnitude;
            float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            bool inside = d <= radius && Mathf.Abs(Mathf.DeltaAngle(0f, a)) <= half;
            bool edgeArc = inside && d >= radius - 3f;
            bool edgeSide = inside && Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, a)) - half) <= 2.2f;
            bool nearCenter = inside && d <= 2.5f;
            bool on = outline ? edgeArc || edgeSide || nearCenter : inside;
            texture.SetPixel(x, y, on ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        sprite = MakeSprite(texture, $"TelegraphSector_{angle}_{(outline ? "Outline" : "Fill")}", size);
        cache[angle] = sprite;
        return sprite;
    }

    private static Texture2D NewTexture(int width, int height, string name)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] clear = new Color[width * height];
        texture.SetPixels(clear);
        return texture;
    }

    private static Sprite MakeSprite(Texture2D texture, string name, float ppu)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
