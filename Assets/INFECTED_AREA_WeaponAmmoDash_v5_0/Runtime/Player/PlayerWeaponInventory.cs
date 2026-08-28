using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWeaponInventory : MonoBehaviour
{
    public static PlayerWeaponInventory Instance { get; private set; }

    [Header("Defaults")]
    [SerializeField] private string defaultMeleeId = "debug_sword";
    [SerializeField] private string defaultRangedId = "pistol";
    [SerializeField] private bool startInRangedMode;

    [Header("Debug Weapon Hotkeys")]
    [SerializeField] private bool enableDebugWeaponHotkeys = true;
    [SerializeField] private bool debugHotkeysOnlyInEditorOrDevelopmentBuild = true;

    private readonly Dictionary<string, WeaponDefinition> definitions = new Dictionary<string, WeaponDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private WeaponDefinition equippedMelee;
    private WeaponDefinition equippedRanged;
    private bool isRangedMode;

    public WeaponDefinition EquippedMelee => equippedMelee;
    public WeaponDefinition EquippedRanged => equippedRanged;
    public WeaponDefinition CurrentWeapon => isRangedMode ? equippedRanged : equippedMelee;
    public WeaponDefinition StandbyWeapon => isRangedMode ? equippedMelee : equippedRanged;
    public bool IsRangedMode => isRangedMode;
    public bool DebugHotkeysEnabled => enableDebugWeaponHotkeys;

    public event Action<WeaponDefinition, WeaponDefinition> WeaponChanged;
    public event Action<string> WeaponPurchased;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LoadDefinitions();
        ResetRunLoadout();
    }

    private void Update()
    {
        if (!enableDebugWeaponHotkeys || GameInputState.IsLocked)
            return;
        if (debugHotkeysOnlyInEditorOrDevelopmentBuild && !Application.isEditor && !Debug.isDebugBuild)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) EquipForDebug("debug_sword");
        else if (Input.GetKeyDown(KeyCode.Alpha2)) EquipForDebug("debug_hammer");
        else if (Input.GetKeyDown(KeyCode.Alpha3)) EquipForDebug("debug_whip");
        else if (Input.GetKeyDown(KeyCode.Alpha4)) EquipForDebug("firewall_sword");
        else if (Input.GetKeyDown(KeyCode.Alpha5)) EquipForDebug("pistol");
        else if (Input.GetKeyDown(KeyCode.Alpha6)) EquipForDebug("machine_gun");
        else if (Input.GetKeyDown(KeyCode.Alpha7)) EquipForDebug("shotgun");
        else if (Input.GetKeyDown(KeyCode.Alpha8)) EquipForDebug("laser_gun");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetRunLoadout()
    {
        owned.Clear();
        foreach (WeaponDefinition definition in definitions.Values)
        {
            if (definition != null && definition.defaultOwned)
                owned.Add(definition.weaponId);
        }

        equippedMelee = GetDefinition(defaultMeleeId) ?? FindFirst(WeaponCategory.Melee, true);
        equippedRanged = GetDefinition(defaultRangedId) ?? FindFirst(WeaponCategory.Ranged, true);

        if (equippedMelee != null)
            owned.Add(equippedMelee.weaponId);
        if (equippedRanged != null)
            owned.Add(equippedRanged.weaponId);

        isRangedMode = startInRangedMode;
        if (CurrentWeapon == null)
            isRangedMode = equippedRanged != null;

        WeaponChanged?.Invoke(CurrentWeapon, StandbyWeapon);
    }

    public void ReloadDefinitions()
    {
        string meleeId = equippedMelee != null ? equippedMelee.weaponId : defaultMeleeId;
        string rangedId = equippedRanged != null ? equippedRanged.weaponId : defaultRangedId;
        LoadDefinitions();
        equippedMelee = GetDefinition(meleeId) ?? FindFirst(WeaponCategory.Melee, true);
        equippedRanged = GetDefinition(rangedId) ?? FindFirst(WeaponCategory.Ranged, true);
        WeaponChanged?.Invoke(CurrentWeapon, StandbyWeapon);
    }

    public bool ToggleCategory()
    {
        bool targetRanged = !isRangedMode;
        if (targetRanged && equippedRanged == null)
            return false;
        if (!targetRanged && equippedMelee == null)
            return false;

        isRangedMode = targetRanged;
        WeaponChanged?.Invoke(CurrentWeapon, StandbyWeapon);
        return true;
    }

    public bool SetCategory(WeaponCategory category)
    {
        bool target = category == WeaponCategory.Ranged;
        if (target == isRangedMode)
            return true;
        return ToggleCategory();
    }

    public bool Owns(string weaponId)
    {
        return !string.IsNullOrWhiteSpace(weaponId) && owned.Contains(weaponId);
    }

    public bool PurchaseAndEquip(string weaponId)
    {
        WeaponDefinition definition = GetDefinition(weaponId);
        if (definition == null)
            return false;

        bool newlyOwned = owned.Add(definition.weaponId);
        Equip(definition);
        if (newlyOwned)
            WeaponPurchased?.Invoke(definition.weaponId);
        return true;
    }

    public bool EquipOwned(string weaponId)
    {
        WeaponDefinition definition = GetDefinition(weaponId);
        if (definition == null || !Owns(weaponId))
            return false;
        Equip(definition);
        return true;
    }

    public bool EquipForDebug(string weaponId)
    {
        WeaponDefinition definition = GetDefinition(weaponId);
        if (definition == null)
        {
            Debug.LogWarning("[Weapon Debug] 정의를 찾지 못했습니다: " + weaponId, this);
            return false;
        }

        Equip(definition);
        return true;
    }

    public WeaponDefinition GetDefinition(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return null;
        WeaponDefinition definition;
        return definitions.TryGetValue(weaponId, out definition) ? definition : null;
    }

    public IReadOnlyCollection<WeaponDefinition> AllDefinitions => definitions.Values;

    private void Equip(WeaponDefinition definition)
    {
        if (definition == null)
            return;

        if (definition.category == WeaponCategory.Melee)
            equippedMelee = definition;
        else
            equippedRanged = definition;

        isRangedMode = definition.category == WeaponCategory.Ranged;
        WeaponChanged?.Invoke(CurrentWeapon, StandbyWeapon);
    }

    private void LoadDefinitions()
    {
        definitions.Clear();
        WeaponDefinition[] loaded = Resources.LoadAll<WeaponDefinition>("WeaponDefinitions");
        for (int i = 0; i < loaded.Length; i++)
        {
            WeaponDefinition definition = loaded[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.weaponId))
                continue;
            definitions[definition.weaponId] = definition;
        }

        if (definitions.Count == 0)
            Debug.LogError("[WeaponInventory] WeaponDefinitions Resources가 비어 있습니다. 복구 설치기의 Rebuild Weapon Definitions를 실행하세요.", this);
    }

    private WeaponDefinition FindFirst(WeaponCategory category, bool preferDefault)
    {
        WeaponDefinition fallback = null;
        foreach (WeaponDefinition definition in definitions.Values)
        {
            if (definition == null || definition.category != category)
                continue;
            if (fallback == null)
                fallback = definition;
            if (preferDefault && definition.defaultOwned)
                return definition;
        }
        return fallback;
    }
}
