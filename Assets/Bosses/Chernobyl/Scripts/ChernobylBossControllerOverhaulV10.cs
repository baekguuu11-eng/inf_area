using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

/// <summary>
/// V11 Chernobyl spatial-control overhaul.
/// Kept as a partial of the existing boss controller so the runtime factory, HUD, music,
/// rewards, intro and existing room flow remain intact.
/// </summary>
public sealed partial class ChernobylBossController
{
    private enum ComboKindV11
    {
        P1_OuterTarget,
        P1_CrossGap,
        P1_CheckerInvert,
        P2_OuterCrossTarget,
        P2_SafeSweep,
        P2_CompressShockwave,
        P2_CheckerDelayed,
        P2_LaneTarget,
        P3_ReactorCycle,
        P3_MeltdownCorridor,
        P3_TimeControl,
        P3_SafeRelay,
        P3_SpiralMeltdown,
        P3_RotatingPressure
    }

    private readonly List<PatternKind> recentPatternHistoryV10 = new List<PatternKind>(6);
    private readonly List<ComboKindV11> recentComboHistoryV11 = new List<ComboKindV11>(3);

    private float lastSustainedHitTimeV10 = -99f;
    private float sustainedAttackTimeV10;
    private float sustainedDamageV11;
    private float lastAttackDistanceV10 = 4f;
    private float counterCooldownUntilV10;
    private bool counterResponseRequestedV10;
    private bool counterNearV10;

    private ChernobylCameraFX cameraFxV10;
    private Light2D coreLightV10;
    private Light2D globalFillLightV10;
    private readonly List<SpriteRenderer> arenaGridLinesV10 = new List<SpriteRenderer>();

    private float nextHitFlashTimeV11;
    private Coroutine hitFeedbackRoutineV11;
    private Vector3 visualBaseScaleV11 = Vector3.one;
    private System.IDisposable antiBurstInputLockV11;

    private int debugForcedPatternV11 = -1;
    private int debugForcedComboV11 = -1;
    private int debugSelectedPatternIndexV11;
    private int debugSelectedComboIndexV11;
    private bool debugIgnoreCooldownV11;
    private bool debugBossInvulnerableV11;
    private bool debugPlayerInvulnerableV11;
    private float nextDebugPlayerInvulnGrantV11;
    private string debugLastPatternV11 = "-";
    private string debugLastComboV11 = "-";

    public string DebugGridDescriptorV11
    {
        get
        {
            int c, r;
            GetGridDimensionsV11(out c, out r);
            return c + "x" + r;
        }
    }
    public string DebugSelectedPatternNameV11 => GetDebugPatternV11().ToString();
    public string DebugSelectedComboNameV11 => GetDebugComboV11().ToString();
    public string DebugLastPatternNameV11 => debugLastPatternV11;
    public string DebugLastComboNameV11 => debugLastComboV11;
    public bool DebugBossInvulnerableV11 => debugBossInvulnerableV11;
    public bool DebugPlayerInvulnerableV11 => debugPlayerInvulnerableV11;
    public bool DebugIgnoreCooldownV11 => debugIgnoreCooldownV11;
    public int DebugGridWarningCountV11 => CountGridWarningsV11();

    private void InitializeOverhaulV10()
    {
        cameraFxV10 = ChernobylCameraFX.CreateOrGet();
        SetupCoreLightV10();
        visualBaseScaleV11 = visualRoot != null ? visualRoot.localScale : Vector3.one;

        // V11: no persistent arena grid. The old CHN_FixedArenaGrid was the source of
        // "grid visible while idle". Only attack-specific telegraphs are created now.
        arenaGridLinesV10.Clear();
        ClearAllGridWarningsV11();
    }

    // Kept for binary/source compatibility, but deliberately unused in V11.
    private void CreateArenaGridVisualV10()
    {
        arenaGridLinesV10.Clear();
    }

    private void SetupCoreLightV10()
    {
        if (coreRenderer == null) return;

        Light2D[] sceneLights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light2D candidate = sceneLights[i];
            if (candidate == null || candidate == coreLightV10) continue;
            if (candidate.lightType == Light2D.LightType.Global && candidate.enabled)
            {
                globalFillLightV10 = candidate;
                break;
            }
        }

        if (globalFillLightV10 == null)
        {
            Transform globalExisting = transform.Find("CHN_GlobalFillLight");
            GameObject globalObject;
            if (globalExisting != null) globalObject = globalExisting.gameObject;
            else
            {
                globalObject = new GameObject("CHN_GlobalFillLight");
                globalObject.hideFlags = HideFlags.DontSave;
                globalObject.transform.SetParent(transform, false);
            }
            globalFillLightV10 = globalObject.GetComponent<Light2D>();
            if (globalFillLightV10 == null) globalFillLightV10 = globalObject.AddComponent<Light2D>();
            globalFillLightV10.lightType = Light2D.LightType.Global;
            globalFillLightV10.blendStyleIndex = 0;
            globalFillLightV10.intensity = 1.0f;
            globalFillLightV10.color = Color.white;
            globalFillLightV10.enabled = true;
        }

        Transform existing = coreRenderer.transform.Find("CHN_CoreLight");
        GameObject lightObject;
        if (existing != null) lightObject = existing.gameObject;
        else
        {
            lightObject = new GameObject("CHN_CoreLight");
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.transform.SetParent(coreRenderer.transform, false);
        }

