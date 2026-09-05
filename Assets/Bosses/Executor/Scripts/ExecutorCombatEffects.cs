using System.Collections;
using UnityEngine;

public static class ExecutorCombatEffects
{
    private static float nextWallImpactCameraTime;
    public static void SpawnExplosion(ExecutorBossController owner, Vector3 position, float radius)
    {
        GameObject root = new GameObject("EXE_Explosion");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetExplosion();
        renderer.color = new Color(1f, 0.45f, 0.18f, 0.96f);
        renderer.sortingOrder = 27;
        float spriteSize = Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        root.transform.localScale = Vector3.one * (radius * 2f / spriteSize * 0.24f);
        ExecutorSimpleFade fade = root.AddComponent<ExecutorSimpleFade>();
        fade.Initialize(radius * 2f / spriteSize, 0.30f);
        Register(owner, root);

        SpawnDustCloud(owner, position, radius * 0.82f, 0.46f, new Color(0.68f, 0.68f, 0.67f, 0.66f));
        SpawnRockBurst(owner, position, 9, 2.8f, 0.58f, 0.11f, 0.24f);
    }

    public static void SpawnDustBurst(ExecutorBossController owner, Vector3 position, int count, float strength)
    {
        int particleCount = Mathf.Max(1, count);
        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("EXE_DustParticle");
            particle.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.24f);
            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = Random.value < 0.72f
                ? ExecutorRuntimeSprites.GetDustCloud()
                : ExecutorRuntimeSprites.GetWhitePixel();
            renderer.color = Random.value > 0.38f
                ? new Color(0.74f, 0.76f, 0.78f, Random.Range(0.48f, 0.80f))
                : new Color(0.38f, 0.40f, 0.43f, Random.Range(0.48f, 0.76f));
            renderer.sortingOrder = Random.Range(18, 24);
            particle.transform.localScale = Vector3.one * Random.Range(0.10f, 0.27f);

