using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExecutorBossMusic : MonoBehaviour
{
    private AudioSource phaseOneSource;
    private AudioSource phaseTwoSource;
    private AudioSource gameplayBgmSource;
    private float gameplayBgmRestoreVolume = 0.70f;
    private Coroutine fadeRoutine;
    private Coroutine ambientFadeRoutine;
    private const float PhaseOneVolume = 0.68f;
    private const float PhaseTwoVolume = 0.73f;

    public static ExecutorBossMusic Create(Transform parent)
    {
        GameObject root = new GameObject("ExecutorBossMusic");
        root.transform.SetParent(parent, false);
        ExecutorBossMusic music = root.AddComponent<ExecutorBossMusic>();
        music.BuildSources();
        return music;
    }

    private void BuildSources()
    {
        phaseOneSource = gameObject.AddComponent<AudioSource>();
        phaseTwoSource = gameObject.AddComponent<AudioSource>();
        Configure(phaseOneSource, Resources.Load<AudioClip>("Bosses/Executor/BOSS1_PHASE1"));
        Configure(phaseTwoSource, Resources.Load<AudioClip>("Bosses/Executor/BOSS1_PHASE2"));
        phaseOneSource.volume = 0f;
        phaseTwoSource.volume = 0f;

        ResolveGameplayBgmSource();
    }

    private void ResolveGameplayBgmSource()
    {
        if (gameplayBgmSource != null)
            return;

        GameObject gameplayBgmObject = GameObject.Find("GameplayBgmAudioSource");
        if (gameplayBgmObject != null)
            gameplayBgmSource = gameplayBgmObject.GetComponent<AudioSource>();

        if (gameplayBgmSource == null)
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource candidate = sources[i];
                if (candidate == null) continue;
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

        if (gameplayBgmSource != null && gameplayBgmSource.volume > 0.01f)
            gameplayBgmRestoreVolume = gameplayBgmSource.volume;
        if (gameplayBgmRestoreVolume <= 0.01f)
            gameplayBgmRestoreVolume = 0.70f;
    }

    private static void Configure(AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    public void PrepareSilentIntro()
    {
        ResolveGameplayBgmSource();
        if (gameplayBgmSource != null && gameplayBgmSource.volume > 0.01f)
            gameplayBgmRestoreVolume = gameplayBgmSource.volume;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        if (ambientFadeRoutine != null)
        {
            StopCoroutine(ambientFadeRoutine);
            ambientFadeRoutine = null;
        }
        if (phaseOneSource != null)
        {
            phaseOneSource.Stop();
            phaseOneSource.volume = 0f;
        }
        if (phaseTwoSource != null)
        {
            phaseTwoSource.Stop();
            phaseTwoSource.volume = 0f;
        }
        if (gameplayBgmSource != null)
            gameplayBgmSource.volume = 0f;
    }

    public void PlayPhaseOne(float fadeDuration = 0.45f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (phaseOneSource != null && phaseOneSource.clip != null)
        {
            phaseOneSource.time = 0f;
            phaseOneSource.Play();
        }
        if (phaseTwoSource != null) phaseTwoSource.Stop();
        fadeRoutine = StartCoroutine(CrossFade(phaseOneSource, PhaseOneVolume, phaseTwoSource, 0f, fadeDuration, false));
        FadeGameplayBgm(0f, fadeDuration);
    }

    public void PlayPhaseTwo(float fadeDuration = 0.55f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (phaseTwoSource != null && phaseTwoSource.clip != null)
        {
            phaseTwoSource.time = 0f;
            phaseTwoSource.Play();
        }
        fadeRoutine = StartCoroutine(CrossFade(phaseTwoSource, PhaseTwoVolume, phaseOneSource, 0f, fadeDuration, true));
    }

    /// <summary>
    /// 플레이어 패배 화면에서는 보스 음악을 끊지 않고 약간만 낮춘다.
    /// 장면 재로드/메뉴 이동 시 오브젝트가 파괴되며 자연스럽게 종료된다.
    /// </summary>
    public void SetDefeatMix(float normalizedVolume = 0.88f, float duration = 0.35f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        float p1Target = phaseOneSource != null && phaseOneSource.isPlaying ? PhaseOneVolume * Mathf.Clamp01(normalizedVolume) : 0f;
        float p2Target = phaseTwoSource != null && phaseTwoSource.isPlaying ? PhaseTwoVolume * Mathf.Clamp01(normalizedVolume) : 0f;
        fadeRoutine = StartCoroutine(CrossFade(phaseOneSource, p1Target, phaseTwoSource, p2Target, duration, false));
    }

    public void FadeOut(float duration = 1f)
    {
        CrossFadeBackToGameplay(duration);
    }

    /// <summary>
    /// 보스 음악은 자연스럽게 페이드아웃하고 기존 게임 BGM은 동시에 페이드인한다.
    /// 기존 BGM이 정지되어 있더라도 클립이 있으면 다시 재생한다.
    /// </summary>
    public void CrossFadeBackToGameplay(float duration = 1.75f)
    {
        ResolveGameplayBgmSource();
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        if (ambientFadeRoutine != null)
        {
            StopCoroutine(ambientFadeRoutine);
            ambientFadeRoutine = null;
        }

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

        fadeRoutine = StartCoroutine(CrossFadeBackRoutine(Mathf.Max(0.05f, duration)));
    }

    private IEnumerator CrossFadeBackRoutine(float duration)
    {
        float phaseOneStart = phaseOneSource != null ? phaseOneSource.volume : 0f;
        float phaseTwoStart = phaseTwoSource != null ? phaseTwoSource.volume : 0f;
        float gameplayStart = gameplayBgmSource != null ? gameplayBgmSource.volume : 0f;
        float gameplayTarget = Mathf.Clamp01(gameplayBgmRestoreVolume > 0.01f ? gameplayBgmRestoreVolume : 0.70f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);
            // 보스 음악은 부드럽게 빠지고 기존 BGM은 같은 시간축에서 올라온다.
            if (phaseOneSource != null) phaseOneSource.volume = Mathf.Lerp(phaseOneStart, 0f, smooth);
            if (phaseTwoSource != null) phaseTwoSource.volume = Mathf.Lerp(phaseTwoStart, 0f, smooth);
            if (gameplayBgmSource != null) gameplayBgmSource.volume = Mathf.Lerp(gameplayStart, gameplayTarget, smooth);
            yield return null;
        }

        if (phaseOneSource != null)
        {
            phaseOneSource.volume = 0f;
            phaseOneSource.Stop();
        }
        if (phaseTwoSource != null)
        {
            phaseTwoSource.volume = 0f;
            phaseTwoSource.Stop();
        }
        if (gameplayBgmSource != null)
        {
            gameplayBgmSource.mute = false;
            gameplayBgmSource.volume = gameplayTarget;
        }
        fadeRoutine = null;
    }

    private void FadeGameplayBgm(float targetVolume, float duration)
    {
        if (gameplayBgmSource == null)
            return;
        if (ambientFadeRoutine != null)
            StopCoroutine(ambientFadeRoutine);
        ambientFadeRoutine = StartCoroutine(FadeSource(gameplayBgmSource, targetVolume, duration));
    }

    private IEnumerator FadeSource(AudioSource source, float targetVolume, float duration)
    {
        if (source == null) yield break;
        float start = source.volume;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, targetVolume, Mathf.Clamp01(elapsed / safeDuration));
            yield return null;
        }
        source.volume = targetVolume;
        ambientFadeRoutine = null;
    }

    private IEnumerator CrossFade(AudioSource fadeIn, float fadeInTarget, AudioSource fadeOut,
        float fadeOutTarget, float duration, bool stopFadeOut)
    {
        float inStart = fadeIn != null ? fadeIn.volume : 0f;
        float outStart = fadeOut != null ? fadeOut.volume : 0f;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            if (fadeIn != null) fadeIn.volume = Mathf.Lerp(inStart, fadeInTarget, t);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(outStart, fadeOutTarget, t);
            yield return null;
        }
        if (fadeIn != null) fadeIn.volume = fadeInTarget;
        if (fadeOut != null)
        {
            fadeOut.volume = fadeOutTarget;
            if (stopFadeOut) fadeOut.Stop();
        }
        fadeRoutine = null;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float first = phaseOneSource != null ? phaseOneSource.volume : 0f;
        float second = phaseTwoSource != null ? phaseTwoSource.volume : 0f;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            if (phaseOneSource != null) phaseOneSource.volume = Mathf.Lerp(first, 0f, t);
            if (phaseTwoSource != null) phaseTwoSource.volume = Mathf.Lerp(second, 0f, t);
            yield return null;
        }
        if (phaseOneSource != null) phaseOneSource.Stop();
        if (phaseTwoSource != null) phaseTwoSource.Stop();
        fadeRoutine = null;
    }
}
