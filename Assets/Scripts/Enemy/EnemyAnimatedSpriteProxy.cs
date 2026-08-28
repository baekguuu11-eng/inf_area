using UnityEngine;

/// <summary>
/// v3.0 계열의 임시 Sprite 복제 구조가 남아 있어도 적이 투명해지지 않도록 하는 안전 프록시입니다.
/// v3.0.3 복구 설치기는 정상 적 프리팹에서 이 컴포넌트를 제거하고 Animator를 BodyVisual에 직접 연결합니다.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class EnemyAnimatedSpriteProxy : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private SpriteRenderer targetRenderer;

    public SpriteRenderer SourceRenderer => sourceRenderer;
    public SpriteRenderer TargetRenderer => targetRenderer;

    private void Awake()
    {
        ResolveReferences();
        SyncNow();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SyncNow();
    }

    private void LateUpdate()
    {
        SyncNow();
    }

    public void Configure(SpriteRenderer source, SpriteRenderer target)
    {
        sourceRenderer = source;
        targetRenderer = target;
        SyncNow();
    }

    private void ResolveReferences()
    {
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<SpriteRenderer>();

        if (targetRenderer != null)
            return;

        Transform visualRoot = transform.Find("VisualRoot");
        if (visualRoot == null)
            return;

        Transform body = visualRoot.Find("BodyVisual");
        if (body != null)
            targetRenderer = body.GetComponent<SpriteRenderer>();

        if (targetRenderer != null)
            return;

        SpriteRenderer[] candidates = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            SpriteRenderer candidate = candidates[i];
            if (candidate == null || candidate == sourceRenderer)
                continue;

            string lowerName = candidate.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("shadow") || lowerName.Contains("telegraph"))
                continue;

            targetRenderer = candidate;
            break;
        }
    }

    private void SyncNow()
    {
        ResolveReferences();

        if (sourceRenderer == null && targetRenderer == null)
            return;

        if (targetRenderer == null || targetRenderer == sourceRenderer)
        {
            if (sourceRenderer != null)
                sourceRenderer.enabled = true;
            return;
        }

        Sprite validSprite = sourceRenderer != null && sourceRenderer.sprite != null
            ? sourceRenderer.sprite
            : targetRenderer.sprite;

        // 두 Renderer 모두 유효한 Sprite를 잃었다면 원본을 숨기지 않습니다.
        if (validSprite == null)
        {
            if (sourceRenderer != null)
                sourceRenderer.enabled = true;
            targetRenderer.enabled = false;
            return;
        }

        if (targetRenderer.sprite != validSprite)
            targetRenderer.sprite = validSprite;

        if (sourceRenderer != null)
        {
            targetRenderer.flipX = sourceRenderer.flipX;
            targetRenderer.flipY = sourceRenderer.flipY;
            targetRenderer.maskInteraction = sourceRenderer.maskInteraction;
            targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            targetRenderer.sortingOrder = sourceRenderer.sortingOrder;
        }

        Color color = targetRenderer.color;
        if (color.a <= 0.001f)
            color.a = 1f;
        targetRenderer.color = color;
        targetRenderer.enabled = true;

        if (sourceRenderer != null)
            sourceRenderer.enabled = false;
    }
}
