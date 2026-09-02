using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExecutorCameraDirector : MonoBehaviour
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

    public float CombatOrthoSize => combatOrthoSize;
    public Vector3 CombatWorldPosition => combatWorldPosition;

    /// <summary>
    /// 방 배경의 실제 Renderer bounds를 사용해 컷신 카메라가 맵 밖의 공허를 비추지 않게 한다.
    /// </summary>
    public void ConfigureRoomBounds(RoomController room)
    {
        hasRoomVisualBounds = false;
        if (room == null)
            return;

        SpriteRenderer background = null;
        Transform named = room.transform.Find("Background");
        if (named != null)
            background = named.GetComponent<SpriteRenderer>();

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
            return;
        }

        // 구형 방 프리팹 안전 대체. EnemySpawnArea보다 바깥 벽까지 여유를 둔다.
        if (room.EnemySpawnArea != null)
        {
            roomVisualBounds = room.EnemySpawnArea.bounds;
            roomVisualBounds.Expand(new Vector3(4.4f, 4.4f, 0f));
            hasRoomVisualBounds = true;
        }
    }

    public void Capture()
    {
        if (captured)
            return;

        targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        feedback = targetCamera.GetComponent<CameraFeedbackController>();
        combatWorldPosition = targetCamera.transform.position;
        combatOrthoSize = targetCamera.orthographicSize;
        combatOrthoSize = ClampOrthoSize(combatOrthoSize);
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
        if (targetCamera == null)
            yield break;

        Vector3 startPosition = GetBaseWorldPosition();
        float startSize = targetCamera.orthographicSize;
        worldPosition.z = combatWorldPosition.z;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            if (skipCheck != null && skipCheck())
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = EaseInOutCubic(t);
            float currentSize = Mathf.Lerp(startSize, orthographicSize, eased);
            currentSize = ClampOrthoSize(currentSize);
            Vector3 currentPosition = Vector3.Lerp(startPosition, worldPosition, eased);
            currentPosition = ClampPositionToRoom(currentPosition, currentSize);

            SetBaseWorldPosition(currentPosition);
            targetCamera.orthographicSize = currentSize;
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
        if (targetCamera == null)
            return;

        directedBaseWorldPosition = combatWorldPosition;
        if (feedback != null)
            feedback.SetBaseWorldPosition(combatWorldPosition, true);
        else
            targetCamera.transform.position = combatWorldPosition;
        targetCamera.orthographicSize = combatOrthoSize;
        RestorePixelPerfect();
    }

    public void Punch(Vector2 direction, float duration, float magnitude)
    {
        if (feedback == null && targetCamera != null)
            feedback = targetCamera.GetComponent<CameraFeedbackController>();

        if (feedback != null)
            feedback.Shake(duration, magnitude, direction);
        else if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.DirectionalShake(duration, magnitude, direction);
    }

    public void RestorePixelPerfect()
    {
        if (pixelPerfectCamera != null)
            pixelPerfectCamera.enabled = pixelPerfectWasEnabled;
    }

    private float ClampOrthoSize(float requestedSize)
    {
        if (!hasRoomVisualBounds || targetCamera == null)
            return requestedSize;

        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        float maxByHeight = roomVisualBounds.extents.y;
        float maxByWidth = roomVisualBounds.extents.x / aspect;
        float maxSize = Mathf.Max(0.1f, Mathf.Min(maxByHeight, maxByWidth));
        return Mathf.Min(requestedSize, maxSize);
    }

    private Vector3 ClampPositionToRoom(Vector3 position, float orthoSize)
    {
        if (!hasRoomVisualBounds || targetCamera == null)
            return position;

        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        float halfHeight = Mathf.Max(0.01f, orthoSize);
        float halfWidth = halfHeight * aspect;

        // 줌인할수록 흔들림 여유를 확보한다. 기본 전투 크기에서는 화면 구도를 바꾸지 않는다.
        float zoom01 = combatOrthoSize > 0.01f
            ? Mathf.Clamp01((combatOrthoSize - orthoSize) / (combatOrthoSize * 0.45f))
            : 0f;
        // 카메라 흔들림과 픽셀 퍼펙트 보정까지 고려해 여유를 넉넉하게 둔다.
        float edgeGuard = Mathf.Lerp(0.16f, 0.52f, zoom01);

        float minX = roomVisualBounds.min.x + halfWidth + edgeGuard;
        float maxX = roomVisualBounds.max.x - halfWidth - edgeGuard;
        float minY = roomVisualBounds.min.y + halfHeight + edgeGuard;
        float maxY = roomVisualBounds.max.y - halfHeight - edgeGuard;

        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : roomVisualBounds.center.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : roomVisualBounds.center.y;
        position.z = combatWorldPosition.z;
        return position;
    }

    private Vector3 GetBaseWorldPosition()
    {
        if (targetCamera == null)
            return combatWorldPosition;

        return directedBaseWorldPosition;
    }

    private void SetBaseWorldPosition(Vector3 worldPosition)
    {
        if (targetCamera == null)
            return;

        directedBaseWorldPosition = worldPosition;
        if (feedback != null)
            feedback.SetBaseWorldPosition(worldPosition, false);
        else
            targetCamera.transform.position = worldPosition;
    }

    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    private void OnDestroy()
    {
        RestorePixelPerfect();
    }
}
