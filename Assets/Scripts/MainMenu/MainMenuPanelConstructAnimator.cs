using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanelConstructAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private Image constructLine;
    [SerializeField] private CanvasGroup[] contentGroups;

    [Header("Style")]
    [SerializeField] private bool dangerStyle = false;
    [SerializeField] private Color normalLineColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Color dangerLineColor = new Color(1f, 0.18f, 0.18f, 1f);

    [Header("Open Timing")]
    [SerializeField] private float lineRiseDuration = 0.15f;
    [SerializeField] private float surfaceExpandDuration = 0.18f;
    [SerializeField] private float contentFadeDuration = 0.14f;
    [SerializeField] private float contentStagger = 0.025f;

    [Header("Close Timing")]
    [SerializeField] private float contentHideDuration = 0.10f;
    [SerializeField] private float surfaceCollapseDuration = 0.14f;
    [SerializeField] private float lineFallDuration = 0.11f;

    [Header("Motion")]
    [SerializeField] private float collapsedXScale = 0.012f;
    [SerializeField] private float contentStartYOffset = -8f;
    [SerializeField] private float lineWidth = 3f;
    [SerializeField] private float lineVerticalMargin = 20f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip constructSound;
    [SerializeField] private AudioClip collapseSound;
    [SerializeField, Range(0f, 1f)] private float constructSoundVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float collapseSoundVolume = 0.45f;

    private RectTransform panelRect;
    private RectTransform lineRect;

    private Color originalBackgroundColor;
    private Vector3 originalPanelScale;

    private readonly List<RectTransform> contentRects = new List<RectTransform>();
    private readonly List<Vector2> contentOriginalPositions = new List<Vector2>();

    private bool initialized;

    public float EstimatedOpenDuration
    {
        get
        {
            int count = contentGroups != null ? contentGroups.Length : 0;
            float contentTotal = contentFadeDuration +
                                 Mathf.Max(0, count - 1) * contentStagger;

            return Mathf.Max(
                lineRiseDuration + surfaceExpandDuration,
                lineRiseDuration + surfaceExpandDuration * 0.55f + contentTotal
            );
        }
    }

    public float EstimatedCloseDuration
    {
        get
        {
            return contentHideDuration +
                   surfaceCollapseDuration +
                   lineFallDuration;
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    public void Configure(
        Image background,
        Image line,
        CanvasGroup[] groups,
        bool useDangerStyle)
    {
        panelBackground = background;
        constructLine = line;
        contentGroups = groups;
        dangerStyle = useDangerStyle;

        initialized = false;
        EnsureInitialized();
    }

    public IEnumerator PlayOpen()
    {
        EnsureInitialized();

        if (panelRect == null)
            yield break;

        StopAllCoroutines();
        PrepareOpenState();

        PlayOneShot(constructSound, constructSoundVolume);

        yield return AnimateLineRise();

        Coroutine surfaceRoutine = StartCoroutine(AnimateSurfaceExpand());
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, surfaceExpandDuration * 0.45f)
        );

        yield return AnimateContentsIn();

        if (surfaceRoutine != null)
            yield return surfaceRoutine;

        ApplyFullyOpenState();
    }

    public IEnumerator PlayClose()
    {
        EnsureInitialized();

        if (panelRect == null)
            yield break;

        StopAllCoroutines();

        PlayOneShot(collapseSound, collapseSoundVolume);

        yield return AnimateContentsOut();
        yield return AnimateSurfaceCollapse();
        yield return AnimateLineFall();

        PrepareOpenState();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        panelRect = GetComponent<RectTransform>();

        if (panelBackground == null)
            panelBackground = GetComponent<Image>();

        if (constructLine != null)
            lineRect = constructLine.GetComponent<RectTransform>();

        originalPanelScale = panelRect != null
            ? panelRect.localScale
            : Vector3.one;

        if (panelBackground != null)
            originalBackgroundColor = panelBackground.color;

        RebuildContentCache();

        initialized = true;
    }

    private void RebuildContentCache()
    {
        contentRects.Clear();
        contentOriginalPositions.Clear();

        if (contentGroups == null)
            return;

        for (int i = 0; i < contentGroups.Length; i++)
        {
            CanvasGroup group = contentGroups[i];

            if (group == null)
            {
                contentRects.Add(null);
                contentOriginalPositions.Add(Vector2.zero);
                continue;
            }

            RectTransform rt = group.GetComponent<RectTransform>();
            contentRects.Add(rt);

            contentOriginalPositions.Add(
                rt != null ? rt.anchoredPosition : Vector2.zero
            );
        }
    }

    private void PrepareOpenState()
    {
        if (panelRect != null)
        {
            panelRect.localScale = new Vector3(
                originalPanelScale.x * collapsedXScale,
                originalPanelScale.y,
                originalPanelScale.z
            );
        }

        SetBackgroundAlpha(0f);

        Color lineColor = dangerStyle
            ? dangerLineColor
            : normalLineColor;

        if (constructLine != null)
        {
            constructLine.gameObject.SetActive(true);

            Color c = lineColor;
            c.a = 1f;
            constructLine.color = c;
        }

        PrepareLineRect(0f);

        if (contentGroups != null)
        {
            for (int i = 0; i < contentGroups.Length; i++)
            {
                CanvasGroup group = contentGroups[i];

                if (group == null)
                    continue;

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                RectTransform rt = i < contentRects.Count
                    ? contentRects[i]
                    : null;

                if (rt != null && i < contentOriginalPositions.Count)
                {
                    rt.anchoredPosition =
                        contentOriginalPositions[i] +
                        new Vector2(0f, contentStartYOffset);
                }
            }
        }
    }

    private IEnumerator AnimateLineRise()
    {
        if (constructLine == null || lineRect == null || panelRect == null)
            yield break;

        float targetHeight = Mathf.Max(
            20f,
            panelRect.rect.height - lineVerticalMargin * 2f
        );

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, lineRiseDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(elapsed / duration);

            PrepareLineRect(targetHeight * t);
            yield return null;
        }

        PrepareLineRect(targetHeight);
    }

    private IEnumerator AnimateSurfaceExpand()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, surfaceExpandDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(elapsed / duration);

            if (panelRect != null)
            {
                panelRect.localScale = new Vector3(
                    Mathf.Lerp(
                        originalPanelScale.x * collapsedXScale,
                        originalPanelScale.x,
                        t
                    ),
                    originalPanelScale.y,
                    originalPanelScale.z
                );
            }

            SetBackgroundAlpha(t);

            if (constructLine != null)
            {
                Color c = constructLine.color;
                c.a = Mathf.Lerp(1f, 0.15f, t);
                constructLine.color = c;
            }

            yield return null;
        }

        if (panelRect != null)
            panelRect.localScale = originalPanelScale;

        SetBackgroundAlpha(1f);

        if (constructLine != null)
        {
            Color c = constructLine.color;
            c.a = 0f;
            constructLine.color = c;
        }
    }

    private IEnumerator AnimateContentsIn()
    {
        if (contentGroups == null || contentGroups.Length == 0)
            yield break;

        int count = contentGroups.Length;

        float totalDuration = Mathf.Max(
            0.01f,
            contentFadeDuration + Mathf.Max(0, count - 1) * contentStagger
        );

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < count; i++)
            {
                CanvasGroup group = contentGroups[i];

                if (group == null)
                    continue;

                float localTime = elapsed - i * contentStagger;
                float t = Mathf.Clamp01(
                    localTime / Mathf.Max(0.01f, contentFadeDuration)
                );

                float smooth = EaseOutCubic(t);
                group.alpha = smooth;

                RectTransform rt = i < contentRects.Count
                    ? contentRects[i]
                    : null;

                if (rt != null && i < contentOriginalPositions.Count)
                {
                    rt.anchoredPosition = Vector2.Lerp(
                        contentOriginalPositions[i] +
                        new Vector2(0f, contentStartYOffset),
                        contentOriginalPositions[i],
                        smooth
                    );
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            CanvasGroup group = contentGroups[i];

            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            RectTransform rt = i < contentRects.Count
                ? contentRects[i]
                : null;

            if (rt != null && i < contentOriginalPositions.Count)
                rt.anchoredPosition = contentOriginalPositions[i];
        }
    }

    private IEnumerator AnimateContentsOut()
    {
        if (contentGroups == null || contentGroups.Length == 0)
            yield break;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, contentHideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < contentGroups.Length; i++)
            {
                CanvasGroup group = contentGroups[i];

                if (group == null)
                    continue;

                group.alpha = 1f - t;
                group.interactable = false;
                group.blocksRaycasts = false;

                RectTransform rt = i < contentRects.Count
                    ? contentRects[i]
                    : null;

                if (rt != null && i < contentOriginalPositions.Count)
                {
                    rt.anchoredPosition = Vector2.Lerp(
                        contentOriginalPositions[i],
                        contentOriginalPositions[i] +
                        new Vector2(0f, contentStartYOffset),
                        t
                    );
                }
            }

            yield return null;
        }

        for (int i = 0; i < contentGroups.Length; i++)
        {
            if (contentGroups[i] != null)
                contentGroups[i].alpha = 0f;
        }
    }

    private IEnumerator AnimateSurfaceCollapse()
    {
        if (constructLine != null)
        {
            constructLine.gameObject.SetActive(true);

            Color c = dangerStyle
                ? dangerLineColor
                : normalLineColor;

            c.a = 0.15f;
            constructLine.color = c;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, surfaceCollapseDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInCubic(elapsed / duration);

            if (panelRect != null)
            {
                panelRect.localScale = new Vector3(
                    Mathf.Lerp(
                        originalPanelScale.x,
                        originalPanelScale.x * collapsedXScale,
                        t
                    ),
                    originalPanelScale.y,
                    originalPanelScale.z
                );
            }

            SetBackgroundAlpha(1f - t);

            if (constructLine != null)
            {
                Color c = constructLine.color;
                c.a = Mathf.Lerp(0.15f, 1f, t);
                constructLine.color = c;
            }

            yield return null;
        }

        SetBackgroundAlpha(0f);
    }

    private IEnumerator AnimateLineFall()
    {
        if (constructLine == null || lineRect == null || panelRect == null)
            yield break;

        float startHeight = Mathf.Max(
            20f,
            panelRect.rect.height - lineVerticalMargin * 2f
        );

        PrepareLineRect(startHeight);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, lineFallDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInCubic(elapsed / duration);

            PrepareLineRect(
                Mathf.Lerp(startHeight, 0f, t)
            );

            yield return null;
        }

        PrepareLineRect(0f);

        if (constructLine != null)
        {
            Color c = constructLine.color;
            c.a = 0f;
            constructLine.color = c;
        }
    }

    private void ApplyFullyOpenState()
    {
        if (panelRect != null)
            panelRect.localScale = originalPanelScale;

        SetBackgroundAlpha(1f);

        if (constructLine != null)
        {
            Color c = constructLine.color;
            c.a = 0f;
            constructLine.color = c;
        }

        if (contentGroups == null)
            return;

        for (int i = 0; i < contentGroups.Length; i++)
        {
            CanvasGroup group = contentGroups[i];

            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            RectTransform rt = i < contentRects.Count
                ? contentRects[i]
                : null;

            if (rt != null && i < contentOriginalPositions.Count)
                rt.anchoredPosition = contentOriginalPositions[i];
        }
    }

    private void PrepareLineRect(float height)
    {
        if (lineRect == null || panelRect == null)
            return;

        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0f);

        float panelHeight = panelRect.rect.height;
        float y = -panelHeight * 0.5f + lineVerticalMargin;

        lineRect.anchoredPosition = new Vector2(0f, y);
        lineRect.sizeDelta = new Vector2(
            Mathf.Max(1f, lineWidth),
            Mathf.Max(0f, height)
        );
    }

    private void SetBackgroundAlpha(float normalized)
    {
        if (panelBackground == null)
            return;

        Color c = originalBackgroundColor;
        c.a = originalBackgroundColor.a * Mathf.Clamp01(normalized);
        panelBackground.color = c;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (uiAudioSource == null || clip == null)
            return;

        uiAudioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(volume)
        );
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private void OnValidate()
    {
        lineRiseDuration = Mathf.Max(0f, lineRiseDuration);
        surfaceExpandDuration = Mathf.Max(0f, surfaceExpandDuration);
        contentFadeDuration = Mathf.Max(0f, contentFadeDuration);
        contentStagger = Mathf.Max(0f, contentStagger);

        contentHideDuration = Mathf.Max(0f, contentHideDuration);
        surfaceCollapseDuration = Mathf.Max(0f, surfaceCollapseDuration);
        lineFallDuration = Mathf.Max(0f, lineFallDuration);

        collapsedXScale = Mathf.Clamp(collapsedXScale, 0.001f, 0.2f);
        lineWidth = Mathf.Max(1f, lineWidth);
        lineVerticalMargin = Mathf.Max(0f, lineVerticalMargin);
    }
}
