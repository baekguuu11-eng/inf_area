using UnityEngine;

/// <summary>
/// 투사체 프리팹의 Sprite/Material 상태와 무관하게 선명한 본체와 외곽선을 보장한다.
/// </summary>
public static class RuntimeProjectileVisual
{
    private const float VisualScaleMultiplier = 1.25f;

    private static Sprite playerSprite;
    private static Sprite enemySprite;

    public static SpriteRenderer Ensure(GameObject owner, bool playerProjectile)
    {
        if (owner == null)
            return null;

        Transform visualTransform = EnsureChild(owner.transform, "ProjectileVisual");
        SpriteRenderer target = EnsureRenderer(visualTransform);
        Transform outlineTransform = EnsureChild(owner.transform, "ProjectileOutline");
        SpriteRenderer outline = EnsureRenderer(outlineTransform);

        SpriteRenderer source = null;
        SpriteRenderer[] existing = owner.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            SpriteRenderer candidate = existing[i];
            if (candidate == null || candidate == target || candidate == outline)
                continue;
            if (source == null || (source.sprite == null && candidate.sprite != null))
                source = candidate;
        }

        Sprite chosenSprite = source != null && source.sprite != null
            ? source.sprite
            : GetFallbackSprite(playerProjectile);

        target.sprite = chosenSprite;
        outline.sprite = chosenSprite;
        target.enabled = true;
        outline.enabled = true;

        target.color = playerProjectile
            ? new Color(0.82f, 1.25f, 1.45f, 1f)
            : new Color(1.45f, 0.52f, 0.14f, 1f);
        outline.color = new Color(0.015f, 0.02f, 0.045f, 0.96f);

        if (source != null)
        {
            target.sortingLayerID = source.sortingLayerID;
            outline.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = Mathf.Max(source.sortingOrder + 4, 80);
            outline.sortingOrder = target.sortingOrder - 1;
            source.enabled = false;
        }
        else
        {
            target.sortingOrder = 80;
            outline.sortingOrder = 79;
        }

        V620SpriteMaterialUtility.Apply(target);
        V620SpriteMaterialUtility.Apply(outline);

        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.identity;
        outlineTransform.localPosition = Vector3.zero;
        outlineTransform.localRotation = Quaternion.identity;

        Vector2 baseSize = playerProjectile ? new Vector2(0.22f, 0.095f) : new Vector2(0.19f, 0.105f);
        Vector2 visibleSize = baseSize * VisualScaleMultiplier;
        FitWorldSize(visualTransform, target, visibleSize);
        FitWorldSize(outlineTransform, outline, new Vector2(visibleSize.x * 1.15f, visibleSize.y * 1.38f));
        return target;
    }

    public static void ConfigureCollider(Collider2D collider, bool playerProjectile)
    {
        if (collider == null)
            return;

        Vector3 scale = collider.transform.lossyScale;
        float safeX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float safeY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        Vector2 worldSize = playerProjectile ? new Vector2(0.14f, 0.075f) : new Vector2(0.13f, 0.085f);
        Vector2 localSize = new Vector2(worldSize.x / safeX, worldSize.y / safeY);

        if (collider is BoxCollider2D box)
        {
            box.size = localSize;
            box.offset = Vector2.zero;
        }
        else if (collider is CapsuleCollider2D capsule)
        {
            capsule.size = localSize;
            capsule.offset = Vector2.zero;
            capsule.direction = CapsuleDirection2D.Horizontal;
        }
    }

    private static Transform EnsureChild(Transform owner, string objectName)
    {
        Transform child = owner.Find(objectName);
        if (child != null)
            return child;

        GameObject visualObject = new GameObject(objectName);
        visualObject.transform.SetParent(owner, false);
        return visualObject.transform;
    }

    private static SpriteRenderer EnsureRenderer(Transform target)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer : target.gameObject.AddComponent<SpriteRenderer>();
    }

    private static void FitWorldSize(Transform targetTransform, SpriteRenderer renderer, Vector2 desiredWorldSize)
    {
        if (targetTransform == null || renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        Vector3 parentScale = targetTransform.parent != null ? targetTransform.parent.lossyScale : Vector3.one;
        float safeX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float safeY = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        targetTransform.localScale = new Vector3(
            desiredWorldSize.x / Mathf.Max(0.0001f, spriteSize.x) / safeX,
            desiredWorldSize.y / Mathf.Max(0.0001f, spriteSize.y) / safeY,
            1f);
    }

    private static Sprite GetFallbackSprite(bool playerProjectile)
    {
        if (playerProjectile && playerSprite != null)
            return playerSprite;
        if (!playerProjectile && enemySprite != null)
            return enemySprite;

        const int width = 10;
        const int height = 5;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = playerProjectile ? "RuntimePlayerProjectile" : "RuntimeEnemyProjectile";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color edge = playerProjectile
            ? new Color(0.20f, 0.75f, 1f, 1f)
            : new Color(1f, 0.10f, 0.02f, 1f);
        Color core = playerProjectile
            ? Color.white
            : new Color(1f, 0.82f, 0.16f, 1f);

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        for (int x = 1; x < width - 1; x++)
        {
            pixels[2 * width + x] = core;
            if (x >= 2 && x <= width - 3)
            {
                pixels[1 * width + x] = edge;
                pixels[3 * width + x] = edge;
            }
        }
        pixels[2 * width] = edge;
        pixels[2 * width + width - 1] = edge;

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 64f, 0u, SpriteMeshType.FullRect);
        sprite.name = texture.name + "_Sprite";

        if (playerProjectile)
            playerSprite = sprite;
        else
            enemySprite = sprite;
        return sprite;
    }
}
