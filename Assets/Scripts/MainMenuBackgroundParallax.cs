using UnityEngine;

public class MainMenuBackgroundParallax : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform backgroundRect;

    [Header("Movement")]
    [SerializeField] private bool enableParallax = true;
    [SerializeField] private Vector2 maxMoveAmount = new Vector2(18f, 10f);
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool invertMovement = true;

    private Vector2 originalAnchoredPosition;

    private void Reset()
    {
        backgroundRect = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (backgroundRect == null)
            backgroundRect = GetComponent<RectTransform>();

        if (backgroundRect != null)
            originalAnchoredPosition = backgroundRect.anchoredPosition;
    }

    private void Update()
    {
        if (!enableParallax || backgroundRect == null)
            return;

        Vector2 mousePosition = Input.mousePosition;

        float normalizedX = (mousePosition.x / Screen.width - 0.5f) * 2f;
        float normalizedY = (mousePosition.y / Screen.height - 0.5f) * 2f;

        normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
        normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);

        Vector2 targetOffset = new Vector2(
            normalizedX * maxMoveAmount.x,
            normalizedY * maxMoveAmount.y
        );

        if (invertMovement)
            targetOffset *= -1f;

        Vector2 targetPosition = originalAnchoredPosition + targetOffset;

        backgroundRect.anchoredPosition = Vector2.Lerp(
            backgroundRect.anchoredPosition,
            targetPosition,
            Time.unscaledDeltaTime * smoothSpeed
        );
    }
}