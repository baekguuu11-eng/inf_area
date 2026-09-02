using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class PlayerProjectile : MonoBehaviour
{
    [Header("Legacy / Safety Cleanup")]
    [SerializeField] private float lifetime = 8f;

    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private ProjectileVisualController visualController;
    private WeaponDefinition weapon;
    private Vector2 origin;
    private Vector2 previousPosition;
    private int damage = 1;
    private int remainingPierce;
    private float falloffEndDistance = 16f;
    private bool terminated;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        projectileCollider = GetComponent<Collider2D>();
        projectileCollider.isTrigger = true;

        visualController = GetComponent<ProjectileVisualController>();
        if (visualController == null)
            visualController = gameObject.AddComponent<ProjectileVisualController>();

        previousPosition = transform.position;
    }

    private void Start()
    {
        // Gameplay range is no longer capped. This lifetime is only a technical cleanup guard
        // for projectiles that somehow leave every room without touching a wall.
        float cleanupLifetime = weapon != null ? weapon.projectileLifetime : lifetime;
        Destroy(gameObject, Mathf.Max(1f, cleanupLifetime));
    }

    private void FixedUpdate()
    {
        if (terminated)
            return;

        Vector2 currentPosition = body.position;
        if (SweepForWall(previousPosition, currentPosition))
            return;

        previousPosition = currentPosition;
    }

    public void Configure(PlayerProjectileContext context)
    {
        weapon = context.weapon;
        origin = context.origin;
        previousPosition = transform.position;
        damage = Mathf.Max(1, context.damage);
        remainingPierce = Mathf.Max(0, context.pierce);
        falloffEndDistance = weapon != null
            ? Mathf.Max(weapon.falloffStartDistance + 0.01f, weapon.maximumRange)
            : 16f;

        Vector2 direction = context.direction.sqrMagnitude > 0.0001f ? context.direction.normalized : Vector2.right;
        body.linearVelocity = direction * Mathf.Max(0.1f, context.speed);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        visualController.Configure(weapon, projectileCollider);
    }

    public void Setup(Vector2 direction, float speed, int dmg)
    {
        Setup(direction, speed, dmg, 0, 1f);
    }

    public void Setup(Vector2 direction, float speed, int dmg, int pierceCount, float visualScale)
    {
        origin = transform.position;
        damage = Mathf.Max(1, dmg);
        remainingPierce = Mathf.Max(0, pierceCount);
        falloffEndDistance = 16f;
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        body.linearVelocity = safeDirection * Mathf.Max(0.1f, speed);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg);
        visualController.Configure(null, projectileCollider);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (terminated || collision == null || collision.GetComponentInParent<PlayerHealth>() != null)
            return;

        EnemyHealth enemy = collision.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            if (enemy.IsDead || hitEnemies.Contains(enemy))
                return;

            hitEnemies.Add(enemy);
            Vector2 direction = body.linearVelocity.sqrMagnitude > 0.0001f ? body.linearVelocity.normalized : Vector2.right;
            float travelled = Vector2.Distance(origin, transform.position);
            int appliedDamage = CalculateDistanceDamage(travelled);
            float stoppingPower = weapon != null ? weapon.knockbackBonus : 0f;
            Vector2 hitPoint = collision.ClosestPoint(transform.position);
            enemy.TakeDamage(new DamageContext(appliedDamage, direction, hitPoint, EnemyHitKind.Ranged, stoppingPower));
            if (PlayerCombatSFX.Instance != null) PlayerCombatSFX.Instance.PlayRangedImpact(weapon, enemy, hitPoint);

            if (remainingPierce <= 0)
                Terminate();
            else
                remainingPierce--;
            return;
        }

        // RoomBoundary처럼 Trigger로 구성된 벽도 충돌 이펙트가 나와야 한다.
        if (!IsWall(collision.gameObject))
            return;

        Vector2 travel = body.linearVelocity.sqrMagnitude > 0.0001f ? body.linearVelocity.normalized : Vector2.right;
        Vector2 point = collision.ClosestPoint(transform.position);
        Vector2 normal = ResolveWallNormal(collision, travel, point);
        WallImpactSystem.Emit(point, normal, travel, damage, weapon, collision);
        Terminate();
    }

    private bool SweepForWall(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return false;

        int wallMask = BuildWallMask();
        if (wallMask == 0)
            return false;

        Vector2 direction = delta / distance;
        RaycastHit2D hit = Physics2D.Raycast(start, direction, distance + 0.02f, wallMask);
        if (hit.collider == null || hit.collider == projectileCollider || !IsWall(hit.collider.gameObject))
            return false;

        Vector2 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -direction;
        WallImpactSystem.Emit(hit.point, normal, direction, damage, weapon, hit.collider);
        Terminate();
        return true;
    }

    private static int BuildWallMask()
    {
        int mask = 0;
        int wall = LayerMask.NameToLayer("Wall");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int boundary = LayerMask.NameToLayer("RoomBoundary");
        if (wall >= 0) mask |= 1 << wall;
        if (obstacle >= 0) mask |= 1 << obstacle;
        if (boundary >= 0) mask |= 1 << boundary;
        return mask;
    }

    private int CalculateDistanceDamage(float distance)
    {
        if (weapon == null)
            return damage;

        float start = Mathf.Max(0f, weapon.falloffStartDistance);
        float end = Mathf.Max(start + 0.01f, falloffEndDistance);
        float t = Mathf.InverseLerp(start, end, distance);
        float multiplier = Mathf.Lerp(1f, Mathf.Clamp(weapon.minimumDamageMultiplier, 0.05f, 1f), t);

        // 샷건은 위험을 감수하고 붙어서 쏠 때 확실한 보상을 준다.
        // 극근접 1.10m 이내는 +55%, 1.10~2.00m는 +25%에서 기본값까지 부드럽게 감소한다.
        if (weapon.weaponId == "shotgun")
        {
            float closeMultiplier = 1f;
            if (distance <= 1.10f)
                closeMultiplier = 1.55f;
            else if (distance < 2.00f)
                closeMultiplier = Mathf.Lerp(1.25f, 1f, Mathf.InverseLerp(1.10f, 2.00f, distance));
            multiplier *= closeMultiplier;
        }

        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    private Vector2 ResolveWallNormal(Collider2D wall, Vector2 travel, Vector2 point)
    {
        float distance = Vector2.Distance(previousPosition, transform.position) + 0.16f;
        int mask = 1 << wall.gameObject.layer;
        RaycastHit2D hit = Physics2D.Raycast(previousPosition, travel, distance, mask);
        if (hit.collider == wall && hit.normal.sqrMagnitude > 0.0001f)
            return hit.normal;

        Vector2 centerToPoint = point - (Vector2)wall.bounds.center;
        if (centerToPoint.sqrMagnitude > 0.0001f)
            return centerToPoint.normalized;
        return -travel;
    }

    private static bool IsWall(GameObject target)
    {
        if (target == null)
            return false;

        int wallLayer = LayerMask.NameToLayer("Wall");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int boundaryLayer = LayerMask.NameToLayer("RoomBoundary");

        Transform current = target.transform;
        while (current != null)
        {
            GameObject gameObject = current.gameObject;
            if (wallLayer >= 0 && gameObject.layer == wallLayer) return true;
            if (obstacleLayer >= 0 && gameObject.layer == obstacleLayer) return true;
            if (boundaryLayer >= 0 && gameObject.layer == boundaryLayer) return true;

            string lower = gameObject.name.ToLowerInvariant();
            if (lower.Contains("wall") || lower.Contains("boundary") || lower.Contains("obstacle"))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void Terminate()
    {
        if (terminated)
            return;
        terminated = true;
        Destroy(gameObject);
    }
}
