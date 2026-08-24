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

    [Header("Portal")]
    [SerializeField] private Transform portalSpawn;

    [Header("Enemy Spawn Area")]
    [Tooltip("적이 랜덤하게 스폰될 범위. 'EnemySpawnArea'라는 이름의 자식 오브젝트에 " +
             "BoxCollider2D를 붙여두면 자동으로 인식됨 (Is Trigger 체크해서 실제 충돌엔 안 쓰이게 해도 됨, " +
             "범위 표시 용도로만 사용되고 콜라이더 자체는 물리에 영향 안 줌)")]
    [SerializeField] private BoxCollider2D enemySpawnArea;

    private readonly Dictionary<GateDirection, RoomController> connections = new();

    public int StageNumber => stageNumber;
    public int RoomNumber => roomNumber;
    public bool IsPortalRoom => isPortalRoom;
    public Transform PortalSpawn => portalSpawn;
    public BoxCollider2D EnemySpawnArea => enemySpawnArea;

    // 이 방에 이미 적을 스폰했는지 여부. MapManager가 재입장 시 중복 스폰을 막는 데 사용함.
    public bool HasSpawnedEnemies { get; set; } = false;

    private void Awake()
    {
        AutoFindReferences();
        ApplyPortalRoomState();
    }

    public void Setup(int stage, int room, bool portalRoom)
    {
        stageNumber = stage;
        roomNumber = room;
        isPortalRoom = portalRoom;

        gameObject.name = $"Room_{stageNumber}_{roomNumber}";

        HasSpawnedEnemies = false;

        AutoFindReferences();
        ApplyPortalRoomState();
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

    // 이 방 안에 아직 살아있는 몬스터(EnemyHealth)가 있는지 확인.
    // 적은 RoomEnemySpawner가 이 방(transform)을 부모로 스폰하므로, 자식만 훑으면 됨.
    public bool HasLivingEnemies()
    {
        EnemyHealth[] enemies = GetComponentsInChildren<EnemyHealth>(false);
        return enemies != null && enemies.Length > 0;
    }

    public void ApplyPortalRoomState()
    {
        ApplyGateState();
        ApplyPortalState();
    }

    private void ApplyGateState()
    {
        Transform gateRoot = transform.Find("Gate");
        if (gateRoot == null)
            return;

        bool enableGates = !isPortalRoom;

        GateTrigger[] gateTriggers = gateRoot.GetComponentsInChildren<GateTrigger>(true);
        for (int i = 0; i < gateTriggers.Length; i++)
            gateTriggers[i].enabled = enableGates;

        Collider2D[] gateColliders = gateRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < gateColliders.Length; i++)
            gateColliders[i].enabled = enableGates;

        SpriteRenderer[] gateRenderers = gateRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < gateRenderers.Length; i++)
            gateRenderers[i].enabled = enableGates;
    }

    private void ApplyPortalState()
    {
        PortalTrigger[] portals = GetComponentsInChildren<PortalTrigger>(true);

        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i] != null)
                portals[i].gameObject.SetActive(isPortalRoom);
        }
    }

    private void AutoFindReferences()
    {
        Transform spawnRoot = transform.Find("SpawnPoints");
        if (spawnRoot != null)
        {
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

        if (portalSpawn == null)
            portalSpawn = transform.Find("PortalSpawn");

        if (enemySpawnArea == null)
        {
            Transform areaTransform = transform.Find("EnemySpawnArea");
            if (areaTransform != null)
                enemySpawnArea = areaTransform.GetComponent<BoxCollider2D>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (enemySpawnArea == null)
            return;

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.7f);
        Gizmos.DrawWireCube(enemySpawnArea.bounds.center, enemySpawnArea.bounds.size);
    }
}
