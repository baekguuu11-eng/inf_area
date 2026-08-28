using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class EnemySizeController : MonoBehaviour
{
    public enum ScaleMode
    {
        PlayerRelative,
        AbsoluteWorldHeight,
        ManualLocalScale
    }

    [Header("크기 설정 방식")]
    [SerializeField] private ScaleMode scaleMode = ScaleMode.PlayerRelative;
    [SerializeField, Min(0.05f)] private float playerHeightMultiplier = 1f;
    [SerializeField, Min(0.05f)] private float referencePlayerWorldHeight = 0.775f;
    [SerializeField, Min(0.05f)] private float absoluteWorldHeight = 0.82f;
    [SerializeField] private Vector3 manualLocalScale = Vector3.one;
    [SerializeField] private bool protectFromAutomaticSetup;
    [SerializeField] private bool previewChangesInEditor = true;
    [SerializeField] private bool useLivePlayerVisualHeight = true;
    [SerializeField, Min(0.1f)] private float visualOnlyMultiplier = 1f;

    [Header("참조")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer groundShadow;
    [SerializeField] private Transform healthBarRoot;

    [Header("판정 및 보조 요소")]
    [SerializeField, Range(0.2f, 1.2f)] private float colliderWidthRatio = 0.72f;
    [SerializeField, Range(0.2f, 1.2f)] private float colliderHeightRatio = 0.70f;
    [SerializeField] private Vector2 colliderOffsetRatio = new Vector2(0f, -0.03f);
    [SerializeField, Min(0.1f)] private float shadowWidthMultiplier = 0.82f;
    [SerializeField, Min(0.02f)] private float shadowHeightWorld = 0.14f;
    [SerializeField, Min(0f)] private float healthBarHeightOffset = 0.18f;

    public ScaleMode CurrentScaleMode => scaleMode;
    public float PlayerHeightMultiplier => playerHeightMultiplier;
    public bool ProtectFromAutomaticSetup => protectFromAutomaticSetup;
    public float TargetWorldHeight => GetEffectiveTargetWorldHeight();

    private void Awake()
    {
        ResolveReferences();
        ApplySizeNow();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (Application.isPlaying)
            ApplySizeNow();
    }

    private void Start()
    {
        // Start runs after every scene object's Awake, so PlayerVisual has already applied
        // its own visual scale. Re-measure once here for an exact screen-size match.
        if (Application.isPlaying)
            ApplySizeNow();
    }

    private void OnValidate()
    {
        playerHeightMultiplier = Mathf.Max(0.05f, playerHeightMultiplier);
        referencePlayerWorldHeight = Mathf.Max(0.05f, referencePlayerWorldHeight);
        absoluteWorldHeight = Mathf.Max(0.05f, absoluteWorldHeight);
        manualLocalScale.z = 1f;
        visualOnlyMultiplier = Mathf.Max(0.1f, visualOnlyMultiplier);

        if (!Application.isPlaying && previewChangesInEditor)
        {
            ResolveReferences();
            ApplySizeNow();
        }
    }

    [ContextMenu("현재 크기 설정 즉시 적용")]
    public void ApplySizeNow()
    {
        ResolveReferences();
        if (visualRoot == null || bodyRenderer == null || bodyRenderer.sprite == null)
            return;

        if (scaleMode == ScaleMode.ManualLocalScale)
        {
            visualRoot.localScale = new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(manualLocalScale.x)) * Mathf.Sign(manualLocalScale.x == 0f ? 1f : manualLocalScale.x),
                Mathf.Max(0.001f, Mathf.Abs(manualLocalScale.y)) * Mathf.Sign(manualLocalScale.y == 0f ? 1f : manualLocalScale.y),
                1f);
        }
        else
        {
            float targetHeight = GetEffectiveTargetWorldHeight();
            float spriteHeight = Mathf.Max(0.0001f, bodyRenderer.sprite.bounds.size.y);
            Transform parent = visualRoot.parent;
            float parentWorldScaleY = parent != null ? Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.y)) : 1f;
            float uniformLocalScale = targetHeight / (spriteHeight * parentWorldScaleY);

            float signX = visualRoot.localScale.x < 0f ? -1f : 1f;
            float signY = visualRoot.localScale.y < 0f ? -1f : 1f;
            visualRoot.localScale = new Vector3(signX * uniformLocalScale, signY * uniformLocalScale, 1f);
        }


        V620SpriteMaterialUtility.Apply(bodyRenderer);
        bodyRenderer.enabled = true;
        Color bodyColor = bodyRenderer.color;
        bodyColor.a = 1f;
        bodyRenderer.color = bodyColor;
        DisableLegacyBodyRenderers();

        ApplyCollider();
        ApplyShadow();
        ApplyHealthBarOffset();

        EnemyVisualMotion motion = GetComponent<EnemyVisualMotion>();
        if (motion != null)
            motion.RefreshBasePose();
    }

    public void SetReferencePlayerHeight(float worldHeight)
    {
        referencePlayerWorldHeight = Mathf.Max(0.05f, worldHeight);
        ApplySizeNow();
    }

    public void SetPlayerRelativeMultiplier(float multiplier)
    {
        scaleMode = ScaleMode.PlayerRelative;
        playerHeightMultiplier = Mathf.Max(0.05f, multiplier);
        ApplySizeNow();
    }

    private float GetTargetWorldHeight()
    {
        switch (scaleMode)
        {
            case ScaleMode.AbsoluteWorldHeight:
                return Mathf.Max(0.05f, absoluteWorldHeight);
            case ScaleMode.ManualLocalScale:
                if (bodyRenderer != null && bodyRenderer.sprite != null)
                    return bodyRenderer.sprite.bounds.size.y * Mathf.Abs(visualRoot != null ? visualRoot.lossyScale.y : manualLocalScale.y);
                return Mathf.Max(0.05f, referencePlayerWorldHeight * playerHeightMultiplier);
            default:
                float playerWorldHeight = useLivePlayerVisualHeight
                    ? ResolveLivePlayerVisualWorldHeight()
                    : 0f;
                if (playerWorldHeight <= 0.001f)
                    playerWorldHeight = referencePlayerWorldHeight;
                return Mathf.Max(0.05f, playerWorldHeight * playerHeightMultiplier);
        }
    }

    private float GetEffectiveTargetWorldHeight()
    {
        return Mathf.Max(0.05f, GetTargetWorldHeight() * Mathf.Max(0.1f, visualOnlyMultiplier));
    }

    private float ResolveLivePlayerVisualWorldHeight()
    {
        if (!Application.isPlaying)
            return 0f;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return 0f;

        PlayerVisual playerVisual = playerObject.GetComponentInChildren<PlayerVisual>(true);
        SpriteRenderer playerRenderer = playerVisual != null
            ? playerVisual.GetComponent<SpriteRenderer>()
            : null;
        if (playerRenderer == null || playerRenderer.sprite == null)
            return 0f;

        return Mathf.Max(0f, playerRenderer.bounds.size.y);
    }

    private void ApplyCollider()
    {
        if (bodyCollider == null || bodyRenderer == null || bodyRenderer.sprite == null)
            return;

        float targetHeight = GetEffectiveTargetWorldHeight();
        float aspect = bodyRenderer.sprite.bounds.size.x / Mathf.Max(0.0001f, bodyRenderer.sprite.bounds.size.y);
        float worldWidth = targetHeight * Mathf.Max(0.15f, aspect) * colliderWidthRatio;
        float worldHeight = targetHeight * colliderHeightRatio;
        float rootScaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float rootScaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        Vector2 localSize = new Vector2(worldWidth / rootScaleX, worldHeight / rootScaleY);
        Vector2 localOffset = new Vector2(
            colliderOffsetRatio.x * targetHeight / rootScaleX,
            colliderOffsetRatio.y * targetHeight / rootScaleY);

        if (bodyCollider is CapsuleCollider2D capsule)
        {
            capsule.size = localSize;
            capsule.offset = localOffset;
            capsule.direction = localSize.y >= localSize.x ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;
        }
        else if (bodyCollider is BoxCollider2D box)
        {
            box.size = localSize;
            box.offset = localOffset;
        }
        else if (bodyCollider is CircleCollider2D circle)
        {
            circle.radius = Mathf.Min(localSize.x, localSize.y) * 0.5f;
            circle.offset = localOffset;
        }
    }

    private void ApplyShadow()
    {
        if (groundShadow == null)
            return;

        float targetHeight = GetEffectiveTargetWorldHeight();
        float aspect = bodyRenderer != null && bodyRenderer.sprite != null
            ? bodyRenderer.sprite.bounds.size.x / Mathf.Max(0.0001f, bodyRenderer.sprite.bounds.size.y)
            : 1f;
        Vector2 spriteSize = groundShadow.sprite != null ? groundShadow.sprite.bounds.size : Vector2.one;
        Transform parent = groundShadow.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;

        float targetWidth = targetHeight * Mathf.Max(0.25f, aspect) * shadowWidthMultiplier;
        float localX = targetWidth / Mathf.Max(0.0001f, spriteSize.x * Mathf.Abs(parentScale.x));
        float localY = shadowHeightWorld / Mathf.Max(0.0001f, spriteSize.y * Mathf.Abs(parentScale.y));
        groundShadow.transform.localScale = new Vector3(localX, localY, 1f);
        groundShadow.transform.localPosition = new Vector3(0f, -targetHeight * 0.43f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 0f);
    }

    private void ApplyHealthBarOffset()
    {
        if (healthBarRoot == null)
            return;

        float targetHeight = GetEffectiveTargetWorldHeight();
        Transform parent = healthBarRoot.parent;
        float parentScaleY = parent != null ? Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.y)) : 1f;
        Vector3 local = healthBarRoot.localPosition;
        local.y = (targetHeight * 0.5f + healthBarHeightOffset) / parentScaleY;
        healthBarRoot.localPosition = local;
    }

    private void DisableLegacyBodyRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null || candidate == bodyRenderer || candidate == groundShadow)
                continue;

            string lower = candidate.gameObject.name.ToLowerInvariant();
            if (lower.Contains("telegraph") || lower.Contains("shadow") || lower.Contains("particle") ||
                lower.Contains("health") || lower.Contains("stain"))
                continue;

            // 과거 패치에서 남은 Root/Visual 복제 Renderer가 분홍 사각형을 만들지 못하게 합니다.
            if (candidate.gameObject == gameObject || lower == "visual" || lower.Contains("legacy"))
                candidate.enabled = false;
        }
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
        {
            Transform named = transform.Find("VisualRoot");
            visualRoot = named != null ? named : transform;
        }

        if (bodyRenderer == null)
        {
            Transform body = transform.Find("VisualRoot/BodyVisual");
            if (body != null)
                bodyRenderer = body.GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
                bodyRenderer = FindLargestBodyRenderer();
        }

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();

        if (groundShadow == null)
        {
            Transform shadow = transform.Find("GroundShadow");
            if (shadow == null && visualRoot != null)
                shadow = visualRoot.Find("GroundShadow");
            if (shadow != null)
                groundShadow = shadow.GetComponent<SpriteRenderer>();
        }

        if (healthBarRoot == null)
        {
            Transform candidate = transform.Find("HealthBar");
            if (candidate != null)
                healthBarRoot = candidate;
        }
    }

    private SpriteRenderer FindLargestBodyRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer best = null;
        float bestArea = -1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null || candidate.sprite == null)
                continue;

            string lower = candidate.gameObject.name.ToLowerInvariant();
            if (lower.Contains("shadow") || lower.Contains("telegraph") || lower.Contains("particle"))
                continue;

            float area = candidate.sprite.bounds.size.x * candidate.sprite.bounds.size.y;
            if (area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }
        return best;
    }
}
