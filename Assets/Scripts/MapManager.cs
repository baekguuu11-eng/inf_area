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

    [Header("Transition Settings")]
    [SerializeField] private float gateCooldown = 0.15f;
    [SerializeField] private float cameraSlideDistance = 0.8f;
    [SerializeField] private float cameraSlideDuration = 0.15f;
    [SerializeField] private float blackHoldDuration = 0.04f;

    private Transform playerRoot;
    private RoomController currentRoom;
    private int currentStage = 1;
    private int highestNormalRoomNumber = 1;
    private bool transitionLocked = false;
    private Vector3 cameraRestPosition;

    private void Awake()
    {
        if (stageRoomsRoot == null)
        {
            GameObject found = GameObject.Find("StageRooms");
            if (found != null)
                stageRoomsRoot = found.transform;
        }

        if (startRoom == null)
            startRoom = FindAnyObjectByType<RoomController>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
            cameraRestPosition = mainCamera.transform.position;

        PlayerMovement playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null)
            playerRoot = playerMovement.transform;

        if (transitionUI == null)
            transitionUI = FindAnyObjectByType<RoomTransitionUI>();
    }

    private IEnumerator Start()
    {
        if (startRoom == null || roomTemplate == null || stageRoomsRoot == null)
            yield break;

        startRoom.transform.SetParent(stageRoomsRoot);
        startRoom.Setup(1, 1, false);
        startRoom.ClearConnections();

        currentRoom = startRoom;
        highestNormalRoomNumber = 1;
        currentStage = 1;

        ShowOnlyCurrentRoom();

        if (transitionUI != null)
            yield return transitionUI.ShowRoomLabel(currentStage, currentRoom.RoomNumber);
    }

    private void Update()
    {
        if (transitionLocked)
            return;

        if (Input.GetKeyDown(KeyCode.M))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void TryUseGate(RoomController fromRoom, GateDirection exitDirection)
    {
        if (transitionLocked)
            return;

        if (fromRoom == null || fromRoom != currentRoom)
            return;

        if (fromRoom.TryGetConnection(exitDirection, out RoomController existingRoom))
        {
            StartCoroutine(TransitionToRoom(existingRoom, GetOppositeDirection(exitDirection), exitDirection));
            return;
        }

        if (highestNormalRoomNumber >= 4)
            return;

        int nextRoomNumber = highestNormalRoomNumber + 1;

        RoomController newRoom = Instantiate(roomTemplate, stageRoomsRoot);
        newRoom.Setup(currentStage, nextRoomNumber, false);
        newRoom.ClearConnections();
        newRoom.gameObject.SetActive(false);

        fromRoom.SetConnection(exitDirection, newRoom);
        newRoom.SetConnection(GetOppositeDirection(exitDirection), fromRoom);

        highestNormalRoomNumber = nextRoomNumber;

        StartCoroutine(TransitionToRoom(newRoom, GetOppositeDirection(exitDirection), exitDirection));
    }

    private IEnumerator TransitionToRoom(RoomController targetRoom, GateDirection entrySide, GateDirection exitDirection)
    {
        if (targetRoom == null)
            yield break;

        transitionLocked = true;
        GameInputState.IsLocked = true;

        yield return PlayExitTransition(exitDirection);

        if (currentRoom != null)
            currentRoom.gameObject.SetActive(false);

        currentRoom = targetRoom;
        currentRoom.gameObject.SetActive(true);

        MovePlayerToSpawn(currentRoom, entrySide);
        ShowOnlyCurrentRoom();

        if (mainCamera != null)
            mainCamera.transform.position = cameraRestPosition + DirectionToVector3(exitDirection) * (cameraSlideDistance * 0.35f);

        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return PlayEnterTransition(exitDirection);

        if (transitionUI != null)
            StartCoroutine(transitionUI.ShowRoomLabel(currentStage, currentRoom.RoomNumber));

        yield return new WaitForSecondsRealtime(gateCooldown);

        GameInputState.IsLocked = false;
        transitionLocked = false;
    }

    private IEnumerator PlayExitTransition(GateDirection exitDirection)
    {
        float duration = cameraSlideDuration;

        if (transitionUI != null)
            duration = Mathf.Max(duration, transitionUI.FadeOutDuration);

        Vector3 startPos = cameraRestPosition;
        Vector3 targetPos = cameraRestPosition + DirectionToVector3(exitDirection) * cameraSlideDistance;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float slideT = cameraSlideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / cameraSlideDuration);
            float fadeT = transitionUI == null || transitionUI.FadeOutDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / transitionUI.FadeOutDuration);

            if (mainCamera != null)
                mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, slideT);

            if (transitionUI != null)
                transitionUI.SetFadeAlpha(fadeT);

            yield return null;
        }

        if (mainCamera != null)
            mainCamera.transform.position = targetPos;

        if (transitionUI != null)
            transitionUI.SetFadeAlpha(1f);
    }

    private IEnumerator PlayEnterTransition(GateDirection exitDirection)
    {
        float duration = cameraSlideDuration;

        if (transitionUI != null)
            duration = Mathf.Max(duration, transitionUI.FadeInDuration);

        Vector3 startPos = cameraRestPosition + DirectionToVector3(exitDirection) * (cameraSlideDistance * 0.35f);
        Vector3 targetPos = cameraRestPosition;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float slideT = cameraSlideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / cameraSlideDuration);
            float fadeT = transitionUI == null || transitionUI.FadeInDuration <= 0f
                ? 0f
                : 1f - Mathf.Clamp01(elapsed / transitionUI.FadeInDuration);

            if (mainCamera != null)
                mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, slideT);

            if (transitionUI != null)
                transitionUI.SetFadeAlpha(fadeT);

            yield return null;
        }

        if (mainCamera != null)
            mainCamera.transform.position = targetPos;

        if (transitionUI != null)
            transitionUI.SetFadeAlpha(0f);
    }

    private void MovePlayerToSpawn(RoomController room, GateDirection entrySide)
    {
        if (playerRoot == null || room == null)
            return;

        Transform spawnPoint = room.GetSpawnPoint(entrySide);
        if (spawnPoint != null)
            playerRoot.position = spawnPoint.position;
        else
            playerRoot.position = room.transform.position;
    }

    private void ShowOnlyCurrentRoom()
    {
        if (stageRoomsRoot == null || currentRoom == null)
            return;

        for (int i = 0; i < stageRoomsRoot.childCount; i++)
        {
            Transform child = stageRoomsRoot.GetChild(i);
            child.gameObject.SetActive(child == currentRoom.transform);
        }
    }

    private Vector3 DirectionToVector3(GateDirection direction)
    {
        switch (direction)
        {
            case GateDirection.Left:
                return Vector3.left;
            case GateDirection.Right:
                return Vector3.right;
            case GateDirection.Top:
                return Vector3.up;
            case GateDirection.Bottom:
                return Vector3.down;
            default:
                return Vector3.zero;
        }
    }

    private GateDirection GetOppositeDirection(GateDirection direction)
    {
        switch (direction)
        {
            case GateDirection.Left:
                return GateDirection.Right;
            case GateDirection.Right:
                return GateDirection.Left;
            case GateDirection.Top:
                return GateDirection.Bottom;
            case GateDirection.Bottom:
                return GateDirection.Top;
            default:
                return GateDirection.None;
        }
    }
}