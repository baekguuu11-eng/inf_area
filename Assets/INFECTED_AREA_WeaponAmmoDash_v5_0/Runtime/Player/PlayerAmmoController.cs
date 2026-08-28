using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAmmoController : MonoBehaviour
{
    [Header("Shared Energy Ammo")]
    [SerializeField, Min(0)] private int initialReserveAmmo = 28;
    [SerializeField, Min(1)] private int maximumReserveAmmo = 72;
    [SerializeField, Min(0f)] private float automaticReloadDelay = 0.12f;
    [SerializeField] private KeyCode manualReloadKey = KeyCode.R;

    private readonly Dictionary<string, int> magazineEnergy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private PlayerWeaponInventory inventory;
    private Coroutine reloadRoutine;
    private WeaponDefinition reloadWeapon;
    private int reserveAmmo;
    private float reloadProgress;

    public int ReserveAmmo => reserveAmmo;
    public int MaximumReserveAmmo => maximumReserveAmmo;
    public bool IsReloading => reloadRoutine != null;
    public float ReloadProgress => reloadProgress;
    public WeaponDefinition ReloadWeapon => reloadWeapon;
    public float ReserveRatio => maximumReserveAmmo > 0 ? reserveAmmo / (float)maximumReserveAmmo : 0f;
    public int CurrentTotalAmmoEnergy
    {
        get
        {
            WeaponDefinition weapon = inventory != null ? inventory.EquippedRanged : null;
            return reserveAmmo + (weapon != null ? GetMagazineEnergy(weapon) : 0);
        }
    }
    public float TotalAmmoRatio
    {
        get
        {
            WeaponDefinition weapon = inventory != null ? inventory.EquippedRanged : null;
            int capacity = Mathf.Max(1, maximumReserveAmmo + (weapon != null ? weapon.MagazineEnergyCapacity : 0));
            return Mathf.Clamp01(CurrentTotalAmmoEnergy / (float)capacity);
        }
    }

    public event Action AmmoChanged;
    public event Action<bool, float> ReloadStateChanged;
    public event Action<int> AmmoPickedUp;

    private void Awake()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        reserveAmmo = Mathf.Clamp(initialReserveAmmo, 0, Mathf.Max(1, maximumReserveAmmo));
    }

    private void OnEnable()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerWeaponInventory>();
        if (inventory != null)
        {
            inventory.WeaponChanged += HandleWeaponChanged;
            inventory.WeaponPurchased += HandleWeaponPurchased;
        }
    }

    private void Start()
    {
        InitializeOwnedMagazines();
        AmmoChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.WeaponChanged -= HandleWeaponChanged;
            inventory.WeaponPurchased -= HandleWeaponPurchased;
        }
        CancelReload();
    }

    private void Update()
    {
        if (GameInputState.IsLocked || inventory == null || !inventory.IsRangedMode)
            return;

        if (Input.GetKeyDown(manualReloadKey))
            RequestReload(inventory.CurrentWeapon, false);
    }

    public int GetMagazineEnergy(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.category != WeaponCategory.Ranged)
            return 0;
        EnsureMagazine(weapon);
        return magazineEnergy[weapon.weaponId];
    }

    public int GetShotsRemaining(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.category != WeaponCategory.Ranged)
            return 0;
        return GetMagazineEnergy(weapon) / Mathf.Max(1, weapon.ammoCostPerShot);
    }

    public bool CanFire(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.category != WeaponCategory.Ranged || IsReloading)
            return false;
        return GetMagazineEnergy(weapon) >= Mathf.Max(1, weapon.ammoCostPerShot);
    }

    public bool TryConsumeShot(WeaponDefinition weapon)
    {
        if (!CanFire(weapon))
        {
            if (weapon != null && GetMagazineEnergy(weapon) < Mathf.Max(1, weapon.ammoCostPerShot))
                RequestReload(weapon, true);
            return false;
        }

        int cost = Mathf.Max(1, weapon.ammoCostPerShot);
        magazineEnergy[weapon.weaponId] = Mathf.Max(0, magazineEnergy[weapon.weaponId] - cost);
        AmmoChanged?.Invoke();

        if (magazineEnergy[weapon.weaponId] < cost && reserveAmmo > 0)
            RequestReload(weapon, true);

        return true;
    }

    public bool RequestReload(WeaponDefinition weapon, bool automatic)
    {
        if (weapon == null || weapon.category != WeaponCategory.Ranged || IsReloading || reserveAmmo <= 0)
            return false;

        EnsureMagazine(weapon);
        int capacity = Mathf.Max(1, weapon.MagazineEnergyCapacity);
        if (magazineEnergy[weapon.weaponId] >= capacity)
            return false;

        reloadRoutine = StartCoroutine(ReloadRoutine(weapon, automatic ? automaticReloadDelay : 0f));
        return true;
    }

    public void CancelReload()
    {
        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        reloadWeapon = null;
        reloadProgress = 0f;
        ReloadStateChanged?.Invoke(false, 0f);
    }

    public int AddReserveAmmo(int amount)
    {
        if (amount <= 0)
            return 0;

        int before = reserveAmmo;
        reserveAmmo = Mathf.Clamp(reserveAmmo + amount, 0, Mathf.Max(1, maximumReserveAmmo));
        int gained = reserveAmmo - before;
        if (gained > 0)
        {
            AmmoChanged?.Invoke();
            AmmoPickedUp?.Invoke(gained);
        }
        return gained;
    }

    public void SetReserveAmmoForDebug(int amount)
    {
        reserveAmmo = Mathf.Clamp(amount, 0, Mathf.Max(1, maximumReserveAmmo));
        AmmoChanged?.Invoke();
    }

    private IEnumerator ReloadRoutine(WeaponDefinition weapon, float delay)
    {
        reloadWeapon = weapon;
        reloadProgress = 0f;

        if (delay > 0f)
        {
            float elapsedDelay = 0f;
            while (elapsedDelay < delay)
            {
                if (GameInputState.IsLocked)
                {
                    CancelReloadFromRoutine();
                    yield break;
                }
                elapsedDelay += Time.deltaTime;
                yield return null;
            }
        }

        ReloadStateChanged?.Invoke(true, 0f);
        float duration = Mathf.Max(0.1f, weapon.reloadDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (inventory == null || !inventory.IsRangedMode || inventory.CurrentWeapon != weapon)
            {
                CancelReloadFromRoutine();
                yield break;
            }

            elapsed += Time.deltaTime;
            reloadProgress = Mathf.Clamp01(elapsed / duration);
            ReloadStateChanged?.Invoke(true, reloadProgress);
            yield return null;
        }

        EnsureMagazine(weapon);
        int capacity = Mathf.Max(1, weapon.MagazineEnergyCapacity);
        int required = Mathf.Max(0, capacity - magazineEnergy[weapon.weaponId]);
        int transferred = Mathf.Min(required, reserveAmmo);
        magazineEnergy[weapon.weaponId] += transferred;
        reserveAmmo -= transferred;

        reloadRoutine = null;
        reloadWeapon = null;
        reloadProgress = 0f;
        AmmoChanged?.Invoke();
        ReloadStateChanged?.Invoke(false, 1f);
    }

    private void CancelReloadFromRoutine()
    {
        reloadRoutine = null;
        reloadWeapon = null;
        reloadProgress = 0f;
        ReloadStateChanged?.Invoke(false, 0f);
    }

    private void HandleWeaponChanged(WeaponDefinition active, WeaponDefinition standby)
    {
        CancelReload();
        if (active != null && active.category == WeaponCategory.Ranged)
            EnsureMagazine(active);
        AmmoChanged?.Invoke();
    }

    private void HandleWeaponPurchased(string weaponId)
    {
        if (inventory == null)
            return;
        WeaponDefinition weapon = inventory.GetDefinition(weaponId);
        if (weapon != null && weapon.category == WeaponCategory.Ranged)
            EnsureMagazine(weapon);
        AmmoChanged?.Invoke();
    }

    private void InitializeOwnedMagazines()
    {
        if (inventory == null)
            return;
        foreach (WeaponDefinition weapon in inventory.AllDefinitions)
        {
            if (weapon != null && weapon.category == WeaponCategory.Ranged && inventory.Owns(weapon.weaponId))
                EnsureMagazine(weapon);
        }
    }

    private void EnsureMagazine(WeaponDefinition weapon)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(weapon.weaponId))
            return;
        if (!magazineEnergy.ContainsKey(weapon.weaponId))
            magazineEnergy[weapon.weaponId] = Mathf.Max(1, weapon.MagazineEnergyCapacity);
    }
}
