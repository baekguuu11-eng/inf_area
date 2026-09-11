using UnityEngine;

[DisallowMultipleComponent]
public sealed class JHLCombatEffects : MonoBehaviour
{
    public static JHLCombatEffects Create(GameObject owner)
    {
        if (owner == null) return null;
        JHLCombatEffects fx = owner.GetComponent<JHLCombatEffects>();
        if (fx == null) fx = owner.AddComponent<JHLCombatEffects>();
        return fx;
    }

    // Compatibility API: V4 no longer creates persistent hand trails by default,
    // but the boss controller still toggles optional TrailRenderer references.
    // Keeping this method makes those calls safe (null trails are simply ignored).
    public void SetTrail(TrailRenderer trail, bool enabledState)
    {
        if (trail == null) return;
        if (enabledState) trail.Clear();
        trail.emitting = enabledState;
    }

    public GameObject SpawnImpact(Vector2 position, float radius, Color color, Transform parent = null)
    {
        GameObject root = new GameObject("JHL_ImpactFX");
        if (parent != null) root.transform.SetParent(parent, true);
        root.transform.position = position;

        CreateRing(root.transform, "RingA", radius * 0.82f, color, 42, 0f);
        Color secondary = Color.Lerp(color, Color.white, 0.55f);
        secondary.a *= 0.82f;
        CreateRing(root.transform, "RingB", radius * 0.62f, secondary, 43, 0.045f);

        int debrisCount = 10;
        for (int i = 0; i < debrisCount; i++)
        {
            float angle = (360f / debrisCount) * i + Random.Range(-9f, 9f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject debris = new GameObject("PixelDebris");
            debris.transform.SetParent(root.transform, false);
            SpriteRenderer sr = debris.AddComponent<SpriteRenderer>();
            sr.sprite = JHLRuntimeSprites.WhitePixel;
            sr.color = Color.Lerp(color, Color.white, Random.Range(0.25f, 0.80f));
            sr.sortingOrder = 44;
            float size = Random.Range(0.06f, 0.14f);
            debris.transform.localScale = new Vector3(size, size, 1f);
            JHLPixelDebris motion = debris.AddComponent<JHLPixelDebris>();
            motion.Initialize(dir * Random.Range(radius * 2.0f, radius * 3.8f), Random.Range(0.18f, 0.32f));
        }

        Destroy(root, 0.42f);
        return root;
    }

    public GameObject SpawnCharge(Transform origin, float duration, float targetSize, Color color)
    {
        if (origin == null) return null;
        GameObject root = new GameObject("JHL_ChargeCore");
        root.transform.SetParent(origin, false);
        root.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = JHLRuntimeSprites.FilledCircle;
        sr.color = color;
        sr.sortingOrder = 41;
        JHLChargePulse pulse = root.AddComponent<JHLChargePulse>();
        pulse.Initialize(sr, duration, targetSize);
        return root;
    }

    private static void CreateRing(Transform parent, string name, float radius, Color color, int order, float delay)
    {
        GameObject ring = new GameObject(name);
        ring.transform.SetParent(parent, false);
        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = JHLRuntimeSprites.Ring;
        sr.color = color;
        sr.sortingOrder = order;
        JHLImpactRing pulse = ring.AddComponent<JHLImpactRing>();
        pulse.Initialize(sr, radius, delay);
    }
}

[DisallowMultipleComponent]
internal sealed class JHLImpactRing : MonoBehaviour
{
    private SpriteRenderer rendererRef;
    private float radius;
    private float delay;
    private float elapsed;
    private const float Life = 0.24f;

    public void Initialize(SpriteRenderer sr, float targetRadius, float startDelay)
    {
        rendererRef = sr;
        radius = Mathf.Max(0.2f, targetRadius);
        delay = Mathf.Max(0f, startDelay);
        transform.localScale = Vector3.one * 0.12f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < delay)
        {
            if (rendererRef != null)
            {
                Color hidden = rendererRef.color;
                hidden.a = 0f;
                rendererRef.color = hidden;
            }
            return;
        }
        float t = Mathf.Clamp01((elapsed - delay) / Life);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.18f, radius * 1.95f, eased);
        if (rendererRef != null)
        {
            Color c = rendererRef.color;
            c.a = Mathf.Lerp(0.90f, 0f, t);
            rendererRef.color = c;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
internal sealed class JHLPixelDebris : MonoBehaviour
{
    private Vector2 velocity;
    private float life;
    private float elapsed;
    private SpriteRenderer rendererRef;

    public void Initialize(Vector2 initialVelocity, float lifetime)
    {
        velocity = initialVelocity;
        life = Mathf.Max(0.08f, lifetime);
        rendererRef = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, 12f * Time.deltaTime);
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, velocity.x * 55f * Time.deltaTime);
        float t = Mathf.Clamp01(elapsed / life);
        if (rendererRef != null)
        {
            Color c = rendererRef.color;
            c.a = 1f - t;
            rendererRef.color = c;
        }
        if (elapsed >= life) Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
internal sealed class JHLChargePulse : MonoBehaviour
{
    private SpriteRenderer rendererRef;
    private float duration;
    private float targetSize;
    private float elapsed;

    public void Initialize(SpriteRenderer sr, float lifetime, float size)
    {
        rendererRef = sr;
        duration = Mathf.Max(0.05f, lifetime);
        targetSize = Mathf.Max(0.08f, size);
        transform.localScale = Vector3.one * 0.05f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float pulse = 0.92f + Mathf.Sin(elapsed * Mathf.Lerp(9f, 28f, t)) * 0.08f;
        float size = Mathf.Lerp(0.05f, targetSize, Mathf.Pow(t, 0.72f)) * pulse;
        transform.localScale = Vector3.one * size;
        if (rendererRef != null)
        {
            Color c = rendererRef.color;
            c.a = Mathf.Lerp(0.45f, 1f, t);
            rendererRef.color = c;
        }
        if (elapsed >= duration) Destroy(gameObject);
    }
}
