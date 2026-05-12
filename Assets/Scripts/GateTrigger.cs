using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GateTrigger : MonoBehaviour
{
    [SerializeField] private GateDirection direction = GateDirection.None;

    private RoomController roomController;

    private void Awake()
    {
        roomController = GetComponentInParent<RoomController>();

        if (direction == GateDirection.None)
            direction = InferDirectionFromName();

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null)
            return;

        MapManager manager = FindAnyObjectByType<MapManager>();
        if (manager == null || roomController == null)
            return;

        manager.TryUseGate(roomController, direction);
    }

    private GateDirection InferDirectionFromName()
    {
        string lowerName = gameObject.name.ToLower();

        if (lowerName.Contains("left"))
            return GateDirection.Left;

        if (lowerName.Contains("right"))
            return GateDirection.Right;

        if (lowerName.Contains("top"))
            return GateDirection.Top;

        if (lowerName.Contains("buttom") || lowerName.Contains("bottom"))
            return GateDirection.Bottom;

        return GateDirection.None;
    }
}