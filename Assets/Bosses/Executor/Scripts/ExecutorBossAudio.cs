using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExecutorBossAudio : MonoBehaviour
{
    private AudioSource oneShotSource;
    private AudioSource titleHitSource;
    private AudioSource rumbleSource;
    private AudioSource undergroundSource;

    private AudioClip rumbleLoop;
    private AudioClip groundCreak;
    private AudioClip burrowDrill;
    private AudioClip riseDebris;
    private AudioClip undergroundMovement;
    private AudioClip machineActivate;
    private AudioClip typeClick;
    private AudioClip typeHeavy;
    private AudioClip artilleryLaunch;
    private AudioClip artilleryFall;
    private AudioClip explosion;
    private AudioClip aimedProjectile;
    private AudioClip spreadProjectile;
    private AudioClip phaseOverload;
    private AudioClip deathMalfunction;
    private AudioClip finalShutdown;

    public static ExecutorBossAudio Create(Transform parent)
    {
        GameObject root = new GameObject("ExecutorBossAudio_UserSFX");
        root.transform.SetParent(parent, false);
        ExecutorBossAudio audio = root.AddComponent<ExecutorBossAudio>();
        audio.Build();
        return audio;
    }

    private void Build()
    {
        // PlayOneShot volume is multiplied by AudioSource.volume.
        // V5 accidentally created this source at volume 0, making every boss one-shot silent.
        oneShotSource = CreateSource(false, 1f);
        // 보스 이름 타건은 일반 SFX와 겹쳐도 잘리지 않도록 전용 소스로 분리한다.
        titleHitSource = CreateSource(false, 1f);
        titleHitSource.priority = 16;
        rumbleSource = CreateSource(true, 0f);
        undergroundSource = CreateSource(true, 0f);

        const string root = "Bosses/Executor/Audio/";
        rumbleLoop = Resources.Load<AudioClip>(root + "01_UndergroundRumble_Loop");
        groundCreak = Resources.Load<AudioClip>(root + "02_GroundMetalCreak");
        burrowDrill = Resources.Load<AudioClip>(root + "03_BurrowDrill");
        riseDebris = Resources.Load<AudioClip>(root + "04_EmergeDebrisBreak");
        undergroundMovement = Resources.Load<AudioClip>(root + "05_UndergroundMovement_Loop");
        machineActivate = Resources.Load<AudioClip>(root + "06_MachineActivation");
        typeClick = Resources.Load<AudioClip>(root + "07_TextTypeClick");
        typeHeavy = Resources.Load<AudioClip>(root + "08_BossNameHeavyHit");
        artilleryLaunch = Resources.Load<AudioClip>(root + "09_ArtilleryLaunchUp");
        artilleryFall = Resources.Load<AudioClip>(root + "10_ArtilleryFallingWhistle");
        explosion = Resources.Load<AudioClip>(root + "11_ArtilleryExplosion");
        aimedProjectile = Resources.Load<AudioClip>(root + "12_AimedProjectileShot");
        spreadProjectile = Resources.Load<AudioClip>(root + "13_SpreadProjectileShot");
        phaseOverload = Resources.Load<AudioClip>(root + "14_PhaseOverload");
        deathMalfunction = Resources.Load<AudioClip>(root + "15_DeathMalfunction");
        finalShutdown = Resources.Load<AudioClip>(root + "16_FinalPowerShutdown");
    }

    private AudioSource CreateSource(bool loop, float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.mute = false;
        source.priority = 64;
        source.bypassReverbZones = true;
        source.volume = volume;
        return source;
    }

    public void StartRumble(float volume = 0.28f)
    {
        StartLoop(rumbleSource, rumbleLoop, volume);
    }

    public void SetRumbleVolume(float volume)
    {
        if (rumbleSource != null) rumbleSource.volume = Mathf.Clamp01(volume);
    }

    public void StopRumble()
    {
        StopLoop(rumbleSource);
    }

    public void StartUndergroundMovement(float volume = 0.34f)
    {
        StartLoop(undergroundSource, undergroundMovement, volume);
    }

    public void StopUndergroundMovement()
    {
        StopLoop(undergroundSource);
    }

    private static void StartLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        if (!source.isPlaying) source.Play();
    }

    private static void StopLoop(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }

    public void PlayGroundCreak(float volume = 0.58f) { Play(groundCreak, volume, 0.96f, 1.03f); }
    public void PlayRiseImpact(float volume = 0.90f) { Play(riseDebris, volume, 0.96f, 1.01f); }
    public void PlayMachineActivate(float volume = 0.68f) { Play(machineActivate, volume, 0.98f, 1.01f); }
    public void PlayTypeClick(bool heavy = false)
    {
        if (heavy)
        {
            PlayBossNameHit(2);
            return;
        }

        Play(typeClick, 0.50f, 0.97f, 1.04f);
    }

    /// <summary>
    /// '집 / 행 / 자'가 찍힐 때 사용하는 전용 무거운 타건음.
    /// 일반 PlayOneShot 소스와 분리해 직전 시스템 타건음에 묻히거나 잘리는 일을 막는다.
    /// </summary>
    public void PlayBossNameHit(int characterIndex)
    {
        if (titleHitSource == null || typeHeavy == null)
        {
            Play(typeHeavy != null ? typeHeavy : typeClick, 0.92f, 0.92f, 0.99f);
            return;
        }

        int safeIndex = Mathf.Clamp(characterIndex, 0, 2);
        float[] volumes = { 0.86f, 0.94f, 1.00f };
        float[] pitches = { 0.96f, 0.92f, 0.88f };

        // 한 글자마다 충격이 확실히 분리되도록 전용 소스를 다시 시작한다.
        titleHitSource.Stop();
        titleHitSource.mute = false;
        titleHitSource.volume = volumes[safeIndex];
        titleHitSource.pitch = pitches[safeIndex];
        titleHitSource.clip = typeHeavy;
        titleHitSource.Play();
    }
    public void PlayArtilleryLaunch(float volume = 0.76f) { Play(artilleryLaunch, volume, 0.98f, 1.02f); }
    public void PlayArtilleryFall(float volume = 0.70f) { Play(artilleryFall, volume, 0.99f, 1.01f); }
    public void PlayExplosion(float volume = 0.82f) { Play(explosion, volume, 0.97f, 1.02f); }
    public void PlayBurrow(float volume = 0.68f) { Play(burrowDrill, volume, 0.94f, 0.99f); }
    public void PlayAimedProjectile(float volume = 0.58f) { Play(aimedProjectile, volume, 0.98f, 1.03f); }
    public void PlaySpreadProjectile(float volume = 0.67f) { Play(spreadProjectile, volume, 0.98f, 1.02f); }
    public void PlayPhaseOverload(float volume = 0.77f) { Play(phaseOverload, volume, 0.98f, 1.01f); }
    public void PlayDeathMalfunction(float volume = 0.76f) { Play(deathMalfunction, volume, 0.98f, 1.01f); }
    public void PlayFinalShutdown(float volume = 0.72f) { Play(finalShutdown, volume, 0.99f, 1.00f); }

    private void Play(AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        if (oneShotSource == null || clip == null) return;
        oneShotSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void StopAllLoops()
    {
        StopRumble();
        StopUndergroundMovement();
    }

    private void OnDestroy()
    {
        StopAllLoops();
    }
}
