using System;
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

// Public entry points and serialized font/music references retained for existing scenes.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class InfectedAreaEndingCreditsAuto : MonoBehaviour
{
    [Serializable] public class VideoLane
    {
        public string file;
        public float x = -.37f, width = .22f, duration = 26f, delay, alpha = .50f, tilt = -3f;
    }
    [Serializable] public class Settings
    {
        public float logoTime = 46f, creditsTime = 49f, finalSeconds = 7f;
        public float fallbackDuration = 149.736f, volume = .85f;
        public float headingSize = 38f, labelSize = 24f, nameSize = 30f, rowGap = 18f, sectionGap = 80f;
        public string team = "KOD", finalText = "플레이해 주셔서 감사합니다.";
        public VideoLane[] videos;
    }
    [Header("Debug: T = full, Shift+T = logo / credits, Escape = stop")]
    [SerializeField] private bool enableTKeyDebug = true;
    [SerializeField] private bool showDebugButton;
    [Header("Existing scene references")]
    [SerializeField] private TMP_FontAsset koreanFont;
    [SerializeField] private AudioClip endingMusic;
    [SerializeField] private bool loadMainMenuAfterEnding;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Header("Optional resource overrides")]
    [SerializeField] private TextAsset creditsText;
    [SerializeField] private TextAsset settingsJson;
    [SerializeField] private Sprite gameLogo;

    private static InfectedAreaEndingCreditsAuto instance;
    private static int stoppedFrame = -1;
    public static bool CapturesInput => (instance != null && instance.playing) || stoppedFrame == Time.frameCount;
    private Settings cfg;
    private GameObject overlay;
    private RectTransform screen, roll, logo;
    private CanvasGroup logoGroup, rollGroup, finalGroup;
    private AudioSource music;
    private bool playing, focusPaused, applicationPaused;
    private double dspStart, pauseDSP;
    private float savedScale, offset, duration, rollHeight;
    private IDisposable inputToken;
    private readonly List<AudioState> audioStates = new List<AudioState>();
    private readonly List<Lane> lanes = new List<Lane>();
    private struct AudioState { public AudioSource source; public bool mute, wasPlaying; }
    private sealed class Lane
    {
        public VideoLane cfg;
        public RectTransform rect;
        public CanvasGroup group;
        public RawImage image;
        public VideoPlayer player;
        public RenderTexture target;
        public bool prepared, failed;
        public double prepareAt;
    }
    public bool IsPlaying => playing;
    public float MusicTime => playing ? offset + (float)((focusPaused ? pauseDSP : AudioSettings.dspTime) - dspStart) : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; stoppedFrame = -1; }
    private void Awake()
    {
        if (instance != null && instance != this) { enabled = false; return; }
        instance = this;
    }
    private void Update()
    {
        bool start = false, preview = false, stop = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (enableTKeyDebug)
        {
            start = Input.GetKeyDown(KeyCode.T);
            preview = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        stop = Input.GetKeyDown(KeyCode.Escape);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (enableTKeyDebug)
            {
                start |= Keyboard.current.tKey.wasPressedThisFrame;
                preview |= Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
            stop |= Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#endif
        if (start) { if (preview) PreviewCredits(); else PlayEndingCredits(); }
        if (!playing) return;
        if (stop) { StopEndingCredits(); return; }
        // Pause/audio-focus must not advance the visual timeline while music is suspended.
        bool shouldPause = applicationPaused || AudioListener.pause;
        SetClockPaused(shouldPause);
        if (focusPaused) return;
        float t = MusicTime;
        if (t >= duration)
        {
            bool go = loadMainMenuAfterEnding;
            StopEndingCredits();
            if (go) { KODBBootSplashController.SkipSplashForMenuReturn(); SceneManager.LoadScene(mainMenuSceneName); }
            return;
        }
        DrawTimeline(t);
    }
    private void OnGUI()
    {
        if (showDebugButton && GUI.Button(new Rect(20,20,260,45), "PREVIEW ENDING CREDITS")) PreviewCredits();
    }
    [ContextMenu("Play full ending (T)")]
    public void PlayEndingCredits() { if (!playing) Begin(false); }
    [ContextMenu("Preview logo and credits (Shift+T)")]
    public void PreviewCredits() { StopEndingCredits(); Begin(true); }
    private void LoadSettings()
    {
        cfg = new Settings();
        TextAsset data = settingsJson != null ? settingsJson : Resources.Load<TextAsset>("EndingCredits/EndingCreditsSettings");
        try { if (data != null) JsonUtility.FromJsonOverwrite(data.text, cfg); }
        catch (Exception e) { Debug.LogWarning("[Credits] Invalid settings: " + e.Message); }
        duration = endingMusic != null ? endingMusic.length : Mathf.Max(30, cfg.fallbackDuration);
        cfg.finalSeconds = Mathf.Clamp(cfg.finalSeconds, 3, duration * .2f);
        cfg.logoTime = Mathf.Clamp(cfg.logoTime, 0, duration - cfg.finalSeconds - 12);
        cfg.creditsTime = Mathf.Clamp(cfg.creditsTime, cfg.logoTime + 2, duration - cfg.finalSeconds - 5);
    }
    private void Begin(bool preview)
    {
        LoadSettings();
        if (koreanFont == null) koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        if (gameLogo == null) gameLogo = Resources.Load<Sprite>("EndingCredits/GameLogo");
        if (creditsText == null) creditsText = Resources.Load<TextAsset>("EndingCredits/EndingCreditsText");
        if (koreanFont == null || creditsText == null)
        { Debug.LogError("[Credits] Korean font or credits text missing. Run Tools/Infected Area/Configure KOD Credits."); return; }
        savedScale = Time.timeScale;
        playing = true;
        inputToken = GameInputState.Acquire("KODEndingCredits");
        Time.timeScale = 0;
        try
        {
            BuildUI();
            audioStates.Clear();
            foreach (AudioSource src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                audioStates.Add(new AudioState { source = src, mute = src.mute, wasPlaying = src.isPlaying });
                if (src.isPlaying) src.Pause();
                src.mute = true;
            }
            music = overlay.AddComponent<AudioSource>();
            music.playOnAwake = false; music.loop = false; music.spatialBlend = 0;
            music.volume = Mathf.Clamp01(cfg.volume); music.clip = endingMusic;
            offset = preview ? cfg.logoTime : 0;
            dspStart = AudioSettings.dspTime + .15;
            if (endingMusic != null) { music.time = offset; music.PlayScheduled(dspStart); }
            else Debug.LogWarning("[Credits] Ending music missing: using silent timeline.");
            DrawTimeline(offset - .15f);
        }
        catch (Exception e) { Debug.LogException(e); StopEndingCredits(); }
    }
    private void BuildUI()
    {
        overlay = new GameObject("KOD_Credits", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32750;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = 1;
        screen = overlay.GetComponent<RectTransform>();
        Image black = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        black.transform.SetParent(screen, false); Stretch(black.rectTransform); black.color = Color.black; black.raycastTarget = true;
        // Keep every gallery behind the text. The center is an opaque reading column.
        RectTransform gallery = Rect("Development films", screen, Vector2.zero, Vector2.zero); Stretch(gallery);
        if (cfg.videos != null)
        {
            var ordered = new List<VideoLane>();
            foreach (VideoLane spec in cfg.videos) if (spec != null) ordered.Add(spec);
            ordered.Sort((a,b) => a.width.CompareTo(b.width));
            foreach (VideoLane spec in ordered) if (!string.IsNullOrWhiteSpace(spec.file)) BuildLane(gallery, spec);
        }
        Image shade = new GameObject("Reading column", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        shade.transform.SetParent(screen, false); shade.color = Color.black; shade.raycastTarget = false;
        shade.rectTransform.anchorMin = new Vector2(.25f,0); shade.rectTransform.anchorMax = new Vector2(.75f,1); shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        RectTransform viewport = Rect("Credits viewport", screen, Vector2.zero, Vector2.zero);
        viewport.anchorMin = new Vector2(.25f,.09f); viewport.anchorMax = new Vector2(.75f,.91f); viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();
        rollGroup = viewport.gameObject.AddComponent<CanvasGroup>();
        roll = Rect("Editable credits", viewport, new Vector2(900,1), Vector2.zero);
        roll.anchorMin = roll.anchorMax = new Vector2(.5f,0); roll.pivot = new Vector2(.5f,1);
        BuildText();
        logo = Rect("Game logo", screen, new Vector2(1120,520), Vector2.zero);
        logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
        if (gameLogo != null) { Image im = logo.gameObject.AddComponent<Image>(); im.sprite = gameLogo; im.preserveAspect = true; im.raycastTarget = false; }
        else Text("INFECTED AREA", logo, 72, new Vector2(1100,200), Vector2.zero);
        RectTransform thanks = Rect("Final thanks", screen, new Vector2(1000,300), Vector2.zero);
        finalGroup = thanks.gameObject.AddComponent<CanvasGroup>();
        Text(cfg.finalText, thanks, 42, new Vector2(1000,130), new Vector2(0,35));
        Text(cfg.team, thanks, 28, new Vector2(1000,80), new Vector2(0,-65));
        Canvas.ForceUpdateCanvases();
    }
    private void BuildText()
    {
        float y = 0;
        foreach (string raw in creditsText.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            if (line.StartsWith("#"))
            {
                y += y > 0 ? cfg.sectionGap : 20;
                Text(line.TrimStart('#').Trim(), roll, cfg.headingSize, new Vector2(880,66), new Vector2(0,-y-33), true);
                y += 98; continue;
            }
            string[] fields = line.Split('|');
            string label = fields[0].Trim(), names = fields.Length > 1 ? fields[1].Trim() : "";
            if (names == "미정" || names == "없음") continue;
            float labelHeight = 0;
            if (names.Length > 0)
            {
                TMP_Text role = Text(label, roll, cfg.labelSize, new Vector2(850,48), new Vector2(0,-y-24), true);
                labelHeight = Mathf.Max(38, role.GetPreferredValues(label,850,0).y + 6);
                role.rectTransform.sizeDelta = new Vector2(850,labelHeight);
                role.rectTransform.anchoredPosition = new Vector2(0,-y-labelHeight/2);
            }
            string value = names.Length > 0 ? names : label;
            TMP_Text person = Text(value, roll, cfg.nameSize, new Vector2(860,50), new Vector2(0,-y-labelHeight-25), true);
            float nameHeight = Mathf.Max(44,person.GetPreferredValues(value,860,0).y+8);
            person.rectTransform.sizeDelta = new Vector2(860,nameHeight);
            person.rectTransform.anchoredPosition = new Vector2(0,-y-labelHeight-nameHeight/2);
            y += labelHeight + nameHeight + Mathf.Max(0,cfg.rowGap);
        }
        rollHeight = y + 40; roll.sizeDelta = new Vector2(900,rollHeight);
    }
    private TMP_Text Text(string value, Transform parent, float size, Vector2 box, Vector2 position, bool top = false)
    {
        RectTransform rt = Rect("Text",parent,box,position);
        if (top) rt.anchorMin = rt.anchorMax = new Vector2(.5f,1);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = koreanFont; t.fontSize = Mathf.Clamp(size,16,100); t.text = value; t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center; t.richText = false; t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
    private void BuildLane(Transform parent, VideoLane spec)
    {
        string root = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath,"EndingCredits/Videos"));
        string path = Path.GetFullPath(Path.Combine(root,spec.file));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar,StringComparison.Ordinal) || !File.Exists(path))
        { Debug.LogWarning("[Credits] Video not found: " + spec.file); return; }
        Lane lane = new Lane { cfg = spec, prepareAt = Time.realtimeSinceStartupAsDouble };
        lane.rect = Rect("Film " + spec.file,parent,new Vector2(400,225),Vector2.zero);
        lane.group = lane.rect.gameObject.AddComponent<CanvasGroup>(); lane.group.alpha = 0;
        lane.rect.localRotation = Quaternion.Euler(0,0,spec.tilt);
        lane.image = lane.rect.gameObject.AddComponent<RawImage>(); lane.image.raycastTarget = false;
        lane.player = lane.rect.gameObject.AddComponent<VideoPlayer>();
        lane.player.playOnAwake = false; lane.player.isLooping = true; lane.player.skipOnDrop = true;
        lane.player.audioOutputMode = VideoAudioOutputMode.None;
        lane.player.renderMode = VideoRenderMode.RenderTexture; lane.player.source = VideoSource.Url;
        lane.player.url = new Uri(path).AbsoluteUri;
        lane.player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        lane.player.prepareCompleted += p =>
        {
            if (!playing || lane.failed) return;
            float ratio = p.height > 0 ? (float)p.width/p.height : 16f/9;
            int h = Mathf.Clamp(Mathf.RoundToInt(640f / Mathf.Max(.25f,ratio)),128,720);
            lane.target = new RenderTexture(640,h,0,RenderTextureFormat.ARGB32);
            lane.target.Create(); p.targetTexture = lane.target; lane.image.texture = lane.target; lane.prepared = true;
        };
        lane.player.errorReceived += (p,message) => { lane.failed = true; lane.group.alpha = 0; p.Stop(); Debug.LogWarning("[Credits] " + spec.file + ": " + message); };
        lanes.Add(lane); lane.player.Prepare();
    }
    private void DrawTimeline(float t)
    {
        float finalAt = duration - cfg.finalSeconds;
        logoGroup.alpha = t < cfg.logoTime ? 0 : Mathf.Clamp01((t-cfg.logoTime)/.2f);
        rollGroup.alpha = Mathf.Clamp01((t-cfg.creditsTime)/1f) * Mathf.Clamp01((finalAt-t)/1f);
        float u = Mathf.InverseLerp(cfg.creditsTime,finalAt,t);
        // Scale column to narrower windows without clipping names.
        float width = screen.rect.width;
        float scale = Mathf.Min(1,(width*.50f-24)/900f);
        roll.localScale = Vector3.one * Mathf.Max(.25f,scale);
        float viewportHeight = screen.rect.height*.82f;
        roll.anchoredPosition = new Vector2(0,Mathf.Lerp(-20,rollHeight*scale+viewportHeight+20,u));
        // Keep the logo ahead of the credits with the exact same vertical displacement.
        logo.anchoredPosition = new Vector2(0,roll.anchoredPosition.y + 20);
        finalGroup.alpha = Mathf.Clamp01((t-finalAt)/1.4f)*Mathf.Clamp01((duration-t)/1.2f);
        logoGroup.transform.localScale = Vector3.one * Mathf.Min(1,width/1400);
        finalGroup.transform.localScale = Vector3.one * Mathf.Min(1,width/1100);
        if (music != null) music.volume = Mathf.Clamp01(cfg.volume) * Mathf.Clamp01((duration-t)/3);
        foreach (Lane lane in lanes)
        {
            if (!lane.prepared || lane.failed)
            {
                if (!lane.failed && Time.realtimeSinceStartupAsDouble-lane.prepareAt > 15)
                { lane.failed = true; lane.player.Stop(); Debug.LogWarning("[Credits] Video prepare timeout: " + lane.cfg.file); }
                continue;
            }
            float local = t-cfg.creditsTime-Mathf.Max(0,lane.cfg.delay);
            bool visible = local >= 0 && t < finalAt;
            if (visible && !lane.player.isPlaying) lane.player.Play();
            if (!visible && lane.player.isPlaying) lane.player.Pause();
            float q = Mathf.Repeat(Mathf.Max(0,local),Mathf.Max(8,lane.cfg.duration))/Mathf.Max(8,lane.cfg.duration);
            float w = width*Mathf.Clamp(lane.cfg.width,.10f,.27f);
            float aspect = lane.player.height > 0 ? (float)lane.player.width/lane.player.height : 16f/9;
            float h = w/Mathf.Max(.25f,aspect);
            lane.rect.sizeDelta = new Vector2(w,h);
            lane.rect.anchoredPosition = new Vector2(width*Mathf.Clamp(lane.cfg.x,-.48f,.48f), Mathf.Lerp(-540-h,540+h,q));
            lane.group.alpha = visible ? Mathf.Clamp01(lane.cfg.alpha)*Mathf.Clamp01(q*8)*Mathf.Clamp01((1-q)*8)*Mathf.Clamp01((finalAt-t)/2) : 0;
        }
    }
    private void SetClockPaused(bool pause)
    {
        if (!playing || focusPaused == pause) return;
        focusPaused = pause;
        if (pause)
        {
            pauseDSP = AudioSettings.dspTime;
            if (music != null) music.Pause();
            foreach (Lane l in lanes) if (l.player != null) l.player.Pause();
        }
        else
        {
            dspStart += AudioSettings.dspTime-pauseDSP;
            if (music != null) music.UnPause();
        }
    }
    private void OnApplicationPause(bool pause) { applicationPaused = pause; SetClockPaused(pause || AudioListener.pause); }
    [ContextMenu("Stop ending")]
    public void StopEndingCredits()
    {
        if (!playing) return;
        playing = false; focusPaused = false; stoppedFrame = Time.frameCount;
        if (music != null) music.Stop();
        foreach (Lane lane in lanes)
        {
            lane.failed = true;
            if (lane.player != null) { lane.player.Stop(); lane.player.targetTexture = null; }
            if (lane.target != null) { lane.target.Release(); Destroy(lane.target); }
        }
        lanes.Clear();
        foreach (AudioState state in audioStates)
            if (state.source != null) { state.source.mute = state.mute; if (state.wasPlaying) state.source.UnPause(); }
        audioStates.Clear();
        if (inputToken != null) { inputToken.Dispose(); inputToken = null; }
        Time.timeScale = savedScale;
        if (overlay != null) { overlay.SetActive(false); Destroy(overlay); }
    }
    private void OnDisable() { StopEndingCredits(); }
    private void OnDestroy() { StopEndingCredits(); if (instance == this) instance = null; }
    private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
    {
        RectTransform rt = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent,false); rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f); rt.sizeDelta = size; rt.anchoredPosition = position; return rt;
    }
    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
}
