using UnityEngine;

/// <summary>Editor/Development build 전용 전투 진단 오버레이.</summary>
[DisallowMultipleComponent]
public sealed class V11DebugOverlay : MonoBehaviour
{
    private MapManager map;
    private PlayerWeaponInventory inventory;
    private PlayerAmmoController ammo;
    private GUIStyle style;

    public static void Ensure(MapManager manager)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (manager == null || manager.GetComponent<V11DebugOverlay>() != null) return;
        V11DebugOverlay overlay = manager.gameObject.AddComponent<V11DebugOverlay>();
        overlay.map = manager;
#endif
    }

    private void Awake()
    {
        if (map == null) map = GetComponent<MapManager>();
        inventory = Object.FindAnyObjectByType<PlayerWeaponInventory>();
        ammo = Object.FindAnyObjectByType<PlayerAmmoController>();
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (map == null) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.normal.textColor = new Color(0.55f, 1f, 0.92f, 0.92f);
            style.richText = false;
        }
        if (inventory == null) inventory = Object.FindAnyObjectByType<PlayerWeaponInventory>();
        if (ammo == null) ammo = Object.FindAnyObjectByType<PlayerAmmoController>();

        ExecutorBossController boss = map.ActiveExecutorBoss;
        string weapon = inventory != null && inventory.CurrentWeapon != null ? inventory.CurrentWeapon.weaponId : "-";
        string ammoText = ammo != null ? ammo.CurrentTotalAmmoEnergy.ToString() : "-";
        string bossText = boss != null
            ? $"Executor {boss.Health.CurrentHealth}/{boss.Health.MaxHealth} P{boss.DebugPhase} {boss.DebugStateName}\nLast: {boss.DebugLastPatternName}  Selected: {boss.DebugSelectedPatternName}"
            : "Executor: -";
        string text = $"V11 DEBUG\nStage {map.CurrentStage}  Room {(map.CurrentRoom != null ? map.CurrentRoom.RoomNumber : 0)}\nWeapon {weapon}  Ammo {ammoText}\n{bossText}\nPgUp/PgDn: pattern  P: force";
        GUI.Label(new Rect(10, 10, 420, 150), text, style);
#endif
    }
}
