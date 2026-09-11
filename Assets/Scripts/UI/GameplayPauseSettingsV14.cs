using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>ESC pause/settings menu for GameScene. All labels are Korean and audio changes are live.</summary>
[DefaultExecutionOrder(-150)]
public sealed class GameplayPauseSettingsV14 : MonoBehaviour
{
    public static GameplayPauseSettingsV14 Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;
    public static bool EscapeIsReservedForPause => SceneManager.GetActiveScene().name == "GameScene";

    private Canvas canvas;
    private GameObject root;
    private bool open;
    private IDisposable inputLock;
    private float previousTimeScale = 1f;
    private TMP_FontAsset font;
    private Slider masterSlider;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private TMP_Text masterValue;
    private TMP_Text bgmValue;
    private TMP_Text sfxValue;
    private TMP_Text fullscreenLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GameplayPauseSettingsV14");
        DontDestroyOnLoad(go);
        go.AddComponent<GameplayPauseSettingsV14>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
        ReleasePause();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReleasePause();
        if (canvas != null) Destroy(canvas.gameObject);
        root = null;
        canvas = null;
        if (scene.name == "GameScene") Build();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // ESC first closes the existing shop, preserving the game's current portal-room behavior.
        if (!open && ShopManager.Instance != null && ShopManager.Instance.IsOpen) return;
        if (open) Close(); else Open();
    }

    public void Open()
    {
        if (open || root == null) return;
        open = true;
        previousTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        inputLock = GameInputState.Acquire("PauseSettingsV14");
        root.SetActive(true);
        SyncControls();
    }

    public void Close()
    {
        if (!open) return;
        ReleasePause();
        if (root != null) root.SetActive(false);
    }

    private void ReleasePause()
    {
        if (!open && inputLock == null) return;
        open = false;
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }
        Time.timeScale = previousTimeScale > 0.001f ? previousTimeScale : 1f;
    }

    private void Build()
    {
        EnsureEventSystem();
        GameObject canvasGo = new GameObject("PauseSettingsCanvasV14", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGo);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateImage("PauseRoot", canvas.transform, new Color(0.005f, 0.012f, 0.025f, 0.78f));
        Stretch(root.GetComponent<RectTransform>());

        GameObject panel = CreateImage("Panel", root.transform, new Color(0.018f, 0.055f, 0.078f, 0.98f));
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(650f, 700f); pr.anchoredPosition = Vector2.zero;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.70f, 0.82f, 0.8f); outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateText("Title", panel.transform, "일시정지", 38f, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0f, -42f), new Vector2(560f, 64f), new Vector2(0.5f, 1f));
        title.color = new Color(0.63f, 0.96f, 1f, 1f);
        TMP_Text subtitle = CreateText("Subtitle", panel.transform, "시스템 설정", 18f, TextAlignmentOptions.Center);
        Place(subtitle.rectTransform, new Vector2(0f, -98f), new Vector2(500f, 36f), new Vector2(0.5f, 1f));
        subtitle.color = new Color(0.30f, 0.70f, 0.78f, 1f);

        masterSlider = CreateVolumeRow(panel.transform, "마스터 음량", -170f, GlobalAudioSettingsV14.MasterVolume, out masterValue, GlobalAudioSettingsV14.SetMaster);
        bgmSlider = CreateVolumeRow(panel.transform, "배경음", -250f, GlobalAudioSettingsV14.BgmVolume, out bgmValue, GlobalAudioSettingsV14.SetBgm);
        sfxSlider = CreateVolumeRow(panel.transform, "효과음", -330f, GlobalAudioSettingsV14.SfxVolume, out sfxValue, GlobalAudioSettingsV14.SetSfx);

        Button fullscreen = CreateButton("Fullscreen", panel.transform, "전체 화면", out fullscreenLabel);
        Place(fullscreen.GetComponent<RectTransform>(), new Vector2(0f, -420f), new Vector2(420f, 52f), new Vector2(0.5f, 1f));
        fullscreen.onClick.AddListener(ToggleFullscreen);

        Button resume = CreateButton("Resume", panel.transform, "계속하기", out _);
        Place(resume.GetComponent<RectTransform>(), new Vector2(0f, 155f), new Vector2(420f, 54f), new Vector2(0.5f, 0f));
        resume.onClick.AddListener(Close);

        Button restart = CreateButton("Restart", panel.transform, "게임 다시 시작", out _);
        Place(restart.GetComponent<RectTransform>(), new Vector2(0f, 92f), new Vector2(420f, 50f), new Vector2(0.5f, 0f));
        restart.onClick.AddListener(RestartGame);

        Button menu = CreateButton("MainMenu", panel.transform, "메인 메뉴로", out _);
        Place(menu.GetComponent<RectTransform>(), new Vector2(0f, 32f), new Vector2(420f, 50f), new Vector2(0.5f, 0f));
        menu.onClick.AddListener(ReturnToMenu);

        root.SetActive(false);
    }

    private Slider CreateVolumeRow(Transform parent, string label, float y, float initial, out TMP_Text valueText, UnityEngine.Events.UnityAction<float> changed)
    {
        TMP_Text name = CreateText(label + "Label", parent, label, 20f, TextAlignmentOptions.Left);
        Place(name.rectTransform, new Vector2(-180f, y), new Vector2(190f, 40f), new Vector2(0.5f, 1f));
        name.color = new Color(0.82f, 0.96f, 1f, 1f);

        GameObject sliderGo = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        RectTransform sr = sliderGo.GetComponent<RectTransform>();
        Place(sr, new Vector2(45f, y - 4f), new Vector2(260f, 32f), new Vector2(0.5f, 1f));

        GameObject bg = CreateImage("Background", sliderGo.transform, new Color(0.04f, 0.15f, 0.20f, 1f)); Stretch(bg.GetComponent<RectTransform>());
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(sliderGo.transform, false); Stretch(fillArea.GetComponent<RectTransform>());
        fillArea.GetComponent<RectTransform>().offsetMin = new Vector2(4f, 7f); fillArea.GetComponent<RectTransform>().offsetMax = new Vector2(-4f, -7f);
        GameObject fill = CreateImage("Fill", fillArea.transform, new Color(0.18f, 0.88f, 0.98f, 1f)); Stretch(fill.GetComponent<RectTransform>());
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(sliderGo.transform, false); Stretch(handleArea.GetComponent<RectTransform>());
        handleArea.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 0f); handleArea.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, 0f);
        GameObject handle = CreateImage("Handle", handleArea.transform, new Color(0.75f, 0.98f, 1f, 1f));
        RectTransform hr = handle.GetComponent<RectTransform>(); hr.sizeDelta = new Vector2(16f, 34f);

        Slider slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = initial;
        slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = hr; slider.targetGraphic = handle.GetComponent<Image>();
        slider.onValueChanged.AddListener(changed);

        valueText = CreateText(label + "Value", parent, Mathf.RoundToInt(initial * 100f) + "%", 18f, TextAlignmentOptions.Right);
        Place(valueText.rectTransform, new Vector2(222f, y), new Vector2(90f, 40f), new Vector2(0.5f, 1f));
        TMP_Text captured = valueText;
        slider.onValueChanged.AddListener(v => captured.text = Mathf.RoundToInt(v * 100f) + "%");
        return slider;
    }

    private void SyncControls()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(GlobalAudioSettingsV14.MasterVolume);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(GlobalAudioSettingsV14.BgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GlobalAudioSettingsV14.SfxVolume);
        if (masterValue != null) masterValue.text = Mathf.RoundToInt(GlobalAudioSettingsV14.MasterVolume * 100f) + "%";
        if (bgmValue != null) bgmValue.text = Mathf.RoundToInt(GlobalAudioSettingsV14.BgmVolume * 100f) + "%";
        if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(GlobalAudioSettingsV14.SfxVolume * 100f) + "%";
        RefreshFullscreenText();
    }

    private void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        RefreshFullscreenText();
    }

    private void RefreshFullscreenText()
    {
        if (fullscreenLabel != null) fullscreenLabel.text = Screen.fullScreen ? "전체 화면 : 켜짐" : "전체 화면 : 꺼짐";
    }

    private void RestartGame()
    {
        ReleasePause();
        SceneManager.LoadScene("GameScene");
    }

    private void ReturnToMenu()
    {
        ReleasePause();
        SceneManager.LoadScene("MainMenu");
    }

    private GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return go;
    }

    private TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = alignment;
        text.color = Color.white; text.raycastTarget = false; text.enableWordWrapping = false; if (font != null) text.font = font; return text;
    }

    private Button CreateButton(string name, Transform parent, string value, out TMP_Text label)
    {
        GameObject go = CreateImage(name, parent, new Color(0.035f, 0.25f, 0.32f, 1f)); Button button = go.AddComponent<Button>();
        label = CreateText("Label", go.transform, value, 20f, TextAlignmentOptions.Center); Stretch(label.rectTransform);
        ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.75f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.55f, 0.90f, 0.95f, 1f); button.colors = colors; return button;
    }

    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    private static void Place(RectTransform rect, Vector2 pos, Vector2 size, Vector2 anchor) { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = pos; rect.sizeDelta = size; }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem_V14", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }
}
