using UnityEngine;

public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Death Effects")]
    [SerializeField] private GameObject shardPrefab;
    [SerializeField] private int shardCount = 6;
    [SerializeField] private float shardSpawnRadius = 0.15f;
    [SerializeField] private float shardBiasStrength = 0.9f;

    [Header("Game Feel")]
    [SerializeField] private float deathHitStopDuration = 0.05f;
    [SerializeField] private float deathShakeDuration = 0.10f;
    [SerializeField] private float deathShakeMagnitude = 0.08f;

    public void PlayDeath(Vector3 worldPosition, Vector2 lastHitDirection)
    {
        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(deathHitStopDuration);
            GameFeelManager.Instance.Shake(deathShakeDuration, deathShakeMagnitude);
        }

        SpawnShards(worldPosition, lastHitDirection);
    }

    private void SpawnShards(Vector3 worldPosition, Vector2 lastHitDirection)
    {
        if (shardPrefab == null)
            return;

        for (int i = 0; i < shardCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * shardSpawnRadius;

            GameObject shard = Instantiate(
                shardPrefab,
                (Vector2)worldPosition + randomOffset,
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))
            );

            EnemyDeathShard shardScript = shard.GetComponent<EnemyDeathShard>();
            if (shardScript != null)
                shardScript.Launch(lastHitDirection, shardBiasStrength);
        }
    }
}