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

        ExecutorBossController executor = map.ActiveExecutorBoss;
        ChernobylBossController chernobyl = map.ActiveChernobylBoss;
        JHLBossController jhl = map.ActiveJHLBoss;
        string weapon = inventory != null && inventory.CurrentWeapon != null ? inventory.CurrentWeapon.weaponId : "-";
        string ammoText = ammo != null ? ammo.CurrentTotalAmmoEnergy.ToString() : "-";
        string bossText;
        if (executor != null)
            bossText = $"Executor {executor.Health.CurrentHealth}/{executor.Health.MaxHealth} P{executor.DebugPhase} {executor.DebugStateName}\nLast: {executor.DebugLastPatternName}  Selected: {executor.DebugSelectedPatternName}";
        else if (jhl != null)
        {
            string safe = jhl.DebugSafeWidth >= 0f ? jhl.DebugSafeWidth.ToString("0.00") : "-";
            bossText = $"JHL {jhl.Health.CurrentHealth}/{jhl.Health.MaxHealth} P{jhl.Phase} {jhl.DebugStateName}\n" +
                       $"Last: {jhl.DebugLastPatternName}  Family: {jhl.DebugPatternFamilyName}  Selected: {jhl.DebugSelectedPatternName}\n" +
                       $"Vel F {jhl.DebugFaceVelocity.magnitude:0.0}  L {jhl.DebugLeftVelocity.magnitude:0.0}  R {jhl.DebugRightVelocity.magnitude:0.0}  Safe {safe}";
        }
        else if (chernobyl != null)
            bossText = $"Chernobyl {chernobyl.Health.CurrentHealth}/{chernobyl.Health.MaxHealth} P{chernobyl.Phase} Grid {chernobyl.DebugGridDescriptorV11} Warnings {chernobyl.DebugGridWarningCountV11}\n" +
                       $"Last {chernobyl.DebugLastPatternNameV11} / Combo {chernobyl.DebugLastComboNameV11}\n" +
                       $"Selected {chernobyl.DebugSelectedPatternNameV11} / {chernobyl.DebugSelectedComboNameV11}  InvBoss {chernobyl.DebugBossInvulnerableV11} InvPlayer {chernobyl.DebugPlayerInvulnerableV11} NoCD {chernobyl.DebugIgnoreCooldownV11}";
        else
            bossText = "Boss: -";
        string text = $"V11 DEBUG\nStage {map.CurrentStage}  Room {(map.CurrentRoom != null ? map.CurrentRoom.RoomNumber : 0)}\nWeapon {weapon}  Ammo {ammoText}\n{bossText}\nChernobyl: Shift+1/2/3 page, Shift+PgUp/PgDn select, Shift+N next+force, Shift+P force, Shift+C/X combo, Shift+O force combo, Shift+B anti-burst, Shift+G clear, Shift+I/U invuln, Shift+K noCD";
        GUI.Label(new Rect(10, 10, 920, 235), text, style);
#endif
    }
}
