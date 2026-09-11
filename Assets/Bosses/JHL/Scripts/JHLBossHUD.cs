using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class JHLBossHUD : MonoBehaviour
{
    public Canvas CanvasComponent => GetComponent<Canvas>();

    private CanvasGroup group;
    private RectTransform fill;
    private RectTransform delayedFill;
    private TMP_Text bossName;
    private TMP_Text introText;
    private TMP_Text cinematicTitle;
    private TMP_Text systemText;
    private TMP_Text skipPrompt;
    private RectTransform topCinemaBar;
    private RectTransform bottomCinemaBar;

    private float ratio = 1f;
    private float delayedRatio = 1f;
    private float delayedVelocity;
    private float lastDamageTime = -100f;
    private float skipPulseTime;
    private bool visible;

    public static JHLBossHUD Create()
    {
        GameObject root = new GameObject("JHLBossHUDCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 264;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 180f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup cg = root.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        JHLBossHUD hud = root.AddComponent<JHLBossHUD>();
        hud.group = cg;
        hud.Build();
        hud.HideAllImmediate();
        return hud;
    }

    public static void CleanupAll()
    {
        JHLBossHUD[] all = UnityEngine.Object.FindObjectsByType<JHLBossHUD>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            all[i].HideAllImmediate();
            UnityEngine.Object.Destroy(all[i].gameObject);
        }
    }

    private void Build()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");

        Image flash = CreateStretchImage("CutsceneDim", transform, new Color(0f, 0f, 0f, 0f));
        flash.raycastTarget = false;

        topCinemaBar = CreateCinemaBar("CinemaBarTop", transform, true);
        bottomCinemaBar = CreateCinemaBar("CinemaBarBottom", transform, false);

        cinematicTitle = CreateText("CinematicTitle", transform, font, string.Empty, 16f, FontStyles.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(210f, 28f));
        cinematicTitle.color = Color.white;
        cinematicTitle.outlineColor = Color.black;
        cinematicTitle.outlineWidth = 0.20f;

        bossName = CreateText("BossName", transform, font, "JHL", 7.2f, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0f, -2.5f), new Vector2(84f, 8f));
        bossName.color = new Color(1f, 0.72f, 0.95f, 1f);
        bossName.outlineColor = new Color(0.04f, 0.005f, 0.04f, 1f);
        bossName.outlineWidth = 0.16f;

        GameObject bar = CreateRect("BossHealthBar", transform, new Vector2(0.5f, 1f), new Vector2(0f, -11f), new Vector2(138f, 7f));
        CreateImage("Outer", bar.transform, new Color(0f, 0f, 0f, 0.82f), Vector2.zero, new Vector2(138f, 7f));
        CreateImage("Frame", bar.transform, new Color32(86, 32, 86, 230), Vector2.zero, new Vector2(134f, 4f));
        CreateImage("Inner", bar.transform, new Color(0.03f, 0.01f, 0.04f, 0.96f), Vector2.zero, new Vector2(132f, 3f));
        GameObject fillArea = CreateRect("FillArea", bar.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128f, 2f));
        delayedFill = CreateLeftFill("Delayed", fillArea.transform, new Color32(125, 224, 255, 255));
        fill = CreateLeftFill("Current", fillArea.transform, new Color32(245, 55, 181, 255));

        // No persistent PHASE 1/2/3 centre banner. Phase changes are communicated by the boss,
        // camera and a small temporary system line only.
        systemText = CreateText("SystemText", transform, font, string.Empty, 4.6f, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(180f, 8f));
        systemText.color = new Color(0.98f, 0.70f, 0.94f, 0.96f);
        systemText.outlineColor = Color.black;
        systemText.outlineWidth = 0.14f;

        introText = CreateText("IntroText", transform, font, string.Empty, 5.4f, FontStyles.Bold,
            new Vector2(0.5f, 0f), new Vector2(0f, 31f), new Vector2(230f, 12f));
        introText.color = new Color(0.94f, 0.96f, 1f, 1f);
        introText.outlineColor = Color.black;
        introText.outlineWidth = 0.16f;

        GameObject skipBackdrop = CreateImage("SkipBackdrop", transform, new Color(0.015f, 0.015f, 0.02f, 0.70f),
            Vector2.zero, new Vector2(112f, 12f));
        RectTransform skipRect = skipBackdrop.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-10f, 27f);
        skipPrompt = CreateText("SkipPrompt", skipBackdrop.transform, font, "아무 버튼이나 눌러 스킵", 5.0f, FontStyles.Normal,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(108f, 10f));
        skipPrompt.color = new Color(0.94f, 0.94f, 0.94f, 0.92f);
        skipPrompt.outlineColor = Color.black;
        skipPrompt.outlineWidth = 0.12f;
    }

    private void Update()
    {
        if (visible && delayedRatio > ratio && Time.unscaledTime - lastDamageTime >= 0.70f)
        {
            delayedRatio = Mathf.SmoothDamp(delayedRatio, ratio, ref delayedVelocity, 0.24f, 4f, Time.unscaledDeltaTime);
            if (Mathf.Abs(delayedRatio - ratio) < 0.001f) delayedRatio = ratio;
            ApplyFill(delayedFill, delayedRatio);
        }

        if (skipPrompt != null && skipPrompt.transform.parent.gameObject.activeSelf)
        {
            skipPulseTime += Time.unscaledDeltaTime;
            Color c = skipPrompt.color;
            c.a = Mathf.Lerp(0.48f, 0.96f, (Mathf.Sin(skipPulseTime * 4.2f) + 1f) * 0.5f);
            skipPrompt.color = c;
        }
    }

    public void ReportHealth(int current, int max, bool immediate)
    {
        float next = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
        if (next < ratio) lastDamageTime = Time.unscaledTime;
        ratio = next;
        if (immediate || next > delayedRatio)
        {
            delayedRatio = next;
            delayedVelocity = 0f;
        }
        ApplyFill(fill, ratio);
        ApplyFill(delayedFill, delayedRatio);
    }

    public void ShowBossBar(bool show)
    {
        visible = show;
        if (bossName != null) bossName.gameObject.SetActive(show);
        Transform bar = transform.Find("BossHealthBar");
        if (bar != null) bar.gameObject.SetActive(show);
    }

    // Kept for controller compatibility; now renders a small temporary system line,
    // never the huge centre-screen phase banner from V2.
    public void ShowPhase(string text)
    {
        ShowSystem(text);
    }

    public void ShowSystem(string text)
    {
        if (systemText == null) return;
        systemText.text = text ?? string.Empty;
        systemText.gameObject.SetActive(!string.IsNullOrEmpty(systemText.text));
    }

    public void ShowIntro(string text)
    {
        if (introText == null) return;
        introText.text = text ?? string.Empty;
        introText.gameObject.SetActive(!string.IsNullOrEmpty(introText.text));
    }

    public void SetSkipVisible(bool show)
    {
        if (skipPrompt == null) return;
        skipPulseTime = 0f;
        skipPrompt.transform.parent.gameObject.SetActive(show);
    }

    public void SetCinematicBarsImmediate(bool show)
    {
        if (topCinemaBar != null) topCinemaBar.anchoredPosition = show ? Vector2.zero : new Vector2(0f, 14f);
        if (bottomCinemaBar != null) bottomCinemaBar.anchoredPosition = show ? Vector2.zero : new Vector2(0f, -14f);
    }

    public IEnumerator AnimateCinematicBars(bool show, float duration)
    {
        if (topCinemaBar == null || bottomCinemaBar == null) yield break;
        Vector2 topFrom = topCinemaBar.anchoredPosition;
        Vector2 bottomFrom = bottomCinemaBar.anchoredPosition;
        Vector2 topTo = show ? Vector2.zero : new Vector2(0f, 14f);
        Vector2 bottomTo = show ? Vector2.zero : new Vector2(0f, -14f);
        float elapsed = 0f;
        float safe = Mathf.Max(0.01f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            float e = t * t * (3f - 2f * t);
            topCinemaBar.anchoredPosition = Vector2.Lerp(topFrom, topTo, e);
            bottomCinemaBar.anchoredPosition = Vector2.Lerp(bottomFrom, bottomTo, e);
            yield return null;
        }
        topCinemaBar.anchoredPosition = topTo;
        bottomCinemaBar.anchoredPosition = bottomTo;
    }

    public IEnumerator TypeCinematicTitle(string text, float characterDelay, float holdDuration, Func<bool> skipCheck)
    {
        if (cinematicTitle == null) yield break;
        cinematicTitle.gameObject.SetActive(true);
        cinematicTitle.text = string.Empty;
        cinematicTitle.alpha = 1f;
        string safe = text ?? string.Empty;
        for (int i = 0; i < safe.Length; i++)
        {
            if (skipCheck != null && skipCheck()) yield break;
            cinematicTitle.text += safe[i];
            float elapsed = 0f;
            float delay = Mathf.Max(0.02f, characterDelay);
            while (elapsed < delay)
            {
                if (skipCheck != null && skipCheck()) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float hold = 0f;
        while (hold < Mathf.Max(0f, holdDuration))
        {
            if (skipCheck != null && skipCheck()) yield break;
            hold += Time.unscaledDeltaTime;
            yield return null;
        }
        cinematicTitle.gameObject.SetActive(false);
    }

    public void HideCinematicTitle()
    {
        if (cinematicTitle == null) return;
        cinematicTitle.text = string.Empty;
        cinematicTitle.gameObject.SetActive(false);
    }

    public void HideAllImmediate()
    {
        if (group != null) group.alpha = 1f;
        ShowBossBar(false);
        ShowSystem(string.Empty);
        ShowIntro(string.Empty);
        HideCinematicTitle();
        SetSkipVisible(false);
        SetCinematicBarsImmediate(false);
    }

    private static RectTransform CreateCinemaBar(string name, Transform parent, bool top)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(0f, 13f);
        rect.anchoredPosition = top ? new Vector2(0f, 14f) : new Vector2(0f, -14f);
        Image image = go.GetComponent<Image>();
        image.sprite = JHLRuntimeSprites.WhitePixel;
        image.color = new Color(0f, 0f, 0f, 0.96f);
        image.raycastTarget = false;
        return rect;
    }

    private static Image CreateStretchImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = JHLRuntimeSprites.WhitePixel;
        image.color = color;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return go;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), pos, size);
        Image image = go.AddComponent<Image>();
        image.sprite = JHLRuntimeSprites.WhitePixel;
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static RectTransform CreateLeftFill(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = JHLRuntimeSprites.WhitePixel;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size,
        FontStyles style, Vector2 anchor, Vector2 pos, Vector2 dimensions)
    {
        GameObject go = CreateRect(name, parent, anchor, pos, dimensions);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void ApplyFill(RectTransform target, float value)
    {
        if (target == null) return;
        target.anchorMin = Vector2.zero;
        target.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}
