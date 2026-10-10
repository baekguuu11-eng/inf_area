using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChernobylBossController
{
    private BossOpeningWindowV15 openingV15;
    private int attacksSinceGuardsV15;
    private int attacksSinceComboV15;
    private bool guardSectionV15;
    private float nextHitSoundV15;
    private float nextHeavyFeedbackV15;

    private IEnumerator CoreOpeningV15(float duration)
    {
        ClearAllGridWarningsV11();
        if (openingV15 == null || dead) yield break;
        // A punish window is a reward: neither ordinary counters nor reinforcements may steal it.
        counterResponseRequestedV10 = false;
        sustainedAttackTimeV10 = 0f;
        sustainedDamageV11 = 0f;
        bool staggered = openingV15.Open(duration);
        if (staggered)
        {
            ChernobylBossEffects.SpawnCoreSparks(transform.position, CurrentHazardColor(), 14, 2.8f);
            PlaySfx(overloadSfx, 0.40f, 0.88f);
            if (GameFeelManager.Instance != null) GameFeelManager.Instance.DoHitStop(0.035f);
        }
        while (!dead && openingV15.IsOpen && (playerHealth == null || !playerHealth.IsDead))
        {
            if (coreRenderer != null) coreRenderer.color = staggered ? new Color(1f, 0.85f, 0.35f) :
                Color.Lerp(CurrentHazardColor(), Color.white, 0.35f);
            yield return null;
        }
        openingV15.Close();
        if (!dead) ApplyPhaseVisuals();
        counterCooldownUntilV10 = Mathf.Max(counterCooldownUntilV10, Time.time + 0.9f);
    }

    private IEnumerator GuardSectionV15()
    {
        guardSectionV15 = true;
        ClearAllGridWarningsV11();
        // Dedicated guard section: only a single locked target circle supports the guards.
        // No dense grid, no third attack channel, no reinforcement stacking.
        yield return WaitCombatSeconds(1.05f);
        int initialGuards = guardDirectorV13.ActiveGuardCount;
        float endsAt = Time.time + 18f; // failsafe only, not the optimal way to finish
        float nextTarget = Time.time + 1.8f;
        while (!dead && guardDirectorV13.ActiveGuardCount > 0 && Time.time < endsAt &&
            !transitionRequested && !meltdownRequested && (playerHealth == null || !playerHealth.IsDead))
        {
            if (Time.time >= nextTarget)
            {
                yield return ExecuteCircleTargetV11(ClampArenaPoint(GetPlayerPosition(), 0.6f), 0.90f, 1.1f, 0.46f);
                nextTarget = Time.time + 2.2f;
            }
            yield return null;
        }
        bool defeated = initialGuards > 0 && guardDirectorV13.ActiveGuardCount == 0;
        guardDirectorV13.RecallForRecoveryV15();
        // Let declared death bursts finish, then remove only this boss room's remaining hazards.
        yield return WaitCombatSeconds(0.9f);
        ClearGuardHazardsV15();
        if (defeated && !dead && !transitionRequested && !meltdownRequested)
        {
            ChernobylBossEffects.SpawnCoreSparks(transform.position, Color.white, 18, 2.8f);
            PlaySfx(overloadSfx, 0.5f, 0.86f);
            yield return WaitCombatSeconds(0.7f);
        }
        guardSectionV15 = false;
    }

    private void ClearGuardHazardsV15()
    {
        if (ownerRoom == null) return;
        EnemyProjectile[] bullets = ownerRoom.GetComponentsInChildren<EnemyProjectile>(true);
        foreach (EnemyProjectile bullet in bullets) if (bullet != null) Destroy(bullet.gameObject);
        ChernobylGuardDeathBurst[] bursts = ownerRoom.GetComponentsInChildren<ChernobylGuardDeathBurst>(true);
        foreach (ChernobylGuardDeathBurst burst in bursts) if (burst != null) Destroy(burst.gameObject);
    }

    private IEnumerator IntentionalComboV15()
    {
        // Two comprehensible decisions with a fresh warning for each. Never overlay independent coroutines.
        debugLastComboV11 = phase == 2 ? "V15 LOCK THEN CROSS" : "V15 SAFE THEN PURGE";
        if (phase == 2)
        {
            RememberPatternV11(PatternKind.TwinTarget);
            yield return TwinTargetRoutineV11();
            yield return WaitCombatSeconds(0.42f);
            if (!dead) yield return CrossBlastRoutine();
        }
        else
        {
            RememberPatternV11(PatternKind.SafeShift);
            yield return SafeShiftRoutineV10();
            yield return WaitCombatSeconds(0.48f);
            if (!dead) yield return CoreShockwaveRoutine();
        }
    }

    // Only accept a safe block if the player's full body fits and a direct swept path is clear.
    // If no such path exists, caller uses a small target attack instead of forcing a bad safe zone.
    private bool TryChooseSafeBlockV15(List<GridCell> cells, List<int[]> sets, int excluded,
        float warning, out int chosen)
    {
        chosen = -1;
        Vector2 start = GetPlayerPosition();
        Collider2D body = playerHealth != null ? playerHealth.GetComponent<Collider2D>() : null;
        float radius = body != null ? Mathf.Max(body.bounds.extents.x, body.bounds.extents.y) + 0.09f : 0.35f;
        PlayerStats stats = playerHealth != null ? playerHealth.GetComponent<PlayerStats>() : null;
        float speed = stats != null ? Mathf.Max(0.1f, stats.MoveSpeed) : 5f;
        float reach = speed * Mathf.Max(0.1f, warning - 0.25f) * 0.85f;
        List<int> options = new List<int>();
        int mask = LayerMask.GetMask("Wall", "Obstacle", "RoomBoundary", "Enemy");
        for (int i = 0; i < sets.Count; i++)
        {
            GridCell first = cells[sets[i][0]], last = cells[sets[i][sets[i].Length - 1]];
            Vector2 min = (Vector2)first.Center - first.Size * 0.5f + Vector2.one * radius;
            Vector2 max = (Vector2)last.Center + last.Size * 0.5f - Vector2.one * radius;
            if (min.x > max.x || min.y > max.y) continue;
            Vector2 end = new Vector2(Mathf.Clamp(start.x, min.x, max.x), Mathf.Clamp(start.y, min.y, max.y));
            Vector2 delta = end - start;
            if (delta.magnitude > reach) continue;
            bool blocked = false;
            Collider2D[] atEnd = Physics2D.OverlapCircleAll(end, radius, mask);
            foreach (Collider2D hit in atEnd)
                if (!hit.isTrigger && hit.GetComponentInParent<PlayerHealth>() == null) { blocked = true; break; }
            if (blocked) continue;
            if (delta.sqrMagnitude > 0.001f)
            {
                RaycastHit2D[] path = Physics2D.CircleCastAll(start, radius, delta.normalized, delta.magnitude, mask);
                foreach (RaycastHit2D hit in path)
                    if (hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<PlayerHealth>() == null)
                    { blocked = true; break; }
            }
            if (!blocked) options.Add(i);
        }
        if (options.Count > 1) options.Remove(excluded);
        if (options.Count == 0) return false;
        chosen = options[Random.Range(0, options.Count)];
        return true;
    }
}
