using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public sealed class EnemyDeathShard : MonoBehaviour
{
    [SerializeField] private float minForce = 3.8f;
    [SerializeField] private float maxForce = 7.0f;
    [SerializeField] private float maxTorque = 520f;
    [SerializeField] private float lifeTime = 0.72f;
    [SerializeField] private float fadeStartTime = 0.34f;
    [SerializeField] private float startScale = 0.30f;
    [SerializeField] private float endScale = 0.05f;

    [Header("v6.2 Wall Bounce")]
    [SerializeField, Range(0f, 1f)] private float wallBounciness = 0.27f;
    [SerializeField, Range(0f, 1f)] private float wallFriction = 0.16f;
    [SerializeField] private float linearDamping = 2.8f;
    [SerializeField] private float maximumSpeed = 8.5f;
    [SerializeField] private float visualScaleMultiplier = 1.25f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Collider2D shardCollider;
    private Coroutine lifeRoutine;
    private float configuredScaleMultiplier = 1f;
    private float configuredLifeMultiplier = 1f;
    private static PhysicsMaterial2D bounceMaterial;

    public SpriteRenderer Renderer
    {
        get
        {
            EnsureReferences();
            return spriteRenderer;
        }
    }

    private void Awake()
    {
        EnsureReferences();
        PrepareForReuse();
    }

    private void EnsureReferences()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (shardCollider == null)
            shardCollider = GetComponent<Collider2D>();
        if (spriteRenderer != null && spriteRenderer.sprite == null)
            spriteRenderer.sprite = RuntimePixelSpriteFactory.GetEnemyShardSprite(0);
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 8);
            V620SpriteMaterialUtility.Apply(spriteRenderer);
        }
        if (body != null)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearDamping = linearDamping;
            body.angularDamping = 1.4f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        if (shardCollider != null)
        {
            shardCollider.isTrigger = false;
            shardCollider.sharedMaterial = GetBounceMaterial();
        }
    }

    public void PrepareForReuse()
    {
        EnsureReferences();
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        configuredScaleMultiplier = 1f;
        configuredLifeMultiplier = 1f;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        transform.localScale = Vector3.one * startScale;
        enabled = true;
    }

    public void ConfigureVisual(Sprite sprite, Color color, float scaleMultiplier, float lifeMultiplier, int sortingOrder)
    {
        EnsureReferences();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite != null ? sprite : RuntimePixelSpriteFactory.GetEnemyShardSprite(0);
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
        }
        configuredScaleMultiplier = Mathf.Max(0.35f, scaleMultiplier) * Mathf.Max(0.1f, visualScaleMultiplier);
        configuredLifeMultiplier = Mathf.Max(0.35f, lifeMultiplier);
        transform.localScale = Vector3.one * (startScale * configuredScaleMultiplier);
    }

    public void Launch(Vector2 biasDirection, float biasStrength)
    {
        LaunchDirectional(biasDirection, 100f, biasStrength);
    }

    public void LaunchDirectional(Vector2 direction, float spreadDegrees, float biasStrength)
    {
        EnsureReferences();
        Vector2 baseDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;
        if (baseDirection.sqrMagnitude < 0.0001f)
            baseDirection = Vector2.up;

        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float randomAngle = baseAngle + Random.Range(-Mathf.Abs(spreadDegrees) * 0.5f, Mathf.Abs(spreadDegrees) * 0.5f);
        Vector2 coneDirection = new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad));
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector2 finalDirection = (coneDirection * Mathf.Max(0.1f, biasStrength) + randomDirection * 0.35f).normalized;

        float sizeWeight = Mathf.Clamp(configuredScaleMultiplier, 0.55f, 1.65f);
        float force = Random.Range(minForce, maxForce) / Mathf.Lerp(0.82f, 1.18f, Mathf.InverseLerp(0.55f, 1.65f, sizeWeight));
        if (body != null)
        {
            body.AddForce(finalDirection * force, ForceMode2D.Impulse);
            body.AddTorque(Random.Range(-maxTorque, maxTorque));
        }

        if (body != null && body.linearVelocity.magnitude > maximumSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maximumSpeed;

        if (lifeRoutine != null)
            StopCoroutine(lifeRoutine);
        lifeRoutine = StartCoroutine(FadeAndRelease());
    }

    private void FixedUpdate()
    {
        if (body != null && body.linearVelocity.magnitude > maximumSpeed)
            body.linearVelocity = body.linearVelocity.normalized * maximumSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (body == null || other == null || !IsWall(other.gameObject) || body.linearVelocity.sqrMagnitude < 0.001f)
            return;

        Vector2 point = other.ClosestPoint(body.position);
        Vector2 normal = body.position - point;
        if (normal.sqrMagnitude < 0.0001f)
            normal = body.position - (Vector2)other.bounds.center;
        if (normal.sqrMagnitude < 0.0001f)
            normal = -body.linearVelocity.normalized;
        normal.Normalize();
        body.position += normal * 0.02f;
        body.linearVelocity = Vector2.Reflect(body.linearVelocity, normal) * Mathf.Max(0.15f, wallBounciness);
    }

    private PhysicsMaterial2D GetBounceMaterial()
    {
        if (bounceMaterial == null)
        {
            bounceMaterial = new PhysicsMaterial2D("IA_V620_EnemyShardBounce")
            {
                bounciness = wallBounciness,
                friction = wallFriction,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        return bounceMaterial;
    }

    private static bool IsWall(GameObject target)
    {
        if (target == null) return false;
        int wall = LayerMask.NameToLayer("Wall");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int boundary = LayerMask.NameToLayer("RoomBoundary");
        Transform current = target.transform;
        while (current != null)
        {
            int layer = current.gameObject.layer;
            if ((wall >= 0 && layer == wall) || (obstacle >= 0 && layer == obstacle) || (boundary >= 0 && layer == boundary))
                return true;
            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("wall") || lower.Contains("obstacle") || lower.Contains("boundary"))
                return true;
            current = current.parent;
        }
        return false;
    }

    private IEnumerator FadeAndRelease()
    {
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        float resolvedLife = Mathf.Max(0.12f, lifeTime * configuredLifeMultiplier);
        float resolvedFadeStart = Mathf.Min(resolvedLife - 0.03f, fadeStartTime * configuredLifeMultiplier);
        float resolvedStartScale = startScale * configuredScaleMultiplier;
        float resolvedEndScale = endScale * configuredScaleMultiplier;
        float elapsed = 0f;

        while (elapsed < resolvedLife)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= resolvedFadeStart)
            {
                float t = Mathf.InverseLerp(resolvedFadeStart, resolvedLife, elapsed);
                if (spriteRenderer != null)
                {
                    Color color = startColor;
                    color.a = Mathf.Lerp(startColor.a, 0f, t);
                    spriteRenderer.color = color;
                }
                transform.localScale = Vector3.one * Mathf.Lerp(resolvedStartScale, resolvedEndScale, t);
            }
            yield return null;
        }

        lifeRoutine = null;
        EnemyShardPoolV61.Release(this);
    }
}
