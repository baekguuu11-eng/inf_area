using System.Collections.Generic;
using UnityEngine;

public static class JHLBossRuntimeFactory
{
    private const int BossMaxHealth = 3000;

    public static JHLBossController Create(RoomController room, MapManager mapManager)
    {
        if (room == null) return null;
        GameObject root = new GameObject("Boss_JHL_V25");
        root.transform.SetParent(room.transform, false);
        root.transform.position = room.EnemySpawnArea.bounds.center;
        root.layer = LayerMask.NameToLayer("Enemy");
        EnemyHealth health = root.AddComponent<EnemyHealth>();
        JHLBossController bridge = root.AddComponent<JHLBossController>();
        JHLCombatV25 combat = root.AddComponent<JHLCombatV25>();
        combat.Initialize(mapManager, room, health);
        bridge.InitializeV25(combat);
        return bridge;
    }

    private static Bounds ResolveRoomCombatBounds(RoomController room, BoxCollider2D fallback)
    {
        if (RoomLayoutV24.TryGetBounds(room, out Bounds layoutBounds)) return layoutBounds;
        if (room != null)
        {
            Transform left = room.GetSpawnPoint(GateDirection.Left);
            Transform right = room.GetSpawnPoint(GateDirection.Right);
            Transform top = room.GetSpawnPoint(GateDirection.Top);
            Transform bottom = room.GetSpawnPoint(GateDirection.Bottom);
            if (left != null && right != null && top != null && bottom != null)
            {
                const float edgePadding = 0.34f;
                float minX = Mathf.Min(left.position.x, right.position.x) - edgePadding;
                float maxX = Mathf.Max(left.position.x, right.position.x) + edgePadding;
                float minY = Mathf.Min(bottom.position.y, top.position.y) - edgePadding;
                float maxY = Mathf.Max(bottom.position.y, top.position.y) + edgePadding;
                return new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                    new Vector3(Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY), 1f));
            }
        }
        if (fallback != null) return fallback.bounds;
        return new Bounds(room != null ? room.transform.position : Vector3.zero, new Vector3(14f, 8f, 1f));
    }

    private static Transform BuildWhitePart(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 worldSize,
        int enemyLayer,
        int sortingOrder,
        List<SpriteRenderer> renderers,
        out Transform visual,
        out BoxCollider2D hurtbox)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        if (enemyLayer >= 0) part.layer = enemyLayer;

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(part.transform, false);
        visualObject.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
        SpriteRenderer sr = visualObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder;
        renderers.Add(sr);
        visual = visualObject.transform;

        hurtbox = part.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = worldSize;
        hurtbox.offset = Vector2.zero;
        return part.transform;
    }


    private static void AddSolidHandBarrier(Transform hand, Vector2 worldSize)
    {
        if (hand == null) return;
        GameObject barrierObject = new GameObject("SolidHandBarrier");
        barrierObject.transform.SetParent(hand, false);
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0) barrierObject.layer = obstacleLayer;

        PolygonCollider2D collider = barrierObject.AddComponent<PolygonCollider2D>();
        collider.isTrigger = false;
        // Default state is pass-through. Individual wall-control patterns opt in explicitly.
        collider.enabled = false;
        const int pointCount = 18;
        Vector2[] points = new Vector2[pointCount];
        float radiusX = Mathf.Max(0.1f, worldSize.x * 0.455f);
        float radiusY = Mathf.Max(0.1f, worldSize.y * 0.455f);
        for (int i = 0; i < pointCount; i++)
        {
            float angle = Mathf.PI * 2f * i / pointCount;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        collider.points = points;

        Rigidbody2D body = hand.GetComponent<Rigidbody2D>();
        if (body == null) body = hand.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.useFullKinematicContacts = true;

        JHLHandBarrier barrier = barrierObject.AddComponent<JHLHandBarrier>();
        barrier.Initialize(collider);
    }

    private static Transform CreateMarker(Transform parent, string name, Vector3 localPosition)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        return marker.transform;
    }
}
