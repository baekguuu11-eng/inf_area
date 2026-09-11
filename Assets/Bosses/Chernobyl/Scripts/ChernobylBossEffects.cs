using System.Collections.Generic;
using UnityEngine;

public static class ChernobylBossEffects
{
    private static readonly Stack<ChernobylDebris> debrisPool = new Stack<ChernobylDebris>(64);
    private const int MaxPooledDebris = 64;
    public static GameObject CreateCircleTelegraph(Vector3 position, float radius, float duration, Color color, int sortingOrder = 12)
    {
        GameObject root = new GameObject("CHN_CircleTelegraph");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer fill = root.AddComponent<SpriteRenderer>();
        fill.sprite = ChernobylRuntimeSprites.FilledCircle;
        fill.color = new Color(color.r, color.g, color.b, 0.12f);
        fill.sortingOrder = sortingOrder;
        root.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.001f, fill.sprite.bounds.size.x));

        GameObject border = new GameObject("Border");
        border.transform.SetParent(root.transform, false);
        SpriteRenderer ring = border.AddComponent<SpriteRenderer>();
        ring.sprite = ChernobylRuntimeSprites.Ring;
        ring.color = new Color(color.r, color.g, color.b, 0.90f);
        ring.sortingOrder = sortingOrder + 1;
        ChernobylTelegraphPulse pulse = root.AddComponent<ChernobylTelegraphPulse>();
        pulse.Initialize(duration, color);
        return root;
    }

    public static GameObject CreateRectTelegraph(Vector3 position, Vector2 size, float duration, Color color, int sortingOrder = 12, bool simple = false)
    {
        GameObject root = new GameObject("CHN_RectTelegraph");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer fill = root.AddComponent<SpriteRenderer>();
        fill.sprite = ChernobylRuntimeSprites.WhitePixel;
        fill.color = new Color(color.r, color.g, color.b, 0.11f);
        fill.sortingOrder = sortingOrder;
        root.transform.localScale = new Vector3(Mathf.Max(0.02f, size.x), Mathf.Max(0.02f, size.y), 1f);

        if (!simple)
        {
            float border = 0.045f;
            CreateBorder(root.transform, new Vector2(0f, size.y * 0.5f), new Vector2(size.x, border), color, sortingOrder + 1);
            CreateBorder(root.transform, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, border), color, sortingOrder + 1);
            CreateBorder(root.transform, new Vector2(size.x * 0.5f, 0f), new Vector2(border, size.y), color, sortingOrder + 1);
            CreateBorder(root.transform, new Vector2(-size.x * 0.5f, 0f), new Vector2(border, size.y), color, sortingOrder + 1);
        }
        ChernobylTelegraphPulse pulse = root.AddComponent<ChernobylTelegraphPulse>();
        pulse.Initialize(duration, color);
        return root;
    }


    public static GameObject CreateSafeRectIndicator(Vector3 position, Vector2 size, float duration)
    {
        Color safe = new Color(0.24f, 0.88f, 1f, 1f);
        GameObject root = new GameObject("CHN_SafeIndicator");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer fill = root.AddComponent<SpriteRenderer>();
        fill.sprite = ChernobylRuntimeSprites.WhitePixel;
        fill.color = new Color(safe.r, safe.g, safe.b, 0.10f);
        fill.sortingOrder = 10;
        root.transform.localScale = new Vector3(Mathf.Max(0.02f, size.x * 0.96f), Mathf.Max(0.02f, size.y * 0.96f), 1f);
        float border = 0.04f;
        CreateBorder(root.transform, new Vector2(0f, size.y * 0.48f), new Vector2(size.x * 0.96f, border), safe, 12);
        CreateBorder(root.transform, new Vector2(0f, -size.y * 0.48f), new Vector2(size.x * 0.96f, border), safe, 12);
        CreateBorder(root.transform, new Vector2(size.x * 0.48f, 0f), new Vector2(border, size.y * 0.96f), safe, 12);
        CreateBorder(root.transform, new Vector2(-size.x * 0.48f, 0f), new Vector2(border, size.y * 0.96f), safe, 12);
        ChernobylSafePulse pulse = root.AddComponent<ChernobylSafePulse>();
        pulse.Initialize(duration, safe);
        return root;
    }

    public static GameObject CreateRingTelegraph(Vector3 position, float innerRadius, float outerRadius, float duration, Color color)
    {
        GameObject root = new GameObject("CHN_RingTelegraph");
        root.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer outer = root.AddComponent<SpriteRenderer>();
        outer.sprite = ChernobylRuntimeSprites.Ring;
        outer.color = new Color(color.r, color.g, color.b, 0.92f);
        outer.sortingOrder = 14;
        float outerScale = outerRadius * 2f / Mathf.Max(0.001f, outer.sprite.bounds.size.x);
        root.transform.localScale = Vector3.one * outerScale;

        GameObject innerObject = new GameObject("InnerBoundary");
        innerObject.transform.SetParent(root.transform, false);
        SpriteRenderer inner = innerObject.AddComponent<SpriteRenderer>();
        inner.sprite = ChernobylRuntimeSprites.Ring;
        inner.color = new Color(0.24f, 0.88f, 1f, 0.88f);
        inner.sortingOrder = 15;
        float ratio = outerRadius > 0.001f ? Mathf.Clamp01(innerRadius / outerRadius) : 0.5f;
        innerObject.transform.localScale = Vector3.one * ratio;

        ChernobylRingTelegraphPulse pulse = root.AddComponent<ChernobylRingTelegraphPulse>();
        pulse.Initialize(duration, color, safeColor: new Color(0.24f, 0.88f, 1f, 1f));
        return root;
    }

    private static void CreateBorder(Transform parent, Vector2 localPosition, Vector2 worldSize, Color color, int order)
    {
        GameObject edge = new GameObject("Edge");
        edge.transform.SetParent(parent, false);
        // parent already scales to rect size, so compensate and place edge in normalized local space below.
        edge.transform.localPosition = new Vector3(
            parent.localScale.x > 0.0001f ? localPosition.x / parent.localScale.x : 0f,
            parent.localScale.y > 0.0001f ? localPosition.y / parent.localScale.y : 0f, 0f);
        edge.transform.localScale = new Vector3(
            parent.localScale.x > 0.0001f ? worldSize.x / parent.localScale.x : worldSize.x,
            parent.localScale.y > 0.0001f ? worldSize.y / parent.localScale.y : worldSize.y, 1f);
        SpriteRenderer renderer = edge.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.WhitePixel;
        renderer.color = new Color(color.r, color.g, color.b, 0.88f);
        renderer.sortingOrder = order;
    }

    public static void SpawnCircleBlast(Vector3 position, float radius, Color color)
    {
        GameObject root = new GameObject("CHN_CircleBlast");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.FilledCircle;
        renderer.color = new Color(1f, 1f, 1f, 0.92f);
        renderer.sortingOrder = 28;
        float target = radius * 2f / Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        root.transform.localScale = Vector3.one * target * 0.28f;
        ChernobylFadeBurst fade = root.AddComponent<ChernobylFadeBurst>();
        fade.Initialize(Vector3.one * target, 0.28f, Color.white, new Color(color.r, color.g, color.b, 0f));
        SpawnFragments(position, color, Mathf.RoundToInt(8 + radius * 5f), 2.7f);
    }

    public static void SpawnRectBlast(Vector3 position, Vector2 size, Color color, bool spawnFragments = true)
    {
        SpawnRectBlastObject(position, size, color, spawnFragments);
    }

    public static GameObject SpawnRectBlastObject(Vector3 position, Vector2 size, Color color, bool spawnFragments = true)
    {
        GameObject root = new GameObject("CHN_RectBlast");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.WhitePixel;
        renderer.color = new Color(1f, 1f, 1f, 0.90f);
        renderer.sortingOrder = 27;
        Vector3 target = new Vector3(size.x, size.y, 1f);
        root.transform.localScale = target * 0.45f;
        ChernobylFadeBurst fade = root.AddComponent<ChernobylFadeBurst>();
        fade.Initialize(target, 0.24f, Color.white, new Color(color.r, color.g, color.b, 0f));
        if (spawnFragments)
            SpawnFragments(position, color, Mathf.Clamp(Mathf.RoundToInt((size.x + size.y) * 0.85f), 4, 9), 2.4f);
        return root;
    }

    public static void SpawnRingBlast(Vector3 position, float innerRadius, float outerRadius, Color color)
    {
        GameObject root = new GameObject("CHN_RingBlast");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.Ring;
        renderer.color = Color.white;
        renderer.sortingOrder = 29;
        float target = outerRadius * 2f / Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        root.transform.localScale = Vector3.one * target * 0.72f;
        ChernobylFadeBurst fade = root.AddComponent<ChernobylFadeBurst>();
        fade.Initialize(Vector3.one * target, 0.26f, Color.white, new Color(color.r, color.g, color.b, 0f));
        SpawnFragments(position, color, 10, 2.7f);
    }

    public static void SpawnCoreSparks(Vector3 position, Color color, int count, float strength = 2.8f)
    {
        SpawnFragments(position, color, count, strength);
    }

    public static void SpawnDeathBurst(Vector3 position)
    {
        SpawnFragments(position, new Color(0.62f, 1f, 0.28f, 1f), 28, 4.2f);
        SpawnCircleBlast(position, 1.25f, new Color(0.62f, 1f, 0.28f, 1f));
    }

    private static void SpawnFragments(Vector3 position, Color color, int count, float strength)
    {
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            ChernobylDebris debris = AcquireDebris();
            if (debris == null) continue;
            GameObject chip = debris.gameObject;
            chip.name = "CHN_WhiteFragment";
            chip.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.16f);
            chip.transform.rotation = Quaternion.identity;
            chip.transform.localScale = new Vector3(Random.Range(0.035f, 0.085f), Random.Range(0.05f, 0.14f), 1f);

            SpriteRenderer renderer = debris.Renderer;
            if (renderer != null)
            {
                renderer.sprite = Random.value > 0.35f ? ChernobylRuntimeSprites.WhitePixel : ChernobylRuntimeSprites.Diamond;
                renderer.color = Color.Lerp(Color.white, color, Random.Range(0.10f, 0.62f));
                renderer.sortingOrder = Random.Range(26, 34);
            }

            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
            debris.Initialize(direction * Random.Range(strength * 0.45f, strength), Random.Range(0.20f, 0.46f),
                Random.Range(220f, 580f));
        }
    }

    private static ChernobylDebris AcquireDebris()
    {
        while (debrisPool.Count > 0)
        {
            ChernobylDebris pooled = debrisPool.Pop();
            if (pooled == null) continue;
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        GameObject chip = new GameObject("CHN_WhiteFragment");
        SpriteRenderer renderer = chip.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.WhitePixel;
        ChernobylDebris debris = chip.AddComponent<ChernobylDebris>();
        debris.BindRenderer(renderer);
        return debris;
    }

    public static void ReleaseDebris(ChernobylDebris debris)
    {
        if (debris == null) return;
        if (debrisPool.Count >= MaxPooledDebris)
        {
            Object.Destroy(debris.gameObject);
            return;
        }
        debris.gameObject.SetActive(false);
        debrisPool.Push(debris);
    }
}

