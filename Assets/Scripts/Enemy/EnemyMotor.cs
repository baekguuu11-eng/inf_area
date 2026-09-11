using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public sealed class EnemyMotor : MonoBehaviour
{
    [Header("이동 반응")]
    [SerializeField, Min(0.1f)] private float acceleration = 18f;
    [SerializeField, Min(0.1f)] private float deceleration = 24f;
    [SerializeField, Range(0.05f, 1f)] private float turnResponsiveness = 0.72f;

    [Header("적 간 분리")]
    [SerializeField, Min(0.1f)] private float separationRadius = 0.9f;
    [SerializeField, Min(0f)] private float separationStrength = 1.45f;

    [Header("벽 회피")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField, Min(0.05f)] private float wallProbeDistance = 0.45f;
    [SerializeField, Min(0.03f)] private float minimumProbeRadius = 0.12f;

    [Header("딱딱한 넉백")]
    [SerializeField, Min(0.01f)] private float minimumKnockbackDuration = 0.04f;
    [SerializeField, Min(0.01f)] private float maximumKnockbackDuration = 0.10f;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private EnemyRole role;
    private EnemyVisualMotion visualMotion;

    private Vector2 desiredDirection;
    private float desiredSpeed;
    private Vector2 currentVelocity;
    private bool movementEnabled = true;
    private float externalSpeedMultiplier = 1f;

    private bool knockbackActive;
    private Vector2 knockbackDirection;
    private float knockbackDistance;
    private float knockbackDuration;
    private float knockbackElapsed;
    private float knockbackPreviousCurve;

    public Rigidbody2D Body => body;
    public Vector2 CurrentVelocity => currentVelocity;
    public bool IsKnockedBack => knockbackActive;
    public bool MovementEnabled => movementEnabled;
    public float ExternalSpeedMultiplier => externalSpeedMultiplier;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        role = GetComponent<EnemyRole>();
        visualMotion = GetComponent<EnemyVisualMotion>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void OnDisable()
    {
        desiredDirection = Vector2.zero;
        desiredSpeed = 0f;
        currentVelocity = Vector2.zero;
        knockbackActive = false;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    public void ConfigureAvoidance(float radius, float strength, LayerMask obstacles)
    {
        separationRadius = Mathf.Max(0.1f, radius);
        separationStrength = Mathf.Max(0f, strength);
        wallMask = obstacles;
    }

    public void SetMoveIntent(Vector2 direction, float speed)
    {
        desiredDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        desiredSpeed = Mathf.Max(0f, speed);
    }

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        externalSpeedMultiplier = Mathf.Clamp(multiplier, 0.25f, 1.5f);
    }

    public void StopIntent()
    {
        desiredDirection = Vector2.zero;
        desiredSpeed = 0f;
    }

    public void SetMovementEnabled(bool enabled, bool stopImmediately = false)
    {
        movementEnabled = enabled;
        if (!enabled)
            StopIntent();
        if (stopImmediately)
        {
            currentVelocity = Vector2.zero;
            if (body != null)
                body.linearVelocity = Vector2.zero;
        }
    }

    public void ApplyKnockback(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
            return;

        float resistance = role != null ? role.KnockbackResistance : 0f;
        float effectiveDistance = Mathf.Max(0f, distance * (1f - resistance));
        if (effectiveDistance <= 0.003f)
            return;

        knockbackDirection = direction.normalized;
        knockbackDistance = Mathf.Max(knockbackDistance * 0.35f, effectiveDistance);
        knockbackDuration = Mathf.Clamp(duration, minimumKnockbackDuration, maximumKnockbackDuration);
        knockbackElapsed = 0f;
        knockbackPreviousCurve = 0f;
        knockbackActive = true;
        currentVelocity = Vector2.zero;
        body.linearVelocity = Vector2.zero;
    }

    public bool MoveScripted(Vector2 delta)
    {
        if (body == null || delta.sqrMagnitude <= 0.0000001f)
            return false;

        float distance = delta.magnitude;
        Vector2 direction = delta / distance;
        if (WouldHitWall(direction, distance))
            return false;

        body.MovePosition(body.position + delta);
        return true;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (knockbackActive)
        {
            UpdateKnockback();
            FeedVisualMotion();
            return;
        }

        if (!movementEnabled || GameInputState.IsLocked)
        {
            currentVelocity = Vector2.MoveTowards(currentVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
            body.linearVelocity = Vector2.zero;
            FeedVisualMotion();
            return;
        }

        Vector2 targetVelocity = Vector2.zero;
        if (desiredDirection.sqrMagnitude > 0.0001f && desiredSpeed > 0f)
        {
            Vector2 steered = BuildSteeredDirection(desiredDirection);
            targetVelocity = steered * desiredSpeed * externalSpeedMultiplier;
        }

        float rate = targetVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        Vector2 velocityStep = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        currentVelocity = Vector2.Lerp(velocityStep, targetVelocity, turnResponsiveness * Time.fixedDeltaTime * 10f);

        Vector2 movement = currentVelocity * Time.fixedDeltaTime;
        if (movement.sqrMagnitude > 0.0000001f)
        {
            if (!WouldHitWall(movement.normalized, movement.magnitude))
                body.MovePosition(body.position + movement);
            else
                currentVelocity = Vector2.zero;
        }

        body.linearVelocity = Vector2.zero;
        FeedVisualMotion();
    }

    private void UpdateKnockback()
    {
        float nextElapsed = Mathf.Min(knockbackDuration, knockbackElapsed + Time.fixedDeltaTime);
        float t = Mathf.Clamp01(nextElapsed / Mathf.Max(0.001f, knockbackDuration));

        // 초반에 대부분의 거리를 즉시 이동하고 마지막에 딱 멈추는 곡선입니다.
        float curve = 1f - Mathf.Pow(1f - t, 4f);
        float stepDistance = Mathf.Max(0f, curve - knockbackPreviousCurve) * knockbackDistance;
        knockbackPreviousCurve = curve;
        knockbackElapsed = nextElapsed;

        if (stepDistance > 0.0001f && !WouldHitWall(knockbackDirection, stepDistance))
            body.MovePosition(body.position + knockbackDirection * stepDistance);

        currentVelocity = knockbackDirection * (stepDistance / Mathf.Max(0.0001f, Time.fixedDeltaTime));
        body.linearVelocity = Vector2.zero;

        if (knockbackElapsed >= knockbackDuration - 0.0001f)
        {
            knockbackActive = false;
            currentVelocity = Vector2.zero;
            knockbackDistance = 0f;
        }
    }

    private Vector2 BuildSteeredDirection(Vector2 desired)
    {
        Vector2 separation = Vector2.zero;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(body.position, Mathf.Max(0.1f, separationRadius));
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider2D hit = nearby[i];
            if (hit == null || hit.attachedRigidbody == body)
                continue;

            EnemyHealth other = hit.GetComponentInParent<EnemyHealth>();
            if (other == null || other.IsDead)
                continue;

            Vector2 away = body.position - (Vector2)hit.bounds.center;
            float distance = Mathf.Max(0.04f, away.magnitude);
            float strength01 = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, separationRadius));
            separation += away.normalized * strength01;
        }

        Vector2 result = desired + separation * separationStrength;
        if (result.sqrMagnitude < 0.0001f)
            result = desired;
        result.Normalize();

        if (!WouldHitWall(result, wallProbeDistance))
            return result;

        Vector2 left = new Vector2(-result.y, result.x);
        Vector2 right = -left;
        bool leftBlocked = WouldHitWall(left, wallProbeDistance * 0.9f);
        bool rightBlocked = WouldHitWall(right, wallProbeDistance * 0.9f);

        if (!leftBlocked && !rightBlocked)
            return Vector2.Dot(left, desired) >= Vector2.Dot(right, desired) ? left : right;
        if (!leftBlocked)
            return left;
        if (!rightBlocked)
            return right;
        return Vector2.zero;
    }

    private bool WouldHitWall(Vector2 direction, float distance)
    {
        if (wallMask.value == 0 || direction.sqrMagnitude < 0.0001f || distance <= 0f)
            return false;

        float radius = minimumProbeRadius;
        if (bodyCollider != null)
            radius = Mathf.Max(minimumProbeRadius, Mathf.Min(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) * 0.72f);

        RaycastHit2D hit = Physics2D.CircleCast(body.position, radius, direction.normalized, distance, wallMask);
        return hit.collider != null;
    }

    private void FeedVisualMotion()
    {
        if (visualMotion != null)
            visualMotion.SetDesiredVelocity(currentVelocity);
    }
}
