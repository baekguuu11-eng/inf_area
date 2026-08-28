using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomController startRoom;
    [SerializeField] private RoomController roomTemplate;
    [SerializeField] private Transform stageRoomsRoot;
    [SerializeField] private RoomTransitionUI transitionUI;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private RoomEnemySpawner enemySpawner;

    [Header("Transition Settings")]
    [SerializeField] private float gateCooldown = 0.15f;
    [SerializeField] private float cameraSlideDistance = 0.8f;
    [SerializeField] private float cameraSlideDuration = 0.15f;
    [SerializeField] private float blackHoldDuration = 0.04f;

    [Header("Stage 1 Boss")]
    [SerializeField] private bool useFirstBossInRoomFour = true;
    [SerializeField] private GameObject firstBossPrefab;

    [Header("Portal Shop")]
    [SerializeField] private bool openShopOnPortalRoomEntry = true;

    [Header("Room Entry Placement")]
    [SerializeField] private bool centerPlayerOnInitialStart = true;
    [SerializeField] private bool portalArrivesFromBottomGate = true;

    [Header("Byte / Ammo Auto Collect")]
    [SerializeField] private bool autoCollectBytesBeforeRoomExit = true;
    [SerializeField] private bool autoCollectAmmoBeforeRoomExit = true;
    [SerializeField, Min(0.03f)] private float byteAutoCollectDuration = 0.16f;
    [SerializeField, Min(0f)] private float byteAutoCollectExtraWait = 0.035f;

    private Transform playerRoot;
    private Rigidbody2D playerBody;
    private RoomController currentRoom;
    private RoomController currentPortalRoom;
    private int currentStage = 1;
    private int highestNormalRoomNumber = 1;
    private bool transitionLocked;
    private Vector3 cameraRestPosition;
    private GameFeelManager cameraEffects;
    private IDisposable transitionInputLock;

    public int CurrentStage { get { return currentStage; } }
    public RoomController CurrentRoom { get { return currentRoom; } }
    public bool IsTransitioning { get { return transitionLocked; } }

    private void Awake()
    {
        Time.timeScale = 1f;
        GameInputState.ClearAll();
        ShopRunUpgradeState.ResetRunUpgrades();

        if (stageRoomsRoot == null)
        {
            GameObject found = GameObject.Find("StageRooms");
            if (found != null) stageRoomsRoot = found.transform;
        }

        if (startRoom == null) startRoom = FindAnyObjectByType<RoomController>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraRestPosition = mainCamera.transform.position;
            cameraEffects = mainCamera.GetComponent<GameFeelManager>();
        }

        PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
        {
            playerRoot = movement.transform;
            playerBody = movement.GetComponent<Rigidbody2D>();
        }

        if (transitionUI == null) transitionUI = FindAnyObjectByType<RoomTransitionUI>();
        if (enemySpawner == null) enemySpawner = FindAnyObjectByType<RoomEnemySpawner>();
    }

    private IEnumerator Start()
    {
        if (startRoom == null || roomTemplate == null || stageRoomsRoot == null)
        {
            Debug.LogError("[MapManager] StartRoom, RoomTemplate, StageRoomsRoot 중 하나가 비어 있습니다.");
            yield break;
        }

        startRoom.transform.SetParent(stageRoomsRoot);
        startRoom.Setup(1, 1, false);
        startRoom.ClearConnections();
        currentStage = 1;
        highestNormalRoomNumber = 1;
        currentPortalRoom = null;
        currentRoom = startRoom;

        if (centerPlayerOnInitialStart)
            MovePlayerToInitialStart(currentRoom);

        ShowOnlyCurrentRoom();
        TrySpawnContentForCurrentRoom();
        SetCameraBase(cameraRestPosition, true);

        if (transitionUI != null)
            yield return transitionUI.ShowRoomLabel(currentStage, currentRoom.RoomNumber);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!transitionLocked && Input.GetKeyDown(KeyCode.M))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
    }

    public void TryUseGate(RoomController fromRoom, GateDirection exitDirection)
    {
        if (transitionLocked || fromRoom == null || fromRoom != currentRoom || fromRoom.IsPortalRoom)
            return;

        if (fromRoom.HasLivingEnemies())
            return;

        RoomController targetRoom;
        if (fromRoom.TryGetConnection(exitDirection, out targetRoom))
        {
            StartCoroutine(TransitionToRoom(targetRoom, GetOppositeDirection(exitDirection), exitDirection));
            return;
        }

        if (highestNormalRoomNumber < 4)
        {
            int nextRoomNumber = highestNormalRoomNumber + 1;
            RoomController newRoom = Instantiate(roomTemplate, stageRoomsRoot);
            newRoom.Setup(currentStage, nextRoomNumber, false);
            newRoom.ClearConnections();
            newRoom.gameObject.SetActive(false);

            fromRoom.SetConnection(exitDirection, newRoom);
            newRoom.SetConnection(GetOppositeDirection(exitDirection), fromRoom);
            highestNormalRoomNumber = nextRoomNumber;
            StartCoroutine(TransitionToRoom(newRoom, GetOppositeDirection(exitDirection), exitDirection));
            return;
        }

        if (fromRoom.RoomNumber == 4)
        {
            if (currentPortalRoom == null)
                currentPortalRoom = CreatePortalRoom();

            fromRoom.SetConnection(exitDirection, currentPortalRoom);
            currentPortalRoom.SetConnection(GetOppositeDirection(exitDirection), fromRoom);
            StartCoroutine(TransitionToRoom(currentPortalRoom, GetOppositeDirection(exitDirection), exitDirection));
        }
    }

    public void TryUsePortal()
    {
        if (transitionLocked || currentRoom == null || !currentRoom.IsPortalRoom)
            return;

        StartCoroutine(TransitionToNextStage());
    }

    private RoomController CreatePortalRoom()
    {
        RoomController portalRoom = Instantiate(roomTemplate, stageRoomsRoot);
        portalRoom.Setup(currentStage, 5, true);
        portalRoom.ClearConnections();

        if (portalRoom.GetComponentInChildren<PortalTrigger>(true) == null)
            SpawnPortal(portalRoom);

        portalRoom.ApplyPortalRoomState();
        portalRoom.gameObject.SetActive(false);
        return portalRoom;
    }

    private void SpawnPortal(RoomController room)
    {
        if (room == null || portalPrefab == null) return;
        Transform point = room.PortalSpawn;
        Instantiate(portalPrefab, point != null ? point.position : room.transform.position, Quaternion.identity, room.transform);
    }

    private void TrySpawnContentForCurrentRoom()
    {
        if (currentRoom == null || currentRoom.HasSpawnedEnemies || currentRoom.IsPortalRoom)
            return;

        if (useFirstBossInRoomFour && currentStage == 1 && currentRoom.RoomNumber == 4 && firstBossPrefab != null)
        {
            Vector3 position = currentRoom.EnemySpawnArea != null
                ? currentRoom.EnemySpawnArea.bounds.center
                : currentRoom.transform.position;
            Instantiate(firstBossPrefab, position, Quaternion.identity, currentRoom.transform);
            currentRoom.HasSpawnedEnemies = true;
            return;
        }

        if (enemySpawner != null)
        {
            enemySpawner.SpawnEnemiesForRoom(currentRoom, currentStage);
            currentRoom.HasSpawnedEnemies = true;
        }
    }

    private IEnumerator TransitionToRoom(RoomController targetRoom, GateDirection entrySide, GateDirection exitDirection)
    {
        if (targetRoom == null || transitionLocked)
            yield break;

        BeginTransitionLock();
        yield return AutoCollectCurrentRoomResources();
        yield return PlayExitTransition(exitDirection);

        if (currentRoom != null) currentRoom.gameObject.SetActive(false);
        currentRoom = targetRoom;
        currentRoom.gameObject.SetActive(true);

        MovePlayerToSpawn(currentRoom, entrySide);
        ShowOnlyCurrentRoom();
        TrySpawnContentForCurrentRoom();
        Physics2D.SyncTransforms();

        SetCameraBase(cameraRestPosition + DirectionToVector3(exitDirection) * (cameraSlideDistance * 0.35f), true);
        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return PlayEnterTransition(exitDirection);

        if (transitionUI != null)
            StartCoroutine(transitionUI.ShowRoomLabel(currentStage, currentRoom.RoomNumber));

        if (currentRoom.IsPortalRoom && openShopOnPortalRoomEntry)
        {
            ShopManager shop = FindAnyObjectByType<ShopManager>();
            if (shop != null) shop.OpenShop();
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, gateCooldown));
        EndTransitionLock();
    }

    private IEnumerator TransitionToNextStage()
    {
        if (transitionLocked) yield break;
        BeginTransitionLock();
        yield return AutoCollectCurrentRoomResources();
        yield return PlayExitTransition(GateDirection.None);

        ClearCurrentStageRooms();
        currentStage++;
        highestNormalRoomNumber = 1;
        currentPortalRoom = null;

        RoomController newStartRoom = Instantiate(roomTemplate, stageRoomsRoot);
        newStartRoom.Setup(currentStage, 1, false);
        newStartRoom.ClearConnections();
        newStartRoom.gameObject.SetActive(true);
        startRoom = newStartRoom;
        currentRoom = newStartRoom;

        MovePlayerToStageStart(newStartRoom);
        ShowOnlyCurrentRoom();
        TrySpawnContentForCurrentRoom();
        Physics2D.SyncTransforms();
        SetCameraBase(cameraRestPosition, true);

        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);
        yield return PlayEnterTransition(GateDirection.None);

        if (transitionUI != null)
            StartCoroutine(transitionUI.ShowRoomLabel(currentStage, currentRoom.RoomNumber));

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, gateCooldown));
        EndTransitionLock();
    }

    private void BeginTransitionLock()
    {
        transitionLocked = true;
        if (transitionInputLock == null)
            transitionInputLock = GameInputState.Acquire("RoomTransition");
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
    }

    private void EndTransitionLock()
    {
        if (transitionInputLock != null)
        {
            transitionInputLock.Dispose();
            transitionInputLock = null;
        }
        transitionLocked = false;
    }

    private void ClearCurrentStageRooms()
    {
        if (stageRoomsRoot == null) return;
        for (int i = stageRoomsRoot.childCount - 1; i >= 0; i--)
            Destroy(stageRoomsRoot.GetChild(i).gameObject);
    }

    private void MovePlayerToSpawn(RoomController room, GateDirection entrySide)
    {
        if (playerRoot == null || room == null) return;
        Transform point = room.GetSpawnPoint(entrySide);
        Vector3 position = point != null ? point.position : room.transform.position;
        SetPlayerPosition(position);
    }

    private void MovePlayerToInitialStart(RoomController room)
    {
        if (room == null) return;
        Transform point = room.GetInitialCenterSpawn();
        SetPlayerPosition(point != null ? point.position : room.transform.position);
    }

    private void MovePlayerToStageStart(RoomController room)
    {
        if (room == null) return;
        Transform point = portalArrivesFromBottomGate ? room.GetPortalArrivalSpawn() : room.GetInitialCenterSpawn();
        SetPlayerPosition(point != null ? point.position : room.transform.position);
    }

    private IEnumerator AutoCollectCurrentRoomResources()
    {
        if (currentRoom == null)
            yield break;

        bool byteRequested = autoCollectBytesBeforeRoomExit
            && ByteRoomRegistry.ForceCollectRoom(currentRoom, byteAutoCollectDuration);
        bool ammoRequested = autoCollectAmmoBeforeRoomExit
            && AmmoRoomRegistry.ForceCollectRoom(currentRoom, byteAutoCollectDuration);

        if (byteRequested || ammoRequested)
        {
            float wait = Mathf.Max(0.03f, byteAutoCollectDuration) + Mathf.Max(0f, byteAutoCollectExtraWait);
            yield return new WaitForSecondsRealtime(wait);
        }

        if (autoCollectBytesBeforeRoomExit)
            ByteRoomRegistry.FinalizeRoomCollection(currentRoom);
        if (autoCollectAmmoBeforeRoomExit)
            AmmoRoomRegistry.FinalizeRoomCollection(currentRoom);
    }

    private void SetPlayerPosition(Vector3 position)
    {
        if (playerRoot == null) return;
        position.z = playerRoot.position.z;
        if (playerBody != null)
        {
            playerBody.position = position;
            playerBody.linearVelocity = Vector2.zero;
        }
        else
            playerRoot.position = position;
    }

    private void ShowOnlyCurrentRoom()
    {
        if (stageRoomsRoot == null || currentRoom == null) return;
        for (int i = 0; i < stageRoomsRoot.childCount; i++)
        {
            Transform child = stageRoomsRoot.GetChild(i);
            child.gameObject.SetActive(child == currentRoom.transform);
        }
    }

    private IEnumerator PlayExitTransition(GateDirection exitDirection)
    {
        float duration = Mathf.Max(cameraSlideDuration, transitionUI != null ? transitionUI.FadeOutDuration : 0f);
        Vector3 start = cameraRestPosition;
        Vector3 target = cameraRestPosition + DirectionToVector3(exitDirection) * cameraSlideDistance;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float slideT = cameraSlideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / cameraSlideDuration);
            float fadeT = transitionUI == null || transitionUI.FadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / transitionUI.FadeOutDuration);
            SetCameraBase(Vector3.Lerp(start, target, slideT), false);
            if (transitionUI != null) transitionUI.SetFadeAlpha(fadeT);
            yield return null;
        }

        SetCameraBase(target, false);
        if (transitionUI != null) transitionUI.SetFadeAlpha(1f);
    }

    private IEnumerator PlayEnterTransition(GateDirection exitDirection)
    {
        float duration = Mathf.Max(cameraSlideDuration, transitionUI != null ? transitionUI.FadeInDuration : 0f);
        Vector3 start = cameraRestPosition + DirectionToVector3(exitDirection) * (cameraSlideDistance * 0.35f);
        Vector3 target = cameraRestPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float slideT = cameraSlideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / cameraSlideDuration);
            float fadeT = transitionUI == null || transitionUI.FadeInDuration <= 0f ? 0f : 1f - Mathf.Clamp01(elapsed / transitionUI.FadeInDuration);
            SetCameraBase(Vector3.Lerp(start, target, slideT), false);
            if (transitionUI != null) transitionUI.SetFadeAlpha(fadeT);
            yield return null;
        }

        SetCameraBase(target, false);
        if (transitionUI != null) transitionUI.SetFadeAlpha(0f);
    }

    private void SetCameraBase(Vector3 position, bool snap)
    {
        if (cameraEffects != null)
            cameraEffects.SetBaseWorldPosition(position, snap);
        else if (mainCamera != null)
            mainCamera.transform.position = position;
    }

    private Vector3 DirectionToVector3(GateDirection direction)
    {
        switch (direction)
        {
            case GateDirection.Left: return Vector3.left;
            case GateDirection.Right: return Vector3.right;
            case GateDirection.Top: return Vector3.up;
            case GateDirection.Bottom: return Vector3.down;
            default: return Vector3.zero;
        }
    }

    private GateDirection GetOppositeDirection(GateDirection direction)
    {
        switch (direction)
        {
            case GateDirection.Left: return GateDirection.Right;
            case GateDirection.Right: return GateDirection.Left;
            case GateDirection.Top: return GateDirection.Bottom;
            case GateDirection.Bottom: return GateDirection.Top;
            default: return GateDirection.None;
        }
    }

    private void OnDestroy()
    {
        EndTransitionLock();
    }
}
