using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBomberAI : MonoBehaviour
{
    private enum AIState { Idle, Chase }

    [Header("Detection")]
    [SerializeField] private float detectRange = 5f;

    [Header("Movement")]
    [Tooltip("기본적으로 다른 적보다 빠르게 잡는 걸 추천 (예: 근거리 적 2.5보다 높게)")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 1.2f;
    [SerializeField] private int explosionDamage = 2;

    [Header("Optional Visual Hook")]
    [Tooltip("있으면 폭발 시 파편 스폰 + 히트스탑/카메라 흔들림을 같이 재생함 (EnemyHealth 없이도 독립 동작)")]
    [SerializeField] private EnemyDeathEffect deathEffect;

    private Rigidbody2D rb;
    private Transform player;
    private AIState state = AIState.Idle;
    private bool hasExploded = false;

    private const float MinDirectionSqrMagnitude = 0.0001f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (deathEffect == null)
            deathEffect = GetComponentInChildren<EnemyDeathEffect>(true);

        ValidateColliderOffset();
    }

    private void ValidateColliderOffset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning($"[{name}] Collider2D가 없습니다. 벽 충돌/접촉 폭발 판정이 제대로 동작하지 않습니다.");
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

    private void Update()
    {
        if (player == null || hasExploded)
            return;

        if (GameInputState.IsLocked)
        {
            state = AIState.Idle;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        state = distance <= detectRange ? AIState.Chase : AIState.Idle;
    }

    private void FixedUpdate()
    {
        if (player == null || GameInputState.IsLocked || hasExploded || state != AIState.Chase)
        {
            if (rb != null)
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
        rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = Vector2.zero;
    }

    // 벽 충돌처럼 물리 충돌(Is Trigger 꺼짐)로 부딪힐 때
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryExplodeOnPlayerContact(collision.gameObject);
    }

    // 혹시 Is Trigger로 설정된 콜라이더를 쓰는 경우도 함께 대응
    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryExplodeOnPlayerContact(collision.gameObject);
    }

    private void TryExplodeOnPlayerContact(GameObject other)
    {
        if (hasExploded)
            return;

        if (!other.CompareTag("Player"))
            return;

        Explode();
    }

    private void Explode()
    {
        hasExploded = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // 폭발 반경 안의 플레이어에게 데미지 (접촉한 콜라이더 하나가 아니라 범위 전체를 판정)
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Player"))
                continue;

            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = hit.GetComponentInChildren<PlayerHealth>();

            if (playerHealth != null)
                playerHealth.TakeDamage(explosionDamage);
        }

        // 연출: 카메라 흔들림 / 히트스탑 + 파편 이펙트 (있으면)
        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(0.05f);
            GameFeelManager.Instance.Shake(0.15f, 0.12f);
        }

        if (deathEffect != null)
            deathEffect.PlayDeath(transform.position, Vector2.zero);

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = new Color(1f, 0.3f, 0f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}