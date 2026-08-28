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
    [SerializeField] private Transform centerSpawn;
    [SerializeField] private Transform portalArrivalSpawn;

    [Header("Portal")]
    [SerializeField] private Transform portalSpawn;

    [Header("Enemy Spawn Area")]
    [SerializeField] private BoxCollider2D enemySpawnArea;

    private readonly Dictionary<GateDirection, RoomController> connections = new Dictionary<GateDirection, RoomController>();

    public int StageNumber => stageNumber;
    public int RoomNumber => roomNumber;
    public bool IsPortalRoom => isPortalRoom;
    public Transform PortalSpawn => portalSpawn;
    public Transform CenterSpawn => centerSpawn;
    public Transform PortalArrivalSpawn => portalArrivalSpawn != null ? portalArrivalSpawn : spawnBottom;
    public BoxCollider2D EnemySpawnArea => enemySpawnArea;
    public bool HasSpawnedEnemies { get; set; }

    private void Awake()
    {
        AutoFindReferences();
        EnsureSpawnPoints();
        ApplyPortalRoomState();
    }

    public void Setup(int stage, int room, bool portalRoom)
    {
        stageNumber = Mathf.Max(1, stage);
        roomNumber = Mathf.Max(1, room);
        isPortalRoom = portalRoom;
        gameObject.name = "Room_" + stageNumber + "_" + roomNumber;
        HasSpawnedEnemies = false;
        AutoFindReferences();
        EnsureSpawnPoints();
        ApplyPortalRoomState();
    }

    public void ClearConnections()
    {
        connections.Clear();
    }

    public void SetConnection(GateDirection direction, RoomController targetRoom)
    {
        if (direction != GateDirection.None && targetRoom != null)
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
            case GateDirection.Left: return spawnLeft;
            case GateDirection.Right: return spawnRight;
            case GateDirection.Top: return spawnTop;
            case GateDirection.Bottom: return spawnBottom;
            default: return centerSpawn;
        }
    }

    public Transform GetInitialCenterSpawn()
    {
        EnsureSpawnPoints();
        return centerSpawn != null ? centerSpawn : transform;
    }

    public Transform GetPortalArrivalSpawn()
    {
        EnsureSpawnPoints();
        if (portalArrivalSpawn != null)
            return portalArrivalSpawn;
        if (spawnBottom != null)
            return spawnBottom;
        return centerSpawn != null ? centerSpawn : transform;
    }

    public bool HasLivingEnemies()
    {
        EnemyHealth[] enemies = GetComponentsInChildren<EnemyHealth>(false);
        if (enemies == null)
            return false;

        HashSet<int> uniqueRoots = new HashSet<int>();
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth health = enemies[i];
            if (health == null || health.IsDead)
                continue;
            uniqueRoots.Add(health.transform.root.GetInstanceID());
        }

        return uniqueRoots.Count > 0;
    }

    public void ApplyPortalRoomState()
    {
        Transform gateRoot = transform.Find("Gate");
        if (gateRoot != null)
        {
            bool enabledState = !isPortalRoom;
            GateTrigger[] triggers = gateRoot.GetComponentsInChildren<GateTrigger>(true);
            for (int i = 0; i < triggers.Length; i++)
                if (triggers[i] != null) triggers[i].enabled = enabledState;

            Collider2D[] colliders = gateRoot.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = enabledState;

            SpriteRenderer[] renderers = gateRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = enabledState;
        }

        PortalTrigger[] portals = GetComponentsInChildren<PortalTrigger>(true);
        for (int i = 0; i < portals.Length; i++)
            if (portals[i] != null) portals[i].gameObject.SetActive(isPortalRoom);
    }

    /// <summary>
    /// Creates reliable center and portal-arrival points when old room prefabs do not contain them.
    /// Safe to call both at runtime and from an editor installer.
    /// </summary>
    public void EnsureSpawnPoints()
    {
        AutoFindReferences();
        Transform spawnRoot = transform.Find("SpawnPoints");
        if (spawnRoot == null)
        {
            GameObject root = new GameObject("SpawnPoints");
            spawnRoot = root.transform;
            spawnRoot.SetParent(transform, false);
        }

        if (centerSpawn == null)
        {
            Transform existing = spawnRoot.Find("CenterSpawn");
            if (existing == null)
            {
                GameObject point = new GameObject("CenterSpawn");
                existing = point.transform;
                existing.SetParent(spawnRoot, false);
            }

            Vector3 centerWorld = enemySpawnArea != null ? enemySpawnArea.bounds.center : transform.position;
            existing.position = centerWorld;
            centerSpawn = existing;
        }

        if (portalArrivalSpawn == null)
        {
            Transform existing = spawnRoot.Find("PortalArrivalSpawn");
            if (existing == null)
            {
                GameObject point = new GameObject("PortalArrivalSpawn");
                existing = point.transform;
                existing.SetParent(spawnRoot, false);
            }

            Vector3 worldPosition = spawnBottom != null ? spawnBottom.position : centerSpawn.position;
            existing.position = worldPosition;
            portalArrivalSpawn = existing;
        }
    }

    private void AutoFindReferences()
    {
        Transform spawnRoot = transform.Find("SpawnPoints");
        if (spawnRoot != null)
        {
            if (spawnLeft == null) spawnLeft = spawnRoot.Find("Spawn_Left");
            if (spawnRight == null) spawnRight = spawnRoot.Find("Spawn_Right");
            if (spawnTop == null) spawnTop = spawnRoot.Find("Spawn_Top");
            if (spawnBottom == null)
            {
                spawnBottom = spawnRoot.Find("Spawn_Buttom");
                if (spawnBottom == null) spawnBottom = spawnRoot.Find("Spawn_Bottom");
            }
            if (centerSpawn == null) centerSpawn = spawnRoot.Find("CenterSpawn");
            if (portalArrivalSpawn == null) portalArrivalSpawn = spawnRoot.Find("PortalArrivalSpawn");
        }

        if (portalSpawn == null)
            portalSpawn = transform.Find("PortalSpawn");

        if (enemySpawnArea == null)
        {
            Transform area = transform.Find("EnemySpawnArea");
            if (area != null)
                enemySpawnArea = area.GetComponent<BoxCollider2D>();
        }
    }

    private void OnDestroy()
    {
        ByteRoomRegistry.ForgetRoom(this, false);
    }

    private void OnDrawGizmosSelected()
    {
        if (enemySpawnArea != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.7f);
            Gizmos.DrawWireCube(enemySpawnArea.bounds.center, enemySpawnArea.bounds.size);
        }

        if (centerSpawn != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centerSpawn.position, 0.18f);
        }

        if (portalArrivalSpawn != null)
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 1f);
            Gizmos.DrawWireSphere(portalArrivalSpawn.position, 0.22f);
        }
    }
}
