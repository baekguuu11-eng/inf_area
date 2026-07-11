using System.Collections;
using UnityEngine;

public class LoadingPanelAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup loadingCanvasGroup;
    [SerializeField] private RectTransform loadingTipText;
    [SerializeField] private RectTransform loadingPercentText;
    [SerializeField] private RectTransform loadingBarBack;
    [SerializeField] private RectTransform loadingBarFill;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float startOffsetY = -18f;

    private Vector2 tipOriginalPos;
    private Vector2 percentOriginalPos;
    private Vector2 barBackOriginalPos;
    private Vector2 barFillOriginalPos;

    private Coroutine appearCoroutine;

    private void Awake()
    {
        if (loadingCanvasGroup == null)
            loadingCanvasGroup = GetComponent<CanvasGroup>();

        if (loadingCanvasGroup == null)
            loadingCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheOriginalPositions();
    }

    private void OnEnable()
    {
        CacheOriginalPositions();

        if (appearCoroutine != null)
            StopCoroutine(appearCoroutine);

        appearCoroutine = StartCoroutine(AppearRoutine());
    }

    private void CacheOriginalPositions()
    {
        if (loadingTipText != null)
            tipOriginalPos = loadingTipText.anchoredPosition;

        if (loadingPercentText != null)
            percentOriginalPos = loadingPercentText.anchoredPosition;

        if (loadingBarBack != null)
            barBackOriginalPos = loadingBarBack.anchoredPosition;

        if (loadingBarFill != null)
            barFillOriginalPos = loadingBarFill.anchoredPosition;
    }

    private IEnumerator AppearRoutine()
    {
        loadingCanvasGroup.alpha = 0f;

        SetOffset(loadingTipText, tipOriginalPos);
        SetOffset(loadingPercentText, percentOriginalPos);
        SetOffset(loadingBarBack, barBackOriginalPos);
        SetOffset(loadingBarFill, barFillOriginalPos);

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            loadingCanvasGroup.alpha = smoothT;

            MoveToOriginal(loadingTipText, tipOriginalPos, smoothT);
            MoveToOriginal(loadingPercentText, percentOriginalPos, smoothT);
            MoveToOriginal(loadingBarBack, barBackOriginalPos, smoothT);
            MoveToOriginal(loadingBarFill, barFillOriginalPos, smoothT);

            yield return null;
        }

        loadingCanvasGroup.alpha = 1f;

        MoveToOriginal(loadingTipText, tipOriginalPos, 1f);
        MoveToOriginal(loadingPercentText, percentOriginalPos, 1f);
        MoveToOriginal(loadingBarBack, barBackOriginalPos, 1f);
        MoveToOriginal(loadingBarFill, barFillOriginalPos, 1f);

        appearCoroutine = null;
    }

    private void SetOffset(RectTransform target, Vector2 originalPos)
    {
        if (target == null)
            return;

        target.anchoredPosition = originalPos + new Vector2(0f, startOffsetY);
    }

    private void MoveToOriginal(RectTransform target, Vector2 originalPos, float t)
    {
        if (target == null)
            return;

        Vector2 startPos = originalPos + new Vector2(0f, startOffsetY);
        target.anchoredPosition = Vector2.Lerp(startPos, originalPos, t);
    }
}