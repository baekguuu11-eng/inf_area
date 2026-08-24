using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMeleeAI : MonoBehaviour
{
    private enum AIState { Idle, Chase, Attack }
    private enum AttackType { Standard, Charge } // Standard: 제자리 판정 / Charge: 박치기(히트박스가 짧게 튀어나갔다 복귀)

    [Header("Detection")]
    [SerializeField] private float detectRange = 5f;
    [SerializeField] private float attackRange = 1.2f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Attack Type")]
    [SerializeField] private AttackType attackType = AttackType.Standard;

    [Header("Standard Attack Settings")]
    [SerializeField] private float attackWindup = 0.3f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 1;
    [Tooltip("실제 데미지 판정 반경. Attack Range보다 작으면 안 됨 (적이 윈드업 중 멈춰있기 때문에, 작으면 거의 항상 빗나감)")]
    [SerializeField] private float attackHitRadius = 1.4f;

    [Header("Charge Attack Settings (박치기 - 짧게 튀어나갔다 복귀)")]
    [Tooltip("튀어나가기 전 짧은 예고(멈칫) 시간")]
    [SerializeField] private float chargeWindup = 0.25f;
    [Tooltip("히트박스(몸체)가 앞으로 튀어나가는 거리")]
    [SerializeField] private float chargeDistance = 1.0f;
    [Tooltip("목표 지점까지 튀어나가는 데 걸리는 시간")]
    [SerializeField] private float chargeOutDuration = 0.12f;
    [Tooltip("원래 자리로 돌아오는 데 걸리는 시간")]
    [SerializeField] private float chargeReturnDuration = 0.18f;
    [Tooltip("복귀가 끝난 뒤 다시 움직이기 전 경직(회복) 시간")]
    [SerializeField] private float chargeRecovery = 0.35f;
    [SerializeField] private int chargeDamage = 2;

    [Header("Optional Visual Hook")]
    [Tooltip("있으면 IsChasing / IsAttacking / IsCharging bool을 보냄. 파라미터가 없으면 그냥 무시됨 (에러 없음)")]
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private Transform player;
    private AIState state = AIState.Idle;
    private bool isAttacking = false;

    private bool isCharging = false;
    private bool hasHitDuringCharge = false;

    private const float MinDirectionSqrMagnitude = 0.0001f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (animator == null)
            animator = GetComponent<Animator>();

        ValidateColliderOffset();
        ValidateAttackRanges();
    }

    private void ValidateColliderOffset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning($"[{name}] Collider2D가 없습니다. 벽 충돌/공격 판정이 제대로 동작하지 않습니다.");
            return;
        }

        float offsetMagnitude = col.offset.magnitude;
        float sizeReference = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y, 0.5f);

        if (offsetMagnitude > sizeReference * 2f)
        {
            Debug.LogWarning(
                $"[{name}] Collider2D의 Offset({col.offset})이 비정상적으로 큽니다. " +
                "Transform 위치와 실제 물리 판정 위치가 크게 어긋나 있을 수 있으니 Offset을 0,0에 가깝게 재설정하세요."
            );
        }
    }

    private void ValidateAttackRanges()
    {
        if (attackType == AttackType.Standard && attackHitRadius < attackRange)
        {
            Debug.LogWarning(
                $"[{name}] Attack Hit Radius({attackHitRadius})가 Attack Range({attackRange})보다 작습니다. " +
                "공격 판정이 자주 빗나갈 수 있으니 Attack Hit Radius를 Attack Range 이상으로 설정하세요."
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
            UpdateAnimator();
            return;
        }

        if (!isAttacking)
        {
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= attackRange)
                state = AIState.Attack;
            else if (distance <= detectRange)
                state = AIState.Chase;
            else
                state = AIState.Idle;

            if (state == AIState.Attack)
                StartCoroutine(AttackRoutine());
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

        if (isAttacking && attackType == AttackType.Charge)
            return;

        if (state == AIState.Chase && !isAttacking)
        {
            Vector2 toPlayer = (Vector2)player.position - rb.position;

            if (toPlayer.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                Vector2 direction = toPlayer.normalized;
                rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
            }
        }

        rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        if (attackType == AttackType.Charge)
            yield return ChargeAttackRoutine();
        else
            yield return StandardAttackRoutine();

        isAttacking = false;
    }

    private IEnumerator StandardAttackRoutine()
    {
        yield return new WaitForSeconds(attackWindup);

        if (player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= attackHitRadius)
            {
                DealDamageToPlayer(player.gameObject, attackDamage);
            }
        }

        yield return new WaitForSeconds(attackCooldown);
    }

    private IEnumerator ChargeAttackRoutine()
    {
        yield return new WaitForSeconds(chargeWindup);

        if (player == null)
            yield break;

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        Vector2 direction = toPlayer.sqrMagnitude > MinDirectionSqrMagnitude ? toPlayer.normalized : Vector2.down;

        Vector2 startPos = rb.position;
        Vector2 outTargetPos = startPos + direction * chargeDistance;

        isCharging = true;
        hasHitDuringCharge = false;

        float elapsed = 0f;
        float safeOutDuration = Mathf.Max(0.01f, chargeOutDuration);

        while (elapsed < safeOutDuration && isCharging)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeOutDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            rb.MovePosition(Vector2.Lerp(startPos, outTargetPos, smoothT));

            yield return new WaitForFixedUpdate();
        }

        isCharging = false;

        Vector2 returnStartPos = rb.position;
        elapsed = 0f;
        float safeReturnDuration = Mathf.Max(0.01f, chargeReturnDuration);

        while (elapsed < safeReturnDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeReturnDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            rb.MovePosition(Vector2.Lerp(returnStartPos, startPos, smoothT));

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(startPos);
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(chargeRecovery);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleChargeContact(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleChargeContact(collision.gameObject);
    }

    private void HandleChargeContact(GameObject other)
    {
        if (!isCharging)
            return;

        if (IsObstacle(other))
        {
            isCharging = false;
            return;
        }

        if (hasHitDuringCharge)
            return;

        if (other.CompareTag("Player"))
        {
            hasHitDuringCharge = true;
            isCharging = false;

            DealDamageToPlayer(other, chargeDamage);

            if (GameFeelManager.Instance != null)
            {
                GameFeelManager.Instance.DoHitStop(0.05f);
                GameFeelManager.Instance.Shake(0.12f, 0.1f);
            }
        }
    }

    private bool IsObstacle(GameObject obj)
    {
        return obj.layer == LayerMask.NameToLayer("Obstacle");
    }

    private void DealDamageToPlayer(GameObject playerObject, int damage)
    {
        PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerObject.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerObject.GetComponentInChildren<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.TakeDamage(damage);
        else
            Debug.LogWarning($"[{name}] PlayerHealth를 찾을 수 없어 데미지를 주지 못했습니다. Player 오브젝트 계층을 확인하세요.");
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetBool("IsChasing", state == AIState.Chase && !isAttacking);
        animator.SetBool("IsAttacking", isAttacking);
        animator.SetBool("IsCharging", isCharging);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (attackType == AttackType.Standard)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, attackHitRadius);
        }
        else
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, chargeDistance);
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}