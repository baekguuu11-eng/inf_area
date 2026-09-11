using UnityEngine;

[DisallowMultipleComponent]
public sealed class JHLProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private int damage;
    private float expiresAt;
    private bool consumed;
    private JHLBossController owner;
    private Vector2 velocity;

    public static JHLProjectile Create(JHLBossController boss, Vector2 position, Vector2 direction, float speed, int damage)
    {
        GameObject go = new GameObject("JHL_FingerProjectile");
        if (boss != null && boss.transform.parent != null)
            go.transform.SetParent(boss.transform.parent, true);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        int layer = LayerMask.NameToLayer("EnemyProjectile");
        if (layer >= 0) go.layer = layer;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = JHLRuntimeSprites.FilledCircle;
        sr.color = Color.white;
        sr.sortingOrder = 34;
        go.transform.localScale = Vector3.one * 0.24f;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.42f;

        JHLProjectile projectile = go.AddComponent<JHLProjectile>();
        projectile.owner = boss;
        projectile.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        projectile.speed = Mathf.Max(0.1f, speed);
        projectile.velocity = projectile.direction * projectile.speed * 0.58f;
        projectile.damage = Mathf.Max(1, damage);
        projectile.expiresAt = Time.time + 5f;
        if (boss != null) boss.RegisterSpawnedObject(go);
        return projectile;
    }

    private void Update()
    {
        if (consumed) return;
        float dt = Time.deltaTime;
        Vector2 desired = direction * speed;
        velocity = Vector2.MoveTowards(velocity, desired, speed * 4.5f * dt);
        transform.position += (Vector3)(velocity * dt);
        float stretch = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, speed));
        transform.localScale = new Vector3(0.24f + stretch * 0.08f, 0.24f - stretch * 0.035f, 1f);
        if (Time.time >= expiresAt)
            Consume();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null) return;
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null) return;
        player.TakeDamage(damage);
        Consume();
    }

    private void Consume()
    {
        if (consumed) return;
        consumed = true;
        Destroy(gameObject);
    }
}
