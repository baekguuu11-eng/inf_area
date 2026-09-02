using UnityEngine;

public static class ChernobylBossEffects
{
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

    public static GameObject CreateRectTelegraph(Vector3 position, Vector2 size, float duration, Color color, int sortingOrder = 12)
    {
        GameObject root = new GameObject("CHN_RectTelegraph");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer fill = root.AddComponent<SpriteRenderer>();
        fill.sprite = ChernobylRuntimeSprites.WhitePixel;
        fill.color = new Color(color.r, color.g, color.b, 0.11f);
        fill.sortingOrder = sortingOrder;
        root.transform.localScale = new Vector3(Mathf.Max(0.02f, size.x), Mathf.Max(0.02f, size.y), 1f);

        float border = 0.045f;
        CreateBorder(root.transform, new Vector2(0f, size.y * 0.5f), new Vector2(size.x, border), color, sortingOrder + 1);
        CreateBorder(root.transform, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, border), color, sortingOrder + 1);
        CreateBorder(root.transform, new Vector2(size.x * 0.5f, 0f), new Vector2(border, size.y), color, sortingOrder + 1);
        CreateBorder(root.transform, new Vector2(-size.x * 0.5f, 0f), new Vector2(border, size.y), color, sortingOrder + 1);
        ChernobylTelegraphPulse pulse = root.AddComponent<ChernobylTelegraphPulse>();
        pulse.Initialize(duration, color);
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

    public static void SpawnRectBlast(Vector3 position, Vector2 size, Color color)
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
        SpawnFragments(position, color, Mathf.Clamp(Mathf.RoundToInt((size.x + size.y) * 1.4f), 7, 18), 2.4f);
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
            GameObject chip = new GameObject("CHN_WhiteFragment");
            chip.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.16f);
            SpriteRenderer renderer = chip.AddComponent<SpriteRenderer>();
            renderer.sprite = Random.value > 0.35f ? ChernobylRuntimeSprites.WhitePixel : ChernobylRuntimeSprites.Diamond;
            renderer.color = Color.Lerp(Color.white, color, Random.Range(0.10f, 0.62f));
            renderer.sortingOrder = Random.Range(26, 34);
            chip.transform.localScale = new Vector3(Random.Range(0.035f, 0.085f), Random.Range(0.05f, 0.14f), 1f);
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
            ChernobylDebris debris = chip.AddComponent<ChernobylDebris>();
            debris.Initialize(direction * Random.Range(strength * 0.45f, strength), Random.Range(0.20f, 0.46f),
                Random.Range(220f, 580f));
        }
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

    public void Initialize(Vector2 initialVelocity, float life, float angularVelocity)
    {
        velocity = initialVelocity;
        duration = Mathf.Max(0.08f, life);
        spin = Random.value > 0.5f ? angularVelocity : -angularVelocity;
        renderer = GetComponent<SpriteRenderer>();
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
        if (t >= 1f) Destroy(gameObject);
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
        wave.damage = Mathf.Max(1, damage);
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
            float d = Vector2.Distance(transform.position, player.transform.position);
            float min = Mathf.Min(previousRadius, radius) - thickness;
            float max = Mathf.Max(previousRadius, radius) + thickness;
            if (d >= min && d <= max)
            {
                player.TakeDamage(damage);
                hit = true;
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
