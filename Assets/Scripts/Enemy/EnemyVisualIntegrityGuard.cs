using UnityEngine;

/// <summary>
/// 런타임 중 적의 본체 Sprite가 누락되거나 Renderer가 꺼지는 경우 즉시 복구합니다.
/// 공격/AI/물리에는 관여하지 않습니다.
/// </summary>
[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class EnemyVisualIntegrityGuard : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer legacyRootRenderer;
    [SerializeField] private Sprite fallbackSprite;
    [SerializeField] private int expectedSortingLayerId;
    [SerializeField] private int expectedSortingOrder;
    [SerializeField, Min(0.05f)] private float validationInterval = 0.25f;

    private float nextValidationTime;
    private bool warned;

    public void Configure(
        SpriteRenderer body,
        SpriteRenderer legacy,
        Sprite fallback,
        int sortingLayerId,
        int sortingOrder)
    {
        bodyRenderer = body;
        legacyRootRenderer = legacy;
        fallbackSprite = fallback;
        expectedSortingLayerId = sortingLayerId;
        expectedSortingOrder = sortingOrder;
        ValidateAndRepair();
    }

    private void Awake()
    {
        ResolveReferences();
        ValidateAndRepair();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ValidateAndRepair();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextValidationTime)
            return;

        nextValidationTime = Time.unscaledTime + validationInterval;
        ValidateAndRepair();
    }

    private void ResolveReferences()
    {
        if (legacyRootRenderer == null)
            legacyRootRenderer = GetComponent<SpriteRenderer>();

        if (bodyRenderer == null)
        {
            Transform visualRoot = transform.Find("VisualRoot");
            Transform body = visualRoot != null ? visualRoot.Find("BodyVisual") : null;
            if (body != null)
                bodyRenderer = body.GetComponent<SpriteRenderer>();
        }

        if (bodyRenderer == null)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer == legacyRootRenderer)
                    continue;

                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("shadow") || lowerName.Contains("telegraph") || lowerName.Contains("particle"))
                    continue;

                bodyRenderer = renderer;
                break;
            }
        }
    }

    private void ValidateAndRepair()
    {
        ResolveReferences();

        Sprite usableSprite = bodyRenderer != null && bodyRenderer.sprite != null
            ? bodyRenderer.sprite
            : legacyRootRenderer != null && legacyRootRenderer.sprite != null
                ? legacyRootRenderer.sprite
                : fallbackSprite;

        if (bodyRenderer != null && usableSprite != null)
        {
            if (bodyRenderer.sprite == null)
                bodyRenderer.sprite = usableSprite;

            Color bodyColor = bodyRenderer.color;
            if (bodyColor.a < 0.95f)
                bodyColor.a = 1f;
            bodyRenderer.color = bodyColor;
            bodyRenderer.sortingLayerID = expectedSortingLayerId;
            bodyRenderer.sortingOrder = expectedSortingOrder;
            V620SpriteMaterialUtility.Apply(bodyRenderer);
            bodyRenderer.enabled = true;

            if (legacyRootRenderer != null && legacyRootRenderer != bodyRenderer)
                legacyRootRenderer.enabled = false;

            warned = false;
            return;
        }

        // BodyVisual이 완전히 망가졌을 때는 원본 Renderer라도 살려서 투명 적을 방지합니다.
        if (legacyRootRenderer != null && legacyRootRenderer.sprite != null)
        {
            Color legacyColor = legacyRootRenderer.color;
            if (legacyColor.a < 0.95f)
                legacyColor.a = 1f;
            legacyRootRenderer.color = legacyColor;
            V620SpriteMaterialUtility.Apply(legacyRootRenderer);
            legacyRootRenderer.enabled = true;
            return;
        }

        if (!warned)
        {
            warned = true;
            Debug.LogError($"[EnemyVisualIntegrityGuard] 표시 가능한 Sprite가 없습니다: {name}", this);
        }
    }
}
