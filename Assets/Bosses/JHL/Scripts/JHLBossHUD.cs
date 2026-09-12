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
    private RectTransform barContent;
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
    private const float DelayBeforeDrain = 1f;

    public static JHLBossHUD Create()
    {
        BossHUDGlobalCleanup.CleanupAll();

        GameObject root = new GameObject("JHLBossHUDCanvas");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = 260;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 180f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
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

        cinematicTitle = CreateText("CinematicTitle", transform, font, string.Empty, 15f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(180f, 24f));
        cinematicTitle.color = new Color(1f, 0.78f, 0.96f, 1f);
        cinematicTitle.outlineColor = new Color32(10, 0, 10, 255);
        cinematicTitle.outlineWidth = 0.20f;

        // V14: exact Executor boss-bar geometry/hierarchy, with JHL's accent colors only.
        bossName = CreateText("BossName", transform, font, "JHL", 8.5f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(142f, 10f));
        bossName.color = new Color(1f, 0.72f, 0.95f, 1f);
        bossName.outlineColor = new Color32(10, 0, 10, 255);
        bossName.outlineWidth = 0.15f;

        GameObject bar = CreateRect("BossHealthBar", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(154f, 11f));
        barContent = bar.GetComponent<RectTransform>();

        CreateImage("OuterFrame", bar.transform, new Color32(7, 2, 3, 255), Vector2.zero, new Vector2(152f, 9f), 0);
        CreateImage("AccentOutline", bar.transform, new Color32(104, 24, 91, 255), Vector2.zero, new Vector2(148f, 7f), 1);
        CreateImage("InnerBlack", bar.transform, new Color32(22, 5, 20, 255), Vector2.zero, new Vector2(144f, 5f), 2);

        GameObject fillArea = CreateRect("FillArea", bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 4f));
        delayedFill = CreateLeftFill("DelayedDamageFill", fillArea.transform, new Color32(255, 144, 230, 255), 3);
        fill = CreateLeftFill("CurrentHealthFill", fillArea.transform, new Color32(220, 52, 167, 255), 4);

        GameObject highlight = CreateRect("TopHighlight", fill, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -0.25f), new Vector2(0f, 0.5f));
        Image highlightImage = highlight.AddComponent<Image>();
        highlightImage.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        highlightImage.color = new Color(1f, 0.68f, 0.94f, 0.26f);
        highlightImage.raycastTarget = false;

        CreateBracket(bar.transform, -1f);
        CreateBracket(bar.transform, 1f);
        CreateImage("LeftBolt", bar.transform, new Color32(142, 31, 119, 255), new Vector2(-72f, 0f), new Vector2(2f, 2f), 8);
        CreateImage("RightBolt", bar.transform, new Color32(142, 31, 119, 255), new Vector2(72f, 0f), new Vector2(2f, 2f), 8);
        // Phase 2 at 70%, Phase 3 at 40%.
        CreateImage("Phase70Marker", bar.transform, new Color32(238, 131, 222, 230), new Vector2(28f, 0f), new Vector2(1f, 7f), 9);
        CreateImage("Phase40Marker", bar.transform, new Color32(255, 76, 203, 235), new Vector2(-14f, 0f), new Vector2(1f, 8f), 9);

        // Keep JHL's small system line instead of a giant persistent phase banner.
        systemText = CreateText("SystemText", transform, font, string.Empty, 4.8f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(180f, 8f));
        systemText.color = new Color(0.98f, 0.70f, 0.94f, 0.96f);
        systemText.outlineColor = Color.black;
        systemText.outlineWidth = 0.14f;

        introText = CreateText("IntroText", transform, font, string.Empty, 6.2f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(215f, 14f));
        introText.color = new Color(0.94f, 0.96f, 1f, 1f);
        introText.outlineColor = Color.black;
        introText.outlineWidth = 0.16f;

        GameObject skipBackdrop = CreateImage("SkipBackdrop", transform, new Color(0.02f, 0.02f, 0.025f, 0.62f),
            Vector2.zero, new Vector2(116f, 12f), 0);
        RectTransform skipRect = skipBackdrop.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-10f, 27f);
        skipPrompt = CreateText("SkipPrompt", skipBackdrop.transform, font, "아무 버튼이나 눌러 스킵", 5.2f, FontStyles.Normal,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        skipPrompt.color = new Color(0.94f, 0.94f, 0.94f, 0.92f);
        skipPrompt.outlineColor = Color.black;
        skipPrompt.outlineWidth = 0.14f;
    }

    private void Update()
    {
        if (visible && delayedRatio > ratio && Time.unscaledTime - lastDamageTime >= DelayBeforeDrain)
        {
            delayedRatio = Mathf.SmoothDamp(delayedRatio, ratio, ref delayedVelocity, 0.28f, 4f, Time.unscaledDeltaTime);
            if (Mathf.Abs(delayedRatio - ratio) < 0.001f) delayedRatio = ratio;
            ApplyFill(delayedFill, delayedRatio);
        }

        if (skipPrompt != null && skipPrompt.transform.parent.gameObject.activeSelf)
        {
            skipPulseTime += Time.unscaledDeltaTime;
            Color c = skipPrompt.color;
            c.a = Mathf.Lerp(0.50f, 0.98f, (Mathf.Sin(skipPulseTime * 4.4f) + 1f) * 0.5f);
            skipPrompt.color = c;
        }
    }

    public void ReportHealth(int current, int max, bool immediate)
    {
        float next = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
        if (next < ratio) lastDamageTime = Time.unscaledTime;
        ratio = next;
        ApplyFill(fill, ratio);
        if (immediate || next > delayedRatio)
        {
            delayedRatio = next;
            delayedVelocity = 0f;
            ApplyFill(delayedFill, delayedRatio);
        }
    }

    public void ShowBossBar(bool show)
    {
        visible = show;
        if (bossName != null) bossName.gameObject.SetActive(show);
        if (barContent != null) barContent.gameObject.SetActive(show);
    }

    public IEnumerator RevealBossBar(int current, int max, float duration)
    {
        ReportHealth(current, max, true);
        ShowBossBar(true);
        Vector3 original = barContent != null ? barContent.localScale : Vector3.one;
        if (barContent != null) barContent.localScale = new Vector3(0.05f, 1f, 1f);
        if (bossName != null) bossName.alpha = 0f;
        float elapsed = 0f;
        float safe = Mathf.Max(0.05f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (barContent != null) barContent.localScale = new Vector3(Mathf.Lerp(0.05f, original.x, eased), original.y, original.z);
            if (bossName != null) bossName.alpha = eased;
            yield return null;
        }
        if (barContent != null) barContent.localScale = original;
        if (bossName != null) bossName.alpha = 1f;
    }

    public void HideBossBarImmediate() => ShowBossBar(false);

    public void ShowPhase(string text) => ShowSystem(text);

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

    private static void ApplyFill(RectTransform target, float value)
    {
        if (target == null) return;
        target.anchorMin = Vector2.zero;
        target.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateLeftFill(string name, Transform parent, Color color, int siblingIndex)
    {
        GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = go.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        go.transform.SetSiblingIndex(siblingIndex);
        return go.GetComponent<RectTransform>();
    }

    private static void CreateBracket(Transform parent, float side)
    {
        float x = side * 78f;
        CreateImage("BracketVertical", parent, new Color32(15, 3, 4, 255), new Vector2(x, 0f), new Vector2(3f, 12f), 6);
        CreateImage("BracketTop", parent, new Color32(15, 3, 4, 255), new Vector2(x - side * 2f, 5f), new Vector2(5f, 2f), 7);
        CreateImage("BracketBottom", parent, new Color32(15, 3, 4, 255), new Vector2(x - side * 2f, -5f), new Vector2(5f, 2f), 7);
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
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = new Color(0f, 0f, 0f, 0.96f);
        image.raycastTarget = false;
        return rect;
    }

    private static Image CreateStretchImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = go.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size, int siblingIndex)
    {
        GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), pos, size);
        Image image = go.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        go.transform.SetSiblingIndex(siblingIndex);
        return go;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size,
        FontStyles style, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 dimensions)
    {
        GameObject go = CreateRect(name, parent, anchorMin, anchorMax, pivot, pos, dimensions);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        return tmp;
    }
}
