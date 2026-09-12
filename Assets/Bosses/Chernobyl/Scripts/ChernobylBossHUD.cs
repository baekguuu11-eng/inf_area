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
    private RectTransform barContent;
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
    private const float DelayBeforeDrain = 1f;

    public Canvas Canvas => canvas;

    public static void CleanupAll()
    {
        ChernobylBossHUD[] active = UnityEngine.Object.FindObjectsByType<ChernobylBossHUD>(FindObjectsInactive.Include);
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i] == null) continue;
            active[i].HideAllImmediate();
            UnityEngine.Object.Destroy(active[i].gameObject);
        }
    }

    public static ChernobylBossHUD Create()
    {
        BossHUDGlobalCleanup.CleanupAll();

        GameObject root = new GameObject("ChernobylBossHUDCanvas");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = 260;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 180f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 1f;
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

        // V14: all boss health bars use Executor geometry and hierarchy.
        bossName = CreateText("BossName", transform, font, "체르노빌", 8.5f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(142f, 10f));
        bossName.color = new Color(0.78f, 1f, 0.72f, 1f);
        bossName.outlineColor = new Color32(3, 18, 7, 255);
        bossName.outlineWidth = 0.15f;

        GameObject bar = CreateRect("BossHealthBar", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(154f, 11f));
        barContent = bar.GetComponent<RectTransform>();

        CreateImage("OuterFrame", bar.transform, new Color32(7, 2, 3, 255), Vector2.zero, new Vector2(152f, 9f), 0);
        CreateImage("AccentOutline", bar.transform, new Color32(29, 104, 47, 255), Vector2.zero, new Vector2(148f, 7f), 1);
        CreateImage("InnerBlack", bar.transform, new Color32(4, 20, 9, 255), Vector2.zero, new Vector2(144f, 5f), 2);

        GameObject fillArea = CreateRect("FillArea", bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 4f));
        delayedFill = CreateLeftFill("DelayedDamageFill", fillArea.transform, new Color32(142, 255, 124, 255), 3);
        currentFill = CreateLeftFill("CurrentHealthFill", fillArea.transform, new Color32(52, 210, 83, 255), 4);

        GameObject highlight = CreateRect("TopHighlight", currentFill, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -0.25f), new Vector2(0f, 0.5f));
        Image highlightImage = highlight.AddComponent<Image>();
        highlightImage.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        highlightImage.color = new Color(0.72f, 1f, 0.68f, 0.25f);
        highlightImage.raycastTarget = false;

        CreateBracket(bar.transform, -1f);
        CreateBracket(bar.transform, 1f);
        CreateImage("LeftBolt", bar.transform, new Color32(45, 126, 59, 255), new Vector2(-72f, 0f), new Vector2(2f, 2f), 8);
        CreateImage("RightBolt", bar.transform, new Color32(45, 126, 59, 255), new Vector2(72f, 0f), new Vector2(2f, 2f), 8);
        // Phase 2 at 65%, Phase 3 at 30% on the same 140 px Executor fill rail.
        CreateImage("Phase65Marker", bar.transform, new Color32(135, 238, 111, 230), new Vector2(21f, 0f), new Vector2(1f, 7f), 9);
        CreateImage("Phase30Marker", bar.transform, new Color32(91, 255, 75, 235), new Vector2(-28f, 0f), new Vector2(1f, 8f), 9);

        phaseText = CreateText("PhaseText", transform, font, string.Empty, 12f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(200f, 24f));
        phaseText.color = new Color(0.64f, 1f, 0.32f, 1f);
        phaseText.outlineColor = new Color(0.02f, 0.08f, 0.02f, 1f);
        phaseText.outlineWidth = 0.20f;

        introText = CreateText("IntroText", transform, font, string.Empty, 6.2f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(215f, 14f));
        introText.color = new Color(0.86f, 1f, 0.80f, 1f);
        introText.outlineColor = new Color(0.01f, 0.05f, 0.01f, 1f);
        introText.outlineWidth = 0.16f;

        GameObject skipBack = CreateImage("SkipBack", transform, new Color(0.02f, 0.02f, 0.025f, 0.62f),
            Vector2.zero, new Vector2(116f, 12f), 0);
        RectTransform skipRect = skipBack.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-10f, 27f);
        skipText = CreateText("SkipText", skipBack.transform, font, "아무 버튼이나 눌러 스킵", 5.2f, FontStyles.Normal,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        skipText.color = new Color(0.94f, 0.94f, 0.94f, 0.92f);
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
        float next = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
        if (next < currentRatio) lastDamageTime = Time.unscaledTime;
        currentRatio = next;
        ApplyFill(currentFill, currentRatio);
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
        if (flash != null)
        {
            Color c = flash.color;
            c.a = 0f;
            flash.color = c;
        }
    }

    private static void ApplyFill(RectTransform target, float ratio)
    {
        if (target == null) return;
        target.anchorMin = Vector2.zero;
        target.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
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

    private static Image CreateStretchImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = go.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string value, float size,
        FontStyles style, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 boxSize)
    {
        GameObject go = CreateRect(name, parent, anchorMin, anchorMax, pivot, pos, boxSize);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }
}
