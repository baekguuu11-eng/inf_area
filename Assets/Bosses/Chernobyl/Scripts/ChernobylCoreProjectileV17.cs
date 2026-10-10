using UnityEngine;

// Swept collision avoids tunnelling at low frame rates; tied to the owning encounter.
public sealed class ChernobylCoreProjectileV17 : MonoBehaviour
{
    private ChernobylBossController owner;
    private PlayerHealth player;
    private Vector2 velocity;
    private float expires;
    private const float Radius = 0.13f;

    public static GameObject Create(ChernobylBossController owner, PlayerHealth player,
        Vector2 position, Vector2 direction, float speed, Color color)
    {
        // V20: retained type for import compatibility; Chernobyl no longer creates projectiles.
        return null;
    }

    private void Update()
    {
        if (owner == null || owner.IsDead || player == null || player.IsDead || Time.time >= expires)
        { Destroy(gameObject); return; }
        Vector2 start = transform.position;
        float distance = velocity.magnitude * Time.deltaTime;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, Radius, velocity.normalized, distance);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D col = hit.collider;
            if (col == null || col.GetComponentInParent<ChernobylBossController>() == owner) continue;
            PlayerHealth target = col.GetComponentInParent<PlayerHealth>();
            if (target != null && !col.isTrigger)
            { target.TakeDamage(1); Destroy(gameObject); return; }
            string layer = LayerMask.LayerToName(col.gameObject.layer);
            if (!col.isTrigger && (layer == "Wall" || layer == "Obstacle" || layer == "RoomBoundary"))
            { Destroy(gameObject); return; }
        }
        transform.position = start + velocity * Time.deltaTime;
    }
}
