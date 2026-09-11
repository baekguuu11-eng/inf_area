using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDeathEffect : MonoBehaviour
{
    [Header("파편")]
    [SerializeField] private GameObject shardPrefab;
    [SerializeField] private int shardCount = 8;
    [SerializeField] private int tankShardBonus = 6;
    [SerializeField] private float shardSpawnRadius = 0.12f;
    [SerializeField] private float shardBiasStrength = 1.65f;
    [SerializeField, Range(10f, 160f)] private float directionalSpreadDegrees = 70f;

    [Header("게임 필")]
    [SerializeField] private float deathHitStopDuration = 0.038f;
    [SerializeField] private float deathShakeDuration = 0.11f;
    [SerializeField] private float deathShakeMagnitude = 0.095f;

    private EnemyRole role;
    private SpriteRenderer sourceRenderer;

    private void Awake()
    {
        role = GetComponent<EnemyRole>();
        if (role == null)
            role = GetComponentInParent<EnemyRole>();
        sourceRenderer = FindBodyRenderer();
    }

    public void PlayDeath(Vector3 worldPosition, Vector2 lastHitDirection)
    {
        PlayDeath(worldPosition, DamageContext.Simple(1, lastHitDirection, EnemyHitKind.Melee, (Vector2)worldPosition));
    }

    public void PlayDeath(Vector3 worldPosition, DamageContext context)
    {
        EnemyRole.Role resolvedRole = role != null ? role.CurrentRole : EnemyRole.Role.Melee;
        CombatImpactFXV11.EmitEnemyDeath(worldPosition, context.Direction, resolvedRole);
        Vector2 direction = context.Direction.sqrMagnitude > 0.0001f ? context.Direction.normalized : Vector2.up;
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null)
        {
            feedback.HitStop(deathHitStopDuration);
            feedback.Shake(deathShakeDuration, deathShakeMagnitude, direction);
        }
        else if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(deathHitStopDuration);
            GameFeelManager.Instance.DirectionalShake(deathShakeDuration, deathShakeMagnitude, direction);
        }

        CombatPostProcessV61.PulseImpact(context.HitKind, true);
        SpawnShards(worldPosition, direction);
    }

    private void SpawnShards(Vector3 worldPosition, Vector2 direction)
    {
        int count = Mathf.Max(1, shardCount);
        if (role != null && role.CurrentRole == EnemyRole.Role.Tank)
            count += Mathf.Max(0, tankShardBonus);
        else if (role != null && role.CurrentRole == EnemyRole.Role.Bomber)
            count += 3;

        EnemyShardPaletteV61.Palette palette = EnemyShardPaletteV61.Resolve(role, sourceRenderer);
        int sortingLayerId = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
        int sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder + 1 : 8;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * shardSpawnRadius;
            EnemyDeathShard shard = EnemyShardPoolV61.Spawn(
                shardPrefab,
                worldPosition + (Vector3)offset,
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

            bool emissive;
            Color color = palette.Pick(Random.value, out emissive);
            color.a = 1f;

            SpriteRenderer renderer = shard.Renderer;
            if (renderer != null)
                renderer.sortingLayerID = sortingLayerId;

            int shape = Random.Range(0, 5);
            float sizeMultiplier = Random.Range(0.72f, 1.28f);
            float lifeMultiplier = Random.Range(0.82f, 1.22f);

            if (role != null && role.CurrentRole == EnemyRole.Role.Tank)
            {
                sizeMultiplier *= 1.28f;
                lifeMultiplier *= 1.20f;
            }
            else if (role != null && role.CurrentRole == EnemyRole.Role.Ranged)
            {
                sizeMultiplier *= 0.88f;
                lifeMultiplier *= 0.90f;
            }
            else if (role != null && role.CurrentRole == EnemyRole.Role.Bomber)
            {
                sizeMultiplier *= Random.Range(0.78f, 1.12f);
            }

            int resolvedSortingOrder = sortingOrder;
            if (emissive)
            {
                sizeMultiplier *= 0.72f;
                lifeMultiplier *= 0.72f;
                resolvedSortingOrder += 1;
            }

            shard.ConfigureVisual(
                RuntimePixelSpriteFactory.GetEnemyShardSprite(shape),
                color,
                sizeMultiplier,
                lifeMultiplier,
                resolvedSortingOrder);

            float spread = directionalSpreadDegrees;
            float bias = shardBiasStrength;
            if (role != null && role.CurrentRole == EnemyRole.Role.Bomber)
            {
                spread = 155f;
                bias = 0.92f;
            }
            else if (role != null && role.CurrentRole == EnemyRole.Role.Tank)
            {
                spread = 82f;
                bias = 1.25f;
            }

            shard.LaunchDirectional(direction, spread, bias);
        }
    }

    private SpriteRenderer FindBodyRenderer()
    {
        Transform body = transform.Find("VisualRoot/BodyVisual");
        if (body == null && transform.parent != null)
            body = transform.parent.Find("VisualRoot/BodyVisual");
        if (body != null)
        {
            SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
            if (renderer != null)
                return renderer;
        }

        SpriteRenderer[] renderers = GetComponentsInParent<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null || candidate.sprite == null)
                continue;
            string lower = candidate.gameObject.name.ToLowerInvariant();
            if (!lower.Contains("shadow") && !lower.Contains("telegraph") && !lower.Contains("particle"))
                return candidate;
        }

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null || candidate.sprite == null)
                continue;
            string lower = candidate.gameObject.name.ToLowerInvariant();
            if (!lower.Contains("shadow") && !lower.Contains("telegraph") && !lower.Contains("particle"))
                return candidate;
        }
        return null;
    }
}