            ExecutorDustParticle movement = particle.AddComponent<ExecutorDustParticle>();
            Vector2 direction = new Vector2(Random.Range(-1f, 1f), Random.Range(0.22f, 1.15f)).normalized;
            movement.Initialize(direction * Random.Range(strength * 0.45f, strength), Random.Range(0.44f, 0.88f));
            Register(owner, particle);
        }

        SpawnDustCloud(owner, position + Vector3.up * 0.06f, Mathf.Lerp(0.42f, 0.86f, Mathf.Clamp01(strength / 3f)),
            0.52f, new Color(0.54f, 0.55f, 0.57f, 0.58f));
    }

    public static void SpawnInwardDust(ExecutorBossController owner, Vector3 position, int count, float strength)
    {
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(0.45f, 0.90f);
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.45f);
            GameObject particle = new GameObject("EXE_InwardDust");
            particle.transform.position = position + (Vector3)(radial * radius);
            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = ExecutorRuntimeSprites.GetDustCloud();
            renderer.color = new Color(0.68f, 0.70f, 0.72f, Random.Range(0.42f, 0.68f));
            renderer.sortingOrder = Random.Range(18, 22);
            particle.transform.localScale = Vector3.one * Random.Range(0.10f, 0.20f);
            Vector2 toCenter = ((Vector2)position - (Vector2)particle.transform.position).normalized;
            ExecutorDustParticle movement = particle.AddComponent<ExecutorDustParticle>();
            movement.Initialize(toCenter * Random.Range(strength * 0.70f, strength), Random.Range(0.18f, 0.34f));
            Register(owner, particle);
        }
    }

    public static void SpawnRockBurst(ExecutorBossController owner, Vector3 position, int count,
        float strength, float life, float minScale, float maxScale)
    {
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            GameObject rock = new GameObject("EXE_RockDebris");
            rock.transform.position = position + new Vector3(Random.Range(-0.38f, 0.38f), Random.Range(-0.05f, 0.14f), 0f);
            SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = ExecutorRuntimeSprites.GetRockChunk();
            renderer.color = Random.value > 0.5f
                ? new Color(0.72f, 0.70f, 0.68f, 1f)
                : new Color(0.39f, 0.40f, 0.43f, 1f);
            renderer.sortingOrder = Random.Range(20, 27);
            float scale = Random.Range(minScale, maxScale);
            rock.transform.localScale = Vector3.one * scale;
            rock.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector2 direction = new Vector2(Random.Range(-1f, 1f), Random.Range(0.58f, 1.35f)).normalized;
            ExecutorDebrisParticle particle = rock.AddComponent<ExecutorDebrisParticle>();
            particle.Initialize(direction * Random.Range(strength * 0.60f, strength), Random.Range(life * 0.88f, life * 1.28f),
                Random.Range(300f, 720f), Random.Range(3.5f, 7.5f));
            Register(owner, rock);
        }
    }

    public static void SpawnMetalSparks(ExecutorBossController owner, Vector3 position, int count)
    {
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            GameObject spark = new GameObject("EXE_MetalSpark");
            spark.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.16f);
            SpriteRenderer renderer = spark.AddComponent<SpriteRenderer>();
            renderer.sprite = ExecutorRuntimeSprites.GetWhitePixel();
            renderer.color = Random.value > 0.35f
                ? new Color(1f, 0.64f, 0.18f, 1f)
                : new Color(1f, 0.92f, 0.55f, 1f);
            renderer.sortingOrder = 30;
            spark.transform.localScale = new Vector3(Random.Range(0.035f, 0.06f), Random.Range(0.10f, 0.18f), 1f);
            Vector2 direction = new Vector2(Random.Range(-1f, 1f), Random.Range(0.15f, 1f)).normalized;
            ExecutorDebrisParticle particle = spark.AddComponent<ExecutorDebrisParticle>();
            particle.Initialize(direction * Random.Range(2.5f, 4.6f), Random.Range(0.16f, 0.30f),
                Random.Range(250f, 550f), Random.Range(5f, 9f));
            Register(owner, spark);
        }
    }

    public static void SpawnProjectileWallImpact(ExecutorBossController owner, Vector3 position, Vector2 incomingDirection, int visualStage)
    {
        Vector2 incoming = incomingDirection.sqrMagnitude > 0.0001f ? incomingDirection.normalized : Vector2.right;
        Vector2 rebound = -incoming;
        Color stageColor = visualStage >= 3
            ? new Color(1f, 0.12f, 0.05f, 1f)
            : visualStage == 2
                ? new Color(0.96f, 0.24f, 0.68f, 1f)
                : new Color(0.20f, 0.78f, 1f, 1f);

        GameObject flash = new GameObject("EXE_WallImpactFlash");
        flash.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = ExecutorRuntimeSprites.GetGlow();
        flashRenderer.color = new Color(stageColor.r, stageColor.g, stageColor.b, 0.82f);
        flashRenderer.sortingOrder = 31;
        flash.transform.localScale = Vector3.one * 0.16f;
        ExecutorSimpleFade flashFade = flash.AddComponent<ExecutorSimpleFade>();
        flashFade.Initialize(0.46f, 0.13f);
        Register(owner, flash);

        if (Time.unscaledTime >= nextWallImpactCameraTime)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 view = camera.WorldToViewportPoint(position);
                if (view.z > 0f && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f)
                {
                    nextWallImpactCameraTime = Time.unscaledTime + 0.065f;
                    CameraFeedbackController feedback = CameraFeedbackController.Instance;
                    if (feedback != null) feedback.Impact(CameraImpactLevelV11.Small, rebound, false);
                }
            }
        }

        int sparkCount = visualStage >= 3 ? 10 : visualStage == 2 ? 8 : 6;
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject spark = new GameObject("EXE_WallImpactSpark");
            spark.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.035f);
            SpriteRenderer renderer = spark.AddComponent<SpriteRenderer>();
            renderer.sprite = ExecutorRuntimeSprites.GetWhitePixel();
            Color c = Color.Lerp(stageColor, Color.white, Random.Range(0.18f, 0.62f));
            renderer.color = c;
            renderer.sortingOrder = 32;
            spark.transform.localScale = new Vector3(Random.Range(0.025f, 0.045f), Random.Range(0.07f, 0.13f), 1f);
            Vector2 scatter = (rebound + Random.insideUnitCircle * 0.72f).normalized;
            ExecutorDebrisParticle particle = spark.AddComponent<ExecutorDebrisParticle>();
            particle.Initialize(scatter * Random.Range(2.0f, 4.1f), Random.Range(0.12f, 0.24f),
                Random.Range(260f, 620f), Random.Range(2.8f, 5.6f));
            Register(owner, spark);
        }

        int debrisCount = visualStage >= 3 ? 5 : 4;
        for (int i = 0; i < debrisCount; i++)
        {
            GameObject chip = new GameObject("EXE_WallImpactChip");
            chip.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.045f);
            SpriteRenderer renderer = chip.AddComponent<SpriteRenderer>();
            renderer.sprite = ExecutorRuntimeSprites.GetRockChunk();
            renderer.color = Color.Lerp(new Color(0.38f, 0.42f, 0.47f, 1f), stageColor, 0.20f);
            renderer.sortingOrder = 29;
            chip.transform.localScale = Vector3.one * Random.Range(0.055f, 0.10f);
            Vector2 scatter = (rebound * 0.75f + Random.insideUnitCircle * 0.95f).normalized;
            ExecutorDebrisParticle particle = chip.AddComponent<ExecutorDebrisParticle>();
            particle.Initialize(scatter * Random.Range(1.4f, 2.9f), Random.Range(0.22f, 0.42f),
                Random.Range(220f, 460f), Random.Range(3.8f, 6.8f));
            Register(owner, chip);
        }
    }

    public static GameObject SpawnEmergenceImpactDecal(ExecutorBossController owner, Vector3 position,
        float duration, float scaleMultiplier)
    {
        Sprite sprite = ExecutorRuntimeSprites.LoadEmergeImpact();
        if (sprite == null)
            return null;

        GameObject root = new GameObject("EXE_EmergeImpact_UserArt");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 1f, 1f, 0f);
        renderer.sortingOrder = 9;

        float targetWidth = 2.25f * Mathf.Max(0.75f, scaleMultiplier);
        float baseWidth = Mathf.Max(0.001f, sprite.bounds.size.x);
        float targetScale = targetWidth / baseWidth;
        root.transform.localScale = Vector3.one * targetScale * 0.82f;

        ExecutorEmergenceImpactMark mark = root.AddComponent<ExecutorEmergenceImpactMark>();
        mark.Initialize(targetScale, Mathf.Max(0.25f, duration));
        Register(owner, root);
        return root;
    }

    public static GameObject SpawnGroundCrack(ExecutorBossController owner, Vector3 position, float radius,
        float duration, Color color)
    {
        GameObject root = new GameObject("EXE_GroundCrack");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetGroundCrack();
        renderer.color = color;
        renderer.sortingOrder = 8;
        float spriteSize = Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        root.transform.localScale = Vector3.one * (radius * 2f / spriteSize);
        ExecutorGroundMark mark = root.AddComponent<ExecutorGroundMark>();
        mark.Initialize(Mathf.Max(0.15f, duration));
        Register(owner, root);
        return root;
    }

    public static void SpawnDustCloud(ExecutorBossController owner, Vector3 position, float finalRadius,
        float duration, Color color)
    {
        GameObject root = new GameObject("EXE_DustCloud");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetDustCloud();
        renderer.color = color;
        renderer.sortingOrder = 19;
        root.transform.localScale = Vector3.one * 0.12f;
        float spriteSize = Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        ExecutorSimpleFade fade = root.AddComponent<ExecutorSimpleFade>();
        fade.Initialize(finalRadius * 2f / spriteSize, duration);
        Register(owner, root);
    }

    public static void SpawnUndergroundTrail(ExecutorBossController owner, Vector3 position, Vector2 direction)
    {
        GameObject mound = new GameObject("EXE_UndergroundMound");
        mound.transform.position = position;
        mound.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        SpriteRenderer moundRenderer = mound.AddComponent<SpriteRenderer>();
        moundRenderer.sprite = ExecutorRuntimeSprites.GetUndergroundMound();
        moundRenderer.color = new Color(0.72f, 0.74f, 0.76f, 0.50f);
        moundRenderer.sortingOrder = 8;
        mound.transform.localScale = new Vector3(0.42f, 0.32f, 1f);
        ExecutorSimpleFade moundFade = mound.AddComponent<ExecutorSimpleFade>();
        moundFade.Initialize(0.86f, 0.30f);
        Register(owner, mound);

        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = (Vector3)(perpendicular * Random.Range(-0.28f, 0.28f));
            SpawnDustCloud(owner, position + offset, Random.Range(0.22f, 0.36f), Random.Range(0.24f, 0.36f),
                new Color(0.68f, 0.70f, 0.72f, 0.40f));
        }
    }

    private static void Register(ExecutorBossController owner, GameObject target)
    {
        if (owner != null && target != null)
            owner.RegisterSpawnedObject(target);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorEmergenceImpactMark : MonoBehaviour
{
    private SpriteRenderer renderer;
    private Vector3 startScale;
    private Vector3 targetScale;
    private Color startColor;
    private float duration;
    private float elapsed;

    public void Initialize(float targetUniformScale, float life)
    {
        renderer = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
        targetScale = Vector3.one * Mathf.Max(0.01f, targetUniformScale);
        startColor = renderer != null ? renderer.color : Color.white;
        duration = Mathf.Max(0.10f, life);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        transform.localScale = Vector3.Lerp(startScale, targetScale, eased);

        if (renderer != null)
        {
            Color color = startColor;
            float fadeIn = Mathf.Clamp01(Mathf.InverseLerp(0f, 0.18f, t));
            float pulse = 0.86f + (Mathf.Sin(t * Mathf.PI * 8f) * 0.5f + 0.5f) * 0.14f;
            float fadeOut = t <= 0.66f ? 1f : 1f - Mathf.InverseLerp(0.66f, 1f, t);
            color.a = fadeIn * fadeOut * pulse;
            renderer.color = color;
            float scalePulse = 1f + Mathf.Sin(t * Mathf.PI * 6f) * 0.028f;
            transform.localScale *= scalePulse;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorSimpleFade : MonoBehaviour
{
    private SpriteRenderer renderer;
    private Vector3 startScale;
    private Vector3 targetScale;
    private float duration;
    private float elapsed;
    private Color startColor;

    public void Initialize(float finalScale, float life)
    {
        renderer = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
        targetScale = Vector3.one * Mathf.Max(0.01f, finalScale);
        duration = Mathf.Max(0.05f, life);
        startColor = renderer != null ? renderer.color : Color.white;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        transform.localScale = Vector3.Lerp(startScale, targetScale, eased);
        if (renderer != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t * t);
            renderer.color = color;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorDustParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float duration;
    private float elapsed;
    private SpriteRenderer renderer;
    private Color startColor;
    private Vector3 startScale;

    public void Initialize(Vector2 initialVelocity, float life)
    {
        velocity = initialVelocity;
        duration = Mathf.Max(0.08f, life);
        renderer = GetComponent<SpriteRenderer>();
        startColor = renderer != null ? renderer.color : Color.white;
        startScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 3.8f);
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, 150f * Time.deltaTime);
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localScale = Vector3.Lerp(startScale, startScale * 0.55f, t);
        if (renderer != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t * t);
            renderer.color = color;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorDebrisParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float duration;
    private float angularVelocity;
    private float gravity;
    private float elapsed;
    private SpriteRenderer renderer;
    private Color startColor;
    private Vector3 startScale;

    public void Initialize(Vector2 initialVelocity, float life, float spin, float gravityStrength)
    {
        velocity = initialVelocity;
        duration = Mathf.Max(0.12f, life);
        angularVelocity = Random.value > 0.5f ? spin : -spin;
        gravity = Mathf.Max(0f, gravityStrength);
        renderer = GetComponent<SpriteRenderer>();
        startColor = renderer != null ? renderer.color : Color.white;
        startScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, angularVelocity * Time.deltaTime);
        float t = Mathf.Clamp01(elapsed / duration);
        if (t > 0.62f)
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.28f, Mathf.InverseLerp(0.62f, 1f, t));
        if (renderer != null)
        {
            Color color = startColor;
            color.a = t < 0.68f ? startColor.a : Mathf.Lerp(startColor.a, 0f, Mathf.InverseLerp(0.68f, 1f, t));
            renderer.color = color;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorGroundMark : MonoBehaviour
{
    private SpriteRenderer renderer;
    private Color startColor;
    private float duration;
    private float elapsed;

    public void Initialize(float life)
    {
        duration = Mathf.Max(0.10f, life);
        renderer = GetComponent<SpriteRenderer>();
        startColor = renderer != null ? renderer.color : Color.white;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        if (renderer != null)
        {
            Color color = startColor;
            color.a = t < 0.55f ? startColor.a : Mathf.Lerp(startColor.a, 0f, Mathf.InverseLerp(0.55f, 1f, t));
            renderer.color = color;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorShockwaveEffect : MonoBehaviour
{
    private SpriteRenderer renderer;
    private PlayerHealth playerHealth;
    private float startRadius;
    private float endRadius;
    private float thickness;
    private float duration;
    private float elapsed;
    private int damage;
    private bool dealtDamage;

    public static ExecutorShockwaveEffect Create(ExecutorBossController owner, Vector3 position,
        float startRadius, float endRadius, float thickness, float duration, int damage)
    {
        GameObject root = new GameObject("EXE_Shockwave");
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetShockwaveRing();
        renderer.color = new Color(1f, 0.18f, 0.12f, 0.90f);
        renderer.sortingOrder = 23;

        ExecutorShockwaveEffect effect = root.AddComponent<ExecutorShockwaveEffect>();
        effect.renderer = renderer;
        effect.playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        effect.startRadius = Mathf.Max(0.05f, startRadius);
        effect.endRadius = Mathf.Max(effect.startRadius, endRadius);
        effect.thickness = Mathf.Max(0.05f, thickness);
        effect.duration = Mathf.Max(0.05f, duration);
        effect.damage = Mathf.Max(1, damage);
        effect.ApplyScale(effect.startRadius);
        if (owner != null) owner.RegisterSpawnedObject(root);
        return effect;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        float radius = Mathf.Lerp(startRadius, endRadius, eased);
        ApplyScale(radius);

        if (renderer != null)
        {
            Color color = renderer.color;
            color.a = Mathf.Lerp(0.92f, 0f, t);
            renderer.color = color;
        }

        if (!dealtDamage && playerHealth != null && !playerHealth.IsDead)
        {
            float distance = Vector2.Distance(transform.position, playerHealth.transform.position);
            if (Mathf.Abs(distance - radius) <= thickness * 0.5f)
            {
                dealtDamage = true;
                playerHealth.TakeDamage(damage);
            }
        }

        if (t >= 1f)
            Destroy(gameObject);
    }

    private void ApplyScale(float radius)
    {
        if (renderer == null || renderer.sprite == null) return;
        float spriteRadius = Mathf.Max(0.001f, renderer.sprite.bounds.extents.x);
        transform.localScale = Vector3.one * (radius / spriteRadius);
    }
}
