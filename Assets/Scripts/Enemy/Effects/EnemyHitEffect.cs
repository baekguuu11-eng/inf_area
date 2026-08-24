using System.Collections;
using UnityEngine;

public class EnemyHitEffect : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.06f;

    [Header("Shake")]
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private float shakeDuration = 0.05f;

    [Header("Outline")]
    [SerializeField] private Color outlineColor = new Color(0.6f, 1f, 1f, 0.85f);
    [SerializeField] private float outlineDuration = 0.06f;
    [SerializeField] private float outlineScaleMultiplier = 1.08f;

    [Header("Optional Animator Hook")]
    [SerializeField] private Animator animator;
    [SerializeField] private string hitTriggerName = "Hit";

    private Color originalColor;
    private SpriteRenderer outlineRenderer;

    // Rigidbody2D가 있으면 흔들림도 물리 위치(MovePosition) 기준으로 처리해서
    // 물리 엔진이 추적하는 위치와 Transform 직접 조작이 충돌하지 않게 함.
    private Rigidbody2D rb;

    private Coroutine flashCoroutine;
    private Coroutine shakeCoroutine;
    private Coroutine outlineCoroutine;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        rb = GetComponent<Rigidbody2D>();

        if (animator == null)
            animator = GetComponent<Animator>();

        CreateOutlineRenderer();
    }

    private void LateUpdate()
    {
        if (outlineRenderer == null || spriteRenderer == null)
            return;

        outlineRenderer.sprite = spriteRenderer.sprite;
        outlineRenderer.flipX = spriteRenderer.flipX;
        outlineRenderer.flipY = spriteRenderer.flipY;
        outlineRenderer.color = outlineColor;
        outlineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
    }

    public void PlayHit()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        if (outlineCoroutine != null)
            StopCoroutine(outlineCoroutine);

        flashCoroutine = StartCoroutine(FlashRoutine());
        shakeCoroutine = StartCoroutine(ShakeRoutine());
        outlineCoroutine = StartCoroutine(OutlineRoutine());

        if (animator != null && !string.IsNullOrEmpty(hitTriggerName))
            animator.SetTrigger(hitTriggerName);
    }

    private IEnumerator FlashRoutine()
    {
        if (spriteRenderer == null)
            yield break;

        spriteRenderer.color = flashColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        spriteRenderer.color = originalColor;
    }

    // [버그 수정] 예전엔 Awake 시점 위치를 계속 재사용해서, 적이 스폰 지점에서 멀리 이동한 뒤
    // 피격당하면 그 스폰 지점으로 순간이동하는 버그가 있었음.
    // 이제는 매번 "현재" 위치를 기준으로 흔들고, 그 자리로 복귀함.
    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        if (rb != null)
        {
            Vector2 basePosition = rb.position;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
                rb.MovePosition(basePosition + offset);
                yield return null;
            }

            rb.MovePosition(basePosition);
        }
        else
        {
            Vector3 basePosition = transform.localPosition;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
                transform.localPosition = basePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            transform.localPosition = basePosition;
        }
    }

    private IEnumerator OutlineRoutine()
    {
        if (outlineRenderer == null)
            yield break;

        outlineRenderer.enabled = true;
        outlineRenderer.transform.localScale = Vector3.one * outlineScaleMultiplier;

        yield return new WaitForSecondsRealtime(outlineDuration);

        outlineRenderer.enabled = false;
    }

    private void CreateOutlineRenderer()
    {
        if (spriteRenderer == null)
            return;

        GameObject outlineObject = new GameObject("HitOutline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one * outlineScaleMultiplier;

        outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = spriteRenderer.sprite;
        outlineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        outlineRenderer.color = outlineColor;
        outlineRenderer.enabled = false;
    }
}
