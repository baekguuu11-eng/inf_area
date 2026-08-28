using System.Collections.Generic;
using UnityEngine;

public static class WallImpactSystem
{
    private struct Record
    {
        public Vector2 point;
        public float time;
    }

    private const int MaximumPooledShards = 96;
    private const int MaximumPooledDecals = 40;

    private static readonly Dictionary<Collider2D, Record> recent = new Dictionary<Collider2D, Record>();
    private static readonly Stack<WallImpactParticle> shardPool = new Stack<WallImpactParticle>();
    private static readonly Stack<WallImpactDecal> decalPool = new Stack<WallImpactDecal>();
    private static readonly List<WallImpactDecal> liveDecals = new List<WallImpactDecal>();

    private static Sprite shardSprite;
    private static Sprite fallbackCrackSprite;
    private static Transform poolRoot;

    public static void Emit(Vector2 point, Vector2 normal, Vector2 incomingDirection, float damage,
        WeaponDefinition weapon, Collider2D wall)
    {
        if (normal.sqrMagnitude < 0.001f)
            normal = incomingDirection.sqrMagnitude > 0.001f ? -incomingDirection.normalized : Vector2.up;

        normal.Normalize();
        Vector2 incoming = incomingDirection.sqrMagnitude > 0.001f ? incomingDirection.normalized : -normal;
        float incidence = Mathf.Clamp01(Vector2.Dot(-incoming, normal));
        float power = Mathf.Clamp01(damage / 50f * 0.65f + incidence * 0.5f);

        bool merged = false;
        if (wall != null)
        {
            Record record;
            if (recent.TryGetValue(wall, out record))
                merged = Time.unscaledTime - record.time < 0.055f && Vector2.Distance(record.point, point) < 0.22f;

            recent[wall] = new Record { point = point, time = Time.unscaledTime };
        }

        PruneRecentRecords();

        string id = weapon != null ? weapon.weaponId : string.Empty;
        int minimum = 4;
        int maximum = 7;
        if (id == "machine_gun")
        {
            minimum = 2;
            maximum = 4;
        }
        else if (id == "shotgun")
        {
            minimum = 7;
            maximum = 12;
        }
        else if (id == "laser_gun")
        {
            minimum = 8;
            maximum = 13;
        }

        int count = Mathf.RoundToInt(Mathf.Lerp(minimum, maximum, power));
        if (merged) count = Mathf.Max(1, count / 2);

        Vector2 reflected = Vector2.Reflect(incoming, normal);
        if (reflected.sqrMagnitude < 0.001f) reflected = normal;
        reflected.Normalize();

        float grazing = 1f - incidence;
        float spread = Mathf.Lerp(76f, 30f, grazing);
        Color accent = weapon != null ? weapon.accentColor : new Color(0.3f, 0.95f, 1f, 1f);
        Transform parent = ResolveImpactParent(wall);

        for (int i = 0; i < count; i++)
        {
            float baseAngle = Mathf.Atan2(reflected.y, reflected.x) * Mathf.Rad2Deg;
            float angle = baseAngle + Random.Range(-spread * 0.5f, spread * 0.5f);
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            WallImpactParticle particle = AcquireShard(parent);
            particle.transform.position = point + normal * 0.015f + Random.insideUnitCircle * 0.012f;
            particle.transform.rotation = Quaternion.Euler(0f, 0f, angle + Random.Range(-25f, 25f));

            SpriteRenderer renderer = particle.Renderer;
            renderer.sprite = GetShardSprite();
            V620SpriteMaterialUtility.Apply(renderer);
            renderer.color = new Color(
                Mathf.Lerp(accent.r, 1f, 0.45f),
                Mathf.Lerp(accent.g, 1f, 0.45f),
                Mathf.Lerp(accent.b, 1f, 0.45f),
                1f);
            renderer.sortingOrder = 90;

            // 기존 코드는 4x2px Sprite에 0.025 Scale을 다시 곱해 사실상 보이지 않았다.
            // 여기서는 목표 월드 길이를 Sprite bounds로 나누어 실제 표시 크기를 보장한다.
            float desiredWorldLength = Random.Range(0.028f, 0.060f) * Mathf.Lerp(0.8f, 1.35f, power) * 1.25f;
            float boundsLength = Mathf.Max(0.0001f, renderer.sprite.bounds.size.x);
            particle.transform.localScale = Vector3.one * (desiredWorldLength / boundsLength);

            particle.Configure(
                Random.Range(0.18f, 0.34f),
                direction * Random.Range(2.4f, 5.2f) * Mathf.Lerp(0.8f, 1.35f, power),
                Random.Range(-540f, 540f));
        }

        if (!merged)
            SpawnCrack(point, normal, power, accent, parent);
    }

