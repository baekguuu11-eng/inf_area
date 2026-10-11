using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class InfectedAreaEndingCreditsAuto : MonoBehaviour
{
    private static InfectedAreaEndingCreditsAuto instance;

    [Header("Debug")]
    [SerializeField] private bool enableTKeyDebug = true;
    [SerializeField] private bool showDebugButton = false;

    [Header("Timing")]
    [SerializeField] private float totalDuration = 120f;
    [SerializeField] private float fadeInDuration = 2.2f;
    [SerializeField] private float fadeOutDuration = 1.2f;
    [SerializeField] private float textFitDuration = 44f;
    [SerializeField] private float galleryStartTime = 40f;
    [SerializeField] private float slideDuration = 4.5f;
    [SerializeField] private float slideFadeDuration = 0.45f;
    [SerializeField] private float videoMaxDuration = 8f;

    [Header("Text Scroll")]
    [SerializeField] private bool autoFitTextInDuration = true;
    [SerializeField] private float scrollSpeed = 180f;
    [SerializeField] private float startTextTopY = -180f;
    [SerializeField] private float textWidth = 1180f;

    [Header("Text Spacing")]
    [SerializeField] private float titleFontSize = 42f;
    [SerializeField] private float bodyFontSize = 27f;
    [SerializeField] private float smallFontSize = 22f;
    [SerializeField] private float lineSpacing = 32f;
    [SerializeField] private float paragraphSpacing = 78f;
    [SerializeField] private float sectionSpacing = 150f;
    [SerializeField] private float compactSpacing = 28f;

    [Header("Font")]
    [SerializeField] private TMP_FontAsset koreanFont;

    [Header("Audio")]
    [SerializeField] private AudioClip endingMusic;
    [SerializeField] private float musicVolume = 0.85f;
    [SerializeField] private float musicStartDelay = 1f;
    [SerializeField] private float musicFadeOutDuration = 4f;
    [SerializeField] private bool muteOtherAudioSources = true;

    [Header("Final")]
    [SerializeField] private string finalThanksText = "감사합니다^^";
    [SerializeField] private float finalThanksShowTime = 6f;

    [Header("Gallery")]
    [SerializeField] private Sprite[] gallerySprites;
    [SerializeField] private VideoClip[] galleryVideos;

    [Header("Decorations")]
    [SerializeField] private Sprite[] decorationSprites;
    [SerializeField] private int decorationCount = 5;
    [SerializeField] private float decorationAlpha = 0.08f;
    [SerializeField] private float decorationMoveAmount = 22f;
    [SerializeField] private float decorationSpeed = 0.35f;

    [Header("Scene")]
    [SerializeField] private bool loadMainMenuAfterEnding = false;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private RectTransform canvasRect;

    private Image blackBackground;

    private RectTransform textRoot;
    private VerticalLayoutGroup textLayout;

    private CanvasGroup galleryGroup;
    private Image galleryImage;
    private RawImage galleryVideoImage;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private TMP_Text galleryCaption;

    private CanvasGroup decorationGroup;
    private readonly List<RectTransform> decorationRects = new List<RectTransform>();
    private readonly List<Vector2> decorationBasePositions = new List<Vector2>();
    private readonly List<float> decorationPhases = new List<float>();

    private CanvasGroup finalThanksGroup;
    private TMP_Text finalThanksTMP;

    private AudioSource endingAudioSource;
    private Coroutine mainRoutine;
    private Coroutine galleryRoutine;
    private Coroutine musicRoutine;
    private Coroutine musicFadeRoutine;

    private bool isPlaying;
    private float elapsed;
    private float runtimeScrollSpeed;
    private float contentHeight;

    private readonly List<string> streamingVideoPaths = new List<string>();

    private struct GalleryItem
    {
        public Sprite sprite;
        public VideoClip videoClip;
        public string videoPath;
        public bool IsVideo
        {
            get { return videoClip != null || !string.IsNullOrEmpty(videoPath); }
        }
    }

    private readonly List<GalleryItem> galleryItems = new List<GalleryItem>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        BuildOrRepairAll();
        HideInstant();
    }

    private void Update()
    {
        UpdateDecorations();

        if (!enableTKeyDebug)
        {
            return;
        }

        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.T))
        {
            pressed = true;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            pressed = true;
        }
