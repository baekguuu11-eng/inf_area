using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerProjectile : MonoBehaviour
{
    [Header("Wall Impact Effect")]
    [Tooltip("벽에 부딪혔을 때 튀는 파편 프리팹")]
    [SerializeField] private GameObject wallImpactShardPrefab;
    [SerializeField] private int wallImpactShardCount = 4;
    [SerializeField] private float wallImpactSpawnRadius = 0.05f;

    private Rigidbody2D rb;
    private int damage = 1;
    private float lifetime = 3f;

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
        if (collision.CompareTag("Player")) return;

        EnemyHealth enemy = collision.GetComponent<EnemyHealth>();
        if (enemy == null)
        {
            enemy = collision.GetComponentInParent<EnemyHealth>();
        }

        if (enemy != null)
        {
            Vector2 hitDir = rb.linearVelocity.normalized;
            enemy.TakeDamage(damage, hitDir);

            Destroy(gameObject);
            return;
        }

        // 장애물 타일맵이나 벽에 닿았을 때 파편 튀기고 삭제
        if (collision.gameObject.layer == LayerMask.NameToLayer("Obstacle") || collision.CompareTag("Wall"))
        {
            SpawnWallFragments(transform.position, rb.linearVelocity);
            Destroy(gameObject);
        }
    }

    private void SpawnWallFragments(Vector3 position, Vector2 travelDirection)
    {
        if (wallImpactShardPrefab == null)
            return;

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