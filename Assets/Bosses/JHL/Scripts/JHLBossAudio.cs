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
    private AudioClip[] faceHitVariants;
    private AudioClip[] heavyHitVariants;
    private AudioClip[] handWindupVariants;
    private AudioClip[] handImpactVariants;
    private AudioClip[] sweepVariants;
    private AudioClip[] projectileVariants;
    private AudioClip[] beamChargeVariants;
    private AudioClip[] beamFireVariants;
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
        oneShot.ignoreListenerPause = false;
        oneShot.priority = 64;

        // V21: retired teammate/ExternalAudioV18 boss sounds explicitly removed.
        // No fallback to unrelated Chernobyl effects; dedicated JHL replacements pending.

    }

    public void PlayIntroFace() => Play(introFace, 0.70f, 0.98f, 1.01f);
    public void PlayIntroHand() => Play(introHand, 0.72f, 0.96f, 1.02f);
    public void PlayHandWindup() => PlayVariant(handWindupVariants, handWindup, 0.50f, 0.96f, 1.03f);
    public void PlayHandImpact() => PlayVariant(handImpactVariants, handImpact, 0.88f, 0.94f, 1.02f);
    public void PlaySweep() => PlayVariant(sweepVariants, sweep, 0.72f, 0.97f, 1.02f);
    public void PlayBeamCharge() => PlayVariant(beamChargeVariants, beamCharge, 0.58f, 0.98f, 1.01f);
    public void PlayBeamFire() => PlayVariant(beamFireVariants, beamFire, 0.86f, 0.97f, 1.01f);
    public void PlayCompression() => Play(compression, 0.80f, 0.96f, 1.01f);
    public void PlayPhaseChange() => Play(phaseChange, 0.80f, 0.98f, 1.01f);
    public void PlayFullAccess() => Play(fullAccess, 0.92f, 0.98f, 1.00f);
    public void PlayHit() => PlayHit(false);
    public void PlayHit(bool heavy)
    {
        if (heavy) PlayVariant(heavyHitVariants, hit, 0.48f, 0.97f, 1.02f);
        else PlayVariant(faceHitVariants, hit, 0.34f, 0.98f, 1.04f);
    }
    public void PlayDeath() => Play(death, 0.92f, 0.97f, 1.00f);
    public void PlayHandWallLock() => Play(handWallLock, 0.52f, 0.97f, 1.02f);
    public void PlayRoar() => Play(roar, 0.84f, 0.97f, 1.00f);
    public void PlayRemoteLock() => Play(remoteLock, 0.56f, 0.99f, 1.01f);
    public void PlayProjectile()
    {
        if (Time.unscaledTime < nextProjectileAt) return;
        nextProjectileAt = Time.unscaledTime + 0.045f;
        PlayVariant(projectileVariants, projectile, 0.24f, 0.96f, 1.05f);
    }

    private static AudioClip[] LoadVariants(string root, string baseName)
    {
        AudioClip[] buffer = new AudioClip[3];
        int count = 0;
        for (int i = 1; i <= 3; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(root + baseName + "_" + i.ToString("00"));
            if (clip != null) buffer[count++] = clip;
        }
        if (count == 0) return null;
        AudioClip[] result = new AudioClip[count];
        for (int i = 0; i < count; i++) result[i] = buffer[i];
        return result;
    }

    private void PlayVariant(AudioClip[] variants, AudioClip fallback, float volume, float minPitch, float maxPitch)
    {
        AudioClip clip = variants != null && variants.Length > 0
            ? variants[Random.Range(0, variants.Length)]
            : fallback;
        Play(clip, volume, minPitch, maxPitch);
    }

    private void Play(AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        if (oneShot == null || clip == null) return;
        oneShot.pitch = Random.Range(minPitch, maxPitch);
        oneShot.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
