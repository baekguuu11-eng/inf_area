using System.Collections;
using UnityEngine;

/// <summary>
/// Unifies the music flow from a normal room -> portal room -> boss intro -> boss combat.
/// The gameplay BGM is ducked in the portal room, fades fully silent while entering the
/// boss room, remains silent during the entrance cutscene, and is restored after the boss.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossMusicTransitionBridge : MonoBehaviour
{
    private const float DefaultRestoreVolume = 0.70f;
    private const float PortalDuckRatio = 0.55f;

    private static BossMusicTransitionBridge instance;

    private AudioSource gameplaySource;
    private Coroutine fadeRoutine;
    private float restoreVolume = DefaultRestoreVolume;
    private bool restoreCaptured;

    public static float RestoreVolume
    {
        get
        {
            BossMusicTransitionBridge bridge = Ensure();
            return bridge != null ? bridge.restoreVolume : DefaultRestoreVolume;
        }
    }

    public static AudioSource GameplaySource
    {
        get
        {
            BossMusicTransitionBridge bridge = Ensure();
            return bridge != null ? bridge.gameplaySource : null;
        }
    }

    public static BossMusicTransitionBridge Ensure(AudioSource preferredSource = null)
    {
        if (instance == null)
        {
            BossMusicTransitionBridge[] existing = UnityEngine.Object.FindObjectsByType<BossMusicTransitionBridge>(FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
                instance = existing[0];
        }

        if (instance == null)
        {
            AudioSource source = preferredSource != null ? preferredSource : FindGameplaySource();
            GameObject host = source != null ? source.gameObject : new GameObject("BossMusicTransitionBridge");
            instance = host.GetComponent<BossMusicTransitionBridge>();
            if (instance == null)
                instance = host.AddComponent<BossMusicTransitionBridge>();
            instance.gameplaySource = source;
        }
        else if (preferredSource != null)
        {
            instance.gameplaySource = preferredSource;
        }

        instance.ResolveGameplaySource();
        instance.CaptureRestoreVolumeIfNeeded();
        return instance;
    }

    public static void RegisterGameplaySource(AudioSource source)
    {
        if (source == null) return;
        BossMusicTransitionBridge bridge = Ensure(source);
        if (bridge == null) return;
        bridge.gameplaySource = source;
        bridge.CaptureRestoreVolumeIfNeeded();
    }

    public static void BeginPortalApproach(float duration = 0.95f)
    {
        BossMusicTransitionBridge bridge = Ensure();
        if (bridge == null || bridge.gameplaySource == null) return;
        bridge.CaptureRestoreVolumeIfNeeded();
        float duckTarget = bridge.restoreVolume * PortalDuckRatio;
        // Never make a currently silent/quiet source louder just because a debug jump entered a portal room.
        duckTarget = Mathf.Min(bridge.gameplaySource.volume, duckTarget);
        bridge.StartFade(duckTarget, duration);
    }

    public static void BeginBossTransition(float duration = 0.75f)
    {
        BossMusicTransitionBridge bridge = Ensure();
        if (bridge == null || bridge.gameplaySource == null) return;
        bridge.CaptureRestoreVolumeIfNeeded();
        bridge.StartFade(0f, duration);
    }

    public static void ForceCutsceneSilence()
    {
        BossMusicTransitionBridge bridge = Ensure();
        if (bridge == null || bridge.gameplaySource == null) return;
        bridge.CaptureRestoreVolumeIfNeeded();
        bridge.StopFade();
        bridge.gameplaySource.mute = false;
        bridge.gameplaySource.volume = 0f;
    }

    public static void CancelActiveFade()
    {
        BossMusicTransitionBridge bridge = Ensure();
        if (bridge != null) bridge.StopFade();
    }

    public static void RestoreGameplay(float duration = 1.75f)
    {
        BossMusicTransitionBridge bridge = Ensure();
        if (bridge == null || bridge.gameplaySource == null) return;

        AudioSource source = bridge.gameplaySource;
        source.mute = false;
        source.loop = true;
        if (source.clip != null && !source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        bridge.StartFade(bridge.restoreVolume, duration);
    }

    public static void RestoreGameplayImmediate()
    {
        BossMusicTransitionBridge bridge = Ensure();
        if (bridge == null || bridge.gameplaySource == null) return;
        bridge.StopFade();
        bridge.gameplaySource.mute = false;
        if (bridge.gameplaySource.clip != null && !bridge.gameplaySource.isPlaying)
            bridge.gameplaySource.Play();
        bridge.gameplaySource.volume = bridge.restoreVolume;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        ResolveGameplaySource();
        CaptureRestoreVolumeIfNeeded();
    }

    private static AudioSource FindGameplaySource()
    {
        GameObject named = GameObject.Find("GameplayBgmAudioSource");
        if (named != null)
        {
            AudioSource direct = named.GetComponent<AudioSource>();
            if (direct != null) return direct;
        }

        AudioSource[] all = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            AudioSource candidate = all[i];
            if (candidate == null) continue;
            string objectName = candidate.gameObject.name.ToLowerInvariant();
            string clipName = candidate.clip != null ? candidate.clip.name.ToLowerInvariant() : string.Empty;
            if (objectName.Contains("gameplaybgm") ||
                (objectName.Contains("bgm") && !objectName.Contains("boss")) ||
                (clipName.Contains("gameplay") && clipName.Contains("bgm")))
                return candidate;
        }
        return null;
    }

    private void ResolveGameplaySource()
    {
        if (gameplaySource == null)
            gameplaySource = FindGameplaySource();
    }

    private void CaptureRestoreVolumeIfNeeded()
    {
        if (gameplaySource == null) return;

        // Only capture from a normal-volume state. This deliberately ignores the already
        // ducked portal-room value so every boss restores to the original gameplay mix.
        if (!restoreCaptured)
        {
            if (gameplaySource.volume > 0.10f)
            {
                restoreVolume = Mathf.Clamp01(gameplaySource.volume);
                restoreCaptured = true;
            }
            else
            {
                restoreVolume = DefaultRestoreVolume;
            }
        }
        else if (gameplaySource.volume > restoreVolume * 0.92f && gameplaySource.volume > 0.10f)
        {
            restoreVolume = Mathf.Clamp01(gameplaySource.volume);
        }

        if (restoreVolume <= 0.01f)
            restoreVolume = DefaultRestoreVolume;
    }

    private void StartFade(float targetVolume, float duration)
    {
        StopFade();
        fadeRoutine = StartCoroutine(FadeRoutine(Mathf.Clamp01(targetVolume), Mathf.Max(0.05f, duration)));
    }

    private void StopFade()
    {
        if (fadeRoutine == null) return;
        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private IEnumerator FadeRoutine(float targetVolume, float duration)
    {
        if (gameplaySource == null) yield break;
        float start = gameplaySource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            gameplaySource.volume = Mathf.Lerp(start, targetVolume, smooth);
            yield return null;
        }
        gameplaySource.volume = targetVolume;
        fadeRoutine = null;
    }
}