    internal static void ReturnParticle(WallImpactParticle particle)
    {
        if (particle == null) return;

        particle.gameObject.SetActive(false);
        particle.transform.SetParent(GetPoolRoot(), false);
        if (shardPool.Count < MaximumPooledShards)
            shardPool.Push(particle);
        else
            Object.Destroy(particle.gameObject);
    }

    internal static void ReturnDecal(WallImpactDecal decal)
    {
        if (decal == null) return;

        liveDecals.Remove(decal);
        decal.gameObject.SetActive(false);
        decal.transform.SetParent(GetPoolRoot(), false);
        if (decalPool.Count < MaximumPooledDecals)
            decalPool.Push(decal);
        else
            Object.Destroy(decal.gameObject);
    }

    private static WallImpactParticle AcquireShard(Transform parent)
    {
        WallImpactParticle particle = null;
        while (shardPool.Count > 0 && particle == null)
            particle = shardPool.Pop();

        if (particle == null)
        {
            GameObject gameObject = new GameObject("WallImpactShard");
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            particle = gameObject.AddComponent<WallImpactParticle>();
            particle.Initialize(renderer);
        }

        particle.transform.SetParent(parent, true);
        particle.gameObject.SetActive(true);
        return particle;
    }

    private static WallImpactDecal AcquireDecal(Transform parent)
    {
        WallImpactDecal decal = null;
        while (decalPool.Count > 0 && decal == null)
            decal = decalPool.Pop();

        if (decal == null)
        {
            GameObject gameObject = new GameObject("WallImpactCrack");
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            decal = gameObject.AddComponent<WallImpactDecal>();
            decal.Initialize(renderer);
        }

        decal.transform.SetParent(parent, true);
        decal.gameObject.SetActive(true);
        return decal;
    }

