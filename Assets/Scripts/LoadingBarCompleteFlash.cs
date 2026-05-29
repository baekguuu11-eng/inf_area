using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LoadingBarCompleteFlash : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    [Header("Complete Flash")]
    [SerializeField] private bool enableCompleteFlash = true;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.16f;
    [SerializeField] private float completeThreshold = 0.999f;

    private Color originalColor;
    private bool hasFlashed = false;
    private Coroutine flashCoroutine;

    private void Reset()
    {
        fillImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (fillImage == null)
            fillImage = GetComponent<Image>();

        if (fillImage != null)
            originalColor = fillImage.color;
    }

    private void OnEnable()
    {
        hasFlashed = false;

        if (fillImage != null)
            originalColor = fillImage.color;
    }

    private void Update()
    {
        if (!enableCompleteFlash || fillImage == null)
            return;

        if (!hasFlashed && fillImage.fillAmount >= completeThreshold)
        {
            hasFlashed = true;

            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);

            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        float half = flashDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            fillImage.color = Color.Lerp(originalColor, flashColor, t);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            fillImage.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        fillImage.color = originalColor;
        flashCoroutine = null;
    }
}