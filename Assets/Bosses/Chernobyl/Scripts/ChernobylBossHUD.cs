using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChernobylBossHUD : MonoBehaviour
{
    private Canvas canvas;
    private CanvasGroup rootGroup;
    private RectTransform currentFill;
    private RectTransform delayedFill;
    private TMP_Text bossName;
    private TMP_Text phaseText;
    private TMP_Text introText;
    private TMP_Text skipText;
    private Image flash;
    private float currentRatio = 1f;
    private float delayedRatio = 1f;
    private float delayedVelocity;
    private float lastDamageTime = -100f;
    private bool visible;
    private const float DelayBeforeDrain = 0.85f;

    public Canvas Canvas => canvas;

    public static ChernobylBossHUD Create()
    {
        GameObject root = new GameObject("ChernobylBossHUDCanvas");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = 262;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 180f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        ChernobylBossHUD hud = root.AddComponent<ChernobylBossHUD>();
        hud.canvas = c;
        hud.rootGroup = group;
        hud.Build();
        hud.HideAllImmediate();
        return hud;
    }

    private void Build()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        flash = CreateStretchImage("RadiationFlash", transform, new Color(0.45f, 1f, 0.20f, 0f));

        bossName = CreateText("BossName", transform, font, "체르노빌", 8.7f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(150f, 10f));
        bossName.color = new Color(0.78f, 1f, 0.72f, 1f);
        bossName.outlineColor = new Color32(3, 18, 7, 255);
        bossName.outlineWidth = 0.16f;

        GameObject bar = CreateRect("BossHealthBar", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(154f, 11f));
        CreateImage("OuterFrame", bar.transform, new Color32(2, 12, 5, 255), Vector2.zero, new Vector2(152f, 9f));
        CreateImage("GreenFrame", bar.transform, new Color32(24, 87, 40, 255), Vector2.zero, new Vector2(148f, 7f));
        CreateImage("InnerBlack", bar.transform, new Color32(4, 20, 9, 255), Vector2.zero, new Vector2(144f, 5f));

        GameObject fillArea = CreateRect("FillArea", bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 4f));
        delayedFill = CreateLeftFill("Delayed", fillArea.transform, new Color32(142, 255, 124, 255));
        currentFill = CreateLeftFill("Current", fillArea.transform, new Color32(52, 210, 83, 255));

        CreateImage("LeftNode", bar.transform, new Color32(108, 244, 104, 255), new Vector2(-72f, 0f), new Vector2(2f, 2f));
        CreateImage("RightNode", bar.transform, new Color32(108, 244, 104, 255), new Vector2(72f, 0f), new Vector2(2f, 2f));

        phaseText = CreateText("PhaseText", transform, font, string.Empty, 11f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(200f, 24f));
        phaseText.color = new Color(0.64f, 1f, 0.32f, 1f);
        phaseText.outlineColor = new Color(0.02f, 0.08f, 0.02f, 1f);
        phaseText.outlineWidth = 0.20f;

        introText = CreateText("IntroText", transform, font, string.Empty, 8.5f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(250f, 22f));
        introText.color = new Color(0.86f, 1f, 0.80f, 1f);
        introText.outlineColor = new Color(0.01f, 0.05f, 0.01f, 1f);
        introText.outlineWidth = 0.17f;

        GameObject skipBack = CreateImage("SkipBack", transform, new Color(0.01f, 0.03f, 0.015f, 0.68f),
            Vector2.zero, new Vector2(116f, 12f));
        RectTransform skipRect = skipBack.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-10f, 27f);
        skipText = CreateText("SkipText", skipBack.transform, font, "아무 버튼이나 눌러 스킵", 5.2f, FontStyles.Normal,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        skipText.color = new Color(0.91f, 1f, 0.90f, 0.95f);
        skipText.outlineColor = Color.black;
        skipText.outlineWidth = 0.14f;
    }

    private void Update()
    {
        if (visible && delayedRatio > currentRatio && Time.unscaledTime - lastDamageTime >= DelayBeforeDrain)
        {
            delayedRatio = Mathf.SmoothDamp(delayedRatio, currentRatio, ref delayedVelocity, 0.28f, 4f, Time.unscaledDeltaTime);
            if (Mathf.Abs(delayedRatio - currentRatio) < 0.001f) delayedRatio = currentRatio;
            ApplyFill(delayedFill, delayedRatio);
        }
    }

    public void ReportHealth(int current, int max, bool immediate = false)
    {
        float ratio = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
        if (ratio < currentRatio) lastDamageTime = Time.unscaledTime;
        currentRatio = ratio;
        if (immediate || ratio > delayedRatio)
        {
            delayedRatio = ratio;
            delayedVelocity = 0f;
        }
        ApplyFill(currentFill, currentRatio);
        ApplyFill(delayedFill, delayedRatio);
    }

    public void ShowBossBar(bool show)
    {
        visible = show;
        if (bossName != null) bossName.gameObject.SetActive(show);
        Transform bar = transform.Find("BossHealthBar");
        if (bar != null) bar.gameObject.SetActive(show);
    }

    public void ShowIntro(string text)
    {
        if (introText == null) return;
        introText.text = text ?? string.Empty;
        introText.gameObject.SetActive(!string.IsNullOrEmpty(introText.text));
    }

    public void ShowPhase(string text)
    {
        if (phaseText == null) return;
        phaseText.text = text ?? string.Empty;
        phaseText.gameObject.SetActive(!string.IsNullOrEmpty(phaseText.text));
    }

    public void SetSkipVisible(bool show)
    {
        if (skipText != null && skipText.transform.parent != null)
            skipText.transform.parent.gameObject.SetActive(show);
    }

    public IEnumerator Flash(Color color, float peakAlpha, float duration)
    {
        if (flash == null) yield break;
        float elapsed = 0f;
        float safe = Mathf.Max(0.05f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            Color c = color;
            c.a = Mathf.Sin(t * Mathf.PI) * Mathf.Clamp01(peakAlpha);
            flash.color = c;
            yield return null;
        }
        Color clear = color;
        clear.a = 0f;
        flash.color = clear;
    }

    public IEnumerator FadeOut(float duration)
    {
        if (rootGroup == null) yield break;
        float start = rootGroup.alpha;
        float elapsed = 0f;
        float safe = Mathf.Max(0.05f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            rootGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / safe));
            yield return null;
        }
        rootGroup.alpha = 0f;
    }

    public void HideAllImmediate()
    {
        if (rootGroup != null) rootGroup.alpha = 1f;
        ShowBossBar(false);
        ShowIntro(string.Empty);
        ShowPhase(string.Empty);
        SetSkipVisible(false);
    }

    private static void ApplyFill(RectTransform target, float ratio)
    {
        if (target == null) return;
        target.anchorMin = new Vector2(0f, 0f);
        target.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        return go;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), pos, size);
        Image img = go.AddComponent<Image>();
        img.sprite = ChernobylRuntimeSprites.WhitePixel;
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private static Image CreateStretchImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image img = go.AddComponent<Image>();
        img.sprite = ChernobylRuntimeSprites.WhitePixel;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform CreateLeftFill(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = ChernobylRuntimeSprites.WhitePixel;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string value, float size,
        FontStyles style, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 boxSize)
    {
        GameObject go = CreateRect(name, parent, anchorMin, anchorMax, pivot, pos, boxSize);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }
}
