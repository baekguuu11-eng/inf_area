using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MachineWormWarningZone : MonoBehaviour
{
    [SerializeField] private float radius = 1.35f;
    [SerializeField] private float duration = 0.95f;
    [SerializeField] private Color startColor = new Color(1f, 0.05f, 0.05f, 0.18f);
    [SerializeField] private Color endColor = new Color(1f, 0.75f, 0.65f, 0.95f);
    [SerializeField] private int sortingOrder = 25;

    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private bool initialized;
    private Vector3 baseScale = Vector3.one;

    public float Duration { get { return duration; } }
    public float Radius { get { return radius; } }
    public bool IsFinished { get { return initialized && elapsed >= duration; } }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = RuntimePixelSpriteFactory.GetWarningZoneSprite();
        spriteRenderer.sortingOrder = sortingOrder;
    }

    public void Initialize(Vector3 worldPosition, float newRadius, float newDuration)
    {
        transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
        radius = Mathf.Max(0.15f, newRadius);
        duration = Mathf.Max(0.1f, newDuration);
        elapsed = 0f;
        initialized = true;

        float spriteWorldSize = spriteRenderer != null && spriteRenderer.sprite != null
            ? Mathf.Max(0.0001f, spriteRenderer.sprite.bounds.size.x)
            : 1f;
        float finalScale = radius * 2f / spriteWorldSize;
        baseScale = Vector3.one * finalScale;
        transform.localScale = baseScale;

        if (spriteRenderer != null)
            spriteRenderer.color = startColor;
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float pulse = 1f + Mathf.Sin(t * Mathf.PI * Mathf.Lerp(7f, 18f, t)) * Mathf.Lerp(0.03f, 0.11f, t);

        if (spriteRenderer != null)
        {
            Color color = Color.Lerp(startColor, endColor, t * t);
            if (t > 0.72f)
                color.a *= Mathf.PingPong(t * 13f, 1f) > 0.35f ? 1f : 0.35f;
            spriteRenderer.color = color;
        }

        transform.localScale = baseScale * pulse;

        if (elapsed >= duration)
            enabled = false;
    }
}
