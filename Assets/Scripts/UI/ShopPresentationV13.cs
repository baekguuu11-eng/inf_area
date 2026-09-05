using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// V13 presentation layer only. It does not change when/why the Portal Room opens the shop.
/// It upgrades the existing opening into a quick ARK/data-terminal reveal.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopPresentationV13 : MonoBehaviour
{
    private RectTransform panelRect;
    private CanvasGroup group;
    private RectTransform scanLine;
    private CanvasGroup scanGroup;
    private Vector3 baseScale = Vector3.one;
    private Coroutine routine;

    public void Bind(GameObject panel, CanvasGroup canvasGroup)
    {
        if (panel == null) return;
        panelRect = panel.GetComponent<RectTransform>();
        group = canvasGroup != null ? canvasGroup : panel.GetComponent<CanvasGroup>();
        if (panelRect != null) baseScale = panelRect.localScale;
        EnsureScanLine(panel.transform);
        ApplyKoreanFont(panel.transform);
        ApplyButtonFeedback(panel.transform);
    }

    public void PlayOpen()
    {
        if (panelRect == null || group == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(OpenRoutine());
    }

    public void StopPresentation()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (panelRect != null) panelRect.localScale = baseScale;
        if (scanGroup != null) scanGroup.alpha = 0f;
    }

    private IEnumerator OpenRoutine()
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        panelRect.localScale = baseScale * 0.965f;
        if (scanLine != null)
        {
            scanLine.anchorMin = new Vector2(0f, 0.92f);
            scanLine.anchorMax = new Vector2(1f, 0.92f);
            scanLine.offsetMin = new Vector2(18f, -1.5f);
            scanLine.offsetMax = new Vector2(-18f, 1.5f);
            scanGroup.alpha = 0f;
        }

        const float fadeDuration = 0.16f;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            group.alpha = eased;
            panelRect.localScale = Vector3.Lerp(baseScale * 0.965f, baseScale, eased);
            yield return null;
        }
        group.alpha = 1f;
        panelRect.localScale = baseScale;

        if (scanLine != null)
        {
            scanGroup.alpha = 0.72f;
            const float scanDuration = 0.30f;
            t = 0f;
            while (t < scanDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / scanDuration);
                float y = Mathf.Lerp(0.90f, 0.10f, p);
                scanLine.anchorMin = new Vector2(0f, y);
                scanLine.anchorMax = new Vector2(1f, y);
                scanGroup.alpha = Mathf.Sin(p * Mathf.PI) * 0.72f;
                yield return null;
            }
            scanGroup.alpha = 0f;
        }

        group.interactable = true;
        group.blocksRaycasts = true;
        routine = null;
    }

    private void EnsureScanLine(Transform panel)
    {
        Transform existing = panel.Find("V13_ShopScanLine");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("V13_ShopScanLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(panel, false);
            go.transform.SetAsLastSibling();
        }
        scanLine = go.GetComponent<RectTransform>();
        scanGroup = go.GetComponent<CanvasGroup>();
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.30f, 0.96f, 1f, 0.9f);
        image.raycastTarget = false;
        scanGroup.alpha = 0f;
        scanGroup.blocksRaycasts = false;
    }

    private static void ApplyButtonFeedback(Transform root)
    {
        if (root == null) return;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].GetComponent<ShopButtonFeedbackV13>() == null)
                buttons[i].gameObject.AddComponent<ShopButtonFeedbackV13>();
        }
    }

    private static void ApplyKoreanFont(Transform root)
    {
        TMP_FontAsset galmuri = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        if (galmuri == null || root == null) return;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
            if (texts[i] != null) texts[i].font = galmuri;
    }
}
