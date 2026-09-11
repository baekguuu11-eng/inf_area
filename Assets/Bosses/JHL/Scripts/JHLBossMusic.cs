using System.Collections;
using UnityEngine;

/// <summary>
/// JHL music flow:
/// Phase 1 -> JHL_PHASE1 (Infected Server)
/// Phase 2 -> JHL_PHASE2 (Iron Throne Protocol)
/// Phase 3 -> continues JHL_PHASE2 without restarting it, only raises the mix slightly.
/// The entrance cutscene is always silent.
/// </summary>
[DisallowMultipleComponent]
public sealed class JHLBossMusic : MonoBehaviour
{
    private AudioSource phaseOneSource;
    private AudioSource phaseTwoPlusSource;
    private AudioSource gameplayBgmSource;
    private Coroutine fadeRoutine;
    private float gameplayRestoreVolume = 0.70f;

    private const float PhaseOneVolume = 0.70f;
    private const float PhaseTwoVolume = 0.74f;
    private const float PhaseThreeVolume = 0.80f;

    public static JHLBossMusic Create(Transform parent)
    {
        GameObject root = new GameObject("JHLBossMusic");
        root.transform.SetParent(parent, false);
        JHLBossMusic music = root.AddComponent<JHLBossMusic>();
        music.Build();
        return music;
    }

    private void Build()
    {
        phaseOneSource = gameObject.AddComponent<AudioSource>();
        phaseTwoPlusSource = gameObject.AddComponent<AudioSource>();

        Configure(phaseOneSource, Resources.Load<AudioClip>("Bosses/JHL/JHL_PHASE1"));
        Configure(phaseTwoPlusSource, Resources.Load<AudioClip>("Bosses/JHL/JHL_PHASE2"));

        phaseOneSource.volume = 0f;
        phaseTwoPlusSource.volume = 0f;
        ResolveGameplayBgm();
    }

    private static void Configure(AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    private void ResolveGameplayBgm()
    {
        if (gameplayBgmSource == null)
        {
            GameObject named = GameObject.Find("GameplayBgmAudioSource");
            if (named != null)
                gameplayBgmSource = named.GetComponent<AudioSource>();
        }

        if (gameplayBgmSource == null)
        {
            AudioSource[] all = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                AudioSource candidate = all[i];
                if (candidate == null || candidate.transform.IsChildOf(transform)) continue;
                string objectName = candidate.gameObject.name.ToLowerInvariant();
                string clipName = candidate.clip != null ? candidate.clip.name.ToLowerInvariant() : string.Empty;
                if (objectName.Contains("gameplaybgm") ||
                    (objectName.Contains("bgm") && !objectName.Contains("boss")) ||
                    (clipName.Contains("gameplay") && clipName.Contains("bgm")))
                {
                    gameplayBgmSource = candidate;
                    break;
                }
            }
        }

        if (gameplayBgmSource != null)
        {
            BossMusicTransitionBridge.RegisterGameplaySource(gameplayBgmSource);
            gameplayRestoreVolume = BossMusicTransitionBridge.RestoreVolume;
        }
        if (gameplayRestoreVolume <= 0.01f)
            gameplayRestoreVolume = 0.70f;
    }

    /// <summary>Called before the entrance cinematic. No BGM is audible during the cutscene.</summary>
    public void PrepareIntro()
    {
        ResolveGameplayBgm();
        gameplayRestoreVolume = BossMusicTransitionBridge.RestoreVolume;
        BossMusicTransitionBridge.ForceCutsceneSilence();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        StopAndZero(phaseOneSource);
        StopAndZero(phaseTwoPlusSource);
    }

