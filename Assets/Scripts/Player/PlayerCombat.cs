using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCombat : MonoBehaviour
{
    [Header("Legacy References")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private GameObject weaponVisual;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private LayerMask enemyMask;

    [Header("Input")]
    [SerializeField] private KeyCode switchWeaponKey = KeyCode.Tab;

    private PlayerStats stats;
    private PlayerWeaponInventory inventory;
    private PlayerAmmoController ammo;
    private WeaponVisualController visuals;
    private WeaponAttackTimeline timeline;
    private MeleeSweepResolver sweep;
    private WeaponFirePresentationV61 firePresentation;
    private PlayerCombatSFX combatSfx;
    private Coroutine meleeRoutine;
    private Coroutine fireVisualRoutine;
    private bool isAttacking;
    private float nextAttackTime;
    private Vector2 lastAttackDirection = Vector2.down;
    private bool machineGunWasFiring;
    private float machineGunHeat;

    public bool IsAttacking => isAttacking;
    public Vector2 AimDirection => lastAttackDirection;
    public bool IsRangedMode => inventory != null && inventory.IsRangedMode;
    public WeaponDefinition CurrentWeapon => inventory != null ? inventory.CurrentWeapon : null;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        inventory = GetComponent<PlayerWeaponInventory>();
        if (inventory == null) inventory = gameObject.AddComponent<PlayerWeaponInventory>();
        ammo = GetComponent<PlayerAmmoController>();
        if (ammo == null) ammo = gameObject.AddComponent<PlayerAmmoController>();
        visuals = GetComponent<WeaponVisualController>();
        if (visuals == null) visuals = gameObject.AddComponent<WeaponVisualController>();
        timeline = GetComponent<WeaponAttackTimeline>();
        if (timeline == null) timeline = gameObject.AddComponent<WeaponAttackTimeline>();
        sweep = GetComponent<MeleeSweepResolver>();
        if (sweep == null) sweep = gameObject.AddComponent<MeleeSweepResolver>();
        firePresentation = GetComponent<WeaponFirePresentationV61>();
        if (firePresentation == null) firePresentation = gameObject.AddComponent<WeaponFirePresentationV61>();
        combatSfx = GetComponent<PlayerCombatSFX>();
        if (combatSfx == null) combatSfx = gameObject.AddComponent<PlayerCombatSFX>();
        ResolveAttackOrigin();
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.WeaponChanged += HandleWeaponChanged;
    }

    private void Start()
    {
        HandleWeaponChanged(inventory != null ? inventory.CurrentWeapon : null,
            inventory != null ? inventory.StandbyWeapon : null);
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.WeaponChanged -= HandleWeaponChanged;
        CancelCurrentAction(false);
    }

    private void Update()
    {
        machineGunHeat = Mathf.MoveTowards(machineGunHeat, 0f, Time.deltaTime * (machineGunWasFiring ? 0.30f : 1.65f));
        if (GameInputState.IsLocked)
        {
            StopMachineGunTailIfNeeded();
            return;
        }

        if (Input.GetKeyDown(switchWeaponKey))
        {
            StopMachineGunTailIfNeeded();
            CancelCurrentAction(false);
            if (ammo != null) ammo.CancelReload();
            if (inventory != null) inventory.ToggleCategory();
            nextAttackTime = Time.time;
            return;
        }

        Vector2 held = GetArrowInputDirection();
        bool down = AnyArrowKeyDown();

        if (!isAttacking && held.sqrMagnitude > 0.0001f)
        {
            lastAttackDirection = Cardinalize(held);
            if (visuals != null) visuals.UpdateAim(lastAttackDirection);
            UpdateAttackOrigin(lastAttackDirection);
        }

        WeaponDefinition weapon = CurrentWeapon;
        if (machineGunWasFiring && (weapon == null || weapon.weaponId != "machine_gun" || held.sqrMagnitude <= 0.0001f))
            StopMachineGunTailIfNeeded();
        if (weapon == null || held.sqrMagnitude <= 0.0001f || Time.time < nextAttackTime) return;

        if (weapon.category == WeaponCategory.Melee)
        {
            if (down && !isAttacking)
                meleeRoutine = StartCoroutine(MeleeAttackRoutine(weapon, lastAttackDirection));
            return;
        }

        bool fire = weapon.fireMode == WeaponFireMode.Automatic ? held.sqrMagnitude > 0.0001f : down;
        if (fire) FireRangedWeapon(weapon, lastAttackDirection);
    }

    public void CancelCurrentActionForDash()
    {
        CancelCurrentAction(true);
        nextAttackTime = Time.time;
    }

    public void CancelCurrentAction(bool keepWeaponVisible)
    {
        if (meleeRoutine != null) StopCoroutine(meleeRoutine);
        meleeRoutine = null;
        if (timeline != null) timeline.Cancel();
        if (sweep != null) sweep.Cancel();
        isAttacking = false;

        if (visuals != null)
        {
            visuals.ShowTrail(false);
            visuals.SetWeaponKick(0f);
            visuals.SetFireFrame(false);
            visuals.EndMeleeAttack();
            visuals.UnlockAim();
            if (!keepWeaponVisible) visuals.SetWeapon(CurrentWeapon, lastAttackDirection);
        }
    }

    private IEnumerator MeleeAttackRoutine(WeaponDefinition weapon, Vector2 direction)
    {
        isAttacking = true;
        float speedFactor = stats != null ? Mathf.Max(0.2f, stats.MeleeCooldown / 0.25f) : 1f;
        float windup = Mathf.Max(0.01f, weapon.meleeWindup * speedFactor);
        float active = Mathf.Max(0.01f, weapon.meleeActiveTime * speedFactor);
        float recovery = Mathf.Max(0f, weapon.meleeRecovery * speedFactor);
        nextAttackTime = Time.time + Mathf.Max(windup + active + recovery, weapon.attackInterval * speedFactor);

        Vector2 lockedDirection = Cardinalize(direction);
        if (visuals != null)
        {
            visuals.SetWeapon(weapon, lockedDirection);
            visuals.BeginMeleeAttack(lockedDirection);
        }

        float statDamageMultiplier = stats != null ? Mathf.Max(0.05f, stats.MeleeDamageMultiplier) : 1f;
        int resolvedDamage = Mathf.Max(1, Mathf.RoundToInt(weapon.damage * statDamageMultiplier));
        float knockback = weapon.knockbackBonus +
            (stats != null ? Mathf.Clamp(stats.MeleeKnockback * 0.025f, 0f, 0.25f) : 0f);

        yield return timeline.Play(
            windup,
            active,
            recovery,
            t =>
            {
                if (visuals != null) visuals.SetMeleeWindupProgress(t);
            },
            () =>
            {
                if (visuals != null)
                {
                    visuals.SetMeleeActiveProgress(0f);
                    visuals.ShowTrail(true);
                    visuals.SetTrailAlpha(1f);
                }
                if (combatSfx != null) combatSfx.PlayMeleeSwing(weapon);
                if (sweep != null) sweep.Begin(weapon, visuals, resolvedDamage, knockback);
            },
            t =>
            {
                if (visuals != null)
                {
                    visuals.SetMeleeActiveProgress(t);
                    visuals.SetTrailAlpha(1f - t * 0.35f);
                }
                if (sweep != null) sweep.Sample(false);
            },
            () =>
            {
                if (sweep != null) sweep.End();
                if (visuals != null) visuals.ShowTrail(false);
            },
            t =>
            {
                if (visuals != null) visuals.SetMeleeRecoveryProgress(t);
            },
            () =>
            {
                isAttacking = false;
                meleeRoutine = null;
                if (visuals != null)
                {
                    visuals.EndMeleeAttack();
                    visuals.UnlockAim();
                    visuals.SetWeapon(weapon, lockedDirection);
                }
            },
            () => GameInputState.IsLocked || !isActiveAndEnabled);

        if (isAttacking)
        {
            if (sweep != null) sweep.Cancel();
            if (visuals != null)
            {
                visuals.ShowTrail(false);
                visuals.EndMeleeAttack();
                visuals.UnlockAim();
            }
            isAttacking = false;
            meleeRoutine = null;
        }
    }

    private void FireRangedWeapon(WeaponDefinition weapon, Vector2 direction)
    {
        if (weapon == null || ammo == null || !ammo.TryConsumeShot(weapon))
        {
            if (weapon != null && weapon.weaponId == "machine_gun") StopMachineGunTailIfNeeded();
            return;
        }

        Vector2 lockedDirection = Cardinalize(direction);
        lastAttackDirection = lockedDirection;
        if (visuals != null) visuals.UpdateAim(lockedDirection);

        float speedFactor = stats != null ? Mathf.Max(0.2f, stats.RangedCooldown / 0.15f) : 1f;
        nextAttackTime = Time.time + Mathf.Max(weapon.minimumAttackInterval, weapon.attackInterval * speedFactor);
        float statDamageMultiplier = stats != null ? Mathf.Max(0.05f, stats.RangedDamageMultiplier) : 1f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(weapon.damage * statDamageMultiplier));
        Vector2 origin = visuals != null
            ? visuals.GetMuzzleWorldPosition()
            : (Vector2)transform.position + lockedDirection * 0.25f;

        if (combatSfx != null) combatSfx.PlayRangedFire(weapon);
        machineGunWasFiring = weapon.weaponId == "machine_gun";

        if (weapon.rangedMode == RangedAttackMode.HitscanBeam)
            FireBeam(weapon, origin, lockedDirection, damage);
        else if (weapon.rangedMode == RangedAttackMode.Shotgun)
            FireShotgun(weapon, origin, lockedDirection, damage);
        else
        {
            float spread = weapon.spreadDegrees;
            if (weapon.weaponId == "machine_gun")
            {
                machineGunHeat = Mathf.Clamp01(machineGunHeat + 0.072f);
                float hot = Mathf.InverseLerp(0.36f, 1f, machineGunHeat);
                spread += Mathf.Lerp(0f, 2.15f, hot);
            }
            SpawnProjectile(weapon, origin, ApplySpread(lockedDirection, spread), damage);
        }

        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null)
        {
            feedback.AddRecoil(lockedDirection, weapon.cameraKick);
            if (weapon.weaponId == "machine_gun")
                feedback.Impact(CameraImpactLevelV11.Small, -lockedDirection, false);
            else if (weapon.rangedMode == RangedAttackMode.Shotgun)
                feedback.Impact(CameraImpactLevelV11.Medium, -lockedDirection, false);
            else if (weapon.cameraShake > 0f)
                feedback.Shake(0.045f, weapon.cameraShake, -lockedDirection);
        }

        if (firePresentation != null)
            firePresentation.PlayFire(weapon, visuals, lockedDirection);

        if (fireVisualRoutine != null) StopCoroutine(fireVisualRoutine);
        fireVisualRoutine = StartCoroutine(FireVisualRoutine(weapon));
    }

    private Vector2 ApplySpread(Vector2 direction, float spread)
    {
        if (spread <= 0.01f) return direction.normalized;
        float angle = Random.Range(-spread * 0.5f, spread * 0.5f);
        return (Vector2)(Quaternion.Euler(0f, 0f, angle) * direction);
    }

    private void FireShotgun(WeaponDefinition weapon, Vector2 origin, Vector2 direction, int damage)
    {
        int count = Mathf.Max(1, weapon.pelletCount);
        for (int i = 0; i < count; i++)
        {
            float angle = count <= 1
                ? 0f
                : Mathf.Lerp(-weapon.spreadDegrees * 0.5f, weapon.spreadDegrees * 0.5f, i / (float)(count - 1));
            angle += Random.Range(-0.5f, 0.5f);
            SpawnProjectile(weapon, origin, (Vector2)(Quaternion.Euler(0f, 0f, angle) * direction), damage);
        }
    }

    private void SpawnProjectile(WeaponDefinition weapon, Vector2 origin, Vector2 direction, int damage)
    {
        PlayerProjectile projectile = PlayerProjectile.Acquire(bulletPrefab, origin);

        int pierce = weapon.pierceCount + (stats != null ? Mathf.Max(0, stats.ProjectilePierce) : 0);
        projectile.Configure(new PlayerProjectileContext(
            weapon,
            direction.normalized,
            origin,
            damage,
            weapon.projectileSpeed,
            pierce));
    }

    private void FireBeam(WeaponDefinition weapon, Vector2 origin, Vector2 direction, int baseDamage)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        // Gameplay range is not capped. The first wall or room boundary ends the beam.
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, safeDirection, Mathf.Infinity, Physics2D.AllLayers);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();
        Vector2 end = origin + safeDirection * Mathf.Max(64f, weapon.beamRange);
        int index = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider.GetComponentInParent<PlayerHealth>() != null)
                continue;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                if (enemy.IsDead || !damaged.Add(enemy))
                    continue;

                float distanceFalloff = CalculateRangedFalloffMultiplier(weapon, hit.distance);
                float targetFalloff = Mathf.Pow(Mathf.Clamp(weapon.beamPerTargetMultiplier, 0.1f, 1f), index);
                int resolved = Mathf.Max(1, Mathf.RoundToInt(baseDamage * distanceFalloff * targetFalloff));
                bool lethal = resolved >= enemy.CurrentHealth;
                EnemyRole role = enemy.GetComponentInParent<EnemyRole>();
                bool tank = role != null && role.CurrentRole == EnemyRole.Role.Tank;
                enemy.TakeDamage(new DamageContext(resolved, safeDirection, hit.point, EnemyHitKind.Ranged, weapon.knockbackBonus));
                CombatImpactFXV11.EmitWeaponHit(weapon, hit.point, safeDirection, lethal, tank, hit.distance);
                if (combatSfx != null) combatSfx.PlayRangedImpact(weapon, enemy, hit.point);
                index++;
                if (index >= weapon.beamMaxTargets)
                {
                    end = hit.point;
                    break;
                }
            }
            else if (IsWall(hit.collider.gameObject))
            {
                end = hit.point;
                WallImpactSystem.Emit(hit.point, hit.normal, safeDirection, baseDamage, weapon, hit.collider);
                break;
            }
        }

        BeamVisual.Spawn(origin, end, weapon);
    }

    private static float CalculateRangedFalloffMultiplier(WeaponDefinition weapon, float distance)
    {
        if (weapon == null)
            return 1f;

        float start = Mathf.Max(0f, weapon.falloffStartDistance);
        // maximumRange is retained for asset compatibility, but now means the distance
        // at which damage reaches its minimum instead of a projectile despawn range.
        float end = Mathf.Max(start + 0.01f, weapon.maximumRange);
        float t = Mathf.InverseLerp(start, end, Mathf.Max(0f, distance));
        return Mathf.Lerp(1f, Mathf.Clamp(weapon.minimumDamageMultiplier, 0.05f, 1f), t);
    }

    private IEnumerator FireVisualRoutine(WeaponDefinition weapon)
    {
        if (visuals == null) yield break;

        visuals.SetFireFrame(true);
        float duration = Mathf.Max(0.02f, weapon.fireSpriteDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            visuals.SetWeaponKick(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        visuals.SetWeaponKick(0f);
        visuals.SetFireFrame(false);
        fireVisualRoutine = null;
    }

    private void HandleWeaponChanged(WeaponDefinition active, WeaponDefinition standby)
    {
        StopMachineGunTailIfNeeded();
        CancelCurrentAction(false);
        nextAttackTime = Time.time;
        if (visuals != null) visuals.SetWeapon(active, lastAttackDirection);
    }


    private void StopMachineGunTailIfNeeded()
    {
        if (!machineGunWasFiring) return;
        machineGunWasFiring = false;
        if (combatSfx != null) combatSfx.PlayMachineGunStop();
    }

    private void ResolveAttackOrigin()
    {
        if (attackOrigin != null) return;
        GameObject gameObject = new GameObject("AttackOrigin");
        gameObject.transform.SetParent(transform, false);
        attackOrigin = gameObject.transform;
    }

    private void UpdateAttackOrigin(Vector2 direction)
    {
        if (attackOrigin != null)
            attackOrigin.position = (Vector2)transform.position + direction.normalized * 0.12f;
    }

    private static Vector2 Cardinalize(Vector2 direction)
    {
        return Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
            ? (direction.x >= 0f ? Vector2.right : Vector2.left)
            : (direction.y >= 0f ? Vector2.up : Vector2.down);
    }

    private static Vector2 GetArrowInputDirection()
    {
        Vector2 direction = Vector2.zero;
        if (Input.GetKey(KeyCode.LeftArrow)) direction.x--;
        if (Input.GetKey(KeyCode.RightArrow)) direction.x++;
        if (Input.GetKey(KeyCode.DownArrow)) direction.y--;
        if (Input.GetKey(KeyCode.UpArrow)) direction.y++;
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
    }

    private static bool AnyArrowKeyDown()
    {
        return Input.GetKeyDown(KeyCode.LeftArrow) ||
               Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.DownArrow) ||
               Input.GetKeyDown(KeyCode.UpArrow);
    }

    private static bool IsWall(GameObject target)
    {
        if (target == null) return false;
        int wall = LayerMask.NameToLayer("Wall");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int boundary = LayerMask.NameToLayer("RoomBoundary");
        if (target.layer == wall || target.layer == obstacle || target.layer == boundary) return true;
        string lower = target.name.ToLowerInvariant();
        return lower.Contains("wall") || lower.Contains("boundary");
    }
}
