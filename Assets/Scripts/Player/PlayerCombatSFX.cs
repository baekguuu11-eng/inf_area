using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCombatSFX : MonoBehaviour
{
    public static PlayerCombatSFX Instance { get; private set; }

    private AudioSource weaponSource;
    private AudioSource impactSource;
    private AudioSource utilitySource;
    private AudioSource reloadSource;
    private PlayerWeaponInventory inventory;
    private PlayerAmmoController ammo;
    private bool ready;
    private bool reloadActive;
    private int pistolIndex;
    private int machineIndex;
    private int laserIndex;
    private int impactIndex = -1;
    private float nextImpactTime;
    private Coroutine shotgunPumpRoutine;

    private AudioClip rangedEquip;
    private AudioClip meleeEquip;
    private AudioClip ammoPickup;
    private AudioClip[] pistolFire;
    private AudioClip pistolReload;
    private AudioClip[] machineFire;
    private AudioClip machineStop;
    private AudioClip machineReload;
    private AudioClip shotgunFire;
    private AudioClip shotgunPump;
    private AudioClip shotgunReload;
    private AudioClip[] laserFire;
    private AudioClip hammerSwing;
    private AudioClip hammerImpact;
    private AudioClip swordSwing;
    private AudioClip swordImpact;
    private AudioClip whipAttack;
    private AudioClip whipImpact;
    private AudioClip firewallSwing;
    private AudioClip firewallImpact;
    private AudioClip[] bulletImpacts;
    private AudioClip suicideExplosion;

    private void Awake()
    {
        Instance = this;
        inventory = GetComponent<PlayerWeaponInventory>();
        ammo = GetComponent<PlayerAmmoController>();
        weaponSource = CreateSource();
        impactSource = CreateSource();
        utilitySource = CreateSource();
        reloadSource = CreateSource();
        LoadClips();
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = false;
        return source;
    }

    private void OnEnable()
    {
        if (inventory == null) inventory = GetComponent<PlayerWeaponInventory>();
        if (ammo == null) ammo = GetComponent<PlayerAmmoController>();
        if (inventory != null) inventory.WeaponChanged += HandleWeaponChanged;
        if (ammo != null)
        {
            ammo.ReloadStateChanged += HandleReloadState;
            ammo.AmmoPickedUp += HandleAmmoPickedUp;
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        ready = true;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.WeaponChanged -= HandleWeaponChanged;
        if (ammo != null)
        {
            ammo.ReloadStateChanged -= HandleReloadState;
            ammo.AmmoPickedUp -= HandleAmmoPickedUp;
        }
        reloadActive = false;
        if (reloadSource != null) reloadSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadClips()
    {
        const string root = "CombatSFX/Weapons/";
        rangedEquip = Resources.Load<AudioClip>(root + "29_RangedWeaponEquip");
        meleeEquip = Resources.Load<AudioClip>(root + "30_MeleeWeaponEquip");
        ammoPickup = Resources.Load<AudioClip>(root + "31_AmmoPickup");
        pistolFire = LoadArray(root, new string[] { "32A_PistolFire", "32B_PistolFire" });
        pistolReload = Resources.Load<AudioClip>(root + "33_PistolReload");
        machineFire = LoadArray(root, new string[] { "34A_MachineGunFire", "34B_MachineGunFire", "34C_MachineGunFire" });
        machineStop = Resources.Load<AudioClip>(root + "35_MachineGunStop");
        machineReload = Resources.Load<AudioClip>(root + "36_MachineGunReload");
        shotgunFire = Resources.Load<AudioClip>(root + "37_ShotgunFire");
        shotgunPump = Resources.Load<AudioClip>(root + "38_ShotgunPump");
        shotgunReload = Resources.Load<AudioClip>(root + "39_ShotgunReload");
        laserFire = LoadArray(root, new string[] { "41A_LaserGunFire", "41B_LaserGunFire", "41C_LaserGunFire" });
        hammerSwing = Resources.Load<AudioClip>(root + "43_HammerSwing");
        hammerImpact = Resources.Load<AudioClip>(root + "44_HammerImpact");
        swordSwing = Resources.Load<AudioClip>(root + "45_SwordSwing");
        swordImpact = Resources.Load<AudioClip>(root + "46_SwordImpact");
        whipAttack = Resources.Load<AudioClip>(root + "47_WhipAttack");
        whipImpact = Resources.Load<AudioClip>(root + "48_WhipImpact");
        firewallSwing = Resources.Load<AudioClip>(root + "49_FirewallSwordSwing");
        firewallImpact = Resources.Load<AudioClip>(root + "50_FirewallSwordImpact");

        bulletImpacts = new AudioClip[12];
        for (int i = 0; i < bulletImpacts.Length; i++)
        {
            char suffix = (char)('A' + i);
            bulletImpacts[i] = Resources.Load<AudioClip>(root + "Impacts/27" + suffix + "_BulletEnemyImpact");
        }
        suicideExplosion = Resources.Load<AudioClip>("Bosses/Executor/Audio/11_ArtilleryExplosion");
    }

    private static AudioClip[] LoadArray(string root, string[] names)
    {
        AudioClip[] clips = new AudioClip[names.Length];
        for (int i = 0; i < names.Length; i++) clips[i] = Resources.Load<AudioClip>(root + names[i]);
        return clips;
    }

    private void HandleWeaponChanged(WeaponDefinition active, WeaponDefinition standby)
    {
        if (!ready || active == null) return;
        Play(utilitySource, active.category == WeaponCategory.Ranged ? rangedEquip : meleeEquip, 0.46f, 0.98f, 1.02f);
    }

    private void HandleAmmoPickedUp(int amount)
    {
        if (amount > 0) Play(utilitySource, ammoPickup, 0.62f, 0.98f, 1.02f);
    }

    private void HandleReloadState(bool active, float progress)
    {
        if (active && !reloadActive)
        {
            reloadActive = true;
            WeaponDefinition weapon = ammo != null ? ammo.ReloadWeapon : null;
            AudioClip clip = ResolveReload(weapon);
            if (clip != null)
            {
                reloadSource.Stop();
                reloadSource.pitch = 1f;
                reloadSource.clip = clip;
                reloadSource.volume = 0.58f;
                reloadSource.Play();
            }
        }
        else if (!active && reloadActive)
        {
            reloadActive = false;
            if (progress < 0.99f && reloadSource != null) reloadSource.Stop();
        }
    }

    private AudioClip ResolveReload(WeaponDefinition weapon)
    {
        if (weapon == null) return null;
        switch (weapon.weaponId)
        {
            case "pistol": return pistolReload;
            case "machine_gun": return machineReload;
            case "shotgun": return shotgunReload;
            default: return null;
        }
    }

    public void PlayRangedFire(WeaponDefinition weapon)
    {
        if (weapon == null) return;
        switch (weapon.weaponId)
        {
            case "pistol":
                PlaySequential(weaponSource, pistolFire, ref pistolIndex, 0.66f, 0.98f, 1.02f);
                break;
            case "machine_gun":
                PlaySequential(weaponSource, machineFire, ref machineIndex, 0.42f, 0.99f, 1.01f);
                break;
            case "shotgun":
                Play(weaponSource, shotgunFire, 0.82f, 0.99f, 1.01f);
                ScheduleShotgunPump(Mathf.Clamp(weapon.attackInterval * 0.35f, 0.12f, 0.28f));
                break;
            case "laser_gun":
                PlaySequential(weaponSource, laserFire, ref laserIndex, 0.68f, 0.98f, 1.02f);
                break;
        }
    }

    public void PlayMachineGunStop()
    {
        Play(weaponSource, machineStop, 0.42f, 0.99f, 1.01f);
    }

    private void ScheduleShotgunPump(float delay)
    {
        if (shotgunPumpRoutine != null) StopCoroutine(shotgunPumpRoutine);
        shotgunPumpRoutine = StartCoroutine(ShotgunPumpRoutine(delay));
    }

    private IEnumerator ShotgunPumpRoutine(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.02f, delay));
        Play(utilitySource, shotgunPump, 0.58f, 0.99f, 1.01f);
        shotgunPumpRoutine = null;
    }

    public void PlayMeleeSwing(WeaponDefinition weapon)
    {
        AudioClip clip = ResolveMelee(weapon, false);
        Play(weaponSource, clip, weapon != null && weapon.weaponId == "debug_hammer" ? 0.68f : 0.58f, 0.98f, 1.02f);
    }

    public void PlayMeleeImpact(WeaponDefinition weapon, EnemyHealth target, Vector2 hitPoint)
    {
        AudioClip clip = ResolveMelee(weapon, true);
        bool boss = target != null && (target.GetComponent<ExecutorBossController>() != null ||
                                      target.GetComponent<ChernobylBossController>() != null);
        float volume = boss ? 0.82f : 0.70f;
        float minPitch = boss ? 0.88f : 0.97f;
        float maxPitch = boss ? 0.94f : 1.03f;
        Play(impactSource, clip, volume, minPitch, maxPitch);
    }

    private AudioClip ResolveMelee(WeaponDefinition weapon, bool impact)
    {
        if (weapon == null) return null;
        switch (weapon.weaponId)
        {
            case "debug_hammer": return impact ? hammerImpact : hammerSwing;
            case "debug_whip": return impact ? whipImpact : whipAttack;
            case "firewall_sword": return impact ? firewallImpact : firewallSwing;
            default: return impact ? swordImpact : swordSwing;
        }
    }

    public void PlayRangedImpact(WeaponDefinition weapon, EnemyHealth target, Vector2 hitPoint)
    {
        if (Time.unscaledTime < nextImpactTime) return;
        nextImpactTime = Time.unscaledTime + (weapon != null && weapon.weaponId == "shotgun" ? 0.035f : 0.018f);
        AudioClip clip = NextImpactClip();
        bool boss = target != null && (target.GetComponent<ExecutorBossController>() != null ||
                                      target.GetComponent<ChernobylBossController>() != null);
        Play(impactSource, clip, boss ? 0.68f : 0.53f, boss ? 0.82f : 0.96f, boss ? 0.90f : 1.04f);
    }

    private AudioClip NextImpactClip()
    {
        if (bulletImpacts == null || bulletImpacts.Length == 0) return null;
        int next = UnityEngine.Random.Range(0, bulletImpacts.Length);
        if (bulletImpacts.Length > 1 && next == impactIndex) next = (next + 1) % bulletImpacts.Length;
        impactIndex = next;
        return bulletImpacts[next];
    }

    public void PlaySuicideExplosion()
    {
        Play(impactSource, suicideExplosion, 0.50f, 1.07f, 1.13f);
    }

    private static void PlaySequential(AudioSource source, AudioClip[] clips, ref int index, float volume, float minPitch, float maxPitch)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[index % clips.Length];
        index = (index + 1) % clips.Length;
        Play(source, clip, volume, minPitch, maxPitch);
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        if (source == null || clip == null) return;
        source.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
