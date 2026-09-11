using UnityEngine;

/// <summary>
/// Optional JHL audio hooks. Missing clips are intentionally silent so gameplay development is never blocked by art/audio availability.
/// Expected Resources path: Resources/Bosses/JHL/...
/// </summary>
[DisallowMultipleComponent]
public sealed class JHLBossAudio : MonoBehaviour
{
    private AudioSource oneShot;
    private AudioClip introFace;
    private AudioClip introHand;
    private AudioClip handWindup;
    private AudioClip handImpact;
    private AudioClip sweep;
    private AudioClip beamCharge;
    private AudioClip beamFire;
    private AudioClip compression;
    private AudioClip phaseChange;
    private AudioClip fullAccess;
    private AudioClip hit;
    private AudioClip death;
    private AudioClip handWallLock;
    private AudioClip roar;
    private AudioClip remoteLock;
    private AudioClip projectile;
    private float nextProjectileAt;

    public static JHLBossAudio Create(Transform parent)
    {
        GameObject root = new GameObject("JHLBossAudio");
        root.transform.SetParent(parent, false);
        JHLBossAudio audio = root.AddComponent<JHLBossAudio>();
        audio.Build();
        return audio;
    }

    private void Build()
    {
        oneShot = gameObject.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.loop = false;
        oneShot.spatialBlend = 0f;
        oneShot.ignoreListenerPause = true;
        oneShot.priority = 64;

        const string root = "Bosses/JHL/";
        introFace = Resources.Load<AudioClip>(root + "JHL_IntroFace");
        introHand = Resources.Load<AudioClip>(root + "JHL_IntroHand");
        handWindup = Resources.Load<AudioClip>(root + "JHL_HandWindup");
        handImpact = Resources.Load<AudioClip>(root + "JHL_HandImpact");
        sweep = Resources.Load<AudioClip>(root + "JHL_Sweep");
        beamCharge = Resources.Load<AudioClip>(root + "JHL_BeamCharge");
        beamFire = Resources.Load<AudioClip>(root + "JHL_BeamFire");
        compression = Resources.Load<AudioClip>(root + "JHL_Compression");
        phaseChange = Resources.Load<AudioClip>(root + "JHL_PhaseChange");
        fullAccess = Resources.Load<AudioClip>(root + "JHL_FullAccess");
        hit = Resources.Load<AudioClip>(root + "JHL_Hit");
        death = Resources.Load<AudioClip>(root + "JHL_Death");
        handWallLock = Resources.Load<AudioClip>(root + "JHL_HandWallLock");
        roar = Resources.Load<AudioClip>(root + "JHL_Roar");
        remoteLock = Resources.Load<AudioClip>(root + "JHL_RemoteLock");
        projectile = Resources.Load<AudioClip>(root + "JHL_Projectile");
    }

    public void PlayIntroFace() => Play(introFace, 0.70f, 0.98f, 1.01f);
    public void PlayIntroHand() => Play(introHand, 0.72f, 0.96f, 1.02f);
    public void PlayHandWindup() => Play(handWindup, 0.50f, 0.96f, 1.03f);
    public void PlayHandImpact() => Play(handImpact, 0.88f, 0.94f, 1.02f);
    public void PlaySweep() => Play(sweep, 0.72f, 0.97f, 1.02f);
    public void PlayBeamCharge() => Play(beamCharge, 0.58f, 0.98f, 1.01f);
    public void PlayBeamFire() => Play(beamFire, 0.86f, 0.97f, 1.01f);
    public void PlayCompression() => Play(compression, 0.80f, 0.96f, 1.01f);
    public void PlayPhaseChange() => Play(phaseChange, 0.80f, 0.98f, 1.01f);
    public void PlayFullAccess() => Play(fullAccess, 0.92f, 0.98f, 1.00f);
    public void PlayHit() => Play(hit, 0.36f, 0.98f, 1.04f);
    public void PlayDeath() => Play(death, 0.92f, 0.97f, 1.00f);
    public void PlayHandWallLock() => Play(handWallLock, 0.52f, 0.97f, 1.02f);
    public void PlayRoar() => Play(roar, 0.84f, 0.97f, 1.00f);
    public void PlayRemoteLock() => Play(remoteLock, 0.56f, 0.99f, 1.01f);
    public void PlayProjectile()
    {
        if (Time.unscaledTime < nextProjectileAt) return;
        nextProjectileAt = Time.unscaledTime + 0.045f;
        Play(projectile, 0.24f, 0.96f, 1.05f);
    }

    private void Play(AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        if (oneShot == null || clip == null) return;
        oneShot.pitch = Random.Range(minPitch, maxPitch);
        oneShot.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
