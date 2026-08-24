using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyRangedAI : MonoBehaviour
{
    private enum AIState { Idle, Chase, Attack, Kite }

    [Header("Detection Zones (Kite < Attack < Detect 순서여야 함)")]
    [Tooltip("이 범위 안에 들어오면 추적 시작")]
    [SerializeField] private float detectRange = 6f;
    [Tooltip("이 범위 안이면 멈춰서 공격. 이보다 멀어지면 다시 쫓아감")]
    [SerializeField] private float attackRange = 4f;
    [Tooltip("이보다 가까워지면 플레이어에게서 멀어짐 (도망)")]
    [SerializeField] private float kiteRange = 2f;

    [Header("State Transition (부드러운 전환)")]
    [SerializeField] private float stateChangeDelay = 0.15f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;      // Chase 속도
    [SerializeField] private float retreatSpeed = 2.5f; // Kite 속도

    [Header("Attack (Attack 구역에서 멈춰서 발사)")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float fireInterval = 1.5f;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private int projectileDamage = 1;

    [Header("Optional Animator Hook")]
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private Transform player;

    private AIState state = AIState.Idle;
    private AIState pendingState = AIState.Idle;
    private float stateChangeTimer = 0f;

    private float nextFireTime = 0f;

    private const float MinDirectionSqrMagnitude = 0.0001f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (firePoint == null)
            firePoint = transform;

        if (animator == null)
            animator = GetComponent<Animator>();

        ValidateColliderOffset();
        ValidateZoneOrder();
    }

    private void ValidateColliderOffset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning($"[{name}] Collider2D가 없습니다. 벽 충돌 판정이 제대로 동작하지 않습니다.");
            return;
        }

        float offsetMagnitude = col.offset.magnitude;
        float sizeReference = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y, 0.5f);

        if (offsetMagnitude > sizeReference * 2f)
        {
            Debug.LogWarning(
                $"[{name}] Collider2D의 Offset({col.offset})이 비정상적으로 큽니다. " +
                "Offset을 0,0에 가깝게 재설정하세요."
            );
        }
    }

    private void ValidateZoneOrder()
    {
        if (!(kiteRange < attackRange && attackRange < detectRange))
        {
            Debug.LogWarning(
                $"[{name}] 구역 크기 순서가 잘못되었습니다 (Kite: {kiteRange}, Attack: {attackRange}, Detect: {detectRange}). " +
                "Kite Range < Attack Range < Detect Range 순서로 설정해야 정상 동작합니다."
            );
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        if (GameInputState.IsLocked)
        {
            state = AIState.Idle;
            pendingState = AIState.Idle;
            stateChangeTimer = 0f;
            UpdateAnimator();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        AIState rawState;
        if (distance <= kiteRange)
            rawState = AIState.Kite;
        else if (distance <= attackRange)
            rawState = AIState.Attack;
        else if (distance <= detectRange)
            rawState = AIState.Chase;
        else
            rawState = AIState.Idle;

        if (rawState != pendingState)
        {
            pendingState = rawState;
            stateChangeTimer = 0f;
        }
        else
        {
            stateChangeTimer += Time.deltaTime;

            if (stateChangeTimer >= stateChangeDelay)
                state = pendingState;
        }

        if (state == AIState.Attack && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireInterval;
            FireAtPlayer();
        }

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (player == null || GameInputState.IsLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - rb.position;

        if (toPlayer.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = toPlayer.normalized;

        switch (state)
        {
            case AIState.Chase:
                rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
                break;

            case AIState.Kite:
                rb.MovePosition(rb.position - direction * retreatSpeed * Time.fixedDeltaTime);
                break;

            case AIState.Attack:
            case AIState.Idle:
                break;
        }

        rb.linearVelocity = Vector2.zero;
    }

    private void FireAtPlayer()
    {
        if (projectilePrefab == null || player == null)
            return;

        Vector2 direction = ((Vector2)player.position - (Vector2)firePoint.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        GameObject projObj = Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0f, 0f, angle));

        EnemyProjectile projectile = projObj.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Setup(direction, projectileSpeed, projectileDamage);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetBool("IsChasing", state == AIState.Chase);
        animator.SetBool("IsAttacking", state == AIState.Attack);
        animator.SetBool("IsKiting", state == AIState.Kite);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, kiteRange);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}