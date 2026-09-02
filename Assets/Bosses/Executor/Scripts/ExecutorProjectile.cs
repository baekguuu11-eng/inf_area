using UnityEngine;

public enum ExecutorProjectileStyle
{
    AimedPhaseOne,
    SpreadPhaseOne,
    AimedPhaseTwo,
    SpreadPhaseTwo
}

[DisallowMultipleComponent]
public sealed class ExecutorProjectile : MonoBehaviour
{
    private ExecutorBossController owner;
    private Rigidbody2D body;
    private CircleCollider2D hitbox;
    private SpriteRenderer renderer;
    private int damage = 1;
    private float lifetime = 5f;
    private float spinSpeed = 300f;
    private bool consumed;
    private float pulseTime;
    private float baseScale;
    private Vector2 launchVelocity;
    private float collisionGraceUntil;

    public static ExecutorProjectile Create(ExecutorBossController owner, Vector3 position, Vector2 direction,
        float speed, int damage, ExecutorProjectileStyle style)
    {
        GameObject root = new GameObject("EXE_Projectile");
        root.transform.position = position;

        int enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");
        if (enemyProjectileLayer >= 0)
            root.layer = enemyProjectileLayer;

        int visualStage = owner != null ? owner.ProjectileVisualStage : 1;
        Color coreColor;
        Color glowColor;
        float scale;
        float radius;
        switch (style)
        {
            case ExecutorProjectileStyle.SpreadPhaseOne:
                coreColor = Color.white;
                glowColor = new Color(0.38f, 0.46f, 1f, 0.40f);
                scale = 0.48f;
                radius = 0.24f;
                break;
            case ExecutorProjectileStyle.AimedPhaseTwo:
                coreColor = Color.white;
                glowColor = new Color(1f, 0.20f, 0.05f, 0.48f);
                scale = 0.58f;
                radius = 0.28f;
                break;
            case ExecutorProjectileStyle.SpreadPhaseTwo:
                coreColor = Color.white;
                glowColor = new Color(1f, 0.10f, 0.02f, 0.44f);
                scale = 0.44f;
                radius = 0.22f;
                break;
            default:
                coreColor = Color.white;
                glowColor = new Color(0.18f, 0.72f, 1f, 0.46f);
                scale = 0.56f;
                radius = 0.28f;
                break;
        }

        if (visualStage >= 3)
            glowColor = new Color(1f, 0.10f, 0.035f, 0.52f);
        else if (visualStage == 2)
            glowColor = new Color(0.95f, 0.22f, 0.62f, 0.46f);

        SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
        sprite.sprite = ExecutorRuntimeSprites.GetProjectile(visualStage);
        sprite.color = coreColor;
        sprite.sortingOrder = 18;
        root.transform.localScale = Vector3.one * scale;

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        CreateTrailPiece(root.transform, "Glow", Vector3.zero, 1.35f, glowColor, 17);
        CreateTrailPiece(root.transform, "TrailNear", new Vector3(-0.30f, 0f, 0f), 0.70f,
            new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * 0.65f), 16);
        CreateTrailPiece(root.transform, "TrailFar", new Vector3(-0.52f, 0f, 0f), 0.42f,
            new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * 0.34f), 15);

        Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
        rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = radius / Mathf.Max(0.01f, scale);

        ExecutorProjectile projectile = root.AddComponent<ExecutorProjectile>();
        projectile.owner = owner;
        projectile.body = rigidbody;
        projectile.hitbox = collider;
        projectile.renderer = sprite;
        projectile.damage = Mathf.Max(1, damage);
        projectile.baseScale = scale;
        projectile.launchVelocity = normalizedDirection * speed;
        projectile.collisionGraceUntil = Time.time + 0.10f;
        rigidbody.linearVelocity = projectile.launchVelocity;

        if (owner != null)
        {
            Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] != null)
                    Physics2D.IgnoreCollision(collider, ownerColliders[i], true);
            }
            owner.RegisterSpawnedObject(root);
        }
        return projectile;
    }

    private static void CreateTrailPiece(Transform parent, string name, Vector3 localPosition,
        float scale, Color color, int sortingOrder)
    {
        GameObject piece = new GameObject(name);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = Vector3.one * scale;
        SpriteRenderer glow = piece.AddComponent<SpriteRenderer>();
        glow.sprite = ExecutorRuntimeSprites.GetGlow();
        glow.color = color;
        glow.sortingOrder = sortingOrder;
    }

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (hitbox == null) hitbox = GetComponent<CircleCollider2D>();
        if (renderer == null) renderer = GetComponent<SpriteRenderer>();
        if (baseScale <= 0f) baseScale = transform.localScale.x;
    }

    private void Start()
    {
        if (body != null && launchVelocity.sqrMagnitude > 0.001f)
            body.linearVelocity = launchVelocity;
        if (renderer != null) renderer.enabled = true;
        Destroy(gameObject, Mathf.Max(0.2f, lifetime));
    }

    private void Update()
    {
        pulseTime += Time.deltaTime;
        float pulse = 1f + Mathf.Sin(pulseTime * 13f) * 0.045f;
        transform.localScale = Vector3.one * baseScale * pulse;
        if (Time.time < collisionGraceUntil && body != null && body.linearVelocity.sqrMagnitude < 0.25f &&
            launchVelocity.sqrMagnitude > 0.001f)
            body.linearVelocity = launchVelocity;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
            return;

        if (other.GetComponentInParent<EnemyHealth>() != null)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            consumed = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger && IsWall(other.gameObject))
        {
            if (Time.time < collisionGraceUntil)
                return;
            consumed = true;
            Vector2 velocity = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                ? body.linearVelocity.normalized
                : launchVelocity.normalized;
            Vector2 impactPoint = other.ClosestPoint(transform.position);
            ExecutorCombatEffects.SpawnProjectileWallImpact(owner, impactPoint, velocity,
                owner != null ? owner.ProjectileVisualStage : 1);
            Destroy(gameObject);
        }
    }

    private static bool IsWall(GameObject target)
    {
        if (target == null)
            return false;

        int wallLayer = LayerMask.NameToLayer("Wall");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int boundaryLayer = LayerMask.NameToLayer("RoomBoundary");
        if (wallLayer >= 0 && target.layer == wallLayer) return true;
        if (obstacleLayer >= 0 && target.layer == obstacleLayer) return true;
        if (boundaryLayer >= 0 && target.layer == boundaryLayer) return true;

        string lower = target.name.ToLowerInvariant();
        return lower.Contains("wall") || lower.Contains("boundary");
    }
}
