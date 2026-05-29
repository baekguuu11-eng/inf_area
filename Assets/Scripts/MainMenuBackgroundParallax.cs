using UnityEngine;

public class MainMenuBackgroundParallax : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform backgroundRect;

    [Header("Parallax")]
    [SerializeField] private bool enableParallaxOnStart = false;
    [SerializeField] private Vector2 maxMoveAmount = new Vector2(18f, 10f);
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool invertMovement = true;

    private Vector2 basePosition;
    private bool isParallaxEnabled;

    private void Reset()
    {
        backgroundRect = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (backgroundRect == null)
            backgroundRect = GetComponent<RectTransform>();

        if (backgroundRect != null)
            basePosition = backgroundRect.anchoredPosition;

        isParallaxEnabled = enableParallaxOnStart;
    }

    private void Update()
    {
        if (!isParallaxEnabled || backgroundRect == null)
            return;

        float normalizedX = (Input.mousePosition.x / Screen.width - 0.5f) * 2f;
        float normalizedY = (Input.mousePosition.y / Screen.height - 0.5f) * 2f;

        normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
        normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);

        Vector2 offset = new Vector2(
            normalizedX * maxMoveAmount.x,
            normalizedY * maxMoveAmount.y
        );

        if (invertMovement)
            offset *= -1f;

        Vector2 targetPosition = basePosition + offset;

        backgroundRect.anchoredPosition = Vector2.Lerp(
            backgroundRect.anchoredPosition,
            targetPosition,
            Time.unscaledDeltaTime * smoothSpeed
        );
    }

    public void SetBasePosition(Vector2 position)
    {
        basePosition = position;
    }

    public void SetParallaxEnabled(bool enabled)
    {
        isParallaxEnabled = enabled;
    }

    public void ResetToBasePosition()
    {
        if (backgroundRect != null)
            backgroundRect.anchoredPosition = basePosition;
    }
}