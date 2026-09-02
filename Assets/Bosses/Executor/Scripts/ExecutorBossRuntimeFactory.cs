using UnityEngine;

public static class ExecutorBossRuntimeFactory
{
    private const int BossMaxHealth = 2400;

    public static ExecutorBossController Create(RoomController room, MapManager mapManager)
    {
        if (room == null)
            return null;

        BoxCollider2D arena = room.EnemySpawnArea;
        Vector3 spawnPosition = arena != null ? arena.bounds.center : room.transform.position;
        spawnPosition.y -= 0.46f;
        spawnPosition.z = 0f;

        GameObject root = new GameObject("Boss_Executor");
        root.transform.SetParent(room.transform, true);
        root.transform.position = spawnPosition;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;

        ExecutorBossController controller = root.AddComponent<ExecutorBossController>();
        root.AddComponent<ExecutorCameraDirector>();
        EnemyHealth health = root.AddComponent<EnemyHealth>();

        // V6: 평상시 보스 아래에 타원/디스크/소켓을 두지 않는다.
        // 돌출 위치 경고는 기존 GroundCrack 효과를 사용하고, 실제 돌출 순간에만
        // 사용자 제공 ExecutorEmergeImpact 스프라이트를 짧게 표시한다.

        // 지하에서 올라오는 동안 본체의 아래쪽이 바로 노출되지 않도록 기존 마스크만 유지한다.
        GameObject maskObject = new GameObject("GroundRevealMask");
        maskObject.transform.SetParent(root.transform, false);
        maskObject.transform.localPosition = new Vector3(0f, 2.30f, 0f);
        maskObject.transform.localScale = new Vector3(4.8f, 4.6f, 1f);
        SpriteMask mask = maskObject.AddComponent<SpriteMask>();
        mask.sprite = ExecutorRuntimeSprites.GetGroundRevealMask();
        mask.alphaCutoff = 0.01f;
        mask.isCustomRangeActive = true;
        mask.frontSortingOrder = 20;
        mask.backSortingOrder = 9;

        GameObject visualObject = new GameObject("VisualRoot");
        visualObject.transform.SetParent(root.transform, false);
        // V8: V7에서 내렸던 보스 비주얼 오프셋을 원래 높이로 복구한다.
        // 하단 접지 문제는 위치를 더 내리는 방식이 아니라 별도 연출로 처리한다.
        visualObject.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        Transform visualRoot = visualObject.transform;

        GameObject bodyObject = new GameObject("Body");
        bodyObject.transform.SetParent(visualRoot, false);
        SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = ExecutorRuntimeSprites.LoadBody();
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 10;
        bodyRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        bodyObject.transform.localScale = Vector3.one * 1.31f;

        // V6: 별도의 눈 오버레이는 만들지 않는다. 눈 표현은 단계별 본체 스프라이트 자체를 사용한다.
        SpriteRenderer leftEye = null;
        SpriteRenderer rightEye = null;

        // 상단 발사구의 충전 표현은 공격 판독성을 위해 유지한다.
        GameObject muzzleGlowObject = CreateLight("MuzzleGlow", visualRoot, new Vector3(0f, 2.29f, 0f), 0.30f, 11);
        SpriteRenderer muzzleGlow = muzzleGlowObject.GetComponent<SpriteRenderer>();

        GameObject muzzlePointObject = new GameObject("MuzzlePoint");
        muzzlePointObject.transform.SetParent(visualRoot, false);
        muzzlePointObject.transform.localPosition = new Vector3(0f, 2.38f, 0f);

        GameObject hurtboxObject = new GameObject("BodyHurtbox");
        hurtboxObject.transform.SetParent(root.transform, false);
        if (enemyLayer >= 0) hurtboxObject.layer = enemyLayer;
        BoxCollider2D hurtbox = hurtboxObject.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = new Vector2(1.48f, 2.34f);
        hurtbox.offset = new Vector2(0f, 1.13f);

        GameObject solidObject = new GameObject("BodySolidCollider");
        solidObject.transform.SetParent(root.transform, false);
        if (enemyLayer >= 0) solidObject.layer = enemyLayer;
        BoxCollider2D solidCollider = solidObject.AddComponent<BoxCollider2D>();
        solidCollider.isTrigger = false;
        solidCollider.size = new Vector2(1.42f, 0.82f);
        solidCollider.offset = new Vector2(0f, 0.41f);

        health.SetMaxHealth(BossMaxHealth, true);
        controller.Initialize(mapManager, room, arena, health, hurtbox, solidCollider, visualRoot, bodyRenderer,
            leftEye, rightEye, muzzleGlow, muzzlePointObject.transform, null,
            null, null, null);
        return controller;
    }

    private static GameObject CreateLight(string name, Transform parent, Vector3 localPosition,
        float scale, int sortingOrder)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetGlow();
        renderer.color = new Color(0.3f, 0.85f, 1f, 0f);
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        return root;
    }
}
