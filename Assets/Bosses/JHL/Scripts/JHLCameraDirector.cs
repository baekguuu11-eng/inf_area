using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class JHLCameraDirector : MonoBehaviour
{
    private Camera targetCamera;
    private CameraFeedbackController feedback;
    private Behaviour pixelPerfectCamera;
    private bool pixelPerfectWasEnabled;
    private Vector3 combatWorldPosition;
    private float combatOrthoSize;
    private bool captured;
    private Vector3 directedBaseWorldPosition;
    private Bounds roomVisualBounds;
    private bool hasRoomVisualBounds;
    private bool usesLayoutV24;

    public float CombatOrthoSize => combatOrthoSize;
    public Vector3 CombatWorldPosition => combatWorldPosition;

    public void ConfigureRoomBounds(RoomController room)
    {
        usesLayoutV24 = room != null && room.GetComponent<RoomLayoutV24>() != null;
        hasRoomVisualBounds = false;
        if (room == null) return;

        SpriteRenderer background = null;
        Transform named = room.transform.Find("Background");
        if (named != null) background = named.GetComponent<SpriteRenderer>();
        if (background == null)
        {
            SpriteRenderer[] renderers = room.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer candidate = renderers[i];
                if (candidate != null && candidate.gameObject.name.ToLowerInvariant().Contains("background"))
                {
                    background = candidate;
                    break;
                }
            }
        }

        if (background != null)
        {
            roomVisualBounds = background.bounds;
            hasRoomVisualBounds = roomVisualBounds.size.x > 0.1f && roomVisualBounds.size.y > 0.1f;
        }
        else
        {
            Transform left = room.GetSpawnPoint(GateDirection.Left);
            Transform right = room.GetSpawnPoint(GateDirection.Right);
            Transform top = room.GetSpawnPoint(GateDirection.Top);
            Transform bottom = room.GetSpawnPoint(GateDirection.Bottom);
            if (left != null && right != null && top != null && bottom != null)
            {
                const float edgePadding = 0.34f;
                float minX = Mathf.Min(left.position.x, right.position.x) - edgePadding;
                float maxX = Mathf.Max(left.position.x, right.position.x) + edgePadding;
                float minY = Mathf.Min(bottom.position.y, top.position.y) - edgePadding;
                float maxY = Mathf.Max(bottom.position.y, top.position.y) + edgePadding;
                roomVisualBounds = new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                    new Vector3(Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY), 1f));
                hasRoomVisualBounds = true;
            }
            else if (room.EnemySpawnArea != null)
            {
                roomVisualBounds = room.EnemySpawnArea.bounds;
                roomVisualBounds.Expand(new Vector3(4.4f, 4.4f, 0f));
                hasRoomVisualBounds = true;
            }
        }
    }

    public void Capture()
    {
        if (captured) return;
        targetCamera = Camera.main;
        if (targetCamera == null) return;
        feedback = targetCamera.GetComponent<CameraFeedbackController>();
        combatWorldPosition = targetCamera.transform.position;
        combatOrthoSize = ClampOrthoSize(targetCamera.orthographicSize);
        combatWorldPosition = ClampPositionToRoom(combatWorldPosition, combatOrthoSize);
        directedBaseWorldPosition = combatWorldPosition;

        Behaviour[] behaviours = targetCamera.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour candidate = behaviours[i];
            if (candidate != null && candidate.GetType().Name == "PixelPerfectCamera")
            {
                pixelPerfectCamera = candidate;
                break;
            }
        }
        if (pixelPerfectCamera != null)
        {
            pixelPerfectWasEnabled = pixelPerfectCamera.enabled;
            pixelPerfectCamera.enabled = false;
        }
        captured = true;
    }

    public IEnumerator MoveTo(Vector3 worldPosition, float orthographicSize, float duration, Func<bool> skipCheck)
    {
        Capture();
        if (targetCamera == null) yield break;

        Vector3 startPosition = directedBaseWorldPosition;
        float startSize = targetCamera.orthographicSize;
        worldPosition.z = combatWorldPosition.z;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            if (skipCheck != null && skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            float size = ClampOrthoSize(Mathf.Lerp(startSize, orthographicSize, eased));
            Vector3 position = ClampPositionToRoom(Vector3.Lerp(startPosition, worldPosition, eased), size);
            SetBaseWorldPosition(position);
            targetCamera.orthographicSize = size;
            yield return null;
        }

        float finalSize = ClampOrthoSize(orthographicSize);
        SetBaseWorldPosition(ClampPositionToRoom(worldPosition, finalSize));
        targetCamera.orthographicSize = finalSize;
    }

    public IEnumerator ReturnToCombat(float duration, Func<bool> skipCheck)
    {
        yield return MoveTo(combatWorldPosition, combatOrthoSize, duration, skipCheck);
    }

    public void SnapToCombat()
    {
        Capture();
        if (targetCamera == null) return;
        directedBaseWorldPosition = combatWorldPosition;
        if (feedback != null) feedback.SetBaseWorldPosition(combatWorldPosition, true);
        else targetCamera.transform.position = combatWorldPosition;
        targetCamera.orthographicSize = combatOrthoSize;
        RestorePixelPerfect();
    }

    public void Punch(Vector2 direction, float duration, float magnitude)
    {
        if (feedback == null && targetCamera != null)
            feedback = targetCamera.GetComponent<CameraFeedbackController>();
        if (feedback != null) feedback.Shake(duration, magnitude, direction);
        else if (GameFeelManager.Instance != null) GameFeelManager.Instance.DirectionalShake(duration, magnitude, direction);
    }

    public void RestorePixelPerfect()
    {
        if (usesLayoutV24) return; // MapManager owns restoration across room transitions.
        if (pixelPerfectCamera != null) pixelPerfectCamera.enabled = pixelPerfectWasEnabled;
    }

    private float ClampOrthoSize(float requestedSize)
    {
        if (usesLayoutV24) return requestedSize;
        if (!hasRoomVisualBounds || targetCamera == null) return requestedSize;
        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        float maxSize = Mathf.Max(0.1f, Mathf.Min(roomVisualBounds.extents.y, roomVisualBounds.extents.x / aspect));
        return Mathf.Min(requestedSize, maxSize);
    }

    private Vector3 ClampPositionToRoom(Vector3 position, float orthoSize)
    {
        if (!hasRoomVisualBounds || targetCamera == null) return position;
        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        float halfHeight = Mathf.Max(0.01f, orthoSize);
        float halfWidth = halfHeight * aspect;
        float minX = roomVisualBounds.min.x + halfWidth + 0.20f;
        float maxX = roomVisualBounds.max.x - halfWidth - 0.20f;
        float minY = roomVisualBounds.min.y + halfHeight + 0.20f;
        float maxY = roomVisualBounds.max.y - halfHeight - 0.20f;
        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : roomVisualBounds.center.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : roomVisualBounds.center.y;
        position.z = combatWorldPosition.z;
        return position;
    }

    private void SetBaseWorldPosition(Vector3 worldPosition)
    {
        directedBaseWorldPosition = worldPosition;
        if (feedback != null) feedback.SetBaseWorldPosition(worldPosition, false);
        else if (targetCamera != null) targetCamera.transform.position = worldPosition;
    }

    private void OnDestroy()
    {
        RestorePixelPerfect();
    }
}
