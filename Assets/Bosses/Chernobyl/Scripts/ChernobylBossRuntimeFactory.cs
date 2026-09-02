using System.Collections.Generic;
using UnityEngine;

public static class ChernobylBossRuntimeFactory
{
    private const int BossMaxHealth = 2700;

    public static ChernobylBossController Create(RoomController room, MapManager mapManager)
    {
        if (room == null) return null;
        BoxCollider2D arena = room.EnemySpawnArea;
        Vector3 spawn = arena != null ? arena.bounds.center : room.transform.position;
        spawn.z = 0f;

        GameObject root = new GameObject("Boss_Chernobyl");
        root.transform.SetParent(room.transform, true);
        root.transform.position = spawn;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;

        EnemyHealth health = root.AddComponent<EnemyHealth>();
        ChernobylBossController controller = root.AddComponent<ChernobylBossController>();

        GameObject visualObject = new GameObject("VisualRoot");
        visualObject.transform.SetParent(root.transform, false);
        Transform visualRoot = visualObject.transform;
        List<SpriteRenderer> structure = new List<SpriteRenderer>();

        // V9 2보스는 외부 이미지 없이 흰색 런타임 스프라이트만 조합한다.
        CreateRect("RearPlate", visualRoot, new Vector2(1.92f, 1.55f), new Vector2(0f, 0f),
            new Color(0.82f, 0.86f, 0.86f, 1f), 10, structure);
        CreateRect("InnerCavity", visualRoot, new Vector2(1.30f, 0.92f), new Vector2(0f, 0.02f),
            new Color(0.055f, 0.075f, 0.065f, 1f), 11, structure);
        CreateRect("TopBand", visualRoot, new Vector2(2.18f, 0.24f), new Vector2(0f, 0.78f),
            new Color(0.94f, 0.97f, 0.95f, 1f), 14, structure);
        CreateRect("BottomBand", visualRoot, new Vector2(2.28f, 0.28f), new Vector2(0f, -0.78f),
            new Color(0.90f, 0.94f, 0.91f, 1f), 14, structure);
        CreateRect("MidBand", visualRoot, new Vector2(2.08f, 0.14f), new Vector2(0f, -0.05f),
            new Color(0.70f, 0.78f, 0.74f, 1f), 13, structure);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                float y = 0.48f - i * 0.42f;
                GameObject rib = CreateRect("SideRib", visualRoot, new Vector2(0.42f, 0.12f),
                    new Vector2(side * 1.05f, y), new Color(0.96f, 0.98f, 0.97f, 1f), 15, structure);
                rib.transform.localRotation = Quaternion.Euler(0f, 0f, side * (i == 1 ? 0f : 10f));
            }
        }

        // 상부 제어봉: 흰색 막대를 촘촘히 배치해 산업 설비 느낌을 준다.
        for (int i = 0; i < 7; i++)
        {
            float x = Mathf.Lerp(-0.72f, 0.72f, i / 6f);
            float h = i % 2 == 0 ? 0.30f : 0.22f;
            CreateRect("ControlRod", visualRoot, new Vector2(0.10f, h), new Vector2(x, 0.99f + h * 0.5f),
                new Color(0.98f, 0.99f, 0.98f, 1f), 13, structure);
        }

        GameObject ringObject = new GameObject("CoreOuterRing");
        ringObject.transform.SetParent(visualRoot, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.10f, 0f);
        SpriteRenderer outerRing = ringObject.AddComponent<SpriteRenderer>();
        outerRing.sprite = ChernobylRuntimeSprites.Ring;
        outerRing.color = new Color(0.92f, 0.98f, 0.94f, 1f);
        outerRing.sortingOrder = 17;
        ringObject.transform.localScale = Vector3.one * 0.96f;
        structure.Add(outerRing);

        GameObject coreBack = new GameObject("CoreBack");
        coreBack.transform.SetParent(visualRoot, false);
        coreBack.transform.localPosition = new Vector3(0f, 0.10f, 0f);
        SpriteRenderer coreBackRenderer = coreBack.AddComponent<SpriteRenderer>();
        coreBackRenderer.sprite = ChernobylRuntimeSprites.FilledCircle;
        coreBackRenderer.color = new Color(0.025f, 0.085f, 0.035f, 1f);
        coreBackRenderer.sortingOrder = 16;
        coreBack.transform.localScale = Vector3.one * 0.73f;
        structure.Add(coreBackRenderer);

        GameObject coreObject = new GameObject("Core");
        coreObject.transform.SetParent(visualRoot, false);
        coreObject.transform.localPosition = new Vector3(0f, 0.10f, 0f);
        SpriteRenderer core = coreObject.AddComponent<SpriteRenderer>();
        core.sprite = ChernobylRuntimeSprites.FilledCircle;
        core.color = new Color(0.30f, 0.95f, 0.36f, 1f);
        core.sortingOrder = 18;
        coreObject.transform.localScale = Vector3.one * 0.42f;

        GameObject coreDiamond = new GameObject("CoreGlyph");
        coreDiamond.transform.SetParent(coreObject.transform, false);
        SpriteRenderer glyph = coreDiamond.AddComponent<SpriteRenderer>();
        glyph.sprite = ChernobylRuntimeSprites.Diamond;
        glyph.color = new Color(0.92f, 1f, 0.88f, 0.95f);
        glyph.sortingOrder = 19;
        coreDiamond.transform.localScale = Vector3.one * 0.55f;
        structure.Add(glyph);

        // 바닥에 별도 받침/그림자 이미지는 사용하지 않는다.
        GameObject hurtboxObject = new GameObject("BodyHurtbox");
        hurtboxObject.transform.SetParent(root.transform, false);
        if (enemyLayer >= 0) hurtboxObject.layer = enemyLayer;
        BoxCollider2D hurtbox = hurtboxObject.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = new Vector2(2.15f, 2.05f);
        hurtbox.offset = new Vector2(0f, 0.10f);

        GameObject solidObject = new GameObject("BodySolidCollider");
        solidObject.transform.SetParent(root.transform, false);
        if (enemyLayer >= 0) solidObject.layer = enemyLayer;
        BoxCollider2D solid = solidObject.AddComponent<BoxCollider2D>();
        solid.isTrigger = false;
        solid.size = new Vector2(1.86f, 1.56f);
        solid.offset = new Vector2(0f, -0.02f);

        health.SetMaxHealth(BossMaxHealth, true);
        controller.Initialize(mapManager, room, arena, health, hurtbox, solid, visualRoot, core,
            ringObject.transform, structure.ToArray());
        return controller;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 size, Vector2 localPosition,
        Color color, int sortingOrder, List<SpriteRenderer> collection)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        root.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ChernobylRuntimeSprites.WhitePixel;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (collection != null) collection.Add(renderer);
        return root;
    }
}