    public void PlayPhase(int phase, float duration = 0.65f)
    {
        ResolveGameplayBgm();
        BossMusicTransitionBridge.ForceCutsceneSilence();

        int clamped = Mathf.Clamp(phase, 1, 3);
        if (clamped == 1)
        {
            if (phaseOneSource == null || phaseOneSource.clip == null) return;
            if (!phaseOneSource.isPlaying)
            {
                phaseOneSource.time = 0f;
                phaseOneSource.Play();
            }
            StartBossMix(phaseOneSource, PhaseOneVolume, phaseTwoPlusSource, 0f, duration, true);
            return;
        }

        if (phaseTwoPlusSource == null || phaseTwoPlusSource.clip == null)
        {
            // If only the first song exists, keep it rather than creating silence.
            if (phaseOneSource != null && phaseOneSource.clip != null)
            {
                if (!phaseOneSource.isPlaying)
                {
                    phaseOneSource.time = 0f;
                    phaseOneSource.Play();
                }
                StartBossMix(phaseOneSource, PhaseOneVolume, phaseTwoPlusSource, 0f, duration, false);
            }
            return;
        }

        // Phase 2 starts Iron Throne Protocol from the beginning.
        // Phase 3 intentionally does NOT restart it: the same source continues seamlessly.
        if (!phaseTwoPlusSource.isPlaying)
        {
            phaseTwoPlusSource.time = 0f;
            phaseTwoPlusSource.Play();
        }

        float target = clamped >= 3 ? PhaseThreeVolume : PhaseTwoVolume;
        StartBossMix(phaseTwoPlusSource, target, phaseOneSource, 0f, duration, true);
    }

    public void CrossFadeBackToGameplay(float duration = 1.75f)
    {
        ResolveGameplayBgm();
        BossMusicTransitionBridge.CancelActiveFade();
        gameplayRestoreVolume = BossMusicTransitionBridge.RestoreVolume;

        if (gameplayBgmSource != null)
        {
            gameplayBgmSource.mute = false;
            gameplayBgmSource.loop = true;
            if (gameplayBgmSource.clip != null && !gameplayBgmSource.isPlaying)
            {
                gameplayBgmSource.volume = 0f;
                gameplayBgmSource.Play();
            }
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeBack(Mathf.Max(0.05f, duration)));
    }

    public void RestoreGameplayImmediate()
    {
        StopAndZero(phaseOneSource);
        StopAndZero(phaseTwoPlusSource);
        BossMusicTransitionBridge.RestoreGameplayImmediate();
    }

    private void StartBossMix(AudioSource fadeIn, float fadeInTarget, AudioSource fadeOut,
        float fadeOutTarget, float duration, bool stopFadeOut)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossFade(fadeIn, fadeInTarget, fadeOut, fadeOutTarget,
            Mathf.Max(0.05f, duration), stopFadeOut));
    }

    private IEnumerator CrossFade(AudioSource fadeIn, float fadeInTarget, AudioSource fadeOut,
        float fadeOutTarget, float duration, bool stopFadeOut)
    {
        float inStart = fadeIn != null ? fadeIn.volume : 0f;
        float outStart = fadeOut != null ? fadeOut.volume : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            if (fadeIn != null) fadeIn.volume = Mathf.Lerp(inStart, fadeInTarget, smooth);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(outStart, fadeOutTarget, smooth);
            if (gameplayBgmSource != null) gameplayBgmSource.volume = 0f;
            yield return null;
        }

        if (fadeIn != null) fadeIn.volume = fadeInTarget;
        if (fadeOut != null)
        {
            fadeOut.volume = fadeOutTarget;
            if (stopFadeOut && fadeOutTarget <= 0.001f) fadeOut.Stop();
        }
        if (gameplayBgmSource != null) gameplayBgmSource.volume = 0f;
        fadeRoutine = null;
    }

    private IEnumerator FadeBack(float duration)
    {
        float phaseOneStart = phaseOneSource != null ? phaseOneSource.volume : 0f;
        float phaseTwoStart = phaseTwoPlusSource != null ? phaseTwoPlusSource.volume : 0f;
        float gameplayStart = gameplayBgmSource != null ? gameplayBgmSource.volume : 0f;
        float gameplayTarget = Mathf.Clamp01(gameplayRestoreVolume > 0.01f ? gameplayRestoreVolume : 0.70f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            if (phaseOneSource != null) phaseOneSource.volume = Mathf.Lerp(phaseOneStart, 0f, smooth);
            if (phaseTwoPlusSource != null) phaseTwoPlusSource.volume = Mathf.Lerp(phaseTwoStart, 0f, smooth);
            if (gameplayBgmSource != null) gameplayBgmSource.volume = Mathf.Lerp(gameplayStart, gameplayTarget, smooth);
            yield return null;
        }

        StopAndZero(phaseOneSource);
        StopAndZero(phaseTwoPlusSource);
        if (gameplayBgmSource != null)
        {
            gameplayBgmSource.mute = false;
            gameplayBgmSource.volume = gameplayTarget;
        }
        fadeRoutine = null;
    }

    private static void StopAndZero(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }
}
