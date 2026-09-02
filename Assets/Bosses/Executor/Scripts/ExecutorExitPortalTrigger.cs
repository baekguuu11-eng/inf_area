using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExecutorExitPortalTrigger : MonoBehaviour
{
    private MapManager mapManager;
    private float nextUseTime;

    public static ExecutorExitPortalTrigger Create(MapManager manager, Vector3 position, Transform parent)
    {
        GameObject root = new GameObject("ExecutorExitPortal");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimePixelSpriteFactory.GetPortalSprite();
        renderer.color = new Color(0.78f, 0.18f, 0.20f, 1f);
        renderer.sortingOrder = 12;
        root.transform.localScale = Vector3.one * 1.18f;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.9f, 1.2f);

        ExecutorExitPortalTrigger trigger = root.AddComponent<ExecutorExitPortalTrigger>();
        trigger.mapManager = manager;
        return trigger;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.06f;
        transform.localScale = Vector3.one * 1.18f * pulse;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.unscaledTime < nextUseTime || other == null)
            return;
        if (other.GetComponentInParent<PlayerMovement>() == null)
            return;
        if (mapManager == null)
            mapManager = FindAnyObjectByType<MapManager>();
        if (mapManager == null)
            return;

        nextUseTime = Time.unscaledTime + 0.5f;
        mapManager.TryUseBossExitPortal();
    }
}
