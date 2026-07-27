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
    [Tooltip("구역이 바뀌어도 이 시간 이상 유지되어야 실제로 상태가 전환됨. 경계선에서 떨리는 현상 방지")]
    [SerializeField] private float stateChangeDelay = 0.15f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;      // Chase 속도
    [SerializeField] private float retreatSpeed = 2.5f; // Kite 속도

    [Header("Attack (Attack 구역에서 멈춰서 발사)")]
    [SerializeField] private Transform firePoint;          // 비워두면 자기 자신 위치에서 발사
    [SerializeField] private GameObject projectilePrefab;  // EnemyProjectile이 붙어있는 프리팹
    [SerializeField] private float fireInterval = 1.5f;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private int projectileDamage = 1;

    private Rigidbody2D rb;
    private Transform player;

    private AIState state = AIState.Idle;         // 실제로 이동/공격에 반영되는 확정 상태
    private AIState pendingState = AIState.Idle;  // 이번 프레임에 감지된 후보 상태
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

    // [버그 방지용 자동 점검] 세 구역이 Kite < Attack < Detect 순서가 아니면
    // 도망가야 할 때 공격하거나, 공격해야 할 때 도망가는 등 의도와 다르게 동작할 수 있음.
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

        // [부드러운 전환] 조건이 바뀌자마자 즉시 상태를 바꾸지 않고,
        // stateChangeDelay 동안 같은 조건이 유지될 때만 실제 상태(state)에 반영함.
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

        // Attack 구역(멈춰서 사격)에 있을 때만 주기적으로 발사
        if (state == AIState.Attack && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireInterval;
            FireAtPlayer();
        }
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
                // Attack 구역에서는 멈춰서 사격, Idle은 그냥 대기
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