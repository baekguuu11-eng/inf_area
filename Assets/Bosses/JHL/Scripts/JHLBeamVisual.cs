using UnityEngine;

[DisallowMultipleComponent]
public sealed class JHLBeamVisual : MonoBehaviour
{
    private SpriteRenderer outer;
    private SpriteRenderer middle;
    private SpriteRenderer core;
    private float baseWidth;
    private float life;
    private float elapsed;
    private bool active;

    public void Initialize(SpriteRenderer outerRenderer, SpriteRenderer middleRenderer, SpriteRenderer coreRenderer, float width, float lifetime)
    {
        outer = outerRenderer;
        middle = middleRenderer;
        core = coreRenderer;
        baseWidth = Mathf.Max(0.02f, width);
        life = Mathf.Max(0.05f, lifetime);
        elapsed = 0f;
        active = true;
    }

    public void SetGeometry(Vector2 origin, Vector2 direction, float length, float width)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        Vector2 center = origin + dir * (length * 0.5f);
        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        baseWidth = Mathf.Max(0.02f, width);
        transform.localScale = new Vector3(length, baseWidth, 1f);
        if (middle != null) middle.transform.localScale = new Vector3(1f, 0.56f, 1f);
        if (core != null) core.transform.localScale = new Vector3(1f, 0.23f, 1f);
    }

    private void Update()
    {
        if (!active) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / life);
        float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.075f));
        float sustain = 1f;
        float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.76f) / 0.24f));
        float envelope = Mathf.Clamp01(attack * sustain * release);
        float micro = 0.97f + Mathf.Sin(elapsed * 57f) * 0.018f + Mathf.Sin(elapsed * 101f) * 0.012f;
        Vector3 scale = transform.localScale;
        scale.y = baseWidth * Mathf.Max(0.06f, envelope * micro);
        transform.localScale = scale;

        SetAlpha(outer, 0.46f * envelope);
        SetAlpha(middle, 0.78f * envelope);
        SetAlpha(core, 1.00f * envelope);
    }

    private static void SetAlpha(SpriteRenderer rendererRef, float alpha)
    {
        if (rendererRef == null) return;
        Color c = rendererRef.color;
        c.a = Mathf.Clamp01(alpha);
        rendererRef.color = c;
    }
}
