using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// V11 공용 전투 충돌 이펙트 풀.
/// 실제 충돌 지점에서 작은 픽셀 스파크/섬광을 재사용해 Instantiate/Destroy 스파이크를 줄인다.
/// </summary>
public static class CombatImpactFXV11
{
    private const int MaxPooledSparks = 96;
    private const int MaxPooledFlashes = 32;
    private static readonly Stack<V11ImpactParticle> sparkPool = new Stack<V11ImpactParticle>();
    private static readonly Stack<V11ImpactParticle> flashPool = new Stack<V11ImpactParticle>();
    private static Transform poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sparkPool.Clear();
        flashPool.Clear();
        poolRoot = null;
    }

    public static void EmitWeaponHit(WeaponDefinition weapon, Vector2 point, Vector2 hitDirection,
        bool lethal, bool tank, float travelledDistance = -1f)
    {
        Vector2 incoming = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.right;
        Vector2 rebound = -incoming;
        string id = weapon != null && !string.IsNullOrEmpty(weapon.weaponId)
            ? weapon.weaponId.ToLowerInvariant()
            : string.Empty;

        bool shotgun = id.Contains("shotgun");
        bool machine = id.Contains("machine");
        bool laser = id.Contains("laser") || (weapon != null && weapon.rangedMode == RangedAttackMode.HitscanBeam);
        bool hammer = id.Contains("hammer");
        bool whip = id.Contains("whip");
        bool closeShotgun = shotgun && travelledDistance >= 0f && travelledDistance <= 1.10f;

        Color color;
        if (laser) color = weapon != null ? Color.Lerp(weapon.accentColor, Color.white, 0.22f) : new Color(0.25f, 0.88f, 1f, 1f);
        else if (shotgun) color = new Color(1f, 0.72f, 0.24f, 1f);
        else if (machine) color = new Color(1f, 0.82f, 0.32f, 1f);
        else if (whip) color = new Color(0.82f, 0.38f, 1f, 1f);
        else if (hammer) color = new Color(1f, 0.62f, 0.22f, 1f);
        else color = weapon != null ? Color.Lerp(weapon.accentColor, Color.white, 0.30f) : Color.white;

        if (tank)
            color = Color.Lerp(color, new Color(0.62f, 0.78f, 0.86f, 1f), 0.46f);

        int sparkCount = lethal ? 7 : closeShotgun ? 8 : hammer ? 7 : shotgun ? 5 : machine ? 2 : 4;
        float strength = lethal ? 3.7f : closeShotgun ? 4.2f : hammer ? 3.8f : shotgun ? 3.0f : machine ? 2.2f : 2.7f;
        float baseSize = closeShotgun ? 0.050f : hammer ? 0.046f : machine ? 0.026f : 0.034f;

        for (int i = 0; i < sparkCount; i++)
        {
            Vector2 scatter = (rebound * Random.Range(0.52f, 1.0f) + Random.insideUnitCircle * 0.78f).normalized;
            SpawnSpark(point + Random.insideUnitCircle * 0.025f, scatter * Random.Range(strength * 0.55f, strength),
                color, Random.Range(baseSize * 0.72f, baseSize * 1.32f), Random.Range(0.10f, 0.22f));
        }

        if (lethal || closeShotgun || hammer || laser)
            SpawnFlash(point, color, closeShotgun || hammer ? 0.20f : 0.13f, lethal ? 0.15f : 0.10f);
    }

    public static void EmitDashBurst(Vector2 point, Vector2 direction, bool ending)
    {
        Vector2 safe = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 backwards = ending ? -safe : -safe;
        Color color = new Color(0.26f, 0.92f, 1f, 0.90f);
        int count = ending ? 4 : 6;
        for (int i = 0; i < count; i++)
        {
            Vector2 scatter = (backwards * 0.8f + Random.insideUnitCircle * 0.72f).normalized;
            SpawnSpark(point, scatter * Random.Range(1.0f, 2.2f), color, Random.Range(0.018f, 0.032f), Random.Range(0.09f, 0.16f));
        }
    }

    public static void EmitPickup(Vector2 point, Color color)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 direction = (Vector2.up * 0.35f + Random.insideUnitCircle).normalized;
            SpawnSpark(point, direction * Random.Range(0.7f, 1.5f), color, Random.Range(0.018f, 0.030f), Random.Range(0.12f, 0.22f));
        }
        SpawnFlash(point, color, 0.12f, 0.10f);
    }

    public static void EmitEnemyDeath(Vector2 point, Vector2 hitDirection, EnemyRole.Role role)
    {
        Color color = role == EnemyRole.Role.Tank
            ? new Color(0.60f, 0.78f, 0.88f, 1f)
            : role == EnemyRole.Role.Ranged
                ? new Color(0.36f, 0.82f, 1f, 1f)
                : role == EnemyRole.Role.Bomber
                    ? new Color(1f, 0.45f, 0.16f, 1f)
                    : new Color(0.82f, 0.94f, 1f, 1f);

        int count = role == EnemyRole.Role.Tank ? 10 : role == EnemyRole.Role.Bomber ? 8 : 6;
        Vector2 rebound = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.up;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = (rebound * 0.55f + Random.insideUnitCircle).normalized;
            SpawnSpark(point, direction * Random.Range(1.1f, role == EnemyRole.Role.Tank ? 3.1f : 2.5f),
                color, Random.Range(0.024f, role == EnemyRole.Role.Tank ? 0.055f : 0.042f), Random.Range(0.14f, 0.30f));
        }
        SpawnFlash(point, color, role == EnemyRole.Role.Tank ? 0.24f : 0.16f, 0.14f);
    }

    private static void SpawnSpark(Vector2 position, Vector2 velocity, Color color, float worldSize, float lifetime)
    {
        EnsureRoot();
        V11ImpactParticle particle = GetParticle(sparkPool, false);
        particle.gameObject.SetActive(true);
        particle.transform.SetParent(poolRoot, false);
        particle.transform.position = position;
        particle.Configure(RuntimePixelSpriteFactory.GetEnergySparkSprite(), color, worldSize, velocity,
            Random.Range(-700f, 700f), lifetime, false, ReleaseParticle);
    }

    private static void SpawnFlash(Vector2 position, Color color, float worldSize, float lifetime)
    {
        EnsureRoot();
        V11ImpactParticle particle = GetParticle(flashPool, true);
        particle.gameObject.SetActive(true);
        particle.transform.SetParent(poolRoot, false);
        particle.transform.position = position;
        Color bright = Color.Lerp(color, Color.white, 0.62f);
        bright.a = 0.92f;
        particle.Configure(RuntimePixelSpriteFactory.GetMuzzleFlashSprite(), bright, worldSize, Vector2.zero,
            Random.Range(-120f, 120f), lifetime, true, ReleaseParticle);
    }

    private static V11ImpactParticle GetParticle(Stack<V11ImpactParticle> pool, bool flash)
    {
        V11ImpactParticle particle = null;
        while (pool.Count > 0 && particle == null)
            particle = pool.Pop();
        if (particle != null)
            return particle;

        GameObject root = new GameObject(flash ? "V11_ImpactFlash" : "V11_ImpactSpark");
        root.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = flash ? 35 : 34;
        V620SpriteMaterialUtility.Apply(renderer);
        particle = root.AddComponent<V11ImpactParticle>();
        particle.Initialize(renderer);
        return particle;
    }

    private static void ReleaseParticle(V11ImpactParticle particle, bool flash)
    {
        if (particle == null) return;
        particle.gameObject.SetActive(false);
        particle.transform.SetParent(poolRoot, false);
        Stack<V11ImpactParticle> pool = flash ? flashPool : sparkPool;
        int max = flash ? MaxPooledFlashes : MaxPooledSparks;
        if (pool.Count < max) pool.Push(particle);
        else Object.Destroy(particle.gameObject);
    }

    private static void EnsureRoot()
    {
        if (poolRoot != null) return;
        GameObject root = new GameObject("V11_CombatImpactFxPool");
        root.hideFlags = HideFlags.DontSave;
        poolRoot = root.transform;
    }
}

