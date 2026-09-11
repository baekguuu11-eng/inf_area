using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ExecutorBossHUD : MonoBehaviour
{
    public Canvas CanvasComponent => GetComponent<Canvas>();
    private CanvasGroup rootGroup;
    private TMP_Text bossName;
    private TMP_Text introMessage;
    private TMP_Text skipPrompt;
    private TMP_Text cinematicTitle;
    private TMP_Text phaseMessage;
    private RectTransform currentFill;
    private RectTransform delayedFill;
    private RectTransform barContent;
    private RectTransform topCinemaBar;
    private RectTransform bottomCinemaBar;
    private Image currentFillImage;
    private Image delayedFillImage;
    private Image outerFrameImage;
    private Image redOutlineImage;
    private Image phaseFlashImage;
    private float currentRatio = 1f;
    private float delayedRatio = 1f;
    private float delayedVelocity;
    private float lastDamageTime = -100f;
    private const float DelayBeforeDrain = 1f;
    private bool bossBarVisible;
    private float skipPulseTime;

    public static ExecutorBossHUD Create()
    {
        GameObject root = new GameObject("BossHUDCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 260;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 180f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        ExecutorBossHUD hud = root.AddComponent<ExecutorBossHUD>();
        hud.rootGroup = group;
        hud.BuildUI();
        hud.HideAllImmediate();
        return hud;
    }

    private void BuildUI()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");

        phaseFlashImage = CreateStretchImage("PhaseFlash", transform, new Color(0.72f, 0.02f, 0.01f, 0f), 0);

        topCinemaBar = CreateCinemaBar("CinemaBarTop", transform, true);
        bottomCinemaBar = CreateCinemaBar("CinemaBarBottom", transform, false);

        cinematicTitle = CreateText("CinematicBossTitle", transform, font, string.Empty, 15f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(180f, 24f));
        cinematicTitle.color = new Color(1f, 0.78f, 0.73f, 1f);
        cinematicTitle.outlineColor = new Color32(10, 0, 0, 255);
        cinematicTitle.outlineWidth = 0.20f;

        bossName = CreateText("BossName", transform, font, "집행자", 8.5f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(142f, 10f));
        bossName.color = new Color(1f, 0.83f, 0.79f, 1f);
        bossName.outlineColor = new Color32(10, 0, 0, 255);
        bossName.outlineWidth = 0.15f;

        GameObject barRootObject = CreateRect("BossHealthBar", transform, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(154f, 11f));
        barContent = barRootObject.GetComponent<RectTransform>();

        GameObject outerFrame = CreateImage("OuterFrame", barRootObject.transform, new Color32(7, 2, 3, 255),
            Vector2.zero, new Vector2(152f, 9f), 0);
        outerFrameImage = outerFrame.GetComponent<Image>();

        GameObject redOutline = CreateImage("RedOutline", barRootObject.transform, new Color32(104, 13, 18, 255),
            Vector2.zero, new Vector2(148f, 7f), 1);
        redOutlineImage = redOutline.GetComponent<Image>();

        CreateImage("InnerBlack", barRootObject.transform, new Color32(22, 5, 7, 255),
            Vector2.zero, new Vector2(144f, 5f), 2);

        GameObject fillArea = CreateRect("FillArea", barRootObject.transform, new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 4f));

        delayedFill = CreateLeftFill("DelayedDamageFill", fillArea.transform,
            new Color32(255, 116, 112, 255), 3, out delayedFillImage);
        currentFill = CreateLeftFill("CurrentHealthFill", fillArea.transform,
            new Color32(205, 43, 49, 255), 4, out currentFillImage);

        GameObject highlight = CreateRect("TopHighlight", currentFill, new Vector2(0f, 1f),
            new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -0.25f), new Vector2(0f, 0.5f));
        Image highlightImage = highlight.AddComponent<Image>();
        highlightImage.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        highlightImage.color = new Color(1f, 0.62f, 0.58f, 0.27f);
        highlightImage.raycastTarget = false;

        CreateBracket(barRootObject.transform, -1f);
        CreateBracket(barRootObject.transform, 1f);
        CreateImage("LeftBolt", barRootObject.transform, new Color32(142, 27, 30, 255),
            new Vector2(-72f, 0f), new Vector2(2f, 2f), 8);
        CreateImage("RightBolt", barRootObject.transform, new Color32(142, 27, 30, 255),
            new Vector2(72f, 0f), new Vector2(2f, 2f), 8);

        // V11: 외형/패턴 전환 지점을 숫자 대신 작은 눈금으로만 표시한다.
        CreateImage("Phase55Marker", barRootObject.transform, new Color32(232, 120, 104, 225),
            new Vector2(7f, 0f), new Vector2(1f, 7f), 9);
        CreateImage("Final15Marker", barRootObject.transform, new Color32(255, 78, 58, 235),
            new Vector2(-49f, 0f), new Vector2(1f, 8f), 9);

        introMessage = CreateText("IntroMessage", transform, font, string.Empty, 6.2f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(215f, 14f));
        introMessage.color = new Color(1f, 0.43f, 0.38f, 1f);
        introMessage.outlineColor = new Color(0.05f, 0f, 0f, 1f);
        introMessage.outlineWidth = 0.16f;

        phaseMessage = CreateText("PhaseMessage", transform, font, "과부하", 12f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(180f, 22f));
        phaseMessage.color = new Color(1f, 0.40f, 0.22f, 1f);
        phaseMessage.outlineColor = new Color(0.08f, 0f, 0f, 1f);
        phaseMessage.outlineWidth = 0.20f;

        GameObject skipBackdrop = CreateImage("SkipBackdrop", transform, new Color(0.02f, 0.02f, 0.025f, 0.62f),
            Vector2.zero, new Vector2(116f, 12f), 0);
        RectTransform backdropRect = skipBackdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = new Vector2(1f, 0f);
        backdropRect.anchorMax = new Vector2(1f, 0f);
        backdropRect.pivot = new Vector2(1f, 0f);
        backdropRect.anchoredPosition = new Vector2(-10f, 27f);

        skipPrompt = CreateText("SkipPrompt", skipBackdrop.transform, font, "아무 버튼이나 눌러 스킵", 5.2f,
            FontStyles.Normal, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        skipPrompt.color = new Color(0.94f, 0.94f, 0.94f, 0.92f);
        skipPrompt.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        skipPrompt.outlineWidth = 0.14f;
    }

    private void Update()
    {
        if (bossBarVisible && delayedRatio > currentRatio && Time.unscaledTime - lastDamageTime >= DelayBeforeDrain)
        {
            delayedRatio = Mathf.SmoothDamp(delayedRatio, currentRatio, ref delayedVelocity,
                0.28f, 4f, Time.unscaledDeltaTime);
            if (Mathf.Abs(delayedRatio - currentRatio) < 0.001f)
                delayedRatio = currentRatio;
            ApplyFill(delayedFill, delayedRatio);
        }

        if (skipPrompt != null && skipPrompt.transform.parent.gameObject.activeSelf)
        {
            skipPulseTime += Time.unscaledDeltaTime;
            Color color = skipPrompt.color;
            color.a = Mathf.Lerp(0.50f, 0.98f, (Mathf.Sin(skipPulseTime * 4.4f) + 1f) * 0.5f);
            skipPrompt.color = color;
        }
    }

    public void ShowIntroMessage(string text)
    {
        if (introMessage == null) return;
        introMessage.text = text ?? string.Empty;
        introMessage.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    public IEnumerator TypeIntroMessage(string text, float characterDelay, Action onCharacter, Func<bool> skipCheck)
    {
        if (introMessage == null) yield break;
        introMessage.gameObject.SetActive(true);
        introMessage.text = string.Empty;
        string safeText = text ?? string.Empty;
        float delay = Mathf.Max(0.01f, characterDelay);
        for (int i = 0; i < safeText.Length; i++)
        {
            if (skipCheck != null && skipCheck()) yield break;
            introMessage.text += safeText[i];
            if (!char.IsWhiteSpace(safeText[i])) onCharacter?.Invoke();
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (skipCheck != null && skipCheck()) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    public IEnumerator TypeCinematicTitle(string text, float characterDelay, float holdDuration,
        Action<int, char> onCharacter, Func<bool> skipCheck)
    {
        if (cinematicTitle == null) yield break;
        cinematicTitle.gameObject.SetActive(true);
        cinematicTitle.text = string.Empty;
        cinematicTitle.alpha = 1f;
        cinematicTitle.transform.localScale = Vector3.one;
        string safeText = text ?? string.Empty;
        float delay = Mathf.Max(0.02f, characterDelay);
        for (int i = 0; i < safeText.Length; i++)
        {
            if (skipCheck != null && skipCheck()) yield break;
            cinematicTitle.text += safeText[i];
            cinematicTitle.transform.localScale = Vector3.one * 1.10f;
            onCharacter?.Invoke(i, safeText[i]);
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (skipCheck != null && skipCheck()) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / delay);
                cinematicTitle.transform.localScale = Vector3.Lerp(Vector3.one * 1.10f, Vector3.one, t);
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

    public void SetSkipVisible(bool visible)
    {
        if (skipPrompt == null) return;
        skipPulseTime = 0f;
        skipPrompt.transform.parent.gameObject.SetActive(visible);
    }

    public void SetCinematicBarsImmediate(bool visible)
    {
        if (topCinemaBar != null)
            topCinemaBar.anchoredPosition = visible ? Vector2.zero : new Vector2(0f, 13f);
        if (bottomCinemaBar != null)
            bottomCinemaBar.anchoredPosition = visible ? Vector2.zero : new Vector2(0f, -13f);
    }

    public IEnumerator AnimateCinematicBars(bool visible, float duration)
    {
        Vector2 topStart = topCinemaBar != null ? topCinemaBar.anchoredPosition : Vector2.zero;
        Vector2 bottomStart = bottomCinemaBar != null ? bottomCinemaBar.anchoredPosition : Vector2.zero;
        Vector2 topTarget = visible ? Vector2.zero : new Vector2(0f, 13f);
        Vector2 bottomTarget = visible ? Vector2.zero : new Vector2(0f, -13f);
        float elapsed = 0f;
        float safe = Mathf.Max(0.05f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (topCinemaBar != null) topCinemaBar.anchoredPosition = Vector2.Lerp(topStart, topTarget, eased);
            if (bottomCinemaBar != null) bottomCinemaBar.anchoredPosition = Vector2.Lerp(bottomStart, bottomTarget, eased);
            yield return null;
        }
        if (topCinemaBar != null) topCinemaBar.anchoredPosition = topTarget;
        if (bottomCinemaBar != null) bottomCinemaBar.anchoredPosition = bottomTarget;
    }

    public IEnumerator ShowCinematicTitle(float duration)
    {
        if (cinematicTitle == null) yield break;
        cinematicTitle.gameObject.SetActive(true);
        cinematicTitle.alpha = 0f;
        cinematicTitle.transform.localScale = Vector3.one * 1.18f;
        float elapsed = 0f;
        float safe = Mathf.Max(0.2f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            float alpha = t < 0.58f ? Mathf.InverseLerp(0f, 0.35f, t) : 1f - Mathf.InverseLerp(0.58f, 1f, t);
            cinematicTitle.alpha = Mathf.Clamp01(alpha);
            cinematicTitle.transform.localScale = Vector3.Lerp(Vector3.one * 1.18f, Vector3.one, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }
        cinematicTitle.gameObject.SetActive(false);
    }

    public IEnumerator ShowPhaseTransition(float duration)
    {
        if (phaseMessage == null || phaseFlashImage == null) yield break;
        phaseMessage.gameObject.SetActive(true);
        phaseMessage.alpha = 0f;
        phaseMessage.transform.localScale = Vector3.one * 1.25f;
        float elapsed = 0f;
        float safe = Mathf.Max(0.4f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            float flash = Mathf.Sin(t * Mathf.PI) * 0.18f;
            Color flashColor = phaseFlashImage.color;
            flashColor.a = flash;
            phaseFlashImage.color = flashColor;
            float alpha = t < 0.35f ? Mathf.InverseLerp(0f, 0.35f, t) : 1f - Mathf.InverseLerp(0.70f, 1f, t);
            phaseMessage.alpha = Mathf.Clamp01(alpha);
            phaseMessage.transform.localScale = Vector3.Lerp(Vector3.one * 1.25f, Vector3.one, Mathf.Clamp01(t * 2f));
            yield return null;
        }
        Color end = phaseFlashImage.color;
        end.a = 0f;
        phaseFlashImage.color = end;
        phaseMessage.gameObject.SetActive(false);
    }

    public void SetPhaseTwoStyle()
    {
        if (currentFillImage != null) currentFillImage.color = new Color32(225, 55, 34, 255);
        if (delayedFillImage != null) delayedFillImage.color = new Color32(255, 144, 86, 255);
        if (redOutlineImage != null) redOutlineImage.color = new Color32(158, 33, 19, 255);
        if (outerFrameImage != null) outerFrameImage.color = new Color32(12, 3, 2, 255);
    }

    public void SetFinalStandStyle()
    {
        if (currentFillImage != null) currentFillImage.color = new Color32(238, 42, 28, 255);
        if (delayedFillImage != null) delayedFillImage.color = new Color32(255, 112, 72, 255);
        if (redOutlineImage != null) redOutlineImage.color = new Color32(202, 38, 20, 255);
        if (outerFrameImage != null) outerFrameImage.color = new Color32(16, 2, 1, 255);
    }

    public IEnumerator ShowFinalStandTransition(float duration)
    {
        if (phaseMessage == null || phaseFlashImage == null) yield break;
        string previous = phaseMessage.text;
        Color previousColor = phaseMessage.color;
        phaseMessage.text = "최종 집행";
        phaseMessage.color = new Color(1f, 0.22f, 0.12f, 1f);
        yield return ShowPhaseTransition(duration);
        phaseMessage.text = previous;
        phaseMessage.color = previousColor;
    }

    public void ShowBossBar(int current, int maximum)
    {
        bossBarVisible = true;
        float ratio = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
        currentRatio = ratio;
        delayedRatio = ratio;
        delayedVelocity = 0f;
        if (bossName != null) bossName.gameObject.SetActive(true);
        if (barContent != null) barContent.gameObject.SetActive(true);
        ApplyFill(currentFill, currentRatio);
        ApplyFill(delayedFill, delayedRatio);
    }

    public IEnumerator RevealBossBar(int current, int maximum, float duration)
    {
        ShowBossBar(current, maximum);
        Vector3 original = barContent != null ? barContent.localScale : Vector3.one;
        if (barContent != null) barContent.localScale = new Vector3(0.05f, 1f, 1f);
        if (bossName != null) bossName.alpha = 0f;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (barContent != null) barContent.localScale = new Vector3(Mathf.Lerp(0.05f, original.x, eased), original.y, original.z);
            if (bossName != null) bossName.alpha = eased;
            yield return null;
        }
        if (barContent != null) barContent.localScale = original;
        if (bossName != null) bossName.alpha = 1f;
    }

    public void ReportHealth(int current, int maximum, bool wasDamaged)
    {
        float ratio = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
        currentRatio = ratio;
        ApplyFill(currentFill, currentRatio);
        if (wasDamaged)
            lastDamageTime = Time.unscaledTime;
        if (delayedRatio < currentRatio)
        {
            delayedRatio = currentRatio;
            delayedVelocity = 0f;
            ApplyFill(delayedFill, delayedRatio);
        }
    }

    public IEnumerator FadeOut(float duration)
    {
        float start = rootGroup != null ? rootGroup.alpha : 1f;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (rootGroup != null)
                rootGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / safeDuration));
            yield return null;
        }
        if (rootGroup != null) rootGroup.alpha = 0f;
    }

    public void HideAllImmediate()
    {
        bossBarVisible = false;
        if (bossName != null) bossName.gameObject.SetActive(false);
        if (barContent != null) barContent.gameObject.SetActive(false);
        if (introMessage != null) introMessage.gameObject.SetActive(false);
        if (skipPrompt != null) skipPrompt.transform.parent.gameObject.SetActive(false);
        if (cinematicTitle != null) cinematicTitle.gameObject.SetActive(false);
        if (phaseMessage != null) phaseMessage.gameObject.SetActive(false);
        if (phaseFlashImage != null)
        {
            Color color = phaseFlashImage.color;
            color.a = 0f;
            phaseFlashImage.color = color;
        }
        SetCinematicBarsImmediate(false);
        if (rootGroup != null) rootGroup.alpha = 1f;
    }

    private static void ApplyFill(RectTransform fill, float ratio)
    {
        if (fill == null) return;
        ratio = Mathf.Clamp01(ratio);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateLeftFill(string name, Transform parent, Color color, int siblingIndex, out Image image)
    {
        GameObject root = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);
        image = root.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        root.transform.SetSiblingIndex(siblingIndex);
        return root.GetComponent<RectTransform>();
    }

    private static void CreateBracket(Transform parent, float side)
    {
        float x = side * 78f;
        CreateImage("BracketVertical", parent, new Color32(15, 3, 4, 255),
            new Vector2(x, 0f), new Vector2(3f, 12f), 6);
        CreateImage("BracketCapTop", parent, new Color32(121, 16, 21, 255),
            new Vector2(x - side * 2f, 4.5f), new Vector2(6f, 2f), 7);
        CreateImage("BracketCapBottom", parent, new Color32(121, 16, 21, 255),
            new Vector2(x - side * 2f, -4.5f), new Vector2(6f, 2f), 7);
    }

    private static RectTransform CreateCinemaBar(string name, Transform parent, bool top)
    {
        GameObject root = CreateRect(name, parent,
            top ? new Vector2(0f, 1f) : new Vector2(0f, 0f),
            top ? new Vector2(1f, 1f) : new Vector2(1f, 0f),
            top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f),
            top ? new Vector2(0f, 13f) : new Vector2(0f, -13f),
            new Vector2(0f, 12f));
        Image image = root.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = new Color(0.01f, 0.005f, 0.008f, 0.94f);
        image.raycastTarget = false;
        return root.GetComponent<RectTransform>();
    }

    private static Image CreateStretchImage(string name, Transform parent, Color color, int siblingIndex)
    {
        GameObject root = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = root.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        root.transform.SetSiblingIndex(siblingIndex);
        return image;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color,
        Vector2 position, Vector2 size, int siblingIndex)
    {
        GameObject root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, size);
        Image image = root.AddComponent<Image>();
        image.sprite = ExecutorRuntimeSprites.GetWhitePixel();
        image.color = color;
        image.raycastTarget = false;
        root.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, Mathf.Max(0, parent.childCount - 1)));
        return root;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, string text,
        float fontSize, FontStyles style, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject root = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, size);
        TextMeshProUGUI label = root.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return root;
    }
}
