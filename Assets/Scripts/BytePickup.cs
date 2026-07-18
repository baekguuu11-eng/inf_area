using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BytePickup : MonoBehaviour
{
    [Header("Value")]
    [SerializeField] private int value = 1;

    [Header("Spawn Motion")]
    [SerializeField] private float scatterRadius = 1.4f;
    [SerializeField] private float scatterDuration = 0.35f;
    [SerializeField] private float arcHeight = 0.45f;

    [Header("Wait Before Flying")]
    [SerializeField] private float minWaitTime = 2f;
    [SerializeField] private float maxWaitTime = 3f;

    [Header("Fly To UI")]
    [SerializeField] private float flyDuration = 0.55f;
    [SerializeField] private float spinSpeed = 540f;
    [SerializeField] private float endScale = 0.25f;

    private ByteCurrencyManager currencyManager;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private bool isInitialized;

    private static Sprite fallbackSprite;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = GetFallbackSprite();
        }
    }

    public void Initialize(int byteValue, ByteCurrencyManager manager)
    {
        Initialize(byteValue, manager, scatterRadius);
    }

    public void Initialize(int byteValue, ByteCurrencyManager manager, float newScatterRadius)
    {
        if (isInitialized)
        {
            return;
        }

        value = Mathf.Max(1, byteValue);
        currencyManager = manager != null ? manager : ByteCurrencyManager.Instance;
        scatterRadius = Mathf.Max(0.1f, newScatterRadius);

        isInitialized = true;

        StopAllCoroutines();
        StartCoroutine(PickupRoutine());
    }

    private IEnumerator PickupRoutine()
    {
        Vector3 startPosition = transform.position;

        Vector2 randomDirection = Random.insideUnitCircle.normalized;

        if (randomDirection == Vector2.zero)
        {
            randomDirection = Vector2.up;
        }

        float randomDistance = Random.Range(scatterRadius * 0.55f, scatterRadius);
        Vector3 landPosition = startPosition + (Vector3)(randomDirection * randomDistance);
        landPosition.z = 0f;

        float elapsed = 0f;

        while (elapsed < scatterDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / scatterDuration);
            float eased = EaseOutQuad(t);

            Vector3 position = Vector3.Lerp(startPosition, landPosition, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = position;
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            yield return null;
        }

        transform.position = landPosition;

        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitTime);

        if (currencyManager == null)
        {
            currencyManager = ByteCurrencyManager.Instance;
        }

        if (currencyManager == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 flyStart = transform.position;
        Vector3 flyEnd = currencyManager.GetByteIconWorldPosition();

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * endScale;

        elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / flyDuration);
            float eased = EaseInCubic(t);

            transform.position = Vector3.Lerp(flyStart, flyEnd, eased);
            transform.localScale = Vector3.Lerp(startScale, targetScale, eased);
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = Mathf.Lerp(1f, 0.75f, eased);
                spriteRenderer.color = color;
            }

            yield return null;
        }

        currencyManager.AddBytes(value);
        Destroy(gameObject);
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        int size = 16;
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color main = new Color(0.15f, 0.95f, 0.85f, 1f);
        Color edge = new Color(0.04f, 0.35f, 0.33f, 1f);

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);

                if (distance <= radius)
                {
                    texture.SetPixel(x, y, distance > radius - 2f ? edge : main);
                }
                else
                {
                    texture.SetPixel(x, y, clear);
                }
            }
        }

        texture.Apply();

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            16f
        );

        return fallbackSprite;
    }
}