public sealed class V11ImpactParticle : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;
    private float angularVelocity;
    private float lifetime;
    private float elapsed;
    private float startScale;
    private Color startColor;
    private bool flash;
    private System.Action<V11ImpactParticle, bool> release;

    public void Initialize(SpriteRenderer renderer)
    {
        spriteRenderer = renderer;
    }

    public void Configure(Sprite sprite, Color color, float worldSize, Vector2 initialVelocity,
        float spin, float life, bool isFlash, System.Action<V11ImpactParticle, bool> releaseCallback)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        V620SpriteMaterialUtility.Apply(spriteRenderer);
        spriteRenderer.color = color;
        startColor = color;
        float spriteWidth = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.x) : 1f;
        startScale = Mathf.Max(0.004f, worldSize / spriteWidth);
        transform.localScale = Vector3.one * startScale;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        velocity = initialVelocity;
        angularVelocity = spin;
        lifetime = Mathf.Max(0.04f, life);
        elapsed = 0f;
        flash = isFlash;
        release = releaseCallback;
        enabled = true;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        elapsed += dt;
        transform.position += (Vector3)(velocity * dt);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, dt * (flash ? 8f : 4.2f));
        transform.Rotate(0f, 0f, angularVelocity * dt);
        float t = Mathf.Clamp01(elapsed / lifetime);
        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = startColor.a * (1f - t * t);
            spriteRenderer.color = color;
        }
        float scale = flash ? Mathf.Lerp(0.72f, 1.42f, 1f - Mathf.Pow(1f - t, 2f)) : Mathf.Lerp(1f, 0.35f, t);
        transform.localScale = Vector3.one * startScale * scale;
        if (elapsed >= lifetime)
        {
            enabled = false;
            if (release != null) release(this, flash);
        }
    }
}
