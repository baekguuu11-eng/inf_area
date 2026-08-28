using UnityEngine;

[DisallowMultipleComponent]
public class GameFeelManager : MonoBehaviour
{
    public static GameFeelManager Instance { get; private set; }

    private CameraFeedbackController feedback;

    private void Awake()
    {
        if (Instance == null || Instance == this)
            Instance = this;

        ResolveFeedback();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveFeedback()
    {
        if (feedback != null)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            feedback = mainCamera.GetComponent<CameraFeedbackController>();
            if (feedback == null)
                feedback = mainCamera.gameObject.AddComponent<CameraFeedbackController>();
        }
        else
        {
            feedback = GetComponent<CameraFeedbackController>();
            if (feedback == null)
                feedback = gameObject.AddComponent<CameraFeedbackController>();
        }
    }

    /// <summary>
    /// MapManager와의 호환 API입니다. 방 이동 기준 위치를 카메라 피드백 시스템에 전달합니다.
    /// </summary>
    public void SetBaseWorldPosition(Vector3 worldPosition, bool snap)
    {
        ResolveFeedback();
        if (feedback != null)
            feedback.SetBaseWorldPosition(worldPosition, snap);
        else
            transform.position = worldPosition;
    }

    public void DoHitStop(float duration)
    {
        ResolveFeedback();
        if (feedback != null)
            feedback.HitStop(duration);
    }

    public void Shake(float duration, float magnitude)
    {
        ResolveFeedback();
        if (feedback != null)
            feedback.Shake(duration, magnitude);
    }

    public void DirectionalShake(float duration, float magnitude, Vector2 direction)
    {
        ResolveFeedback();
        if (feedback != null)
            feedback.Shake(duration, magnitude, direction);
    }
}
