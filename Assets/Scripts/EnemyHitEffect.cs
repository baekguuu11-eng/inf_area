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

    private Color originalColor;
    private Vector3 originalLocalPosition;
    private SpriteRenderer outlineRenderer;

    private Coroutine flashCoroutine;
    private Coroutine shakeCoroutine;
    private Coroutine outlineCoroutine;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        originalLocalPosition = transform.localPosition;
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
    }

    private IEnumerator FlashRoutine()
    {
        if (spriteRenderer == null)
            yield break;

        spriteRenderer.color = flashColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        spriteRenderer.color = originalColor;
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
            transform.localPosition = originalLocalPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        transform.localPosition = originalLocalPosition;
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