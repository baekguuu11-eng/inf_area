using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChernobylBossMusic : MonoBehaviour
{
    private AudioSource phaseOne;
    private AudioSource phaseTwo;
    private AudioSource gameplay;
    private Coroutine fadeRoutine;
    private float gameplayRestoreVolume = 0.70f;
    private const float PhaseOneVolume = 0.68f;
    private const float PhaseTwoVolume = 0.74f;

    public static ChernobylBossMusic Create(Transform parent)
    {
        GameObject root = new GameObject("ChernobylBossMusic");
        root.transform.SetParent(parent, false);
        ChernobylBossMusic music = root.AddComponent<ChernobylBossMusic>();
        music.Build();
        return music;
    }

    private void Build()
    {
        phaseOne = gameObject.AddComponent<AudioSource>();
        phaseTwo = gameObject.AddComponent<AudioSource>();
        Configure(phaseOne, Resources.Load<AudioClip>("Bosses/Chernobyl/Audio/Phase1_MeltdownCore"));
        Configure(phaseTwo, Resources.Load<AudioClip>("Bosses/Chernobyl/Audio/Phase2_GeigerMeltdown"));
        phaseOne.volume = 0f;
        phaseTwo.volume = 0f;
        ResolveGameplay();
    }

    private static void Configure(AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    private void ResolveGameplay()
    {
        if (gameplay != null) return;
        GameObject named = GameObject.Find("GameplayBgmAudioSource");
        if (named != null) gameplay = named.GetComponent<AudioSource>();
        if (gameplay == null)
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || source.transform.IsChildOf(transform)) continue;
                string n = source.gameObject.name.ToLowerInvariant();
                string c = source.clip != null ? source.clip.name.ToLowerInvariant() : string.Empty;
                if (n.Contains("gameplaybgm") || (n.Contains("bgm") && !n.Contains("boss")) ||
                    (c.Contains("gameplay") && c.Contains("bgm")))
                {
                    gameplay = source;
                    break;
                }
            }
        }
        if (gameplay != null && gameplay.volume > 0.01f)
            gameplayRestoreVolume = gameplay.volume;
    }

    public void PrepareSilentIntro()
    {
        ResolveGameplay();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (phaseOne != null) { phaseOne.Stop(); phaseOne.volume = 0f; }
        if (phaseTwo != null) { phaseTwo.Stop(); phaseTwo.volume = 0f; }
        if (gameplay != null)
        {
            if (gameplay.volume > 0.01f) gameplayRestoreVolume = gameplay.volume;
            gameplay.volume = 0f;
        }
    }

    public void PlayPhaseOne(float duration = 0.55f)
    {
        ResolveGameplay();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (phaseOne != null && phaseOne.clip != null && !phaseOne.isPlaying)
        {
            phaseOne.time = 0f;
            phaseOne.Play();
        }
        if (phaseTwo != null) phaseTwo.Stop();
        fadeRoutine = StartCoroutine(FadeMix(PhaseOneVolume, 0f, 0f, duration, false));
    }

    public void PlayPhaseTwo(float duration = 0.75f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (phaseTwo != null && phaseTwo.clip != null && !phaseTwo.isPlaying)
        {
            phaseTwo.time = 0f;
            phaseTwo.Play();
        }
        fadeRoutine = StartCoroutine(FadeMix(0f, PhaseTwoVolume, 0f, duration, true));
    }

    public void SetDefeatMix(float normalized = 0.88f, float duration = 0.35f)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        float p1 = phaseOne != null && phaseOne.isPlaying ? PhaseOneVolume * Mathf.Clamp01(normalized) : 0f;
        float p2 = phaseTwo != null && phaseTwo.isPlaying ? PhaseTwoVolume * Mathf.Clamp01(normalized) : 0f;
        fadeRoutine = StartCoroutine(FadeMix(p1, p2, 0f, duration, false));
    }

    public void CrossFadeBackToGameplay(float duration = 1.65f)
    {
        ResolveGameplay();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (gameplay != null)
        {
            gameplay.mute = false;
            gameplay.loop = true;
            if (gameplay.clip != null && !gameplay.isPlaying)
            {
                gameplay.volume = 0f;
                gameplay.Play();
            }
        }
        fadeRoutine = StartCoroutine(FadeMix(0f, 0f,
            Mathf.Clamp01(gameplayRestoreVolume > 0.01f ? gameplayRestoreVolume : 0.70f), duration, true));
    }

    private IEnumerator FadeMix(float p1Target, float p2Target, float gameplayTarget, float duration, bool stopSilentBossTrack)
    {
        float p1Start = phaseOne != null ? phaseOne.volume : 0f;
        float p2Start = phaseTwo != null ? phaseTwo.volume : 0f;
        float gameStart = gameplay != null ? gameplay.volume : 0f;
        float elapsed = 0f;
        float safe = Mathf.Max(0.05f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safe);
            float smooth = t * t * (3f - 2f * t);
            if (phaseOne != null) phaseOne.volume = Mathf.Lerp(p1Start, p1Target, smooth);
            if (phaseTwo != null) phaseTwo.volume = Mathf.Lerp(p2Start, p2Target, smooth);
            if (gameplay != null) gameplay.volume = Mathf.Lerp(gameStart, gameplayTarget, smooth);
            yield return null;
        }
        if (phaseOne != null)
        {
            phaseOne.volume = p1Target;
            if (stopSilentBossTrack && p1Target <= 0.001f) phaseOne.Stop();
        }
        if (phaseTwo != null)
        {
            phaseTwo.volume = p2Target;
            if (stopSilentBossTrack && p2Target <= 0.001f) phaseTwo.Stop();
        }
        if (gameplay != null) gameplay.volume = gameplayTarget;
        fadeRoutine = null;
    }
}
