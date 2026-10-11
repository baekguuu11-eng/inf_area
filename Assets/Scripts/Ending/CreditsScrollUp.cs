using UnityEngine;

public class CreditsScrollUp : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private float scrollSpeed = 220f;   // 더 빠르게
    [SerializeField] private float startY = -1400f;      // 시작 위치
    [SerializeField] private bool useUnscaledTime = true;

    private bool isPlaying = false;

    private void OnEnable()
    {
        ResetPosition();
    }

    public void Begin()
    {
        ResetPosition();
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
    }

    public void ResetPosition()
    {
        if (contentRoot == null) return;

        Vector2 pos = contentRoot.anchoredPosition;
        pos.y = startY;
        contentRoot.anchoredPosition = pos;
    }

    private void Update()
    {
        if (!isPlaying || contentRoot == null) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        Vector2 pos = contentRoot.anchoredPosition;
        pos.y += scrollSpeed * dt;   // 위로 올라감
        contentRoot.anchoredPosition = pos;
    }
}