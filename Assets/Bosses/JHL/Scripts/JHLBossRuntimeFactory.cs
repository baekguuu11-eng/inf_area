using System.Collections.Generic;
using UnityEngine;

public static class JHLBossRuntimeFactory
{
    private const int BossMaxHealth = 3000;

    public static JHLBossController Create(RoomController room, MapManager mapManager)
    {
        if (room == null) return null;

        BoxCollider2D arena = room.EnemySpawnArea;
        Bounds bounds = ResolveRoomCombatBounds(room, arena);
        Camera cam = Camera.main;
        float screenHeight = cam != null && cam.orthographic ? cam.orthographicSize * 2f : Mathf.Max(8f, bounds.size.y);
        float screenWidth = cam != null && cam.orthographic ? screenHeight * cam.aspect : Mathf.Max(14f, bounds.size.x);

        // V4 screen-first layout: the boss is the arena, not an object sitting inside it.
        // Placeholder art stays intentionally primitive: a wide white face block and two oversized white circular hands.
        Vector2 faceSize = new Vector2(screenWidth * 0.50f, screenHeight * 0.30f);
        Vector2 handSize = new Vector2(screenWidth * 0.38f, screenHeight * 0.45f);

        GameObject root = new GameObject("Boss_JHL");
        root.transform.SetParent(room.transform, true);
        root.transform.position = bounds.center;
        root.transform.position = new Vector3(root.transform.position.x, root.transform.position.y, 0f);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;

        EnemyHealth health = root.AddComponent<EnemyHealth>();
        JHLBossController controller = root.AddComponent<JHLBossController>();
        JHLCombatEffects combatEffects = JHLCombatEffects.Create(root);

        GameObject visualRootObject = new GameObject("VisualRoot");
        visualRootObject.transform.SetParent(root.transform, false);
        Transform visualRoot = visualRootObject.transform;

        List<SpriteRenderer> allRenderers = new List<SpriteRenderer>();

        Transform face = BuildWhitePart(
            visualRoot,
            "Face",
            JHLRuntimeSprites.WhitePixel,
            faceSize,
            enemyLayer,
            30,
            allRenderers,
            out Transform faceVisual,
            out BoxCollider2D faceHurtbox);

        Transform leftHand = BuildWhitePart(
            visualRoot,
            "LeftHand",
            JHLRuntimeSprites.FilledCircle,
            handSize,
            enemyLayer,
            31,
            allRenderers,
            out Transform leftVisual,
            out BoxCollider2D leftHurtbox);

        Transform rightHand = BuildWhitePart(
            visualRoot,
            "RightHand",
            JHLRuntimeSprites.FilledCircle,
            handSize,
            enemyLayer,
            31,
            allRenderers,
            out Transform rightVisual,
            out BoxCollider2D rightHurtbox);

        AddSolidHandBarrier(leftHand, handSize);
        AddSolidHandBarrier(rightHand, handSize);

        Transform faceFireOrigin = CreateMarker(face, "FaceFireOrigin", new Vector3(0f, -faceSize.y * 0.48f, 0f));
        Transform leftFingerTip = CreateMarker(leftHand, "FingerFireOrigin", new Vector3(handSize.x * 0.42f, 0f, 0f));
        Transform rightFingerTip = CreateMarker(rightHand, "FingerFireOrigin", new Vector3(-handSize.x * 0.42f, 0f, 0f));

        JHLPartMotion faceMotion = face.gameObject.AddComponent<JHLPartMotion>();
        JHLPartMotion leftMotion = leftHand.gameObject.AddComponent<JHLPartMotion>();
        JHLPartMotion rightMotion = rightHand.gameObject.AddComponent<JHLPartMotion>();

        Vector3 cameraCenter = cam != null ? cam.transform.position : bounds.center;
        cameraCenter.z = 0f;
        float halfW = screenWidth * 0.5f;
        float halfH = screenHeight * 0.5f;
        Vector3 faceStart = cameraCenter + new Vector3(0f, halfH + faceSize.y, 0f);
        Vector3 leftStart = cameraCenter + new Vector3(-halfW - handSize.x, -halfH * 0.05f, 0f);
        Vector3 rightStart = cameraCenter + new Vector3(halfW + handSize.x, -halfH * 0.05f, 0f);

        faceMotion.Initialize(faceVisual, faceStart, faceVisual.localScale);
        leftMotion.Initialize(leftVisual, leftStart, leftVisual.localScale);
        rightMotion.Initialize(rightVisual, rightStart, rightVisual.localScale);

        // Heavy defaults. Pattern code can temporarily raise speed/acceleration for attacks.
        faceMotion.SetKinematicProfile(15f, 45f, 72f);
        leftMotion.SetKinematicProfile(22f, 62f, 92f);
        rightMotion.SetKinematicProfile(23f, 66f, 96f);
        faceMotion.SetRotationProfile(2.6f, 1.25f);
        leftMotion.SetRotationProfile(3.1f, 1.25f);
        rightMotion.SetRotationProfile(3.25f, 1.25f);

        health.SetMaxHealth(BossMaxHealth, true);
        controller.Initialize(
            mapManager,
            room,
            arena,
            health,
            visualRoot,
            face,
            leftHand,
            rightHand,
            faceFireOrigin,
            leftFingerTip,
            rightFingerTip,
            faceHurtbox,
            leftHurtbox,
            rightHurtbox,
            faceMotion,
            leftMotion,
            rightMotion,
            faceSize,
            handSize,
            allRenderers.ToArray(),
            combatEffects);

        return controller;
    }

    private static Bounds ResolveRoomCombatBounds(RoomController room, BoxCollider2D fallback)
    {
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
