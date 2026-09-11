using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShopButtonFeedbackV13 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rect;
    private Image image;
    private Vector3 baseScale;
    private Color baseColor;
    private Coroutine scaleRoutine;

    private void Awake()
    {
        rect = transform as RectTransform;
        image = GetComponent<Image>();
        baseScale = rect != null ? rect.localScale : Vector3.one;
        baseColor = image != null ? image.color : Color.white;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (image != null) image.color = Color.Lerp(baseColor, new Color(0.22f, 0.88f, 0.96f, baseColor.a), 0.42f);
        AnimateScale(baseScale * 1.025f, 0.08f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (image != null) image.color = baseColor;
        AnimateScale(baseScale, 0.08f);
    }

    public void OnPointerDown(PointerEventData eventData) => AnimateScale(baseScale * 0.97f, 0.045f);
    public void OnPointerUp(PointerEventData eventData) => AnimateScale(baseScale * 1.025f, 0.055f);

    private void AnimateScale(Vector3 target, float duration)
    {
        if (rect == null) return;
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleRoutine(target, duration));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration)
    {
        Vector3 start = rect.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
            rect.localScale = Vector3.Lerp(start, target, 1f - Mathf.Pow(1f - p, 3f));
            yield return null;
        }
        rect.localScale = target;
        scaleRoutine = null;
    }
}