        coreLightV10 = lightObject.GetComponent<Light2D>();
        if (coreLightV10 == null) coreLightV10 = lightObject.AddComponent<Light2D>();
        coreLightV10.lightType = Light2D.LightType.Point;
        coreLightV10.blendStyleIndex = 0;
        coreLightV10.intensity = 0.24f;
        coreLightV10.color = CurrentHazardColor();
        coreLightV10.enabled = true;
    }

    private void UpdateCoreLightV10()
    {
        if (coreLightV10 == null) return;
        float pulseSpeed = phase == 1 ? 2.5f : phase == 2 ? 4.2f : 6.8f;
        float pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f;
        float baseIntensity = phase == 1 ? 0.22f : phase == 2 ? 0.32f : 0.46f;
        coreLightV10.intensity = baseIntensity + pulse * (phase == 1 ? 0.06f : phase == 2 ? 0.10f : 0.16f);
        coreLightV10.color = CurrentHazardColor();

        // Defensive cleanup for projects upgraded from V10 while the old runtime grid still exists.
        for (int i = 0; i < arenaGridLinesV10.Count; i++)
            if (arenaGridLinesV10[i] != null) arenaGridLinesV10[i].enabled = false;
    }

    // -------------------------------------------------------------------------
    // Hit feedback
    // -------------------------------------------------------------------------

    private void TriggerHitFeedbackV11(int damage, Vector2 direction)
    {
        if (dead || state == BossState.Intro || state == BossState.Dormant) return;

        int heavyThreshold = health != null ? Mathf.Max(20, Mathf.RoundToInt(health.MaxHealth * 0.009f)) : 24;
        bool heavy = damage >= heavyThreshold;

        if (Time.unscaledTime >= nextHitFlashTimeV11)
        {
            nextHitFlashTimeV11 = Time.unscaledTime + (heavy ? 0.045f : 0.070f);
            if (hitFeedbackRoutineV11 != null) StopCoroutine(hitFeedbackRoutineV11);
            hitFeedbackRoutineV11 = StartCoroutine(HitFeedbackRoutineV11(heavy));
        }

        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.08f,
            CurrentHazardColor(), heavy ? 8 : 3, heavy ? 2.9f : 1.8f);
        if (heavy) PlaySfx(heavyHitSfxV11, 0.32f, 0.94f);
        else PlaySfx(hitSfxV11, 0.16f, Random.Range(0.97f, 1.04f));

        if (heavy)
        {
            if (GameFeelManager.Instance != null)
                GameFeelManager.Instance.DoHitStop(damage >= heavyThreshold * 2 ? 0.030f : 0.020f);
            CameraFeedbackController feedback = CameraFeedbackController.Instance;
            if (feedback != null)
                feedback.Impact(CameraImpactLevelV11.Small, direction.sqrMagnitude > 0.001f ? direction : Vector2.down, false);
            if (cameraFxV10 != null) cameraFxV10.PulseExplosion(0.22f);
        }
    }

    private IEnumerator HitFeedbackRoutineV11(bool heavy)
    {
        float flashDuration = heavy ? 0.070f : 0.050f;
        if (coreRenderer != null) coreRenderer.color = Color.white;
        for (int i = 0; i < structureRenderers.Length; i++)
        {
            SpriteRenderer r = structureRenderers[i];
            if (r == null || r.gameObject.name == "InnerCavity" || r.gameObject.name == "CoreBack") continue;
            Color c = r.color;
            r.color = Color.Lerp(c, Color.white, heavy ? 0.72f : 0.48f);
        }

        float elapsed = 0f;
        float compress = heavy ? 0.972f : 0.986f;
        float overshoot = heavy ? 1.010f : 1.005f;
        while (elapsed < 0.125f && !dead)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.125f);
            float scale;
            if (t < 0.36f) scale = Mathf.Lerp(1f, compress, t / 0.36f);
            else if (t < 0.68f) scale = Mathf.Lerp(compress, overshoot, (t - 0.36f) / 0.32f);
            else scale = Mathf.Lerp(overshoot, 1f, (t - 0.68f) / 0.32f);
            if (visualRoot != null) visualRoot.localScale = visualBaseScaleV11 * scale;

            if (elapsed >= flashDuration)
                ApplyPhaseVisuals();
            yield return null;
        }
        if (visualRoot != null) visualRoot.localScale = visualBaseScaleV11;
        ApplyPhaseVisuals();
        hitFeedbackRoutineV11 = null;
    }

    // -------------------------------------------------------------------------
    // Anti-burst / sustained pressure
    // -------------------------------------------------------------------------

    private void RegisterSustainedHitV10(int damage)
    {
        if (!combatStarted || dead) return;
        lastSustainedHitTimeV10 = Time.time;
        lastAttackDistanceV10 = Vector2.Distance(GetPlayerPosition(), transform.position);
        sustainedDamageV11 += Mathf.Max(0, damage);
    }

    private void UpdateSustainedPressureV10()
    {
        if (!combatStarted || state != BossState.Combat || dead)
        {
            sustainedAttackTimeV10 = Mathf.MoveTowards(sustainedAttackTimeV10, 0f, Time.deltaTime * 2.0f);
            sustainedDamageV11 = Mathf.MoveTowards(sustainedDamageV11, 0f, Time.deltaTime * 90f);
            return;
        }

        bool recentlyHit = Time.time - lastSustainedHitTimeV10 <= 0.34f;
        if (recentlyHit && !counterResponseRequestedV10 && Time.time >= counterCooldownUntilV10)
            sustainedAttackTimeV10 += Time.deltaTime;
        else
        {
            sustainedAttackTimeV10 = Mathf.MoveTowards(sustainedAttackTimeV10, 0f, Time.deltaTime * 1.5f);
            if (!recentlyHit) sustainedDamageV11 = Mathf.MoveTowards(sustainedDamageV11, 0f, Time.deltaTime * 65f);
        }

        float timeThreshold = phase == 1 ? 7.6f : phase == 2 ? 6.1f : 4.8f;
        float damageRatio = phase == 1 ? 0.135f : phase == 2 ? 0.112f : 0.092f;
        float damageThreshold = health != null ? health.MaxHealth * damageRatio : BossMaxHealth * damageRatio;

        if (!counterResponseRequestedV10 && Time.time >= counterCooldownUntilV10 &&
            (sustainedAttackTimeV10 >= timeThreshold || sustainedDamageV11 >= damageThreshold))
        {
            counterNearV10 = lastAttackDistanceV10 <= 2.85f;
            counterResponseRequestedV10 = true;
            sustainedAttackTimeV10 = 0f;
            sustainedDamageV11 = 0f;
            ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.1f, CurrentHazardColor(), 12 + phase * 4, 3.1f);
            PlaySfx(antiBurstChargeSfxV11, 0.44f, phase >= 3 ? 1.05f : 0.98f);
            if (cameraFxV10 != null) cameraFxV10.PulseCounter(0.55f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugPlayerInvulnerableV11 && playerHealth != null && !playerHealth.IsDead && Time.unscaledTime >= nextDebugPlayerInvulnGrantV11)
        {
            nextDebugPlayerInvulnGrantV11 = Time.unscaledTime + 0.16f;
            playerHealth.GrantTemporaryInvulnerability(0.25f);
        }
        if (hurtbox != null)
        {
            if (debugBossInvulnerableV11) hurtbox.enabled = false;
            else if (combatStarted && state == BossState.Combat && !dead) hurtbox.enabled = true;
        }
#endif
    }

    private IEnumerator RunCounterResponseV10()
    {
        if (!counterResponseRequestedV10 || dead) yield break;
        ClearAllGridWarningsV11();
        counterResponseRequestedV10 = false;
        counterCooldownUntilV10 = Time.time + (phase == 1 ? 8.2f : phase == 2 ? 6.8f : 5.4f);
        if (counterNearV10) yield return CorePurgeCounterRoutineV10();
        else yield return ContainmentCounterRoutineV10();
        ClearAllGridWarningsV11();
    }

    private IEnumerator CorePurgeCounterRoutineV10()
    {
        Color color = CurrentHazardColor();
        float radius = phase == 1 ? 2.00f : phase == 2 ? 2.22f : 2.42f;
        float warning = phase == 1 ? 1.00f : phase == 2 ? 0.82f : 0.68f;
        GameObject mark = ChernobylBossEffects.CreateCircleTelegraph(transform.position, radius, warning, color, 18);
        RegisterSpawnedObject(mark);
        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.08f, color, 16 + phase * 4, 3.8f);
        PlaySfx(antiBurstChargeSfxV11, 0.55f, phase >= 3 ? 0.92f : 1.00f);
        if (cameraFxV10 != null) cameraFxV10.PulseCounter(0.72f);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;

        ForgetAndDestroy(mark);
        ChernobylBossEffects.SpawnCircleBlast(transform.position, radius, color);
        ChernobylShockwave.Create(transform.position, 0.56f, radius, 0.20f, 0.50f, 0, color);
        PlaySfx(antiBurstFireSfxV11, 0.76f, phase >= 3 ? 0.86f : 0.92f);
        Shake(0.125f);
        if (cameraFxV10 != null) cameraFxV10.PulseCounter(1f);

        // Anti-Burst is primarily displacement, not unavoidable punishment.
        if (PlayerWithinRadiusV11(transform.position, radius + 0.25f))
            yield return PushPlayerFromCoreV11(phase == 1 ? 2.8f : phase == 2 ? 3.25f : 3.65f, 0.28f);
    }

    private IEnumerator PushPlayerFromCoreV11(float distance, float duration)
    {
        if (playerHealth == null || playerHealth.IsDead) yield break;
        Rigidbody2D rb = playerHealth.GetComponent<Rigidbody2D>();
        if (rb == null) yield break;
        PlayerDashController dash = playerHealth.GetComponent<PlayerDashController>();
        if (dash != null) dash.CancelForExternalForce();

        Vector2 dir = ((Vector2)playerHealth.transform.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
        float safeDistance = CalculatePlayerPushDistanceV11(rb, dir, distance);
        Vector2 start = rb.position;
        Vector2 target = start + dir * safeDistance;
        ReleaseAntiBurstInputLockV11();
        antiBurstInputLockV11 = GameInputState.Acquire("ChernobylAntiBurstPush");
        playerHealth.GrantTemporaryInvulnerability(Mathf.Max(0.34f, duration + 0.08f));
        float elapsed = 0f;
        while (elapsed < duration && !dead && playerHealth != null && !playerHealth.IsDead)
        {
            elapsed += Time.fixedUnscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rb.MovePosition(Vector2.Lerp(start, target, eased));
            yield return new WaitForFixedUpdate();
        }
        rb.linearVelocity = Vector2.zero;
        ReleaseAntiBurstInputLockV11();
    }

    private void ReleaseAntiBurstInputLockV11()
    {
        if (antiBurstInputLockV11 != null)
        {
            antiBurstInputLockV11.Dispose();
            antiBurstInputLockV11 = null;
        }
    }

    private float CalculatePlayerPushDistanceV11(Rigidbody2D rb, Vector2 direction, float requestedDistance)
    {
        RaycastHit2D[] hits = new RaycastHit2D[16];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(playerHealth.gameObject.layer));
        int count = rb.Cast(direction, filter, hits, requestedDistance);
        float nearest = requestedDistance;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(playerHealth.transform)) continue;
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            nearest = Mathf.Min(nearest, Mathf.Max(0f, hits[i].distance - 0.06f));
        }
        return nearest;
    }

    private IEnumerator ContainmentCounterRoutineV10()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        Vector3 playerPos = GetPlayerPosition();
        int col, row;
        WorldToGridV11(playerPos, cols, rows, out col, out row);
        bool useColumn = Mathf.Abs(playerPos.x - transform.position.x) >= Mathf.Abs(playerPos.y - transform.position.y) * 0.85f;
        float warning = phase == 1 ? 0.92f : phase == 2 ? 0.76f : 0.64f;
        Color color = CurrentHazardColor();

        PlaySfx(antiBurstChargeSfxV11, 0.48f, 1.04f);
        if (cameraFxV10 != null) cameraFxV10.PulseCounter(0.72f);
        if (useColumn)
        {
            yield return DetonateColumnV10(cells, col, warning, color, 0.60f);
            if (phase >= 2 && !dead)
            {
                int next = col < cols / 2 ? Mathf.Min(cols - 1, col + 1) : Mathf.Max(0, col - 1);
                yield return WaitCombatSeconds(0.18f);
                yield return DetonateColumnV10(cells, next, Mathf.Max(0.58f, warning - 0.10f), color, 0.56f);
            }
        }
        else
        {
            yield return DetonateRowV10(cells, row, warning, color, 0.60f);
            if (phase >= 2 && !dead)
            {
                int next = row < rows / 2 ? Mathf.Min(rows - 1, row + 1) : Mathf.Max(0, row - 1);
                yield return WaitCombatSeconds(0.18f);
                yield return DetonateRowV10(cells, next, Mathf.Max(0.58f, warning - 0.10f), color, 0.56f);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Attack Director / combos
    // -------------------------------------------------------------------------

    private PatternKind ChoosePatternV10()
    {
        List<PatternKind> pool = BuildPatternPoolV11();
        PatternKind last = recentPatternHistoryV10.Count > 0
            ? recentPatternHistoryV10[recentPatternHistoryV10.Count - 1]
            : PatternKind.SparseGrid;
        int lastFamily = PatternFamilyV10(last);
        List<PatternKind> filtered = new List<PatternKind>();

        for (int i = 0; i < pool.Count; i++)
        {
            PatternKind p = pool[i];
            bool recentlyUsed = recentPatternHistoryV10.Contains(p);
            bool sameFamily = PatternFamilyV10(p) == lastFamily;
            if (!recentlyUsed && !sameFamily) filtered.Add(p);
        }
        if (filtered.Count < 4)
        {
            filtered.Clear();
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != last && !recentPatternHistoryV10.Contains(pool[i])) filtered.Add(pool[i]);
        }
        if (filtered.Count == 0) filtered.AddRange(pool);

        PatternKind chosen = WeightedPatternChoiceV11(filtered);
        RememberPatternV11(chosen);
        return chosen;
    }

    private List<PatternKind> BuildPatternPoolV11()
    {
        List<PatternKind> pool = new List<PatternKind>();
        pool.AddRange(new[]
        {
            PatternKind.TargetBlast, PatternKind.CrossBlast, PatternKind.SparseGrid,
            PatternKind.CheckerA, PatternKind.CheckerB,
            PatternKind.SweepL2R, PatternKind.SweepR2L, PatternKind.SweepTopDown, PatternKind.SweepBottomUp,
            PatternKind.OuterCollapse, PatternKind.CenterEvacuation, PatternKind.TwinTarget,
            PatternKind.GapSweep, PatternKind.SplitField
        });

        if (phase >= 2)
        {
            pool.AddRange(new[]
            {
                PatternKind.ChainGrid, PatternKind.CoreShockwave,
                PatternKind.CompressHorizontal, PatternKind.ExpandHorizontal,
                PatternKind.CompressVertical, PatternKind.ExpandVertical,
                PatternKind.ChainInvert, PatternKind.SafeShift,
                PatternKind.DelayedTiles, PatternKind.DelayedCross,
                PatternKind.MovingSafeLane, PatternKind.CornerQuarantine,
                PatternKind.RadiationRing, PatternKind.CorridorCrush, PatternKind.GridChase
            });
        }
        if (phase >= 3)
        {
            pool.AddRange(new[]
            {
                PatternKind.MeltdownGrid, PatternKind.RotatingLines, PatternKind.RotatingSafeSector,
                PatternKind.SpiralGrid, PatternKind.SafeZoneRelay, PatternKind.DelayedCascade,
                PatternKind.ReactorPulse, PatternKind.SectorCollapse
            });
        }
        return pool;
    }

    private PatternKind WeightedPatternChoiceV11(List<PatternKind> candidates)
    {
        if (candidates == null || candidates.Count == 0) return PatternKind.TargetBlast;
        Bounds b = GetArenaBounds();
        Vector2 p = GetPlayerPosition();
        float nx = Mathf.Abs(p.x - b.center.x) / Mathf.Max(0.1f, b.extents.x);
        float ny = Mathf.Abs(p.y - b.center.y) / Mathf.Max(0.1f, b.extents.y);
        bool nearEdge = Mathf.Max(nx, ny) > 0.68f;
        bool nearCenter = Mathf.Max(nx, ny) < 0.34f;
        float speed = GetPlayerVelocityV11().magnitude;

        float total = 0f;
        float[] weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            PatternKind pattern = candidates[i];
            float w = 1f;
            if (nearEdge && (pattern == PatternKind.OuterCollapse || pattern == PatternKind.GapSweep || pattern == PatternKind.MovingSafeLane)) w += 0.70f;
            if (nearCenter && (pattern == PatternKind.CenterEvacuation || pattern == PatternKind.CrossBlast || pattern == PatternKind.RadiationRing)) w += 0.55f;
            if (speed > 3.4f && (pattern == PatternKind.DelayedTiles || pattern == PatternKind.GridChase || pattern == PatternKind.RotatingLines)) w += 0.35f;
            if (speed < 0.75f && (pattern == PatternKind.TargetBlast || pattern == PatternKind.TwinTarget || pattern == PatternKind.CrossBlast)) w += 0.35f;
            weights[i] = w;
            total += w;
        }

        float roll = Random.value * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    private void RememberPatternV11(PatternKind pattern)
    {
        debugLastPatternV11 = pattern.ToString();
        recentPatternHistoryV10.Add(pattern);
        while (recentPatternHistoryV10.Count > 4) recentPatternHistoryV10.RemoveAt(0);
    }

    private bool ShouldRunComboV11()
    {
        if (debugIgnoreCooldownV11) return false;
        float chance = phase == 1 ? 0.32f : phase == 2 ? 0.52f : 0.68f;
        return Random.value < chance;
    }

    private ComboKindV11 ChooseComboV11()
    {
        List<ComboKindV11> pool = GetComboPoolV11();
        List<ComboKindV11> filtered = new List<ComboKindV11>();
        for (int i = 0; i < pool.Count; i++)
            if (!recentComboHistoryV11.Contains(pool[i])) filtered.Add(pool[i]);
        if (filtered.Count == 0) filtered.AddRange(pool);
        ComboKindV11 result = filtered[Random.Range(0, filtered.Count)];
        recentComboHistoryV11.Add(result);
        while (recentComboHistoryV11.Count > 2) recentComboHistoryV11.RemoveAt(0);
        return result;
    }

    private List<ComboKindV11> GetComboPoolV11()
    {
        List<ComboKindV11> pool = new List<ComboKindV11>();
        pool.Add(ComboKindV11.P1_OuterTarget);
        pool.Add(ComboKindV11.P1_CrossGap);
        pool.Add(ComboKindV11.P1_CheckerInvert);
        if (phase >= 2)
        {
            pool.Add(ComboKindV11.P2_OuterCrossTarget);
            pool.Add(ComboKindV11.P2_SafeSweep);
            pool.Add(ComboKindV11.P2_CompressShockwave);
            pool.Add(ComboKindV11.P2_CheckerDelayed);
            pool.Add(ComboKindV11.P2_LaneTarget);
        }
        if (phase >= 3)
        {
            pool.Add(ComboKindV11.P3_ReactorCycle);
            pool.Add(ComboKindV11.P3_MeltdownCorridor);
            pool.Add(ComboKindV11.P3_TimeControl);
            pool.Add(ComboKindV11.P3_SafeRelay);
            pool.Add(ComboKindV11.P3_SpiralMeltdown);
            pool.Add(ComboKindV11.P3_RotatingPressure);
        }
        return pool;
    }

    private IEnumerator RunComboV11(ComboKindV11 combo)
    {
        debugLastComboV11 = combo.ToString();
        ClearAllGridWarningsV11();
        switch (combo)
        {
            case ComboKindV11.P1_OuterTarget:
                yield return RunComboPatternV11(PatternKind.OuterCollapse, 0.34f);
                yield return RunComboPatternV11(PatternKind.TargetBlast, 0f);
                break;
            case ComboKindV11.P1_CrossGap:
                yield return RunComboPatternV11(PatternKind.CrossBlast, 0.32f);
                yield return RunComboPatternV11(PatternKind.GapSweep, 0f);
                break;
            case ComboKindV11.P1_CheckerInvert:
                yield return RunComboPatternV11(PatternKind.CheckerA, 0.26f);
                yield return RunComboPatternV11(PatternKind.CheckerB, 0f);
                break;
            case ComboKindV11.P2_OuterCrossTarget:
                yield return RunComboPatternV11(PatternKind.OuterCollapse, 0.24f);
                yield return RunComboPatternV11(PatternKind.CrossBlast, 0.22f);
                yield return RunComboPatternV11(PatternKind.TargetBlast, 0f);
                break;
            case ComboKindV11.P2_SafeSweep:
                yield return RunComboPatternV11(PatternKind.SafeShift, 0.22f);
                yield return RunComboPatternV11(Random.value < 0.5f ? PatternKind.SweepL2R : PatternKind.SweepR2L, 0f);
                break;
            case ComboKindV11.P2_CompressShockwave:
                yield return RunComboPatternV11(PatternKind.CompressHorizontal, 0.30f);
                yield return RunComboPatternV11(PatternKind.CoreShockwave, 0f);
                break;
            case ComboKindV11.P2_CheckerDelayed:
                yield return RunComboPatternV11(PatternKind.ChainInvert, 0.24f);
                yield return RunComboPatternV11(PatternKind.DelayedTiles, 0f);
                break;
            case ComboKindV11.P2_LaneTarget:
                yield return RunComboPatternV11(PatternKind.MovingSafeLane, 0.24f);
                yield return RunComboPatternV11(PatternKind.TwinTarget, 0f);
                break;
            case ComboKindV11.P3_ReactorCycle:
                yield return RunComboPatternV11(PatternKind.CoreShockwave, 0.20f);
                yield return RunComboPatternV11(PatternKind.OuterCollapse, 0.20f);
                yield return RunComboPatternV11(PatternKind.DelayedCross, 0f);
                break;
            case ComboKindV11.P3_MeltdownCorridor:
                yield return RunComboPatternV11(PatternKind.CorridorCrush, 0.18f);
                yield return RunComboPatternV11(PatternKind.RotatingLines, 0.18f);
                yield return RunComboPatternV11(PatternKind.TargetBlast, 0f);
                break;
            case ComboKindV11.P3_TimeControl:
                yield return RunComboPatternV11(PatternKind.ChainInvert, 0.18f);
                yield return RunComboPatternV11(PatternKind.DelayedCascade, 0f);
                break;
            case ComboKindV11.P3_SafeRelay:
                yield return RunComboPatternV11(PatternKind.SafeZoneRelay, 0.20f);
                yield return RunComboPatternV11(PatternKind.CoreShockwave, 0f);
                break;
            case ComboKindV11.P3_SpiralMeltdown:
                yield return RunComboPatternV11(PatternKind.SpiralGrid, 0.18f);
                yield return RunComboPatternV11(PatternKind.RadiationRing, 0.18f);
                yield return RunComboPatternV11(PatternKind.CrossBlast, 0f);
                break;
            case ComboKindV11.P3_RotatingPressure:
                yield return RunComboPatternV11(PatternKind.RotatingSafeSector, 0.18f);
                yield return RunComboPatternV11(PatternKind.RotatingLines, 0.18f);
                yield return RunComboPatternV11(PatternKind.GridChase, 0f);
                break;
        }
        ClearAllGridWarningsV11();
    }

    private IEnumerator RunComboPatternV11(PatternKind pattern, float gapAfter)
    {
        if (dead) yield break;
        RememberPatternV11(pattern);
        yield return RunPatternV10(pattern);
        ClearAllGridWarningsV11();
        if (!dead && gapAfter > 0f) yield return WaitCombatSeconds(gapAfter);
    }

    private void ResetDirectorV11()
    {
        recentPatternHistoryV10.Clear();
        recentComboHistoryV11.Clear();
        sustainedAttackTimeV10 = 0f;
        sustainedDamageV11 = 0f;
        counterResponseRequestedV10 = false;
        debugForcedPatternV11 = -1;
        debugForcedComboV11 = -1;
        debugLastPatternV11 = "-";
        debugLastComboV11 = "-";
        ReleaseAntiBurstInputLockV11();
    }

    private int PatternFamilyV10(PatternKind pattern)
    {
        switch (pattern)
        {
            case PatternKind.SweepL2R:
            case PatternKind.SweepR2L:
            case PatternKind.SweepTopDown:
            case PatternKind.SweepBottomUp:
            case PatternKind.GapSweep: return 1;

            case PatternKind.CheckerA:
            case PatternKind.CheckerB:
            case PatternKind.ChainInvert: return 2;

            case PatternKind.CompressHorizontal:
            case PatternKind.ExpandHorizontal:
            case PatternKind.CompressVertical:
            case PatternKind.ExpandVertical:
            case PatternKind.OuterCollapse:
            case PatternKind.CenterEvacuation:
            case PatternKind.SplitField:
            case PatternKind.CorridorCrush: return 3;

            case PatternKind.TargetBlast:
            case PatternKind.CrossBlast:
            case PatternKind.TwinTarget:
            case PatternKind.GridChase:
            case PatternKind.DelayedCross: return 4;

            case PatternKind.CoreShockwave:
            case PatternKind.RadiationRing:
            case PatternKind.ReactorPulse: return 5;

            case PatternKind.SparseGrid:
            case PatternKind.ChainGrid:
            case PatternKind.MeltdownGrid:
            case PatternKind.DelayedTiles:
            case PatternKind.DelayedCascade: return 6;

            case PatternKind.SafeShift:
            case PatternKind.MovingSafeLane:
            case PatternKind.SafeZoneRelay: return 7;

            case PatternKind.RotatingLines:
            case PatternKind.RotatingSafeSector:
            case PatternKind.SpiralGrid:
            case PatternKind.SectorCollapse: return 8;

            case PatternKind.CornerQuarantine: return 9;
            default: return 0;
        }
    }

    private IEnumerator RunPatternV10(PatternKind pattern)
    {
        ClearAllGridWarningsV11();
        switch (pattern)
        {
            case PatternKind.CheckerA: yield return CheckerRoutineV10(0); break;
            case PatternKind.CheckerB: yield return CheckerRoutineV10(1); break;
            case PatternKind.SweepL2R: yield return SweepColumnsRoutineV10(true); break;
            case PatternKind.SweepR2L: yield return SweepColumnsRoutineV10(false); break;
            case PatternKind.SweepTopDown: yield return SweepRowsRoutineV10(false); break;
            case PatternKind.SweepBottomUp: yield return SweepRowsRoutineV10(true); break;
            case PatternKind.CompressHorizontal: yield return HorizontalCompressionRoutineV10(true); break;
            case PatternKind.ExpandHorizontal: yield return HorizontalCompressionRoutineV10(false); break;
            case PatternKind.CompressVertical: yield return VerticalCompressionRoutineV10(true); break;
            case PatternKind.ExpandVertical: yield return VerticalCompressionRoutineV10(false); break;
            case PatternKind.ChainInvert: yield return ChainInvertRoutineV10(); break;
            case PatternKind.SafeShift: yield return SafeShiftRoutineV10(); break;
            case PatternKind.OuterCollapse: yield return OuterCollapseRoutineV11(); break;
            case PatternKind.CenterEvacuation: yield return CenterEvacuationRoutineV11(); break;
            case PatternKind.TwinTarget: yield return TwinTargetRoutineV11(); break;
            case PatternKind.GapSweep: yield return GapSweepRoutineV11(); break;
            case PatternKind.SplitField: yield return SplitFieldRoutineV11(); break;
            case PatternKind.DelayedTiles: yield return DelayedTilesRoutineV11(); break;
            case PatternKind.DelayedCross: yield return DelayedCrossRoutineV11(); break;
            case PatternKind.MovingSafeLane: yield return MovingSafeLaneRoutineV11(); break;
            case PatternKind.CornerQuarantine: yield return CornerQuarantineRoutineV11(); break;
            case PatternKind.RadiationRing: yield return RadiationRingRoutineV11(); break;
            case PatternKind.CorridorCrush: yield return CorridorCrushRoutineV11(); break;
            case PatternKind.GridChase: yield return GridChaseRoutineV11(); break;
            case PatternKind.RotatingLines: yield return RotatingLinesRoutineV11(); break;
            case PatternKind.RotatingSafeSector: yield return RotatingSafeSectorRoutineV11(); break;
            case PatternKind.SpiralGrid: yield return SpiralGridRoutineV11(); break;
            case PatternKind.SafeZoneRelay: yield return SafeZoneRelayRoutineV11(); break;
            case PatternKind.DelayedCascade: yield return DelayedCascadeRoutineV11(); break;
            case PatternKind.ReactorPulse: yield return ReactorPulseRoutineV11(); break;
            case PatternKind.SectorCollapse: yield return SectorCollapseRoutineV11(); break;
            default: yield return RunPattern(pattern); break;
        }
        ClearAllGridWarningsV11();
    }

    // -------------------------------------------------------------------------
    // Page-based grid
    // -------------------------------------------------------------------------

    private void GetGridDimensionsV11(out int cols, out int rows)
    {
        if (phase <= 1) { cols = 4; rows = 3; }
        else if (phase == 2) { cols = 6; rows = 4; }
        else { cols = 8; rows = 5; }
    }

    private List<GridCell> BuildFixedGridV10()
    {
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        Bounds b = GetArenaBounds();
        float marginX = 0.14f;
        float marginY = 0.14f;
        float width = Mathf.Max(1f, b.size.x - marginX * 2f);
        float height = Mathf.Max(1f, b.size.y - marginY * 2f);
        float cellW = width / cols;
        float cellH = height / rows;
        Vector2 size = new Vector2(cellW * 1.012f, cellH * 1.012f);
        float startX = b.min.x + marginX + cellW * 0.5f;
        float startY = b.min.y + marginY + cellH * 0.5f;
        List<GridCell> cells = new List<GridCell>(cols * rows);
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                cells.Add(new GridCell(new Vector3(startX + x * cellW, startY + y * cellH, 0f), size));
        return cells;
    }

    private void WorldToGridV11(Vector3 world, int cols, int rows, out int col, out int row)
    {
        Bounds b = GetArenaBounds();
        float nx = Mathf.InverseLerp(b.min.x, b.max.x, world.x);
        float ny = Mathf.InverseLerp(b.min.y, b.max.y, world.y);
        col = Mathf.Clamp(Mathf.FloorToInt(nx * cols), 0, cols - 1);
        row = Mathf.Clamp(Mathf.FloorToInt(ny * rows), 0, rows - 1);
    }

    private List<GridCell> GetColumnV10(List<GridCell> cells, int col)
    {
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        col = Mathf.Clamp(col, 0, cols - 1);
        List<GridCell> result = new List<GridCell>(rows);
        for (int y = 0; y < rows; y++) result.Add(cells[y * cols + col]);
        return result;
    }

    private List<GridCell> GetRowV10(List<GridCell> cells, int row)
    {
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        row = Mathf.Clamp(row, 0, rows - 1);
        List<GridCell> result = new List<GridCell>(cols);
        int start = row * cols;
        for (int x = 0; x < cols; x++) result.Add(cells[start + x]);
        return result;
    }

    private List<GridCell> GetCheckerV10(List<GridCell> cells, int parity)
    {
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<GridCell> result = new List<GridCell>();
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                if (((x + y) & 1) == (parity & 1)) result.Add(cells[y * cols + x]);
        return result;
    }

    private IEnumerator CheckerRoutineV10(int parity)
    {
        List<GridCell> cells = BuildFixedGridV10();
        float warning = phase == 1 ? 1.00f : phase == 2 ? 0.82f : 0.68f;
        PlaySfx(activationSfx, 0.22f, parity == 0 ? 1.02f : 1.08f);
        yield return TelegraphAndDetonateCells(GetCheckerV10(cells, parity), warning, CurrentHazardColor());
        if (phase >= 3 && !dead && Random.value < 0.35f)
        {
            yield return WaitCombatSeconds(0.20f);
            yield return TelegraphAndDetonateCells(GetCheckerV10(cells, 1 - parity), 0.64f, CurrentHazardColor());
        }
    }

    private IEnumerator SweepColumnsRoutineV10(bool leftToRight)
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        Color color = CurrentHazardColor();
        float warning = phase == 1 ? 0.82f : phase == 2 ? 0.68f : 0.58f;
        PlaySfx(activationSfx, 0.20f, 1.10f);
        for (int step = 0; step < cols && !dead; step++)
        {
            int col = leftToRight ? step : cols - 1 - step;
            yield return DetonateColumnV10(cells, col, warning, color, 0.38f);
            if (!dead) yield return WaitCombatSeconds(phase == 1 ? 0.10f : 0.07f);
        }
    }

    private IEnumerator SweepRowsRoutineV10(bool bottomToTop)
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        Color color = CurrentHazardColor();
        float warning = phase == 1 ? 0.86f : phase == 2 ? 0.72f : 0.60f;
        PlaySfx(activationSfx, 0.20f, 1.07f);
        for (int step = 0; step < rows && !dead; step++)
        {
            int row = bottomToTop ? step : rows - 1 - step;
            yield return DetonateRowV10(cells, row, warning, color, 0.40f);
            if (!dead) yield return WaitCombatSeconds(phase == 1 ? 0.11f : 0.08f);
        }
    }

    private IEnumerator HorizontalCompressionRoutineV10(bool inward)
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        Color color = CurrentHazardColor();
        int pairCount = cols / 2;
        float warning = phase == 1 ? 0.94f : phase == 2 ? 0.78f : 0.66f;
        for (int step = 0; step < pairCount && !dead; step++)
        {
            int left = inward ? step : pairCount - 1 - step;
            int right = cols - 1 - left;
            List<GridCell> hazards = GetColumnV10(cells, left);
            if (right != left) hazards.AddRange(GetColumnV10(cells, right));
            yield return TelegraphAndDetonateCells(hazards, warning, color);
            if (!dead) yield return WaitCombatSeconds(0.12f);
        }
    }

    private IEnumerator VerticalCompressionRoutineV10(bool inward)
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        Color color = CurrentHazardColor();
        int pairCount = rows / 2;
        float warning = phase == 1 ? 0.96f : phase == 2 ? 0.80f : 0.68f;
        for (int step = 0; step < pairCount && !dead; step++)
        {
            int bottom = inward ? step : pairCount - 1 - step;
            int top = rows - 1 - bottom;
            List<GridCell> hazards = GetRowV10(cells, bottom);
            if (top != bottom) hazards.AddRange(GetRowV10(cells, top));
            yield return TelegraphAndDetonateCells(hazards, warning, color);
            if (!dead) yield return WaitCombatSeconds(0.13f);
        }
    }

    private IEnumerator ChainInvertRoutineV10()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int first = Random.value < 0.5f ? 0 : 1;
        float warning = phase == 1 ? 0.98f : phase == 2 ? 0.82f : 0.68f;
        yield return TelegraphAndDetonateCells(GetCheckerV10(cells, first), warning, CurrentHazardColor());
        if (dead) yield break;
        yield return WaitCombatSeconds(phase == 1 ? 0.30f : 0.22f);
        yield return TelegraphAndDetonateCells(GetCheckerV10(cells, 1 - first), Mathf.Max(0.62f, warning - 0.12f), CurrentHazardColor());
        if (phase >= 3 && !dead && Random.value < 0.30f)
        {
            yield return WaitCombatSeconds(0.18f);
            yield return TelegraphAndDetonateCells(GetCheckerV10(cells, first), 0.62f, CurrentHazardColor());
        }
    }

    private IEnumerator SafeShiftRoutineV10()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<int[]> candidates = BuildSafeBlocksV11(cols, rows, 2, 2);
        if (candidates.Count == 0) yield break;

        float warning = phase == 1 ? 1.02f : phase == 2 ? 0.90f : 0.72f;
        int firstIndex = ChooseReachableSafeSetV11(cells, candidates, -1, GetPlayerPosition(), BasicReachDistanceV11(warning));
        yield return DetonateAllExceptSafeV11(cells, candidates[firstIndex], warning);
        if (dead) yield break;
        yield return WaitCombatSeconds(phase == 2 ? 0.28f : 0.22f);

        float secondWarning = Mathf.Max(0.66f, warning - 0.10f);
        int secondIndex = ChooseReachableSafeSetV11(cells, candidates, firstIndex, GetPlayerPosition(), BasicReachDistanceV11(secondWarning));
        yield return DetonateAllExceptSafeV11(cells, candidates[secondIndex], secondWarning);
    }

    private List<int[]> BuildSafeBlocksV11(int cols, int rows, int blockW, int blockH)
    {
        List<int[]> result = new List<int[]>();
        blockW = Mathf.Clamp(blockW, 1, cols);
        blockH = Mathf.Clamp(blockH, 1, rows);
        for (int y = 0; y <= rows - blockH; y++)
        {
            for (int x = 0; x <= cols - blockW; x++)
            {
                int[] set = new int[blockW * blockH];
                int k = 0;
                for (int yy = 0; yy < blockH; yy++)
                    for (int xx = 0; xx < blockW; xx++)
                        set[k++] = (y + yy) * cols + (x + xx);
                result.Add(set);
            }
        }
        return result;
    }

    private int ChooseReachableSafeSetV11(List<GridCell> cells, List<int[]> source, int excludedIndex, Vector2 playerPosition, float maxDistance)
    {
        if (source == null || source.Count == 0) return 0;
        List<int> reachable = new List<int>();
        float nearestDistance = float.PositiveInfinity;
        int nearestIndex = 0;
        for (int candidate = 0; candidate < source.Count; candidate++)
        {
            if (candidate == excludedIndex) continue;
            float distance = DistanceToSafeSetV11(cells, source[candidate], playerPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = candidate;
            }
            if (distance <= maxDistance) reachable.Add(candidate);
        }
        return reachable.Count > 0 ? reachable[Random.Range(0, reachable.Count)] : nearestIndex;
    }

    private static float DistanceToSafeSetV11(List<GridCell> cells, int[] safeSet, Vector2 playerPosition)
    {
        float best = float.PositiveInfinity;
        if (cells == null || safeSet == null) return best;
        for (int i = 0; i < safeSet.Length; i++)
        {
            int index = safeSet[i];
            if (index < 0 || index >= cells.Count) continue;
            GridCell cell = cells[index];
            Vector2 half = cell.Size * 0.5f;
            Vector2 nearest = new Vector2(
                Mathf.Clamp(playerPosition.x, cell.Center.x - half.x, cell.Center.x + half.x),
                Mathf.Clamp(playerPosition.y, cell.Center.y - half.y, cell.Center.y + half.y));
            best = Mathf.Min(best, Vector2.Distance(playerPosition, nearest));
        }
        return best;
    }

    private float BasicReachDistanceV11(float warning)
    {
        // 5 u/s is PlayerMovement's base speed. Use only 84% of theoretical travel so
        // the generated route does not require frame-perfect movement or a dash.
        return Mathf.Max(1.1f, 5f * Mathf.Max(0.35f, warning) * 0.84f);
    }

    private IEnumerator DetonateAllExceptSafeV11(List<GridCell> cells, int[] safe, float warning)
    {
        HashSet<int> safeSet = new HashSet<int>(safe);
        List<GridCell> hazards = new List<GridCell>(Mathf.Max(0, cells.Count - safeSet.Count));
        List<GameObject> safeMarks = new List<GameObject>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (safeSet.Contains(i))
            {
                GameObject indicator = ChernobylBossEffects.CreateSafeRectIndicator(cells[i].Center, cells[i].Size, warning);
                RegisterSpawnedObject(indicator);
                safeMarks.Add(indicator);
            }
            else hazards.Add(cells[i]);
        }
        yield return TelegraphAndDetonateCells(hazards, warning, CurrentHazardColor());
        for (int i = 0; i < safeMarks.Count; i++) ForgetAndDestroy(safeMarks[i]);
    }

    private IEnumerator DetonateColumnV10(List<GridCell> cells, int col, float warning, Color color, float cameraStrength)
    {
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        if (cameraFxV10 != null) cameraFxV10.PulseSweep(cameraStrength);
        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.DirectionalShake(0.10f, 0.040f + phase * 0.007f, col < cols / 2 ? Vector2.right : Vector2.left);
        yield return TelegraphAndDetonateCells(GetColumnV10(cells, Mathf.Clamp(col, 0, cols - 1)), warning, color);
    }

    private IEnumerator DetonateRowV10(List<GridCell> cells, int row, float warning, Color color, float cameraStrength)
    {
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        if (cameraFxV10 != null) cameraFxV10.PulseSweep(cameraStrength);
        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.DirectionalShake(0.10f, 0.040f + phase * 0.007f, row < rows / 2 ? Vector2.up : Vector2.down);
        yield return TelegraphAndDetonateCells(GetRowV10(cells, Mathf.Clamp(row, 0, rows - 1)), warning, color);
    }

    // -------------------------------------------------------------------------
    // New V11 pattern families
    // -------------------------------------------------------------------------

    private IEnumerator OuterCollapseRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<GridCell> hazards = new List<GridCell>();
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                if (x == 0 || x == cols - 1 || y == 0 || y == rows - 1) hazards.Add(cells[y * cols + x]);
        PlaySfx(activationSfx, 0.24f, 0.94f);
        yield return TelegraphAndDetonateCells(hazards, phase == 1 ? 1.06f : phase == 2 ? 0.88f : 0.74f, CurrentHazardColor());
    }

    private IEnumerator CenterEvacuationRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<GridCell> hazards = new List<GridCell>();
        float cx = (cols - 1) * 0.5f;
        float cy = (rows - 1) * 0.5f;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                if (Mathf.Abs(x - cx) <= 0.65f && Mathf.Abs(y - cy) <= (phase == 1 ? 0.35f : 0.65f))
                    hazards.Add(cells[y * cols + x]);
        yield return TelegraphAndDetonateCells(hazards, phase == 1 ? 1.08f : phase == 2 ? 0.88f : 0.72f, CurrentHazardColor());
    }

    private IEnumerator TwinTargetRoutineV11()
    {
        Vector3 first = ClampArenaPoint(GetPlayerPosition(), 0.55f);
        float radius = phase == 1 ? 0.95f : phase == 2 ? 1.02f : 1.10f;
        float warning = phase == 1 ? 1.02f : phase == 2 ? 0.84f : 0.70f;
        yield return ExecuteCircleTargetV11(first, radius, warning, 0.58f);
        if (dead) yield break;
        yield return WaitCombatSeconds(0.28f);
        Vector2 velocity = GetPlayerVelocityV11();
        Vector3 predicted = ClampArenaPoint(GetPlayerPosition() + (Vector3)(velocity * (phase == 1 ? 0.22f : 0.30f)), 0.55f);
        yield return ExecuteCircleTargetV11(predicted, radius, Mathf.Max(0.68f, warning - 0.10f), 0.60f);
    }

    private IEnumerator ExecuteCircleTargetV11(Vector3 target, float radius, float warning, float sfxVolume)
    {
        GameObject mark = ChernobylBossEffects.CreateCircleTelegraph(target, radius, warning, CurrentHazardColor());
        RegisterSpawnedObject(mark);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        ForgetAndDestroy(mark);
        ChernobylBossEffects.SpawnCircleBlast(target, radius, CurrentHazardColor());
        DamagePlayerCircle(target, radius, 1);
        PlaySfx(explosionSfx, sfxVolume, 0.96f);
        if (cameraFxV10 != null) cameraFxV10.PulseExplosion(0.42f);
    }

    private IEnumerator GapSweepRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        bool horizontalTravel = Random.value < 0.5f;
        bool forward = Random.value < 0.5f;
        int gap = horizontalTravel ? Random.Range(0, rows) : Random.Range(0, cols);
        float warning = phase == 1 ? 0.92f : phase == 2 ? 0.74f : 0.62f;
        int steps = horizontalTravel ? cols : rows;
        for (int s = 0; s < steps && !dead; s++)
        {
            int line = forward ? s : steps - 1 - s;
            List<GridCell> hazards = new List<GridCell>();
            if (horizontalTravel)
            {
                for (int y = 0; y < rows; y++) if (y != gap) hazards.Add(cells[y * cols + line]);
                if (phase >= 2 && s < steps - 1 && Random.value < 0.55f)
                    gap = Mathf.Clamp(gap + (Random.value < 0.5f ? -1 : 1), 0, rows - 1);
            }
            else
            {
                for (int x = 0; x < cols; x++) if (x != gap) hazards.Add(cells[line * cols + x]);
                if (phase >= 2 && s < steps - 1 && Random.value < 0.55f)
                    gap = Mathf.Clamp(gap + (Random.value < 0.5f ? -1 : 1), 0, cols - 1);
            }
            yield return TelegraphAndDetonateCells(hazards, warning, CurrentHazardColor());
            if (!dead) yield return WaitCombatSeconds(0.08f);
        }
    }

    private IEnumerator SplitFieldRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        bool vertical = Random.value < 0.5f;
        bool firstLow = Random.value < 0.5f;
        List<GridCell> a = new List<GridCell>();
        List<GridCell> b = new List<GridCell>();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool low = vertical ? x < cols / 2 : y < rows / 2;
                if (low == firstLow) a.Add(cells[y * cols + x]); else b.Add(cells[y * cols + x]);
            }
        }
        float warning = phase == 1 ? 1.08f : phase == 2 ? 0.88f : 0.74f;
        yield return TelegraphAndDetonateCells(a, warning, CurrentHazardColor());
        if (dead) yield break;
        yield return WaitCombatSeconds(0.32f);
        yield return TelegraphAndDetonateCells(b, Mathf.Max(0.70f, warning - 0.10f), CurrentHazardColor());
    }

    private IEnumerator DelayedTilesRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        List<GridCell> shuffled = new List<GridCell>(cells);
        Shuffle(shuffled);
        int each = Mathf.Max(2, Mathf.RoundToInt(cells.Count * (phase == 2 ? 0.26f : 0.30f)));
        List<GridCell> early = new List<GridCell>();
        List<GridCell> late = new List<GridCell>();
        for (int i = 0; i < Mathf.Min(each, shuffled.Count); i++) early.Add(shuffled[i]);
        for (int i = each; i < Mathf.Min(each * 2, shuffled.Count); i++) late.Add(shuffled[i]);
        yield return TelegraphDelayedGroupsV11(early, late, phase == 2 ? 0.88f : 0.72f, phase == 2 ? 0.42f : 0.34f);
    }

    private IEnumerator TelegraphDelayedGroupsV11(List<GridCell> early, List<GridCell> late, float initialWarning, float lateDelay)
    {
        List<GameObject> earlyMarks = CreateCellMarksV11(early, initialWarning, CurrentHazardColor());
        Color delayedColor = new Color(1f, 0.62f, 0.14f, 1f);
        List<GameObject> lateMarks = CreateCellMarksV11(late, initialWarning + lateDelay, delayedColor);
        PlaySfx(activationSfx, 0.25f, 1.13f);
        yield return WaitCombatSeconds(initialWarning);
        if (dead) yield break;
        bool hit = DetonateCellGroupV11(early, CurrentHazardColor(), true);
        DestroyMarkListV11(earlyMarks);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.58f, 0.98f);
        yield return WaitCombatSeconds(lateDelay);
        if (dead) yield break;
        hit = DetonateCellGroupV11(late, delayedColor, true);
        DestroyMarkListV11(lateMarks);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.60f, 0.92f);
    }

    private IEnumerator DelayedCrossRoutineV11()
    {
        Bounds b = GetArenaBounds();
        Vector3 p = ClampArenaPoint(GetPlayerPosition(), 0.45f);
        Vector2 velocity = GetPlayerVelocityV11();
        Vector3 p2 = ClampArenaPoint(p + (Vector3)(velocity.normalized * 1.45f), 0.45f);
        float thickness = phase == 2 ? 0.78f : 0.88f;
        float warning = phase == 2 ? 0.90f : 0.72f;
        Color first = CurrentHazardColor();
        Color second = new Color(1f, 0.66f, 0.15f, 1f);

        Vector3 v1 = new Vector3(p.x, b.center.y, 0f);
        Vector3 h1 = new Vector3(b.center.x, p.y, 0f);
        Vector3 v2 = new Vector3(p2.x, b.center.y, 0f);
        Vector3 h2 = new Vector3(b.center.x, p2.y, 0f);
        Vector2 vSize = new Vector2(thickness, b.size.y - 0.25f);
        Vector2 hSize = new Vector2(b.size.x - 0.25f, thickness);

        List<GameObject> firstMarks = new List<GameObject>
        {
            RegisterAndReturnV11(ChernobylBossEffects.CreateRectTelegraph(v1, vSize, warning, first)),
            RegisterAndReturnV11(ChernobylBossEffects.CreateRectTelegraph(h1, hSize, warning, first))
        };
        List<GameObject> secondMarks = new List<GameObject>
        {
            RegisterAndReturnV11(ChernobylBossEffects.CreateRectTelegraph(v2, vSize, warning + 0.42f, second)),
            RegisterAndReturnV11(ChernobylBossEffects.CreateRectTelegraph(h2, hSize, warning + 0.42f, second))
        };
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        bool hit = PlayerInsideRect(v1, vSize) || PlayerInsideRect(h1, hSize);
        DestroyMarkListV11(firstMarks);
        ChernobylBossEffects.SpawnRectBlast(v1, vSize, first, false);
        ChernobylBossEffects.SpawnRectBlast(h1, hSize, first, false);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.62f, 0.98f);
        yield return WaitCombatSeconds(0.42f);
        if (dead) yield break;
        hit = PlayerInsideRect(v2, vSize) || PlayerInsideRect(h2, hSize);
        DestroyMarkListV11(secondMarks);
        ChernobylBossEffects.SpawnRectBlast(v2, vSize, second, false);
        ChernobylBossEffects.SpawnRectBlast(h2, hSize, second, false);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.64f, 0.92f);
    }

    private IEnumerator MovingSafeLaneRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        bool verticalLane = Random.value < 0.5f;
        int laneCount = verticalLane ? cols : rows;
        Vector3 pp = GetPlayerPosition();
        int pCol, pRow;
        WorldToGridV11(pp, cols, rows, out pCol, out pRow);
        int lane = verticalLane ? pCol : pRow;
        int direction = lane < laneCount / 2 ? 1 : -1;
        float warning = phase == 2 ? 0.86f : 0.70f;
        int waves = phase == 2 ? 3 : 4;
        for (int wave = 0; wave < waves && !dead; wave++)
        {
            List<int> safe = new List<int>();
            if (verticalLane)
            {
                for (int y = 0; y < rows; y++) safe.Add(y * cols + lane);
            }
            else
            {
                for (int x = 0; x < cols; x++) safe.Add(lane * cols + x);
            }
            yield return DetonateAllExceptSafeV11(cells, safe.ToArray(), warning);
            if (dead) yield break;
            yield return WaitCombatSeconds(0.22f);
            int next = lane + direction;
            if (next < 0 || next >= laneCount) { direction *= -1; next = lane + direction; }
            lane = Mathf.Clamp(next, 0, laneCount - 1);
        }
    }

    private IEnumerator CornerQuarantineRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        bool diagonal = Random.value < 0.5f;
        List<GridCell> first = new List<GridCell>();
        List<GridCell> second = new List<GridCell>();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool left = x < cols / 2;
                bool bottom = y < rows / 2;
                bool groupA = diagonal ? (left == bottom) : (left != bottom);
                if (groupA) first.Add(cells[y * cols + x]); else second.Add(cells[y * cols + x]);
            }
        }
        yield return TelegraphAndDetonateCells(first, phase == 2 ? 0.92f : 0.74f, CurrentHazardColor());
        if (dead) yield break;
        yield return WaitCombatSeconds(0.26f);
        yield return TelegraphAndDetonateCells(second, phase == 2 ? 0.78f : 0.66f, CurrentHazardColor());
    }

    private IEnumerator RadiationRingRoutineV11()
    {
        Bounds b = GetArenaBounds();
        float outer = Mathf.Min(b.extents.y * 0.74f, phase == 2 ? 2.7f : 3.05f);
        float inner = outer * (phase == 2 ? 0.44f : 0.38f);
        float warning = phase == 2 ? 0.90f : 0.72f;
        GameObject mark = ChernobylBossEffects.CreateRingTelegraph(transform.position, inner, outer, warning, CurrentHazardColor());
        RegisterSpawnedObject(mark);
        PlaySfx(shockwaveChargeSfxV11, 0.38f, 1.08f);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        bool hit = PlayerInsideRingV11(transform.position, inner, outer);
        ForgetAndDestroy(mark);
        ChernobylBossEffects.SpawnRingBlast(transform.position, inner, outer, CurrentHazardColor());
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.66f, 0.90f);
        Shake(0.11f);
    }

    private IEnumerator CorridorCrushRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        bool vertical = Random.value < 0.5f;
        int pCol, pRow;
        WorldToGridV11(GetPlayerPosition(), cols, rows, out pCol, out pRow);
        List<int> safe = new List<int>();
        if (vertical)
        {
            int a = Mathf.Clamp(pCol, 0, cols - 1);
            int b = Mathf.Clamp(a + (a < cols - 1 ? 1 : -1), 0, cols - 1);
            for (int y = 0; y < rows; y++) { safe.Add(y * cols + a); if (b != a) safe.Add(y * cols + b); }
        }
        else
        {
            int a = Mathf.Clamp(pRow, 0, rows - 1);
            int b = Mathf.Clamp(a + (a < rows - 1 ? 1 : -1), 0, rows - 1);
            for (int x = 0; x < cols; x++) { safe.Add(a * cols + x); if (b != a) safe.Add(b * cols + x); }
        }
        yield return DetonateAllExceptSafeV11(cells, safe.ToArray(), phase == 2 ? 0.94f : 0.76f);
    }

    private IEnumerator GridChaseRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        int col, row;
        WorldToGridV11(GetPlayerPosition(), cols, rows, out col, out row);
        Vector2 v = GetPlayerVelocityV11();
        int dx = Mathf.Abs(v.x) > Mathf.Abs(v.y) ? (v.x >= 0f ? 1 : -1) : 0;
        int dy = dx == 0 ? (v.y >= 0f ? 1 : -1) : 0;
        if (v.sqrMagnitude < 0.35f) { dx = Random.value < 0.5f ? 1 : -1; dy = 0; }
        int waves = phase == 2 ? 3 : 4;
        float warning = phase == 2 ? 0.82f : 0.66f;
        for (int i = 0; i < waves && !dead; i++)
        {
            int cx = Mathf.Clamp(col + dx * i, 0, cols - 1);
            int cy = Mathf.Clamp(row + dy * i, 0, rows - 1);
            List<GridCell> one = new List<GridCell> { cells[cy * cols + cx] };
            yield return TelegraphAndDetonateCells(one, warning, CurrentHazardColor());
            if (!dead) yield return WaitCombatSeconds(0.16f);
        }
    }

    private IEnumerator RotatingLinesRoutineV11()
    {
        Bounds b = GetArenaBounds();
        float baseAngle = Random.Range(0f, 180f);
        float direction = Random.value < 0.5f ? 1f : -1f;
        int waves = 5;
        float length = Mathf.Sqrt(b.size.x * b.size.x + b.size.y * b.size.y) * 1.05f;
        float width = 0.62f;
        float warning = 0.72f;
        for (int i = 0; i < waves && !dead; i++)
        {
            float angle = baseAngle + direction * i * 24f;
            yield return OrientedLineBlastV11(b.center, new Vector2(length, width), angle, warning);
            if (!dead) yield return WaitCombatSeconds(0.16f);
        }
    }

    private IEnumerator OrientedLineBlastV11(Vector3 center, Vector2 size, float angle, float warning)
    {
        GameObject mark = ChernobylBossEffects.CreateRectTelegraph(center, size, warning, CurrentHazardColor());
        mark.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        RegisterSpawnedObject(mark);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        bool hit = PlayerInsideOrientedRectV11(center, size, angle);
        ForgetAndDestroy(mark);
        GameObject blast = ChernobylBossEffects.SpawnRectBlastObject(center, size, CurrentHazardColor(), false);
        if (blast != null) blast.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.58f, 0.96f);
        if (cameraFxV10 != null) cameraFxV10.PulseSweep(0.52f);
    }

    private IEnumerator RotatingSafeSectorRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        Bounds b = GetArenaBounds();
        Vector2 fromCenter = (Vector2)GetPlayerPosition() - (Vector2)b.center;
        float sectorCenter = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
        float direction = Random.value < 0.5f ? 1f : -1f;
        float halfWidth = 54f;
        for (int wave = 0; wave < 4 && !dead; wave++)
        {
            List<GridCell> hazards = new List<GridCell>();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 delta = (Vector2)cells[i].Center - (Vector2)b.center;
                if (delta.magnitude < 0.85f) continue;
                float a = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(a, sectorCenter)) > halfWidth) hazards.Add(cells[i]);
            }
            yield return TelegraphAndDetonateCells(hazards, 0.74f, CurrentHazardColor());
            if (dead) yield break;
            yield return WaitCombatSeconds(0.22f);
            sectorCenter += direction * 38f;
        }
    }

    private IEnumerator SpiralGridRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<int> order = BuildSpiralOrderV11(cols, rows);
        int chunk = 4;
        for (int i = 0; i < order.Count && !dead; i += chunk)
        {
            List<GridCell> hazards = new List<GridCell>();
            for (int k = i; k < Mathf.Min(i + chunk, order.Count); k++) hazards.Add(cells[order[k]]);
            yield return TelegraphAndDetonateCells(hazards, 0.64f, CurrentHazardColor());
            if (!dead) yield return WaitCombatSeconds(0.10f);
        }
    }

    private List<int> BuildSpiralOrderV11(int cols, int rows)
    {
        List<int> result = new List<int>(cols * rows);
        int left = 0, right = cols - 1, bottom = 0, top = rows - 1;
        while (left <= right && bottom <= top)
        {
            for (int x = left; x <= right; x++) result.Add(bottom * cols + x);
            bottom++;
            for (int y = bottom; y <= top; y++) result.Add(y * cols + right);
            right--;
            if (bottom <= top)
            {
                for (int x = right; x >= left; x--) result.Add(top * cols + x);
                top--;
            }
            if (left <= right)
            {
                for (int y = top; y >= bottom; y--) result.Add(y * cols + left);
                left++;
            }
        }
        return result;
    }

    private IEnumerator SafeZoneRelayRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<int[]> candidates = BuildSafeBlocksV11(cols, rows, 2, 2);
        int previous = -1;
        float warning = 0.74f;
        for (int wave = 0; wave < 3 && !dead; wave++)
        {
            int next = ChooseReachableSafeSetV11(cells, candidates, previous, GetPlayerPosition(), BasicReachDistanceV11(warning));
            yield return DetonateAllExceptSafeV11(cells, candidates[next], warning);
            previous = next;
            if (!dead) yield return WaitCombatSeconds(0.22f);
        }
    }

    private IEnumerator DelayedCascadeRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<GridCell>[] groups = { new List<GridCell>(), new List<GridCell>(), new List<GridCell>() };
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++) groups[(x + y) % 3].Add(cells[y * cols + x]);

        Color[] colors = { CurrentHazardColor(), new Color(1f, 0.76f, 0.16f, 1f), new Color(1f, 0.42f, 0.12f, 1f) };
        List<GameObject>[] marks =
        {
            CreateCellMarksV11(groups[0], 0.78f, colors[0]),
            CreateCellMarksV11(groups[1], 1.14f, colors[1]),
            CreateCellMarksV11(groups[2], 1.50f, colors[2])
        };
        yield return WaitCombatSeconds(0.78f);
        for (int g = 0; g < 3 && !dead; g++)
        {
            if (g > 0) yield return WaitCombatSeconds(0.36f);
            bool hit = DetonateCellGroupV11(groups[g], colors[g], true);
            DestroyMarkListV11(marks[g]);
            if (hit) DamagePlayerOnce(1);
            PlaySfx(explosionSfx, 0.58f + g * 0.03f, 1.0f - g * 0.05f);
        }
    }

    private IEnumerator ReactorPulseRoutineV11()
    {
        yield return OuterCollapseRoutineV11();
        if (dead) yield break;
        yield return WaitCombatSeconds(0.28f);
        yield return CoreShockwaveRoutine();
    }

    private IEnumerator SectorCollapseRoutineV11()
    {
        List<GridCell> cells = BuildFixedGridV10();
        Bounds b = GetArenaBounds();
        int sectors = 6;
        int start = Random.Range(0, sectors);
        int direction = Random.value < 0.5f ? 1 : -1;
        for (int wave = 0; wave < 4 && !dead; wave++)
        {
            int sector = (start + direction * wave + sectors * 8) % sectors;
            List<GridCell> hazards = new List<GridCell>();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 d = (Vector2)cells[i].Center - (Vector2)b.center;
                float a = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 360f, 360f);
                int cellSector = Mathf.FloorToInt(a / (360f / sectors)) % sectors;
                if (cellSector == sector || cellSector == (sector + 1) % sectors) hazards.Add(cells[i]);
            }
            yield return TelegraphAndDetonateCells(hazards, 0.72f, CurrentHazardColor());
            if (!dead) yield return WaitCombatSeconds(0.18f);
        }
    }

    // -------------------------------------------------------------------------
    // Telegraph helpers / cleanup
    // -------------------------------------------------------------------------

    private List<GameObject> CreateCellMarksV11(List<GridCell> cells, float warning, Color color)
    {
        List<GameObject> marks = new List<GameObject>();
        bool simple = cells.Count >= 10;
        for (int i = 0; i < cells.Count; i++)
        {
            GameObject mark = ChernobylBossEffects.CreateRectTelegraph(cells[i].Center, cells[i].Size, warning, color, 11, simple);
            RegisterSpawnedObject(mark);
            marks.Add(mark);
        }
        return marks;
    }

    private bool DetonateCellGroupV11(List<GridCell> hazards, Color color, bool fragments)
    {
        bool hit = false;
        int stride = hazards.Count >= 18 ? 6 : hazards.Count >= 10 ? 4 : 2;
        for (int i = 0; i < hazards.Count; i++)
        {
            if (PlayerInsideRect(hazards[i].Center, hazards[i].Size)) hit = true;
            ChernobylBossEffects.SpawnRectBlast(hazards[i].Center, hazards[i].Size, color,
                fragments && (hazards.Count <= 6 || i % stride == 0));
        }
        if (cameraFxV10 != null) cameraFxV10.PulseExplosion(hazards.Count >= 10 ? 0.68f : 0.42f);
        return hit;
    }

    private GameObject RegisterAndReturnV11(GameObject go)
    {
        RegisterSpawnedObject(go);
        return go;
    }

    private void DestroyMarkListV11(List<GameObject> marks)
    {
        if (marks == null) return;
        for (int i = 0; i < marks.Count; i++) ForgetAndDestroy(marks[i]);
        marks.Clear();
    }

    private void ClearAllGridWarningsV11()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = spawnedObjects[i];
            if (go == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }
            string n = go.name;
            if (n.Contains("Telegraph") || n.Contains("SafeIndicator") || n.Contains("GridWarning"))
            {
                spawnedObjects.RemoveAt(i);
                Destroy(go);
            }
        }

        for (int i = 0; i < arenaGridLinesV10.Count; i++)
            if (arenaGridLinesV10[i] != null) arenaGridLinesV10[i].enabled = false;
    }

    private int CountGridWarningsV11()
    {
        int count = 0;
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            GameObject go = spawnedObjects[i];
            if (go == null) continue;
            if (go.name.Contains("Telegraph") || go.name.Contains("SafeIndicator") || go.name.Contains("GridWarning")) count++;
        }
        return count;
    }

    private void NotifyGridReconfiguredV11()
    {
        ClearAllGridWarningsV11();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.08f, CurrentHazardColor(), 12 + phase * 4, 3.5f);
    }

    private bool PlayerWithinRadiusV11(Vector3 center, float radius)
    {
        if (playerHealth == null || playerHealth.IsDead) return false;
        Collider2D[] colliders = playerHealth.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (Vector2.Distance(center, c.ClosestPoint(center)) <= radius) return true;
        }
        return Vector2.Distance(center, playerHealth.transform.position) <= radius;
    }

    private bool PlayerInsideRingV11(Vector3 center, float innerRadius, float outerRadius)
    {
        if (playerHealth == null || playerHealth.IsDead) return false;
        Collider2D[] colliders = playerHealth.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            Vector2 closest = c.ClosestPoint(center);
            float minD = Vector2.Distance(center, closest);
            Bounds cb = c.bounds;
            Vector2[] corners =
            {
                new Vector2(cb.min.x, cb.min.y), new Vector2(cb.min.x, cb.max.y),
                new Vector2(cb.max.x, cb.min.y), new Vector2(cb.max.x, cb.max.y)
            };
            float maxD = minD;
            for (int k = 0; k < corners.Length; k++) maxD = Mathf.Max(maxD, Vector2.Distance(center, corners[k]));
            if (maxD >= innerRadius && minD <= outerRadius) return true;
        }
        return false;
    }

    private bool PlayerInsideOrientedRectV11(Vector3 center, Vector2 size, float angle)
    {
        if (playerHealth == null || playerHealth.IsDead) return false;
        Quaternion inv = Quaternion.Euler(0f, 0f, -angle);
        Collider2D[] colliders = playerHealth.GetComponentsInChildren<Collider2D>(true);
        Vector2 half = size * 0.5f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            Vector3 local = inv * (c.bounds.center - center);
            Vector2 ext = c.bounds.extents;
            if (Mathf.Abs(local.x) <= half.x + ext.x && Mathf.Abs(local.y) <= half.y + ext.y) return true;
        }
        Vector3 playerLocal = inv * (playerHealth.transform.position - center);
        return Mathf.Abs(playerLocal.x) <= half.x && Mathf.Abs(playerLocal.y) <= half.y;
    }

    private Vector2 GetPlayerVelocityV11()
    {
        if (playerHealth == null) return Vector2.zero;
        Rigidbody2D rb = playerHealth.GetComponent<Rigidbody2D>();
        return rb != null ? rb.linearVelocity : Vector2.zero;
    }

    // -------------------------------------------------------------------------
    // Development/debug controls. Separated from normal game rules.
    // -------------------------------------------------------------------------

    private PatternKind GetDebugPatternV11()
    {
        System.Array values = System.Enum.GetValues(typeof(PatternKind));
        if (values.Length == 0) return PatternKind.TargetBlast;
        debugSelectedPatternIndexV11 = (debugSelectedPatternIndexV11 % values.Length + values.Length) % values.Length;
        return (PatternKind)values.GetValue(debugSelectedPatternIndexV11);
    }

    private ComboKindV11 GetDebugComboV11()
    {
        System.Array values = System.Enum.GetValues(typeof(ComboKindV11));
        if (values.Length == 0) return ComboKindV11.P1_OuterTarget;
        debugSelectedComboIndexV11 = (debugSelectedComboIndexV11 % values.Length + values.Length) % values.Length;
        return (ComboKindV11)values.GetValue(debugSelectedComboIndexV11);
    }

    private void UpdateDebugControlsV11()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!combatStarted || dead) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!shift) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) { DebugBeginCombatAtHealth(1.00f); return; }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { DebugBeginCombatAtHealth(0.64f); return; }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { DebugBeginCombatAtHealth(0.29f); return; }
        if (Input.GetKeyDown(KeyCode.PageUp)) debugSelectedPatternIndexV11++;
        if (Input.GetKeyDown(KeyCode.PageDown)) debugSelectedPatternIndexV11--;
        if (Input.GetKeyDown(KeyCode.N)) { debugSelectedPatternIndexV11++; debugForcedPatternV11 = (int)GetDebugPatternV11(); }
        if (Input.GetKeyDown(KeyCode.P)) debugForcedPatternV11 = (int)GetDebugPatternV11();
        if (Input.GetKeyDown(KeyCode.C)) debugSelectedComboIndexV11++;
        if (Input.GetKeyDown(KeyCode.X)) debugSelectedComboIndexV11--;
        if (Input.GetKeyDown(KeyCode.O)) debugForcedComboV11 = (int)GetDebugComboV11();
        if (Input.GetKeyDown(KeyCode.B)) counterResponseRequestedV10 = true;
        if (Input.GetKeyDown(KeyCode.G)) ClearAllGridWarningsV11();
        if (Input.GetKeyDown(KeyCode.I)) debugBossInvulnerableV11 = !debugBossInvulnerableV11;
        if (Input.GetKeyDown(KeyCode.U)) debugPlayerInvulnerableV11 = !debugPlayerInvulnerableV11;
        if (Input.GetKeyDown(KeyCode.K)) debugIgnoreCooldownV11 = !debugIgnoreCooldownV11;
#endif
    }
}
