using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class MachineWormProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float spinSpeed = 260f;
    [SerializeField, Range(0.2f, 1f)] private float colliderVisualRatio = 0.7f;
    [SerializeField] private bool useTrail = true;
    [SerializeField] private float trailTime = 0.18f;
    [SerializeField] private float trailWidth = 0.16f;

    private static Material trailMaterial;

    private Rigidbody2D body;
    private CircleCollider2D hitbox;
    private SpriteRenderer spriteRenderer;
    private int damage = 1;
    private bool consumed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.freezeRotation = true;
        hitbox.isTrigger = true;

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = RuntimePixelSpriteFactory.GetBossProjectileSprite();

        NormalizeCollider();
        EnsureTrail();
    }

    private void Start()
    {
        Destroy(gameObject, Mathf.Max(0.2f, lifetime));
    }

    private void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    public void Setup(Vector2 direction, float speed, int newDamage)
    {
        damage = Mathf.Max(1, newDamage);
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        body.linearVelocity = safeDirection * Mathf.Max(0.2f, speed);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
            return;

        if (other.GetComponentInParent<EnemyHealth>() != null)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            consumed = true;
            health.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger && IsWall(other.gameObject))
        {
            consumed = true;
            Destroy(gameObject);
        }
    }

    private bool IsWall(GameObject target)
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

    private void NormalizeCollider()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || hitbox == null)
            return;

        Vector2 bounds = spriteRenderer.sprite.bounds.size;
        float radius = Mathf.Min(bounds.x, bounds.y) * 0.5f * Mathf.Clamp(colliderVisualRatio, 0.2f, 1f);
        hitbox.radius = Mathf.Max(0.03f, radius);
        hitbox.offset = Vector2.zero;
    }

    private void EnsureTrail()
    {
        if (!useTrail)
            return;

        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        if (trailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                trailMaterial = new Material(shader);
                trailMaterial.name = "MachineWormTrail_Runtime";
                trailMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        trail.time = Mathf.Max(0.03f, trailTime);
        trail.startWidth = Mathf.Max(0.01f, trailWidth);
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.03f;
        trail.alignment = LineAlignment.TransformZ;
        trail.sortingOrder = spriteRenderer.sortingOrder - 1;
        if (trailMaterial != null)
            trail.material = trailMaterial;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.62f, 0.28f, 1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.95f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;
    }
}
