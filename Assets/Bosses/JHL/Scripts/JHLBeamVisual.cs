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
        // Damage remains live until controller cleanup: never visually shrink a live hitbox.
        Vector3 scale = transform.localScale;
        scale.y = baseWidth;
        transform.localScale = scale;
        float pulse = 0.94f + Mathf.Sin(elapsed * 45f) * 0.06f;
        SetAlpha(outer, 0.55f);
        SetAlpha(middle, 0.80f * pulse);
        SetAlpha(core, pulse);
    }

    private static void SetAlpha(SpriteRenderer rendererRef, float alpha)
    {
        if (rendererRef == null) return;
        Color c = rendererRef.color;
        c.a = Mathf.Clamp01(alpha);
        rendererRef.color = c;
    }
}