    private static void SpawnCrack(Vector2 point, Vector2 normal, float power, Color accent, Transform parent)
    {
        for (int i = liveDecals.Count - 1; i >= 0; i--)
        {
            if (liveDecals[i] == null || !liveDecals[i].gameObject.activeSelf)
                liveDecals.RemoveAt(i);
        }

        if (liveDecals.Count >= MaximumPooledDecals)
        {
            WallImpactDecal oldest = liveDecals[0];
            liveDecals.RemoveAt(0);
            if (oldest != null && oldest.gameObject.activeSelf)
                oldest.ReleaseImmediately();
        }

        WallImpactDecal decal = AcquireDecal(parent);
        liveDecals.Add(decal);

        SpriteRenderer renderer = decal.Renderer;
        renderer.sprite = GetCrackSprite();
        V620SpriteMaterialUtility.Apply(renderer);
        renderer.sortingOrder = 89;
        Color crackColor = Color.Lerp(new Color(0.08f, 0.14f, 0.17f, 0.9f), accent, 0.32f);
        crackColor.a = Mathf.Lerp(0.52f, 0.86f, power);
        renderer.color = crackColor;

        float normalAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
        decal.transform.position = point + normal * 0.006f;
        decal.transform.rotation = Quaternion.Euler(0f, 0f, normalAngle - 90f + Random.Range(-25f, 25f));

        float desiredSize = Mathf.Lerp(0.12f, 0.23f, power) * Random.Range(0.86f, 1.12f) * 1.25f;
        float boundsSize = Mathf.Max(0.0001f, Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y));
        decal.transform.localScale = Vector3.one * (desiredSize / boundsSize);
        decal.Configure(3.4f, 1.15f);
    }

    private static Transform ResolveImpactParent(Collider2D wall)
    {
        if (wall == null) return null;
        RoomController room = wall.GetComponentInParent<RoomController>();
        return room != null ? room.transform : null;
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null) return poolRoot;

        GameObject root = new GameObject("__IA_WallImpactPool");
        root.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(root);
        poolRoot = root.transform;
        return poolRoot;
    }

    private static Sprite GetShardSprite()
    {
        if (shardSprite != null) return shardSprite;

        Texture2D texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
        texture.name = "IA_V60_WallShardTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new[]
        {
            Color.clear, Color.white, Color.white, Color.clear,
            Color.white, Color.white, Color.clear, Color.clear
        });
        texture.Apply(false, true);
        shardSprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0.5f), 32f);
        shardSprite.name = "IA_V60_WallShardSprite";
        return shardSprite;
    }

    private static Sprite GetCrackSprite()
    {
        Sprite loaded = Resources.Load<Sprite>("WeaponVisuals/wall_crack");
        if (loaded != null) return loaded;
        if (fallbackCrackSprite != null) return fallbackCrackSprite;

        const int size = 9;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "IA_V60_FallbackWallCrackTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        SetPixel(pixels, size, 4, 4);
        SetPixel(pixels, size, 3, 4);
        SetPixel(pixels, size, 2, 5);
        SetPixel(pixels, size, 1, 6);
        SetPixel(pixels, size, 5, 4);
        SetPixel(pixels, size, 6, 3);
        SetPixel(pixels, size, 7, 2);
        SetPixel(pixels, size, 4, 3);
        SetPixel(pixels, size, 4, 2);
        SetPixel(pixels, size, 3, 1);
        SetPixel(pixels, size, 4, 5);
        SetPixel(pixels, size, 5, 6);
        SetPixel(pixels, size, 5, 7);
        texture.SetPixels(pixels);
        texture.Apply(false, true);

        fallbackCrackSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        fallbackCrackSprite.name = "IA_V60_FallbackWallCrackSprite";
        return fallbackCrackSprite;
    }

    private static void SetPixel(Color[] pixels, int width, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= width) return;
        pixels[y * width + x] = Color.white;
    }

    private static void PruneRecentRecords()
    {
        if (recent.Count < 64) return;

        List<Collider2D> remove = null;
        foreach (KeyValuePair<Collider2D, Record> pair in recent)
        {
            if (pair.Key == null || Time.unscaledTime - pair.Value.time > 2f)
            {
                if (remove == null) remove = new List<Collider2D>();
                remove.Add(pair.Key);
            }
        }

        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++)
            recent.Remove(remove[i]);
    }
}

public sealed class WallImpactParticle : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;
    private float spin;
    private float life;
    private float elapsed;
    private Color startColor;

    public SpriteRenderer Renderer => spriteRenderer;

    public void Initialize(SpriteRenderer renderer)
    {
        spriteRenderer = renderer;
    }

    public void Configure(float lifetime, Vector2 initialVelocity, float angularVelocity)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        velocity = initialVelocity;
        spin = angularVelocity;
        life = Mathf.Max(0.08f, lifetime);
        elapsed = 0f;
        startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        enabled = true;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        elapsed += deltaTime;
        transform.position += (Vector3)(velocity * deltaTime);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, 7.5f * deltaTime);
        transform.Rotate(0f, 0f, spin * deltaTime);

        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = 1f - Mathf.Clamp01(elapsed / life);
            spriteRenderer.color = color;
        }

        if (elapsed >= life)
        {
            enabled = false;
            WallImpactSystem.ReturnParticle(this);
        }
    }
}

public sealed class WallImpactDecal : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float life;
    private float fadeDuration;
    private float elapsed;
    private Color startColor;

    public SpriteRenderer Renderer => spriteRenderer;

    public void Initialize(SpriteRenderer renderer)
    {
        spriteRenderer = renderer;
    }

    public void Configure(float lifetime, float fadeTime)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        life = Mathf.Max(0.1f, lifetime);
        fadeDuration = Mathf.Clamp(fadeTime, 0.05f, life);
        elapsed = 0f;
        startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        enabled = true;
    }

    public void ReleaseImmediately()
    {
        enabled = false;
        WallImpactSystem.ReturnDecal(this);
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float fadeStart = life - fadeDuration;
        if (spriteRenderer != null && elapsed >= fadeStart)
        {
            float t = Mathf.InverseLerp(fadeStart, life, elapsed);
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = color;
        }

        if (elapsed >= life)
            ReleaseImmediately();
    }
}
