using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomTransitionUI : MonoBehaviour
{
    [System.Serializable]
    public class RoomLabelOverride
    {
        public int stageNumber = 1;
        public int roomNumber = 1;
        [TextArea] public string labelText;
    }

    [Header("References")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private TMP_Text roomLabelText;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private float fadeInDuration = 0.18f;

    [Header("Room Label Settings")]
    [SerializeField] private float roomLabelFadeInDuration = 0.16f;
    [SerializeField] private float roomLabelHoldDuration = 0.82f;
    [SerializeField] private float roomLabelFadeOutDuration = 0.28f;
    [SerializeField] private List<RoomLabelOverride> roomLabelOverrides = new();

    private CanvasGroup labelFrameGroup;
    private RectTransform labelFrameRect;
    private TMP_Text subtitleText;
    private readonly List<Image> accentImages = new List<Image>();
    private Color activeTextColor = new Color(0.38f, 0.88f, 1f, 1f);
    private Color activeAccentColor = new Color(0.18f, 0.66f, 1f, 1f);

    public float FadeOutDuration => fadeOutDuration;
    public float FadeInDuration => fadeInDuration;

    private void Awake()
    {
        if (fadePanel != null)
            SetFadeAlpha(0f);

        if (roomLabelText != null)
        {
            ConfigureLabelTypography();
            BuildLabelFrame();
            roomLabelText.text = string.Empty;
            SetLabelAlpha(0f);
        }
    }

    private void ConfigureLabelTypography()
    {
        TMP_FontAsset galmuri = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        if (galmuri != null)
            roomLabelText.font = galmuri;

        roomLabelText.fontSize = 64f;
        roomLabelText.fontStyle = FontStyles.Bold;
        roomLabelText.alignment = TextAlignmentOptions.Center;
        roomLabelText.characterSpacing = 4f;
        roomLabelText.enableWordWrapping = false;
        roomLabelText.raycastTarget = false;
        roomLabelText.color = activeTextColor;
    }

    private void BuildLabelFrame()
    {
        RectTransform labelRect = roomLabelText != null ? roomLabelText.rectTransform : null;
        if (labelRect == null || labelRect.parent == null)
            return;

        GameObject frameObject = new GameObject("RoomLabelFrame", typeof(RectTransform), typeof(CanvasGroup));
        labelFrameRect = frameObject.GetComponent<RectTransform>();
        labelFrameRect.SetParent(labelRect.parent, false);
        labelFrameRect.anchorMin = labelRect.anchorMin;
        labelFrameRect.anchorMax = labelRect.anchorMax;
        labelFrameRect.pivot = labelRect.pivot;
        labelFrameRect.anchoredPosition = labelRect.anchoredPosition;
        labelFrameRect.sizeDelta = new Vector2(720f, 116f);
        labelFrameRect.SetSiblingIndex(labelRect.GetSiblingIndex());

        labelFrameGroup = frameObject.GetComponent<CanvasGroup>();
        labelFrameGroup.alpha = 0f;
        labelFrameGroup.interactable = false;
        labelFrameGroup.blocksRaycasts = false;

        Image background = CreatePanel("Background", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(660f, 78f), new Color(0.008f, 0.035f, 0.075f, 0.78f));
        background.raycastTarget = false;

        accentImages.Add(CreatePanel("TopLine", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 42f), new Vector2(430f, 2f), activeAccentColor));
        accentImages.Add(CreatePanel("BottomLine", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -42f), new Vector2(430f, 2f), activeAccentColor));
        accentImages.Add(CreatePanel("LeftBracket", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-342f, 0f), new Vector2(5f, 56f), activeAccentColor));
        accentImages.Add(CreatePanel("RightBracket", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(342f, 0f), new Vector2(5f, 56f), activeAccentColor));

        accentImages.Add(CreatePanel("LeftNode", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-326f, 0f), new Vector2(7f, 7f), activeAccentColor));
        accentImages.Add(CreatePanel("RightNode", labelFrameRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(326f, 0f), new Vector2(7f, 7f), activeAccentColor));

        GameObject subtitleObject = new GameObject("RoomLabelSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.SetParent(labelFrameRect, false);
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.pivot = new Vector2(0.5f, 0.5f);
        subtitleRect.anchoredPosition = new Vector2(0f, -63f);
        subtitleRect.sizeDelta = new Vector2(520f, 32f);
        subtitleText = subtitleObject.GetComponent<TMP_Text>();
        TMP_FontAsset subtitleFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        if (subtitleFont != null)
            subtitleText.font = subtitleFont;
        subtitleText.fontSize = 20f;
        subtitleText.characterSpacing = 5f;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.enableWordWrapping = false;
        subtitleText.raycastTarget = false;
        subtitleText.text = "NETWORK SECTOR";
        subtitleText.color = new Color(activeAccentColor.r, activeAccentColor.g, activeAccentColor.b, 0.82f);
    }

    private static Image CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public void SetFadeAlpha(float alpha)
    {
        if (fadePanel == null)
            return;

        Color color = fadePanel.color;
        color.a = Mathf.Clamp01(alpha);
        fadePanel.color = color;
    }

    public string GetRoomLabel(int stage, int room)
    {
        for (int i = 0; i < roomLabelOverrides.Count; i++)
        {
            RoomLabelOverride item = roomLabelOverrides[i];
            if (item.stageNumber == stage && item.roomNumber == room && !string.IsNullOrWhiteSpace(item.labelText))
                return item.labelText;
        }

        return GetFallbackRoomLabel(stage, room);
    }

    private string GetFallbackRoomLabel(int stage, int room)
    {
        if (room == 5)
            return "PORTAL ROOM";
        return $"SECTOR {stage}-{room}";
    }

    private void ApplyRoomTheme(int room)
    {
        bool portal = room == 5;
        activeTextColor = portal
            ? new Color(0.82f, 0.64f, 1f, 1f)
            : new Color(0.42f, 0.92f, 1f, 1f);
        activeAccentColor = portal
            ? new Color(0.62f, 0.30f, 1f, 1f)
            : new Color(0.10f, 0.62f, 1f, 1f);

        if (roomLabelText != null)
            roomLabelText.color = activeTextColor;
        if (subtitleText != null)
        {
            subtitleText.text = portal ? "TRANSFER NODE" : "NETWORK SECTOR";
            subtitleText.color = new Color(activeAccentColor.r, activeAccentColor.g, activeAccentColor.b, 0.86f);
        }
        for (int i = 0; i < accentImages.Count; i++)
        {
            if (accentImages[i] != null)
                accentImages[i].color = activeAccentColor;
        }
    }

    public IEnumerator ShowRoomLabel(int stage, int room)
    {
        if (roomLabelText == null)
            yield break;

        ApplyRoomTheme(room);
        roomLabelText.text = GetRoomLabel(stage, room);

        RectTransform labelRect = roomLabelText.rectTransform;
        Vector3 originalScale = Vector3.one;
        labelRect.localScale = originalScale * 0.93f;
        if (labelFrameRect != null)
            labelFrameRect.localScale = originalScale * 0.96f;

        yield return AnimateLabel(0f, 1f, roomLabelFadeInDuration, 0.93f, 1f);
        yield return new WaitForSecondsRealtime(roomLabelHoldDuration);
        yield return AnimateLabel(1f, 0f, roomLabelFadeOutDuration, 1f, 0.98f);
    }

    private IEnumerator AnimateLabel(float fromAlpha, float toAlpha, float duration, float fromScale, float toScale)
    {
        if (roomLabelText == null)
            yield break;

        if (duration <= 0f)
        {
            SetLabelAlpha(toAlpha);
            roomLabelText.rectTransform.localScale = Vector3.one * toScale;
            if (labelFrameRect != null) labelFrameRect.localScale = Vector3.one * toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            SetLabelAlpha(Mathf.Lerp(fromAlpha, toAlpha, eased));
            float scale = Mathf.Lerp(fromScale, toScale, eased);
            roomLabelText.rectTransform.localScale = Vector3.one * scale;
            if (labelFrameRect != null) labelFrameRect.localScale = Vector3.one * scale;
            yield return null;
        }

        SetLabelAlpha(toAlpha);
        roomLabelText.rectTransform.localScale = Vector3.one * toScale;
        if (labelFrameRect != null) labelFrameRect.localScale = Vector3.one * toScale;
    }

    private void SetLabelAlpha(float alpha)
    {
        if (roomLabelText == null)
            return;

        float safe = Mathf.Clamp01(alpha);
        roomLabelText.color = new Color(activeTextColor.r, activeTextColor.g, activeTextColor.b, safe);
        if (labelFrameGroup != null)
            labelFrameGroup.alpha = safe;
    }
}