public sealed class ChernobylTelegraphPulse : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color baseColor;
    private float duration;
    private float elapsed;

    public void Initialize(float life, Color color)
    {
        duration = Mathf.Max(0.05f, life);
        baseColor = color;
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // V10: 예고 -> 충전 -> 폭발 직전의 3단계 경고.
        // 단순히 같은 색으로 깜빡이지 않고 녹색 위험색이 황색/백색 과열로 올라가게 한다.
        float phasePulseSpeed = Mathf.Lerp(4.5f, 13.5f, t);
        float pulse = Mathf.Sin(Time.time * phasePulseSpeed) * 0.5f + 0.5f;
        Color warm = new Color(1f, 0.92f, 0.22f, 1f);
        Color hot = new Color(1f, 1f, 0.92f, 1f);
        Color stageColor = t < 0.56f
            ? Color.Lerp(baseColor, warm, Mathf.InverseLerp(0.28f, 0.56f, t) * 0.38f)
            : Color.Lerp(warm, hot, Mathf.InverseLerp(0.56f, 1f, t));

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = renderers[i].color;
            float baseAlpha = i == 0 ? Mathf.Lerp(0.09f, 0.30f, t) : Mathf.Lerp(0.66f, 1f, t);
            c.r = stageColor.r; c.g = stageColor.g; c.b = stageColor.b;
            c.a = Mathf.Clamp01(baseAlpha * Mathf.Lerp(0.78f, 1f, pulse));
            renderers[i].color = c;
        }
    }
}

