using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GateTrigger : MonoBehaviour
{
    [SerializeField] private GateDirection direction = GateDirection.None;
    [SerializeField] private float localCooldown = 0.35f;

    private RoomController roomController;
    private float nextUseTime;

    private void Awake()
    {
        roomController = GetComponentInParent<RoomController>();
        if (direction == GateDirection.None) direction = InferDirectionFromName();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.unscaledTime < nextUseTime) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        MapManager manager = FindAnyObjectByType<MapManager>();
        if (manager == null || roomController == null) return;
        nextUseTime = Time.unscaledTime + Mathf.Max(0.05f, localCooldown);
        manager.TryUseGate(roomController, direction);
    }

    private GateDirection InferDirectionFromName()
    {
        string lower = gameObject.name.ToLowerInvariant();
        if (lower.Contains("left")) return GateDirection.Left;
        if (lower.Contains("right")) return GateDirection.Right;
        if (lower.Contains("top")) return GateDirection.Top;
        if (lower.Contains("buttom") || lower.Contains("bottom")) return GateDirection.Bottom;
        return GateDirection.None;
    }
}
