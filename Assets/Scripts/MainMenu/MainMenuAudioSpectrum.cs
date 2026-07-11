using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class MainMenuAudioSpectrum : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource bgmAudioSource;

    [Header("Spectrum Settings")]
    [SerializeField, Range(8, 128)] private int barCount = 64;
    [SerializeField] private float minHeight = 4f;
    [SerializeField] private float maxHeight = 90f;
    [SerializeField] private float sensitivity = 250f;
    [SerializeField] private float smoothSpeed = 12f;
    [SerializeField] private float frequencyCurve = 1.6f;

    [Header("Layout")]
    [SerializeField, Range(0.1f, 1f)] private float widthRatio = 0.95f;
    [SerializeField, Range(0.1f, 1f)] private float barWidthRatio = 0.65f;
    [SerializeField] private bool mirrorFromCenter = true;

    [Header("Intro Fade")]
    [SerializeField] private bool hideOnStart = true;

    [Tooltip("스펙트럼이 최종적으로 얼마나 진하게 보일지 조절")]
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 0.35f;

    [Tooltip("버튼들이 다 올라온 뒤 스펙트럼이 서서히 나타나는 데 걸리는 시간")]
    [SerializeField] private float fadeInDuration = 1.2f;

    [Header("Visual")]
    [SerializeField] private Color barColor = new Color(0.2f, 1f, 0.9f, 1f);

    private const int SpectrumSize = 1024;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private RectTransform[] bars;
    private float[] spectrum;
    private float[] currentHeights;

    private bool isReady;
    private bool needsRebuild;
    private int lastBarCount;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        spectrum = new float[SpectrumSize];

        if (hideOnStart)
            HideInstant();
        else
            ShowInstant();
    }

    private void Start()
    {
        RebuildBars();
        isReady = true;

        if (hideOnStart)
            HideInstant();
        else
            ShowInstant();
    }

    private void Update()
    {
        if (needsRebuild || bars == null || lastBarCount != barCount)
            RebuildBars();

        if (bgmAudioSource == null || bars == null || bars.Length == 0)
            return;

        bgmAudioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        UpdateBars();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isReady)
            needsRebuild = true;
    }

    private void RebuildBars()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        ClearBars();

        if (barCount < 2)
            barCount = 2;

        if (barCount % 2 != 0)
            barCount += 1;

        lastBarCount = barCount;

        bars = new RectTransform[barCount];
        currentHeights = new float[barCount];

        float totalWidth = Mathf.Max(1f, rectTransform.rect.width);
        float usableWidth = totalWidth * widthRatio;
        float spacing = usableWidth / barCount;
        float barWidth = spacing * barWidthRatio;
        float startX = -usableWidth * 0.5f + spacing * 0.5f;

        for (int i = 0; i < barCount; i++)
        {
            GameObject barObject = new GameObject("SpectrumBar_" + i);
            barObject.transform.SetParent(transform, false);

            Image image = barObject.AddComponent<Image>();
            image.color = barColor;
            image.raycastTarget = false;

            RectTransform bar = barObject.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);

            bar.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            bar.sizeDelta = new Vector2(barWidth, minHeight);

            bars[i] = bar;
            currentHeights[i] = minHeight;
        }

        needsRebuild = false;
    }

    private void ClearBars()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private void UpdateBars()
    {
        float center = (bars.Length - 1) * 0.5f;

        for (int i = 0; i < bars.Length; i++)
        {
            float normalizedIndex;

            if (mirrorFromCenter)
            {
                float distanceFromCenter = Mathf.Abs(i - center);
                normalizedIndex = distanceFromCenter / Mathf.Max(1f, center);
            }
            else
            {
                normalizedIndex = i / (float)Mathf.Max(1, bars.Length - 1);
            }

            float curvedIndex = Mathf.Pow(normalizedIndex, frequencyCurve);
            int spectrumIndex = Mathf.FloorToInt(curvedIndex * (SpectrumSize - 1) * 0.45f);
            spectrumIndex = Mathf.Clamp(spectrumIndex, 0, SpectrumSize - 1);

            float targetHeight = Mathf.Clamp(
                spectrum[spectrumIndex] * sensitivity * maxHeight,
                minHeight,
                maxHeight
            );

            currentHeights[i] = Mathf.Lerp(
                currentHeights[i],
                targetHeight,
                Time.unscaledDeltaTime * smoothSpeed
            );

            Vector2 size = bars[i].sizeDelta;
            size.y = currentHeights[i];
            bars[i].sizeDelta = size;
        }
    }

    public void HideInstant()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void ShowInstant()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = visibleAlpha;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void PlayFadeIn()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine(canvasGroup.alpha, visibleAlpha, fadeInDuration));
    }

    public void PlayFadeOut(float duration)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine(canvasGroup.alpha, 0f, duration));
    }

    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
    }
}