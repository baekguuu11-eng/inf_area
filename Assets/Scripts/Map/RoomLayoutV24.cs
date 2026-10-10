using System.Collections.Generic;
using UnityEngine;

/// <summary>Room art and a single authoritative world-space boss arena.</summary>
[DisallowMultipleComponent]
public sealed class RoomLayoutV24 : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private Bounds combatBounds;
    private int layoutStage;
    public Vector3 CameraCenter => layoutStage >= 2 ? combatBounds.center : VisualBounds.center;
    private SpriteRenderer background;
    public Bounds CombatBounds => combatBounds;
    public Bounds VisualBounds => background != null ? background.bounds : combatBounds;

    public static bool TryGetBounds(RoomController room, out Bounds bounds)
    {
        RoomLayoutV24 layout = room != null ? room.GetComponent<RoomLayoutV24>() : null;
        bounds = layout != null ? layout.combatBounds : default(Bounds);
        return layout != null;
    }

    private static Sprite Load(string key)
    {
        if (!sprites.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = Resources.Load<Sprite>("RoomsV24/" + key);
            if (sprite != null) sprites[key] = sprite;
        }
        return sprite;
    }

    public static void Apply(RoomController room)
    {
        if (room == null) return;
        Transform art = room.transform.Find("Background");
        SpriteRenderer sr = art != null ? art.GetComponent<SpriteRenderer>() : null;
        if (sr == null) { Debug.LogError("V24: Room Background missing", room); return; }
        bool boss = room.RoomNumber >= 6 && room.StageNumber <= 3;
        string key = boss ? (room.StageNumber == 1 ? "Executor" : room.StageNumber == 2 ? "Chernobyl" : "JHL")
            : room.StageNumber == 1 ? (room.RoomNumber == 1 ? "Tutorial" : "Network")
            : room.StageNumber == 2 ? "Power" : "Core";
        Sprite sprite = Load(key);
        if (sprite == null) { Debug.LogError("V24: Missing RoomsV24/" + key, room); return; }
        // All normal backgrounds share the original full-canvas dimensions and transform.
        sr.sprite = sprite;
        if (!boss) return;
        RoomLayoutV24 layout = room.GetComponent<RoomLayoutV24>();
        if (layout == null) layout = room.gameObject.AddComponent<RoomLayoutV24>();
        layout.background = sr;
        layout.Build(room);
    }

    private void Build(RoomController room)
    {
        int stage = room.StageNumber;
        layoutStage = stage;
        Vector2 size = stage == 1 ? new Vector2(18.3f, 8f) : stage == 2 ? new Vector2(18f, 9f) : new Vector2(18.8f, 9f);
        // Measured wall-foot rectangles in the supplied images (bottom-left UV origin).
        Rect floor = stage == 1 ? Rect.MinMaxRect(.045f, .11f, .955f, .85f)
            : stage == 2 ? Rect.MinMaxRect(.1705f, .2487f, .8313f, .7705f)
            : Rect.MinMaxRect(.219f, .283f, .779f, .760f);
        Vector3 center = room.EnemySpawnArea != null ? room.EnemySpawnArea.bounds.center : room.transform.position;
        center.z = 0f;
        combatBounds = new Bounds(center, new Vector3(size.x, size.y, 1f));
        float fullWidth = size.x / floor.width;
        float fullHeight = size.y / floor.height;
        Vector2 spriteSize = background.sprite.bounds.size;
        Transform art = background.transform;
        Vector3 parentScale = art.parent != null ? art.parent.lossyScale : Vector3.one;
        art.localScale = new Vector3(fullWidth / spriteSize.x / Mathf.Abs(parentScale.x), fullHeight / spriteSize.y / Mathf.Abs(parentScale.y), 1f);
        art.position = center - new Vector3((floor.center.x - .5f) * fullWidth, (floor.center.y - .5f) * fullHeight, 0f);

        Transform oldWalls = room.transform.Find("Walls");
        int wallLayer = LayerMask.NameToLayer("Wall");
        if (oldWalls != null)
        {
            Collider2D[] old = oldWalls.GetComponentsInChildren<Collider2D>(true);
            if (old.Length > 0) wallLayer = old[0].gameObject.layer;
            foreach (Collider2D c in old) c.enabled = false;
            oldWalls.gameObject.SetActive(false);
        }
        Transform gates = room.transform.Find("Gate");
        if (gates != null) gates.gameObject.SetActive(false);
        Transform prior = room.transform.Find("ArenaWallsV24");
        if (prior != null) { prior.gameObject.SetActive(false); Destroy(prior.gameObject); }
        GameObject root = new GameObject("ArenaWallsV24");
        root.transform.SetParent(room.transform, false);
        float x = size.x * .5f, y = size.y * .5f;
        const float thickness = 1f;
        Wall(root.transform, "Left", center + Vector3.left * (x + thickness * .5f), new Vector2(thickness, size.y + 2f), wallLayer);
        Wall(root.transform, "Right", center + Vector3.right * (x + thickness * .5f), new Vector2(thickness, size.y + 2f), wallLayer);
        Wall(root.transform, "Bottom", center + Vector3.down * (y + thickness * .5f), new Vector2(size.x + 2f, thickness), wallLayer);
        Wall(root.transform, "Top", center + Vector3.up * (y + thickness * .5f), new Vector2(size.x + 2f, thickness), wallLayer);
        // Executor's lower-corner machinery is visible solid equipment, not walkable floor.
        if (stage == 1)
        {
            Wall(root.transform, "LowerLeftEquipment", center + new Vector3(-x + 1.55f, -y + .65f, 0f), new Vector2(3.1f, 1.3f), wallLayer);
            Wall(root.transform, "LowerRightEquipment", center + new Vector3(x - 1.55f, -y + .65f, 0f), new Vector2(3.1f, 1.3f), wallLayer);
        }
        Place(room.GetSpawnPoint(GateDirection.Left), center + new Vector3(-x + .65f, 0f, 0f));
        Place(room.GetSpawnPoint(GateDirection.Right), center + new Vector3(x - .65f, 0f, 0f));
        Place(room.GetSpawnPoint(GateDirection.Top), center + new Vector3(0f, y - .65f, 0f));
        Place(room.GetSpawnPoint(GateDirection.Bottom), center + new Vector3(0f, -y + 1f, 0f));
        Place(room.PortalArrivalSpawn, center + new Vector3(0f, -y + 1f, 0f));
        Place(room.CenterSpawn, center + new Vector3(0f, -y + 1f, 0f));
        Place(room.PortalSpawn, center + new Vector3(0f, -y + 1.4f, 0f));
        BoxCollider2D area = room.EnemySpawnArea;
        if (area != null)
        {
            area.transform.position = center;
            area.transform.localScale = Vector3.one;
            area.offset = Vector2.zero;
            // Preserve Executor's movement/targeting envelope, independent of outer walls.
            Vector2 region = stage == 1 ? new Vector2(14.743109f, 5.3258243f) : size;
            Vector3 scale = area.transform.lossyScale;
            area.size = new Vector2(region.x / Mathf.Abs(scale.x), region.y / Mathf.Abs(scale.y));
        }
        Physics2D.SyncTransforms();
    }

    private static void Place(Transform point, Vector3 position) { if (point != null) point.position = position; }
    private static void Wall(Transform parent, string name, Vector3 center, Vector2 size, int layer)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.position = center;
        if (layer >= 0) wall.layer = layer;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        Vector3 scale = wall.transform.lossyScale;
        collider.size = new Vector2(size.x / Mathf.Abs(scale.x), size.y / Mathf.Abs(scale.y));
    }
    public float FitCamera(float aspect)
    {
        Bounds visual = VisualBounds;
        if (layoutStage < 2) return Mathf.Max(visual.extents.y, visual.extents.x / Mathf.Max(.1f, aspect)) + .25f;
        // Fit combat area, NOT the complete expanded facility art. Extra art covers motion.
        return Mathf.Max(combatBounds.extents.y + .9f, (combatBounds.extents.x + .9f) / Mathf.Max(.1f, aspect));
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(combatBounds.center, combatBounds.size);
    }
}
