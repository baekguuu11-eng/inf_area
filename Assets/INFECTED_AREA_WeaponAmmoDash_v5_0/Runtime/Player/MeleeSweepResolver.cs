using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MeleeSweepResolver : MonoBehaviour
{
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private bool drawRuntimeDebug;

    private const int MaxOverlapResults = 64;
    private readonly HashSet<EnemyHealth> hitTargets = new HashSet<EnemyHealth>();
    private readonly List<Vector2> debugPoints = new List<Vector2>();
    private readonly Vector2[] previousTracks = new Vector2[3];
    private readonly Vector2[] currentTracks = new Vector2[3];
    private readonly Collider2D[] overlapResults = new Collider2D[MaxOverlapResults];

    private WeaponDefinition weapon;
    private WeaponVisualController visuals;
    private int damage;
    private float knockback;
    private bool hasPrevious;
    private float playerHeight;
    private ContactFilter2D contactFilter;
    private PlayerCombatSFX combatSfx;

    public void Begin(WeaponDefinition definition, WeaponVisualController visualController, int resolvedDamage, float resolvedKnockback)
    {
        weapon = definition;
        if (combatSfx == null) combatSfx = GetComponent<PlayerCombatSFX>();
        visuals = visualController;
        damage = Mathf.Max(1, resolvedDamage);
        knockback = Mathf.Max(0f, resolvedKnockback);
        hitTargets.Clear();
        debugPoints.Clear();
        hasPrevious = false;

        SpriteRenderer body = FindPlayerBody();
        playerHeight = body != null ? Mathf.Max(0.3f, body.bounds.size.y) : 0.8f;

        contactFilter = new ContactFilter2D();
        contactFilter.useLayerMask = true;
        contactFilter.SetLayerMask(EffectiveMask());
        contactFilter.useTriggers = true;

        Sample(false);
    }

    public void Sample(bool finalSample)
    {
        if (weapon == null || visuals == null) return;

        ResolveTrackPoints(currentTracks);
        float radius = Mathf.Max(0.015f, playerHeight * weapon.sweepThicknessPlayerRatio * 0.5f);

        if (!hasPrevious)
        {
            for (int i = 0; i < currentTracks.Length; i++)
            {
                previousTracks[i] = currentTracks[i];
                DamageCircle(currentTracks[i], radius);
                debugPoints.Add(currentTracks[i]);
            }

            DamageCapsule(currentTracks[0], currentTracks[2], radius * 0.8f);
            hasPrevious = true;
            return;
        }

        for (int i = 0; i < currentTracks.Length; i++)
        {
            DamageCapsule(previousTracks[i], currentTracks[i], radius);
            previousTracks[i] = currentTracks[i];
            debugPoints.Add(currentTracks[i]);
        }

        // 현재 프레임의 무기 전체 길이도 덮어 시각 궤적과 실제 판정을 일치시킨다.
        DamageCapsule(currentTracks[0], currentTracks[2], radius * 0.8f);

        if (weapon.meleeHitShape == MeleeHitShape.ImpactCircle && finalSample)
            DamageCircle(currentTracks[2], Mathf.Max(radius, playerHeight * weapon.impactRadiusPlayerRatio));
    }

    public void End()
    {
        Sample(true);
        weapon = null;
        visuals = null;
        hasPrevious = false;
    }

    public void Cancel()
    {
        weapon = null;
        visuals = null;
        hitTargets.Clear();
        hasPrevious = false;
        debugPoints.Clear();
    }

    private void ResolveTrackPoints(Vector2[] output)
    {
        float a = 0.45f;
        float b = 0.72f;
        float c = 1f;

        if (weapon != null && weapon.weaponId == "debug_whip")
        {
            a = 0.30f;
            b = 0.62f;
            c = 1f;
        }
        else if (weapon != null && weapon.weaponId == "debug_hammer")
        {
            a = 0.58f;
            b = 0.80f;
            c = 1f;
        }

        output[0] = visuals.GetAttackPointWorld(a);
        output[1] = visuals.GetAttackPointWorld(b);
        output[2] = visuals.GetAttackPointWorld(c);
    }

    private void DamageCapsule(Vector2 a, Vector2 b, float radius)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.001f)
        {
            DamageCircle(b, radius);
            return;
        }

        Vector2 center = (a + b) * 0.5f;
        Vector2 size = new Vector2(length + radius * 2f, radius * 2f);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        int count = Physics2D.OverlapCapsule(center, size, CapsuleDirection2D.Horizontal, angle, contactFilter, overlapResults);
        Damage(overlapResults, count, b);
    }

    private void DamageCircle(Vector2 center, float radius)
    {
        int count = Physics2D.OverlapCircle(center, radius, contactFilter, overlapResults);
        Damage(overlapResults, count, center);
    }

    private int EffectiveMask()
    {
        if (enemyMask.value != 0)
            return enemyMask.value;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        return enemyLayer >= 0 ? 1 << enemyLayer : Physics2D.AllLayers;
    }

    private void Damage(Collider2D[] hits, int count, Vector2 hitPoint)
    {
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            hits[i] = null;

            EnemyHealth health = hit != null ? hit.GetComponentInParent<EnemyHealth>() : null;
            if (health == null || health.IsDead || !hitTargets.Add(health)) continue;

            Vector2 dir = ((Vector2)health.transform.position - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude < 0.001f)
                dir = visuals != null ? visuals.AimDirection : Vector2.right;

            Vector2 trueHitPoint = hit != null ? hit.ClosestPoint(hitPoint) : hitPoint;
            bool lethal = damage >= health.CurrentHealth;
            EnemyRole role = health.GetComponentInParent<EnemyRole>();
            bool tank = role != null && role.CurrentRole == EnemyRole.Role.Tank;
            health.TakeDamage(new DamageContext(damage, dir, trueHitPoint, EnemyHitKind.Melee, knockback));
            CombatImpactFXV11.EmitWeaponHit(weapon, trueHitPoint, dir, lethal, tank, 0f);
            if (combatSfx != null) combatSfx.PlayMeleeImpact(weapon, health, trueHitPoint);
            if (!health.IsDead && weapon != null && weapon.weaponId == "debug_whip")
            {
                // V14 DATA BIND: whip hits slow movement but never slow attack animations/timers.
                EnemySlowStatusV14 slow = health.GetComponent<EnemySlowStatusV14>();
                if (slow == null) slow = health.gameObject.AddComponent<EnemySlowStatusV14>();
                slow.Apply(tank ? 0.85f : 0.70f, tank ? 1.2f : 1.5f);
            }

            if (!health.IsDead && weapon != null && weapon.burnDamagePerTick > 0 && weapon.burnTicks > 0)
            {
                EnemyBurnStatus burn = health.GetComponent<EnemyBurnStatus>();
                if (burn == null) burn = health.gameObject.AddComponent<EnemyBurnStatus>();
                burn.Apply(weapon.burnDamagePerTick, weapon.burnTicks, weapon.burnTickInterval, dir);
            }
        }
    }

    private SpriteRenderer FindPlayerBody()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer best = null;
        float area = 0f;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null) continue;
            string lower = renderer.gameObject.name.ToLowerInvariant();
            if (lower.Contains("weapon") || lower.Contains("trail") || lower.Contains("grip") ||
                lower.Contains("shadow") || lower.Contains("hand") || lower.Contains("arm"))
                continue;

            float currentArea = renderer.bounds.size.x * renderer.bounds.size.y;
            if (currentArea > area)
            {
                area = currentArea;
                best = renderer;
            }
        }
        return best;
    }

    private void OnDrawGizmos()
    {
        if (!drawRuntimeDebug || debugPoints.Count < 2) return;
        Gizmos.color = Color.green;
        for (int i = 1; i < debugPoints.Count; i++)
            Gizmos.DrawLine(debugPoints[i - 1], debugPoints[i]);
    }
}
