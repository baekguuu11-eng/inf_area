using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EndingCreditsGallerySlideshow : MonoBehaviour
{
    [SerializeField] private Image displayImage;
    [SerializeField] private CanvasGroup displayGroup;
    [SerializeField] private Sprite[] slides;

    [Header("Timing")]
    [SerializeField] private float slideDuration = 4f;
    [SerializeField] private float fadeDuration = 0.35f;

    private Coroutine playRoutine;

    public void PlaySlideshow()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void StopSlideshow()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = null;

        if (displayGroup != null)
            displayGroup.alpha = 0f;
    }

    private IEnumerator PlayRoutine()
    {
        if (displayImage == null || displayGroup == null || slides == null || slides.Length == 0)
            yield break;

        displayGroup.alpha = 0f;

        for (int i = 0; i < slides.Length; i++)
        {
            displayImage.sprite = slides[i];
            displayImage.SetNativeSize();

            yield return Fade(0f, 1f, fadeDuration);
            yield return new WaitForSecondsRealtime(slideDuration);
            yield return Fade(1f, 0f, fadeDuration);
        }

        displayGroup.alpha = 0f;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        displayGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            displayGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        displayGroup.alpha = to;
    }
}