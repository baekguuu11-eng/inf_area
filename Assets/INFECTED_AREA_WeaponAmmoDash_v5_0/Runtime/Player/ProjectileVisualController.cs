using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ProjectileVisualController : MonoBehaviour
{
    private const float VisualScaleMultiplier = 1.25f;

    private SpriteRenderer legacy;
    private SpriteRenderer outline;
    private SpriteRenderer glow;
    private SpriteRenderer core;
    private TrailRenderer trail;
    private static Sprite fallbackSprite;

    public void Configure(WeaponDefinition weapon, Collider2D projectileCollider)
    {
        EnsureHierarchy();

        Sprite sprite = weapon != null && weapon.projectileSprite != null
            ? weapon.projectileSprite
            : GetFallbackSprite();

        outline.sprite = sprite;
        glow.sprite = sprite;
        core.sprite = sprite;

        Color accent = weapon != null ? weapon.accentColor : Color.cyan;
        Color coreColor = weapon != null ? weapon.projectileCoreColor : Color.white;
        Color glowColor = weapon != null ? weapon.projectileGlowColor : new Color(0.1f, 0.9f, 1f, 0.55f);

        outline.color = new Color(0.015f, 0.025f, 0.055f, 0.95f);
        core.color = coreColor;
        glow.color = glowColor;

        outline.sortingOrder = 78;
        glow.sortingOrder = 79;
        core.sortingOrder = 80;

        V620SpriteMaterialUtility.Apply(outline);
        V620SpriteMaterialUtility.Apply(glow);
        V620SpriteMaterialUtility.Apply(core);

        float gameplayLength = weapon != null ? weapon.projectileWorldLength : 0.14f;
        float gameplayThickness = weapon != null ? weapon.projectileWorldThickness : 0.055f;
        float visualLength = Mathf.Max(0.18f, gameplayLength * VisualScaleMultiplier);
        float visualThickness = Mathf.Max(0.070f, gameplayThickness * VisualScaleMultiplier);

        SetSize(core, visualLength, visualThickness);
        SetSize(outline, visualLength * 1.16f, visualThickness * 1.48f);

        float glowMultiplier = weapon != null ? weapon.projectileGlowMultiplier : 1.8f;
        SetSize(glow, visualLength * 1.16f, visualThickness * Mathf.Max(1.55f, glowMultiplier));

        trail.time = weapon != null ? Mathf.Max(0.045f, weapon.projectileTrailDuration) : 0.055f;
        trail.startWidth = visualThickness * 0.88f;
        trail.endWidth = visualThickness * 0.08f;
        trail.startColor = new Color(
            Mathf.Lerp(coreColor.r, 1f, 0.55f),
            Mathf.Lerp(coreColor.g, 1f, 0.55f),
            Mathf.Lerp(coreColor.b, 1f, 0.55f),
            0.85f);
        trail.endColor = new Color(accent.r, accent.g, accent.b, 0f);
        trail.enabled = trail.time > 0.001f;
        trail.emitting = trail.enabled;
        trail.Clear();

        // 충돌 판정은 기존 크기를 유지하고 표시만 1.25배 키운다.
        if (projectileCollider is BoxCollider2D box)
        {
            box.size = new Vector2(gameplayLength * 0.72f, gameplayThickness * 0.72f);
            box.offset = Vector2.zero;
        }
        else if (projectileCollider is CapsuleCollider2D capsule)
        {
            capsule.size = new Vector2(gameplayLength * 0.72f, gameplayThickness * 0.72f);
            capsule.offset = Vector2.zero;
            capsule.direction = CapsuleDirection2D.Horizontal;
        }

        if (legacy != null)
            legacy.enabled = false;
    }

    private void EnsureHierarchy()
    {
        legacy = GetComponent<SpriteRenderer>();
        outline = EnsureRenderer("ProjectileOutline");
        glow = EnsureRenderer("ProjectileGlow");
        core = EnsureRenderer("ProjectileCore");

        trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        trail.sharedMaterial = V620SpriteMaterialUtility.GetTrailMaterial();
        trail.minVertexDistance = 0.008f;
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.TransformZ;
        trail.numCapVertices = 0;
        trail.numCornerVertices = 0;
        trail.generateLightingData = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private SpriteRenderer EnsureRenderer(string objectName)
    {
        Transform child = transform.Find(objectName);
        if (child == null)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(transform, false);
            child = visualObject.transform;
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer : child.gameObject.AddComponent<SpriteRenderer>();
    }

    private static void SetSize(SpriteRenderer renderer, float width, float height)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localPosition = Vector3.zero;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = new Vector3(
            width / Mathf.Max(0.0001f, spriteSize.x),
            height / Mathf.Max(0.0001f, spriteSize.y),
            1f);
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        Texture2D texture = new Texture2D(8, 3, TextureFormat.RGBA32, false);
        texture.name = "IA_V620_ProjectileTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[24];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        for (int x = 1; x < 7; x++)
        {
            pixels[1 * 8 + x] = Color.white;
            if (x >= 2 && x <= 5)
            {
                pixels[0 * 8 + x] = new Color(0.55f, 0.95f, 1f, 1f);
                pixels[2 * 8 + x] = new Color(0.55f, 0.95f, 1f, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 3f), new Vector2(0.5f, 0.5f), 32f, 0u, SpriteMeshType.FullRect);
        fallbackSprite.name = "IA_V620_ProjectileSprite";
        return fallbackSprite;
    }
}