#endif

        if (pressed)
        {
            PlayEndingCredits();
        }
    }

    private void OnGUI()
    {
        if (!showDebugButton)
        {
            return;
        }

        if (GUI.Button(new Rect(20, 20, 260, 60), "TEST ENDING CREDITS"))
        {
            PlayEndingCredits();
        }
    }

    [ContextMenu("TEST - Play Ending Credits")]
    public void PlayEndingCredits()
    {
        if (isPlaying)
        {
            return;
        }

        if (mainRoutine != null)
        {
            StopCoroutine(mainRoutine);
        }

        mainRoutine = StartCoroutine(PlayEndingRoutine());
    }

    [ContextMenu("TEST - Stop Ending Credits")]
    public void StopEndingCredits()
    {
        StopAllEndingCoroutines();
        StopEndingMusicImmediate();
        HideInstant();

        isPlaying = false;
        elapsed = 0f;
        Time.timeScale = 1f;
    }

    private IEnumerator PlayEndingRoutine()
    {
        isPlaying = true;
        elapsed = 0f;

        BuildOrRepairAll();
        BuildCreditsText();
        BuildGalleryItems();

        if (muteOtherAudioSources)
        {
            StopAllOtherAudioSources();
        }

        Time.timeScale = 0f;

        PrepareStartState();

        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
        }

        musicRoutine = StartCoroutine(PlayMusicDelayedRoutine());

        if (galleryRoutine != null)
        {
            StopCoroutine(galleryRoutine);
        }

        galleryRoutine = StartCoroutine(PlayGalleryRoutine());

        yield return FadeRoot(0f, 1f, fadeInDuration);

        while (elapsed < totalDuration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            Vector2 pos = textRoot.anchoredPosition;
            pos.y += runtimeScrollSpeed * dt;
            textRoot.anchoredPosition = pos;

            if (elapsed >= totalDuration - finalThanksShowTime)
            {
                ShowFinalThanks();
                StartMusicFadeOutOnce();
            }

            yield return null;
        }

        yield return FadeRoot(1f, 0f, fadeOutDuration);

        StopAllEndingCoroutines();
        StopEndingMusicImmediate();
        HideInstant();

        isPlaying = false;
        elapsed = 0f;
        Time.timeScale = 1f;

        if (loadMainMenuAfterEnding && !string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void BuildOrRepairAll()
    {
        BuildCanvas();
        BuildBlackBackground();
        BuildTextRoot();
        BuildGalleryRoot();
        BuildDecorationRoot();
        BuildFinalThanks();
        BuildAudio();
    }

    private void BuildCanvas()
    {
        Transform old = transform.Find("EndingCreditsCanvas_FIXED");

        if (old == null)
        {
            GameObject canvasObject = new GameObject(
                "EndingCreditsCanvas_FIXED",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup)
            );

            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            rootGroup = canvasObject.GetComponent<CanvasGroup>();
        }
        else
        {
            canvas = old.GetComponent<Canvas>();
            if (canvas == null) canvas = old.gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = old.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = old.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            rootGroup = old.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = old.gameObject.AddComponent<CanvasGroup>();
        }

        canvasRect = canvas.GetComponent<RectTransform>();
        Stretch(canvasRect);
    }

    private void BuildBlackBackground()
    {
        Transform bgTransform = canvas.transform.Find("BlackBackground");

        if (bgTransform == null)
        {
            GameObject bg = new GameObject("BlackBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);

            blackBackground = bg.GetComponent<Image>();
        }
        else
        {
            blackBackground = bgTransform.GetComponent<Image>();
            if (blackBackground == null) blackBackground = bgTransform.gameObject.AddComponent<Image>();
        }

        blackBackground.color = Color.black;
        blackBackground.raycastTarget = true;
        Stretch(blackBackground.rectTransform);
        blackBackground.transform.SetAsFirstSibling();
    }

    private void BuildTextRoot()
    {
        Transform textTransform = canvas.transform.Find("TextRoot");

        if (textTransform == null)
        {
            GameObject textObject = new GameObject("TextRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
            textObject.transform.SetParent(canvas.transform, false);

            textRoot = textObject.GetComponent<RectTransform>();
        }
        else
        {
            textRoot = textTransform.GetComponent<RectTransform>();
            if (textRoot == null) textRoot = textTransform.gameObject.AddComponent<RectTransform>();

            textLayout = textTransform.GetComponent<VerticalLayoutGroup>();
            if (textLayout == null) textLayout = textTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        textRoot.anchorMin = new Vector2(0.5f, 0.5f);
        textRoot.anchorMax = new Vector2(0.5f, 0.5f);
        textRoot.pivot = new Vector2(0.5f, 1f);
        textRoot.sizeDelta = new Vector2(textWidth, 10000f);
        textRoot.anchoredPosition = new Vector2(0f, startTextTopY);
        textRoot.localScale = Vector3.one;

        textLayout = textRoot.GetComponent<VerticalLayoutGroup>();
        textLayout.childAlignment = TextAnchor.UpperCenter;
        textLayout.spacing = paragraphSpacing;
        textLayout.padding = new RectOffset(0, 0, 0, 0);
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = false;
        textLayout.childForceExpandHeight = false;
    }

    private void BuildGalleryRoot()
    {
        Transform galleryTransform = canvas.transform.Find("GalleryRoot");

        if (galleryTransform == null)
        {
            GameObject galleryObject = new GameObject("GalleryRoot", typeof(RectTransform), typeof(CanvasGroup));
            galleryObject.transform.SetParent(canvas.transform, false);

            RectTransform galleryRect = galleryObject.GetComponent<RectTransform>();
            galleryRect.anchorMin = new Vector2(0.5f, 0.5f);
            galleryRect.anchorMax = new Vector2(0.5f, 0.5f);
            galleryRect.pivot = new Vector2(0.5f, 0.5f);
            galleryRect.sizeDelta = new Vector2(1100f, 650f);
            galleryRect.anchoredPosition = new Vector2(0f, -35f);

            galleryGroup = galleryObject.GetComponent<CanvasGroup>();
        }
        else
        {
            galleryGroup = galleryTransform.GetComponent<CanvasGroup>();
            if (galleryGroup == null) galleryGroup = galleryTransform.gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform root = galleryGroup.GetComponent<RectTransform>();

        Transform imageTransform = root.Find("GalleryImage");
        if (imageTransform == null)
        {
            GameObject imageObject = new GameObject("GalleryImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(root, false);
            galleryImage = imageObject.GetComponent<Image>();
        }
        else
        {
            galleryImage = imageTransform.GetComponent<Image>();
            if (galleryImage == null) galleryImage = imageTransform.gameObject.AddComponent<Image>();
        }

        RectTransform imageRect = galleryImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(1000f, 560f);
        imageRect.anchoredPosition = new Vector2(0f, 20f);
        galleryImage.preserveAspect = true;
        galleryImage.raycastTarget = false;

        Transform rawTransform = root.Find("GalleryVideo");
        if (rawTransform == null)
        {
            GameObject rawObject = new GameObject("GalleryVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rawObject.transform.SetParent(root, false);
            galleryVideoImage = rawObject.GetComponent<RawImage>();
        }
        else
        {
            galleryVideoImage = rawTransform.GetComponent<RawImage>();
            if (galleryVideoImage == null) galleryVideoImage = rawTransform.gameObject.AddComponent<RawImage>();
        }

        RectTransform rawRect = galleryVideoImage.rectTransform;
        rawRect.anchorMin = new Vector2(0.5f, 0.5f);
        rawRect.anchorMax = new Vector2(0.5f, 0.5f);
        rawRect.pivot = new Vector2(0.5f, 0.5f);
        rawRect.sizeDelta = new Vector2(1000f, 560f);
        rawRect.anchoredPosition = new Vector2(0f, 20f);
        galleryVideoImage.raycastTarget = false;
        galleryVideoImage.gameObject.SetActive(false);

        Transform captionTransform = root.Find("GalleryCaption");
        if (captionTransform == null)
        {
            GameObject captionObject = new GameObject("GalleryCaption", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            captionObject.transform.SetParent(root, false);
            galleryCaption = captionObject.GetComponent<TMP_Text>();
        }
        else
        {
            galleryCaption = captionTransform.GetComponent<TMP_Text>();
            if (galleryCaption == null) galleryCaption = captionTransform.gameObject.AddComponent<TextMeshProUGUI>();
        }

        if (koreanFont != null)
        {
            galleryCaption.font = koreanFont;
        }

        galleryCaption.text = "";
        galleryCaption.fontSize = 24f;
        galleryCaption.alignment = TextAlignmentOptions.Center;
        galleryCaption.color = new Color(0.78f, 0.92f, 1f, 1f);
        galleryCaption.raycastTarget = false;

        RectTransform captionRect = galleryCaption.rectTransform;
        captionRect.anchorMin = new Vector2(0.5f, 0f);
        captionRect.anchorMax = new Vector2(0.5f, 0f);
        captionRect.pivot = new Vector2(0.5f, 0f);
        captionRect.sizeDelta = new Vector2(1000f, 54f);
        captionRect.anchoredPosition = new Vector2(0f, -30f);

        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;

        if (videoTexture == null)
        {
            videoTexture = new RenderTexture(1280, 720, 0);
            videoTexture.name = "EndingCreditsVideoRT";
        }

        videoPlayer.targetTexture = videoTexture;
        galleryVideoImage.texture = videoTexture;

        galleryGroup.alpha = 0f;
        galleryGroup.interactable = false;
        galleryGroup.blocksRaycasts = false;
    }

    private void BuildDecorationRoot()
    {
        Transform decoTransform = canvas.transform.Find("DecorationRoot");

        if (decoTransform == null)
        {
            GameObject decoObject = new GameObject("DecorationRoot", typeof(RectTransform), typeof(CanvasGroup));
            decoObject.transform.SetParent(canvas.transform, false);

            decorationGroup = decoObject.GetComponent<CanvasGroup>();
            Stretch(decoObject.GetComponent<RectTransform>());
        }
        else
        {
            decorationGroup = decoTransform.GetComponent<CanvasGroup>();
            if (decorationGroup == null) decorationGroup = decoTransform.gameObject.AddComponent<CanvasGroup>();
            Stretch(decorationGroup.GetComponent<RectTransform>());
        }

        decorationGroup.alpha = 1f;
        decorationGroup.interactable = false;
        decorationGroup.blocksRaycasts = false;

        decorationGroup.transform.SetSiblingIndex(1);

        BuildDecorations();
    }

    private void BuildDecorations()
    {
        decorationRects.Clear();
        decorationBasePositions.Clear();
        decorationPhases.Clear();

        RectTransform parent = decorationGroup.GetComponent<RectTransform>();

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }

        Sprite[] sprites = GetDecorationSprites();

        int count = Mathf.Max(0, decorationCount);

        for (int i = 0; i < count; i++)
        {
            GameObject deco = new GameObject("Decoration_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            deco.transform.SetParent(parent, false);

            Image img = deco.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, decorationAlpha);

            if (sprites != null && sprites.Length > 0)
            {
                img.sprite = sprites[i % sprites.Length];
                img.preserveAspect = true;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.1f, 0.9f, 1f, decorationAlpha);
            }

            RectTransform rt = deco.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float x = Mathf.Lerp(-760f, 760f, Mathf.Repeat(i * 0.37f, 1f));
            float y = Mathf.Lerp(-350f, 360f, Mathf.Repeat(i * 0.53f, 1f));
            float size = Mathf.Lerp(45f, 95f, Mathf.Repeat(i * 0.21f, 1f));

            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, i * 31f);

            decorationRects.Add(rt);
            decorationBasePositions.Add(rt.anchoredPosition);
            decorationPhases.Add(i * 1.7f);
        }
    }

    private void BuildFinalThanks()
    {
        Transform thanksTransform = canvas.transform.Find("FinalThanks");

        if (thanksTransform == null)
        {
            GameObject thanksObject = new GameObject("FinalThanks", typeof(RectTransform), typeof(CanvasGroup), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            thanksObject.transform.SetParent(canvas.transform, false);

            finalThanksGroup = thanksObject.GetComponent<CanvasGroup>();
            finalThanksTMP = thanksObject.GetComponent<TMP_Text>();
        }
        else
        {
            finalThanksGroup = thanksTransform.GetComponent<CanvasGroup>();
            if (finalThanksGroup == null) finalThanksGroup = thanksTransform.gameObject.AddComponent<CanvasGroup>();

            finalThanksTMP = thanksTransform.GetComponent<TMP_Text>();
            if (finalThanksTMP == null) finalThanksTMP = thanksTransform.gameObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rect = finalThanksTMP.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 140f);
        rect.anchoredPosition = Vector2.zero;

        if (koreanFont != null)
        {
            finalThanksTMP.font = koreanFont;
        }

        finalThanksTMP.text = finalThanksText;
        finalThanksTMP.fontSize = 58f;
        finalThanksTMP.alignment = TextAlignmentOptions.Center;
        finalThanksTMP.color = Color.white;
        finalThanksTMP.raycastTarget = false;

        finalThanksGroup.alpha = 0f;
        finalThanksGroup.interactable = false;
        finalThanksGroup.blocksRaycasts = false;

        finalThanksTMP.transform.SetAsLastSibling();
    }

    private void BuildAudio()
    {
        endingAudioSource = GetComponent<AudioSource>();
        if (endingAudioSource == null)
        {
            endingAudioSource = gameObject.AddComponent<AudioSource>();
        }

        endingAudioSource.playOnAwake = false;
        endingAudioSource.loop = false;
        endingAudioSource.ignoreListenerPause = true;
        endingAudioSource.volume = musicVolume;
    }

    private void BuildCreditsText()
    {
        ClearTextRoot();

        string raw = LoadCreditsText();

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = GetDefaultCreditsText();
        }

        string[] lines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        bool previousWasCompactHeader = false;

        AddSpacer(80f);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line == null)
            {
                continue;
            }

            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                AddSpacer(paragraphSpacing * 0.45f);
                previousWasCompactHeader = false;
                continue;
            }

            if (trimmed == "---")
            {
                AddSpacer(sectionSpacing);
                previousWasCompactHeader = false;
                continue;
            }

            if (trimmed.StartsWith("# "))
            {
                string title = trimmed.Substring(2).Trim();
                AddText(title, titleFontSize, FontStyles.Bold, new Color(0.86f, 0.96f, 1f, 1f), false);
                AddSpacer(sectionSpacing * 0.35f);

                previousWasCompactHeader =
                    title.ToUpperInvariant().Contains("PROJECT TEAM") ||
                    title.ToUpperInvariant().Contains("CREDITS");

                continue;
            }

            if (trimmed.StartsWith("## "))
            {
                AddText(trimmed.Substring(3).Trim(), bodyFontSize + 4f, FontStyles.Bold, new Color(0.75f, 0.92f, 1f, 1f), false);
                AddSpacer(paragraphSpacing * 0.35f);
                previousWasCompactHeader = false;
                continue;
            }

            if (trimmed.StartsWith("- "))
            {
                AddText(trimmed.Substring(2).Trim(), smallFontSize, FontStyles.Normal, Color.white, true);
                AddSpacer(compactSpacing);
                previousWasCompactHeader = true;
                continue;
            }

            AddText(trimmed, bodyFontSize, previousWasCompactHeader ? FontStyles.Bold : FontStyles.Normal, Color.white, previousWasCompactHeader);

            if (previousWasCompactHeader)
            {
                AddSpacer(compactSpacing);
            }
            else
            {
                AddSpacer(paragraphSpacing * 0.55f);
            }
        }

        AddSpacer(220f);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRoot);

        contentHeight = CalculateContentHeight();

        if (autoFitTextInDuration)
        {
            float screenHalfHeight = 540f;
            float distance = contentHeight + screenHalfHeight + 160f - startTextTopY;
            runtimeScrollSpeed = distance / Mathf.Max(1f, textFitDuration);
        }
        else
        {
            runtimeScrollSpeed = scrollSpeed;
        }

        if (runtimeScrollSpeed < 80f)
        {
            runtimeScrollSpeed = 80f;
        }
    }

    private TMP_Text AddText(string text, float fontSize, FontStyles style, Color color, bool compact)
    {
        GameObject textObject = new GameObject("CreditText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(textRoot, false);

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();

        if (koreanFont != null)
        {
            tmp.font = koreanFont;
        }

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.lineSpacing = lineSpacing;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        RectTransform rect = tmp.rectTransform;
        rect.sizeDelta = new Vector2(textWidth, GetTextHeight(text, fontSize, compact));

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredWidth = textWidth;
        layout.preferredHeight = GetTextHeight(text, fontSize, compact);

        return tmp;
    }

    private void AddSpacer(float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(textRoot, false);

        LayoutElement layout = spacer.GetComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;

        RectTransform rect = spacer.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1f, height);
    }

    private float GetTextHeight(string text, float fontSize, bool compact)
    {
        int lineCount = 1;

        if (!string.IsNullOrEmpty(text))
        {
            lineCount = text.Split('\n').Length;
        }

        float multiplier = compact ? 1.28f : 1.58f;
        float height = fontSize * multiplier * lineCount;
        height += lineSpacing * Mathf.Max(0, lineCount - 1);
        height += compact ? 8f : 18f;

        return Mathf.Max(height, fontSize + 18f);
    }

    private float CalculateContentHeight()
    {
        float total = 0f;

        for (int i = 0; i < textRoot.childCount; i++)
        {
            RectTransform child = textRoot.GetChild(i) as RectTransform;
            if (child != null)
            {
                total += child.sizeDelta.y;
            }
        }

        VerticalLayoutGroup layout = textRoot.GetComponent<VerticalLayoutGroup>();
        if (layout != null && textRoot.childCount > 1)
        {
            total += layout.spacing * (textRoot.childCount - 1);
        }

        return total;
    }

    private string LoadCreditsText()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("EndingCredits/EndingCreditsText");

        if (textAsset != null)
        {
            return textAsset.text;
        }

        return "";
    }

    private string GetDefaultCreditsText()
    {
        return
            "# INFECTED AREA\n" +
            "엔딩 크레딧\n" +
            "---\n" +
            "# 당신은 백신이 아니었다.\n" +
            "감염구역은 당신을 치료제로 부르지 않았다.\n" +
            "그저 가장 오래 버틴 감염체에게 마지막 문을 열 권한을 주었을 뿐.\n" +
            "\n" +
            "쓰러진 적들의 데이터는 모두 같은 문장을 남겼다.\n" +
            "“우리도 한때는 탈출하고 싶었다.”\n" +
            "\n" +
            "검은 연구소의 심장은 멈췄고,\n" +
            "닫혀 있던 격리문 너머로 처음 바람이 들어왔다.\n" +
            "\n" +
            "감염은 끝나지 않았을지도 모른다.\n" +
            "하지만 누군가는 마침내 괴물이 아닌 이름으로 아침을 맞이했다.\n" +
            "---\n" +
            "# PROJECT TEAM\n" +
            "김두현\n" +
            "이주윤\n" +
            "김희주\n" +
            "박믿음\n" +
            "---\n" +
            "# CREDITS\n" +
            "- 기획  ____________________\n" +
            "- 프로그래밍  ____________________\n" +
            "- 아트 / UI  ____________________\n" +
            "- 사운드 / 음악  ____________________\n" +
            "- 테스트 플레이  ____________________\n" +
            "- 도움 준 사람들  ____________________\n" +
            "---\n" +
            "# MAKING PROCESS\n" +
            "잠시 후 제작 과정의 기록이 이어집니다.\n";
    }

    private void ClearTextRoot()
    {
        if (textRoot == null)
        {
            return;
        }

        for (int i = textRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(textRoot.GetChild(i).gameObject);
        }
    }

    private void BuildGalleryItems()
    {
        galleryItems.Clear();
        streamingVideoPaths.Clear();

        Sprite[] resourceSprites = Resources.LoadAll<Sprite>("EndingCredits/Gallery");

        if (resourceSprites != null)
        {
            for (int i = 0; i < resourceSprites.Length; i++)
            {
                if (resourceSprites[i] != null)
                {
                    GalleryItem item = new GalleryItem();
                    item.sprite = resourceSprites[i];
                    galleryItems.Add(item);
                }
            }
        }

        if (gallerySprites != null)
        {
            for (int i = 0; i < gallerySprites.Length; i++)
            {
                if (gallerySprites[i] != null)
                {
                    GalleryItem item = new GalleryItem();
                    item.sprite = gallerySprites[i];
                    galleryItems.Add(item);
                }
            }
        }

        if (galleryVideos != null)
        {
            for (int i = 0; i < galleryVideos.Length; i++)
            {
                if (galleryVideos[i] != null)
                {
                    GalleryItem item = new GalleryItem();
                    item.videoClip = galleryVideos[i];
                    galleryItems.Add(item);
                }
            }
        }

        AddStreamingVideos();

        Debug.Log("[EndingCredits] Gallery items: " + galleryItems.Count);
    }

    private void AddStreamingVideos()
    {
        string folder = Path.Combine(Application.streamingAssetsPath, "EndingCredits/Videos");

        if (!Directory.Exists(folder))
        {
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            string ext = Path.GetExtension(files[i]).ToLowerInvariant();

            if (ext == ".mp4" || ext == ".mov" || ext == ".webm" || ext == ".m4v")
            {
                string path = files[i].Replace('\\', '/');

                GalleryItem item = new GalleryItem();
                item.videoPath = path;
                galleryItems.Add(item);

                streamingVideoPaths.Add(path);
            }
        }
    }

    private IEnumerator PlayGalleryRoutine()
    {
        if (galleryGroup == null)
        {
            yield break;
        }

        galleryGroup.alpha = 0f;

        while (elapsed < galleryStartTime)
        {
            yield return null;
        }

        if (galleryItems.Count <= 0)
        {
            Debug.Log("[EndingCredits] Gallery items: 0");
            yield break;
        }

        for (int i = 0; i < galleryItems.Count; i++)
        {
            GalleryItem item = galleryItems[i];

            if (item.IsVideo)
            {
                yield return PlayVideoItem(item, i);
            }
            else
            {
                yield return PlaySpriteItem(item, i);
            }
        }

        galleryGroup.alpha = 0f;
    }

    private IEnumerator PlaySpriteItem(GalleryItem item, int index)
    {
        StopVideoDisplay();

        galleryImage.gameObject.SetActive(true);
        galleryVideoImage.gameObject.SetActive(false);

        galleryImage.sprite = item.sprite;
        galleryImage.preserveAspect = true;

        if (galleryCaption != null)
        {
            galleryCaption.text = "제작 과정 기록  " + (index + 1).ToString("00");
        }

        yield return FadeGallery(0f, 1f, slideFadeDuration);

        float timer = 0f;
        while (timer < slideDuration && elapsed < totalDuration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeGallery(1f, 0f, slideFadeDuration);
    }

    private IEnumerator PlayVideoItem(GalleryItem item, int index)
    {
        galleryImage.gameObject.SetActive(false);
        galleryVideoImage.gameObject.SetActive(true);

        if (galleryCaption != null)
        {
            galleryCaption.text = "제작 과정 영상  " + (index + 1).ToString("00");
        }

        videoPlayer.Stop();

        if (item.videoClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = item.videoClip;
        }
        else
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = item.videoPath;
        }

        videoPlayer.targetTexture = videoTexture;
        galleryVideoImage.texture = videoTexture;

        videoPlayer.Prepare();

        float prepareTimer = 0f;
        while (!videoPlayer.isPrepared && prepareTimer < 3f)
        {
            prepareTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeGallery(0f, 1f, slideFadeDuration);

        videoPlayer.Play();

        float timer = 0f;
        while (timer < videoMaxDuration && elapsed < totalDuration)
        {
            timer += Time.unscaledDeltaTime;

            if (videoPlayer.isPrepared && !videoPlayer.isPlaying && timer > 0.8f)
            {
                break;
            }

            yield return null;
        }

        videoPlayer.Stop();

        yield return FadeGallery(1f, 0f, slideFadeDuration);
    }

    private void StopVideoDisplay()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (galleryVideoImage != null)
        {
            galleryVideoImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeGallery(float from, float to, float duration)
    {
        float timer = 0f;
        galleryGroup.alpha = from;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            galleryGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        galleryGroup.alpha = to;
    }

    private void PrepareStartState()
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
        }

        rootGroup.alpha = 0f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;

        if (decorationGroup != null)
        {
            decorationGroup.alpha = 1f;
        }

        if (galleryGroup != null)
        {
            galleryGroup.alpha = 0f;
        }

        if (finalThanksGroup != null)
        {
            finalThanksGroup.alpha = 0f;
        }

        textRoot.anchoredPosition = new Vector2(0f, startTextTopY);
    }

    private IEnumerator FadeRoot(float from, float to, float duration)
    {
        float timer = 0f;
        rootGroup.alpha = from;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            rootGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        rootGroup.alpha = to;
    }

    private IEnumerator PlayMusicDelayedRoutine()
    {
        if (endingAudioSource == null || endingMusic == null)
        {
            yield break;
        }

        endingAudioSource.Stop();
        endingAudioSource.clip = endingMusic;
        endingAudioSource.volume = musicVolume;
        endingAudioSource.loop = false;
        endingAudioSource.time = 0f;

        float timer = 0f;
        while (timer < musicStartDelay)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        endingAudioSource.Play();
    }

    private void StartMusicFadeOutOnce()
    {
        if (musicFadeRoutine != null)
        {
            return;
        }

        musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine());
    }

    private IEnumerator FadeOutMusicRoutine()
    {
        if (endingAudioSource == null)
        {
            yield break;
        }

        float startVolume = endingAudioSource.volume;
        float timer = 0f;

        while (timer < musicFadeOutDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / musicFadeOutDuration);
            endingAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        endingAudioSource.volume = 0f;
    }

    private void StopAllOtherAudioSources()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];

            if (source == null)
            {
                continue;
            }

            if (source == endingAudioSource)
            {
                continue;
            }

            if (source.transform.IsChildOf(transform))
            {
                continue;
            }

            if (source.isPlaying)
            {
                source.Stop();
            }

            source.playOnAwake = false;
        }

        AudioListener.pause = false;
    }

    private void StopEndingMusicImmediate()
    {
        if (endingAudioSource != null)
        {
            endingAudioSource.Stop();
            endingAudioSource.volume = musicVolume;
        }
    }

    private void ShowFinalThanks()
    {
        if (finalThanksGroup == null)
        {
            return;
        }

        if (finalThanksGroup.alpha < 1f)
        {
            finalThanksGroup.alpha = Mathf.MoveTowards(finalThanksGroup.alpha, 1f, Time.unscaledDeltaTime * 0.8f);
        }
    }

    private void HideInstant()
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        if (galleryGroup != null)
        {
            galleryGroup.alpha = 0f;
        }

        if (finalThanksGroup != null)
        {
            finalThanksGroup.alpha = 0f;
        }

        StopVideoDisplay();
    }

    private void StopAllEndingCoroutines()
    {
        if (mainRoutine != null)
        {
            StopCoroutine(mainRoutine);
            mainRoutine = null;
        }

        if (galleryRoutine != null)
        {
            StopCoroutine(galleryRoutine);
            galleryRoutine = null;
        }

        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
            musicRoutine = null;
        }

        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }
    }

    private Sprite[] GetDecorationSprites()
    {
        if (decorationSprites != null && decorationSprites.Length > 0)
        {
            return decorationSprites;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>("EndingCredits/Decorations");

        if (loaded != null && loaded.Length > 0)
        {
            return loaded;
        }

        return null;
    }

    private void UpdateDecorations()
    {
        if (!isPlaying)
        {
            return;
        }

        for (int i = 0; i < decorationRects.Count; i++)
        {
            if (decorationRects[i] == null)
            {
                continue;
            }

            float wave = Mathf.Sin((Time.unscaledTime * decorationSpeed) + decorationPhases[i]);
            float wave2 = Mathf.Cos((Time.unscaledTime * decorationSpeed * 0.73f) + decorationPhases[i]);

            Vector2 basePos = decorationBasePositions[i];
            decorationRects[i].anchoredPosition = basePos + new Vector2(wave * decorationMoveAmount, wave2 * decorationMoveAmount);
        }
    }

    private void Stretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}