public sealed class ChernobylSafePulse : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color safeColor;
    private float duration;
    private float elapsed;

    public void Initialize(float life, Color color)
    {
        duration = Mathf.Max(0.05f, life);
        safeColor = color;
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float pulse = Mathf.Sin(Time.time * Mathf.Lerp(3.5f, 7.0f, t)) * 0.5f + 0.5f;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer r = renderers[i];
            if (r == null) continue;
            Color c = safeColor;
            c.a = i == 0 ? Mathf.Lerp(0.08f, 0.18f, pulse) : Mathf.Lerp(0.72f, 0.98f, pulse);
            r.color = c;
        }
    }
}

public sealed class ChernobylRingTelegraphPulse : MonoBehaviour
{
    private SpriteRenderer outer;
    private SpriteRenderer inner;
    private Color hazardColor;
    private Color safeColor;
    private float duration;
    private float elapsed;

    public void Initialize(float life, Color hazard, Color safeColor)
    {
        duration = Mathf.Max(0.05f, life);
        hazardColor = hazard;
        this.safeColor = safeColor;
        outer = GetComponent<SpriteRenderer>();
        Transform child = transform.Find("InnerBoundary");
        inner = child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float hazardPulse = Mathf.Sin(Time.time * Mathf.Lerp(4.5f, 13.5f, t)) * 0.5f + 0.5f;
        Color warm = new Color(1f, 0.92f, 0.22f, 1f);
        Color hot = new Color(1f, 1f, 0.92f, 1f);
        Color h = t < 0.56f
            ? Color.Lerp(hazardColor, warm, Mathf.InverseLerp(0.28f, 0.56f, t) * 0.38f)
            : Color.Lerp(warm, hot, Mathf.InverseLerp(0.56f, 1f, t));
        if (outer != null)
        {
            h.a = Mathf.Lerp(0.72f, 1f, hazardPulse);
            outer.color = h;
        }
        if (inner != null)
        {
            Color s = safeColor;
            s.a = Mathf.Lerp(0.68f, 0.98f, Mathf.Sin(Time.time * 5f) * 0.5f + 0.5f);
            inner.color = s;
        }
    }
}

