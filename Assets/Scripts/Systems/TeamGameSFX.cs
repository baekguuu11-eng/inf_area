using UnityEngine;

/// <summary>
/// Runtime bridge for the team-curated CC0 game SFX pack.
/// The original audio files are preserved under Assets/Audio/_SourceOriginal;
/// runtime copies are loaded from Resources/TeamSFX without overwriting legacy clips.
/// </summary>
[DisallowMultipleComponent]
public sealed class TeamGameSFX : MonoBehaviour
{
    private static TeamGameSFX instance;

    private AudioSource playerSource;
    private AudioSource worldSource;
    private AudioSource impactSource;

    private AudioClip bytePickup;
    private AudioClip dash;
    private AudioClip playerHit;
    private AudioClip enemyDeath;
    private AudioClip portalActivate;
    private AudioClip portalEnter;
    private AudioClip[] wallImpacts;

    private float nextByteTime;
    private float nextHitTime;
    private float nextEnemyDeathTime;
    private float nextPortalActivateTime;
    private float nextPortalEnterTime;
    private float nextWallImpactTime;
    private int lastWallImpactIndex = -1;

    private static TeamGameSFX Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject root = new GameObject("__IA_TeamGameSFX");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<TeamGameSFX>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        playerSource = CreateSource("PlayerUtility");
        worldSource = CreateSource("WorldUtility");
        impactSource = CreateSource("Impact");

        bytePickup = Resources.Load<AudioClip>("TeamSFX/Pickup/Byte_01");
        dash = Resources.Load<AudioClip>("TeamSFX/Player/Dash_01");
        playerHit = Resources.Load<AudioClip>("TeamSFX/Player/Hit_01");
        enemyDeath = Resources.Load<AudioClip>("TeamSFX/Enemy/Death_01");
        portalActivate = Resources.Load<AudioClip>("TeamSFX/Portal/Activate_01");
        portalEnter = Resources.Load<AudioClip>("TeamSFX/Portal/Enter_01");
        wallImpacts = new AudioClip[]
        {
            Resources.Load<AudioClip>("TeamSFX/Impact/Projectile_WallHit_01"),
            Resources.Load<AudioClip>("TeamSFX/Impact/Projectile_WallHit_02")
        };
    }

    private AudioSource CreateSource(string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 128;
        return source;
    }

    public static AudioClip LoadAmmoPickupPrimary()
    {
        return Resources.Load<AudioClip>("TeamSFX/Pickup/Ammo_01");
    }

    public static void PlayBytePickup()
    {
        TeamGameSFX sfx = Instance;
        if (Time.unscaledTime < sfx.nextByteTime) return;
        sfx.nextByteTime = Time.unscaledTime + 0.045f;
        sfx.PlayOneShot(sfx.playerSource, sfx.bytePickup, 0.50f, 0.98f, 1.03f);
    }

    public static void PlayDash()
    {
        TeamGameSFX sfx = Instance;
        sfx.PlayOneShot(sfx.playerSource, sfx.dash, 0.72f, 1.03f, 1.08f);
    }

    public static void PlayPlayerHit()
    {
        TeamGameSFX sfx = Instance;
        if (Time.unscaledTime < sfx.nextHitTime) return;
        sfx.nextHitTime = Time.unscaledTime + 0.08f;
        // The teammate-selected source is intentionally kept restrained because it is a
        // bright/electronic terminal-family sound rather than a body-heavy hit.
        sfx.PlayOneShot(sfx.playerSource, sfx.playerHit, 0.42f, 0.94f, 1.02f);
    }

    public static void PlayEnemyDeath()
    {
        TeamGameSFX sfx = Instance;
        if (Time.unscaledTime < sfx.nextEnemyDeathTime) return;
        sfx.nextEnemyDeathTime = Time.unscaledTime + 0.035f;
        sfx.PlayOneShot(sfx.worldSource, sfx.enemyDeath, 0.46f, 0.94f, 1.04f);
    }

    public static void PlayPortalActivate()
    {
        TeamGameSFX sfx = Instance;
        if (Time.unscaledTime < sfx.nextPortalActivateTime) return;
        sfx.nextPortalActivateTime = Time.unscaledTime + 0.20f;
        sfx.PlayOneShot(sfx.worldSource, sfx.portalActivate, 0.62f, 0.99f, 1.01f);
    }

    public static void PlayPortalEnter()
    {
        TeamGameSFX sfx = Instance;
        if (Time.unscaledTime < sfx.nextPortalEnterTime) return;
        sfx.nextPortalEnterTime = Time.unscaledTime + 0.20f;
        sfx.PlayOneShot(sfx.worldSource, sfx.portalEnter, 0.50f, 0.99f, 1.01f);
    }

    public static void PlayWallImpact(float power = 0.5f)
    {
        TeamGameSFX sfx = Instance;
        if (Time.unscaledTime < sfx.nextWallImpactTime) return;
        sfx.nextWallImpactTime = Time.unscaledTime + 0.024f;
        AudioClip clip = sfx.NextWallImpact();
        float volume = Mathf.Lerp(0.38f, 0.56f, Mathf.Clamp01(power));
        sfx.PlayOneShot(sfx.impactSource, clip, volume, 0.96f, 1.04f);
    }

    private AudioClip NextWallImpact()
    {
        if (wallImpacts == null || wallImpacts.Length == 0) return null;
        int index = Random.Range(0, wallImpacts.Length);
        if (wallImpacts.Length > 1 && index == lastWallImpactIndex)
            index = (index + 1) % wallImpacts.Length;
        lastWallImpactIndex = index;
        return wallImpacts[index];
    }

    private void PlayOneShot(AudioSource source, AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        if (source == null || clip == null) return;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
