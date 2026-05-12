using System.Collections.Generic;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("Room Info")]
    [SerializeField] private int stageNumber = 1;
    [SerializeField] private int roomNumber = 1;
    [SerializeField] private bool isPortalRoom = false;

    [Header("Spawn Points")]
    [SerializeField] private Transform spawnLeft;
    [SerializeField] private Transform spawnRight;
    [SerializeField] private Transform spawnTop;
    [SerializeField] private Transform spawnBottom;

    private readonly Dictionary<GateDirection, RoomController> connections = new();

    public int StageNumber => stageNumber;
    public int RoomNumber => roomNumber;
    public bool IsPortalRoom => isPortalRoom;

    private void Awake()
    {
        AutoFindSpawnPoints();
    }

    public void Setup(int stage, int room, bool portalRoom)
    {
        stageNumber = stage;
        roomNumber = room;
        isPortalRoom = portalRoom;
        gameObject.name = $"Room_{stageNumber}_{roomNumber}";
    }

    public void ClearConnections()
    {
        connections.Clear();
    }

    public void SetConnection(GateDirection direction, RoomController targetRoom)
    {
        if (direction == GateDirection.None || targetRoom == null)
            return;

        connections[direction] = targetRoom;
    }

    public bool TryGetConnection(GateDirection direction, out RoomController targetRoom)
    {
        return connections.TryGetValue(direction, out targetRoom);
    }

    public Transform GetSpawnPoint(GateDirection entrySide)
    {
        switch (entrySide)
        {
            case GateDirection.Left:
                return spawnLeft;
            case GateDirection.Right:
                return spawnRight;
            case GateDirection.Top:
                return spawnTop;
            case GateDirection.Bottom:
                return spawnBottom;
            default:
                return null;
        }
    }

    private void AutoFindSpawnPoints()
    {
        Transform spawnRoot = transform.Find("SpawnPoints");
        if (spawnRoot == null)
            return;

        if (spawnLeft == null)
            spawnLeft = spawnRoot.Find("Spawn_Left");

        if (spawnRight == null)
            spawnRight = spawnRoot.Find("Spawn_Right");

        if (spawnTop == null)
            spawnTop = spawnRoot.Find("Spawn_Top");

        if (spawnBottom == null)
        {
            spawnBottom = spawnRoot.Find("Spawn_Buttom");
            if (spawnBottom == null)
                spawnBottom = spawnRoot.Find("Spawn_Bottom");
        }
    }
}