public sealed class ChernobylFadeBurst : MonoBehaviour
{
    private SpriteRenderer renderer;
    private Vector3 startScale;
    private Vector3 targetScale;
    private float duration;
    private float elapsed;
    private Color startColor;
    private Color endColor;

    public void Initialize(Vector3 target, float life, Color from, Color to)
    {
        renderer = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
        targetScale = target;
        duration = Mathf.Max(0.05f, life);
        startColor = from;
        endColor = to;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float e = 1f - Mathf.Pow(1f - t, 3f);
        transform.localScale = Vector3.Lerp(startScale, targetScale, e);
        if (renderer != null) renderer.color = Color.Lerp(startColor, endColor, t);
        if (t >= 1f) Destroy(gameObject);
    }
}

public sealed class ChernobylDebris : MonoBehaviour
{
    private Vector2 velocity;
    private float duration;
    private float elapsed;
    private float spin;
    private SpriteRenderer renderer;
    private Color start;

    public SpriteRenderer Renderer => renderer;

    public void BindRenderer(SpriteRenderer target)
    {
        renderer = target;
    }

    public void Initialize(Vector2 initialVelocity, float life, float angularVelocity)
    {
        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        velocity = initialVelocity;
        duration = Mathf.Max(0.08f, life);
        elapsed = 0f;
        spin = Random.value > 0.5f ? angularVelocity : -angularVelocity;
        start = renderer != null ? renderer.color : Color.white;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 2.8f);
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        float t = Mathf.Clamp01(elapsed / duration);
        if (renderer != null)
        {
            Color c = start;
            c.a = 1f - t * t;
            renderer.color = c;
        }
        if (t >= 1f)
            ChernobylBossEffects.ReleaseDebris(this);
    }
}

