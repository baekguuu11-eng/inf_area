using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Wall Impact Effect")]
    [Tooltip("벽에 부딪혔을 때 튀는 파편 프리팹 (EnemyDeathShard 스크립트가 붙은 프리팹 재사용 가능)")]
    [SerializeField] private GameObject wallImpactShardPrefab;
    [SerializeField] private int wallImpactShardCount = 4;
    [SerializeField] private float wallImpactSpawnRadius = 0.05f;

    private Rigidbody2D rb;
    private int damage = 1;
    private float lifetime = 4f; // 화면 밖으로 벗어난 탄환이 무한히 남지 않도록 일정 시간 뒤 삭제

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Setup(Vector2 direction, float speed, int dmg)
    {
        damage = dmg;
        rb.linearVelocity = direction.normalized * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 다른 적이나 자기 자신과의 충돌은 무시
        if (collision.CompareTag("Enemy"))
            return;

        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = collision.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 장애물 타일맵이나 벽 레이어("Obstacle")에 닿아도 탄환 소멸 + 파편 이펙트 재생
        if (collision.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            SpawnWallFragments(transform.position, rb.linearVelocity);
            Destroy(gameObject);
        }
    }

    private void SpawnWallFragments(Vector3 position, Vector2 travelDirection)
    {
        if (wallImpactShardPrefab == null)
            return;

        // 탄환이 날아가던 방향의 반대쪽으로 파편이 튀어나가서 "벽에 맞고 튕겨나온" 느낌을 줌
        Vector2 biasDirection = travelDirection.sqrMagnitude > 0.0001f
            ? -travelDirection.normalized
            : Vector2.zero;

        for (int i = 0; i < wallImpactShardCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * wallImpactSpawnRadius;

            GameObject shard = Instantiate(
                wallImpactShardPrefab,
                position + (Vector3)randomOffset,
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))
            );

            EnemyDeathShard shardScript = shard.GetComponent<EnemyDeathShard>();
            if (shardScript != null)
                shardScript.Launch(biasDirection, 0.9f);
        }
    }
}
