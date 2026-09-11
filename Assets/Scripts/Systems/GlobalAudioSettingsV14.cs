using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// V14 global runtime audio settings. It keeps the existing PlayerPrefs keys used by the main menu,
/// but applies BGM/SFX multipliers to every active AudioSource in gameplay as well.
/// </summary>
[DefaultExecutionOrder(9000)]
public sealed class GlobalAudioSettingsV14 : MonoBehaviour
{
    private sealed class SourceState
    {
        public float baseVolume;
        public float lastAppliedVolume;
        public bool bgm;
        public bool initialized;
    }

    public const string MasterVolumeKey = "IA_MASTER_VOLUME";
    public const string BgmVolumeKey = "IA_BGM_VOLUME";
    public const string SfxVolumeKey = "IA_SFX_VOLUME";

    public static GlobalAudioSettingsV14 Instance { get; private set; }
    public static float MasterVolume { get; private set; } = 1f;
    public static float BgmVolume { get; private set; } = 0.7f;
    public static float SfxVolume { get; private set; } = 0.8f;

    private readonly Dictionary<AudioSource, SourceState> states = new Dictionary<AudioSource, SourceState>();
    private float nextScanAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GlobalAudioSettingsV14");
        DontDestroyOnLoad(go);
        go.AddComponent<GlobalAudioSettingsV14>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        states.Clear();
        nextScanAt = 0f;
        ApplyMaster();
    }

    private void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 0.7f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f));
        ApplyMaster();
    }

    private void LateUpdate()
    {
        ApplyMaster();
        if (Time.unscaledTime >= nextScanAt)
        {
            nextScanAt = Time.unscaledTime + 0.25f;
            ScanSources();
        }

        if (states.Count == 0) return;
        List<AudioSource> remove = null;
        foreach (KeyValuePair<AudioSource, SourceState> pair in states)
        {
            AudioSource source = pair.Key;
            SourceState state = pair.Value;
            if (source == null)
            {
                if (remove == null) remove = new List<AudioSource>();
                remove.Add(source);
                continue;
            }

            float current = source.volume;
            if (!state.initialized)
            {
                state.baseVolume = current;
                state.initialized = true;
            }
            else if (Mathf.Abs(current - state.lastAppliedVolume) > 0.0025f)
            {
                // A gameplay fade / loop controller changed the source. Treat that value as the new raw mix level.
                state.baseVolume = Mathf.Clamp01(current);
            }

            float multiplier = state.bgm ? BgmVolume : SfxVolume;
            float desired = Mathf.Clamp01(state.baseVolume * multiplier);
            if (Mathf.Abs(source.volume - desired) > 0.0005f)
                source.volume = desired;
            state.lastAppliedVolume = desired;
        }

        if (remove != null)
            for (int i = 0; i < remove.Count; i++) states.Remove(remove[i]);
    }

    private void ScanSources()
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || states.ContainsKey(source)) continue;
            states[source] = new SourceState
            {
                baseVolume = Mathf.Clamp01(source.volume),
                lastAppliedVolume = source.volume,
                bgm = IsBgmSource(source),
                initialized = true
            };
        }
    }

    private static bool IsBgmSource(AudioSource source)
    {
        if (source == null) return false;
        Transform cursor = source.transform;
        while (cursor != null)
        {
            string n = cursor.name.ToLowerInvariant();
            if (n.Contains("bgm") || n.Contains("music")) return true;
            cursor = cursor.parent;
        }

        string clip = source.clip != null ? source.clip.name.ToLowerInvariant() : string.Empty;
        return clip.Contains("bgm") || clip.Contains("music") || clip.Contains("boss1_phase") ||
               clip.Contains("meltdowncore") || clip.Contains("geigermeltdown") ||
               clip.Contains("meltdown core") || clip.Contains("geiger meltdown");
    }

    private static void ApplyMaster()
    {
        AudioListener.volume = Mathf.Clamp01(MasterVolume);
    }

    public static void SetMaster(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.Save();
        ApplyMaster();
    }

    public static void SetBgm(float value)
    {
        BgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        PlayerPrefs.Save();
    }

    public static void SetSfx(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.Save();
    }
}