public sealed class ChernobylShockwave : MonoBehaviour
{
    private SpriteRenderer renderer;
    private PlayerHealth player;
    private float startRadius;
    private float endRadius;
    private float thickness;
    private float duration;
    private float elapsed;
    private int damage;
    private bool hit;
    private float previousRadius;
    private Color color;

    public static ChernobylShockwave Create(Vector3 position, float startRadius, float endRadius, float thickness,
        float duration, int damage, Color color)
    {
        GameObject root = new GameObject("CHN_CoreShockwave");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.Ring;
        renderer.color = new Color(color.r, color.g, color.b, 0.88f);
        renderer.sortingOrder = 25;
        ChernobylShockwave wave = root.AddComponent<ChernobylShockwave>();
        wave.renderer = renderer;
        wave.player = Object.FindAnyObjectByType<PlayerHealth>();
        wave.startRadius = Mathf.Max(0.05f, startRadius);
        wave.endRadius = Mathf.Max(wave.startRadius + 0.05f, endRadius);
        wave.thickness = Mathf.Max(0.05f, thickness);
        wave.duration = Mathf.Max(0.08f, duration);
        wave.damage = Mathf.Max(0, damage);
        wave.color = color;
        wave.previousRadius = wave.startRadius;
        wave.ApplyScale(wave.startRadius);
        return wave;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float radius = Mathf.Lerp(startRadius, endRadius, t);
        ApplyScale(radius);
        if (!hit && player != null && !player.IsDead)
        {
            float ringMin = Mathf.Max(0f, Mathf.Min(previousRadius, radius) - thickness);
            float ringMax = Mathf.Max(previousRadius, radius) + thickness;
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
            Vector2 center = transform.position;
            for (int i = 0; i < colliders.Length && !hit; i++)
            {
                Collider2D c = colliders[i];
                if (c == null || !c.enabled || c.isTrigger) continue;

                // Use the player's actual collider extent instead of only transform.position.
                Vector2 closest = c.ClosestPoint(center);
                float minDistance = Vector2.Distance(center, closest);
                Bounds cb = c.bounds;
                Vector2[] corners =
                {
                    new Vector2(cb.min.x, cb.min.y), new Vector2(cb.min.x, cb.max.y),
                    new Vector2(cb.max.x, cb.min.y), new Vector2(cb.max.x, cb.max.y)
                };
                float maxDistance = minDistance;
                for (int corner = 0; corner < corners.Length; corner++)
                    maxDistance = Mathf.Max(maxDistance, Vector2.Distance(center, corners[corner]));

                if (ringMax >= minDistance && ringMin <= maxDistance)
                {
                    if (damage > 0) player.TakeDamage(damage);
                    hit = true;
                }
            }
        }
        previousRadius = radius;
        if (renderer != null)
        {
            Color c = color;
            c.a = Mathf.Lerp(0.90f, 0f, t * t);
            renderer.color = c;
        }
        if (t >= 1f) Destroy(gameObject);
    }

    private void ApplyScale(float radius)
    {
        if (renderer == null || renderer.sprite == null) return;
        float size = Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        transform.localScale = Vector3.one * (radius * 2f / size);
    }
}
