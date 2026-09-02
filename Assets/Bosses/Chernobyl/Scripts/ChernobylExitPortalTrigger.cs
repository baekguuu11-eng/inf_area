using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChernobylExitPortalTrigger : MonoBehaviour
{
    private MapManager mapManager;
    private float nextUseTime;

    public static ChernobylExitPortalTrigger Create(MapManager manager, Vector3 position, Transform parent)
    {
        GameObject root = new GameObject("ChernobylExitPortal");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimePixelSpriteFactory.GetPortalSprite();
        renderer.color = new Color(0.34f, 0.94f, 0.38f, 1f);
        renderer.sortingOrder = 12;
        root.transform.localScale = Vector3.one * 1.18f;
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.9f, 1.2f);
        ChernobylExitPortalTrigger trigger = root.AddComponent<ChernobylExitPortalTrigger>();
        trigger.mapManager = manager;
        return trigger;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 4.3f) * 0.055f;
        transform.localScale = Vector3.one * 1.18f * pulse;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.unscaledTime < nextUseTime || other == null) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (mapManager == null) mapManager = FindAnyObjectByType<MapManager>();
        if (mapManager == null) return;
        nextUseTime = Time.unscaledTime + 0.5f;
        mapManager.TryUseBossExitPortal();
    }
}
