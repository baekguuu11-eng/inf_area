using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public sealed partial class ChernobylBossController
{
    private const int FixedGridColsV10 = 6;
    private const int FixedGridRowsV10 = 4;

    private readonly List<PatternKind> recentPatternHistoryV10 = new List<PatternKind>(5);
    private float lastSustainedHitTimeV10 = -99f;
    private float sustainedAttackTimeV10;
    private float lastAttackDistanceV10 = 4f;
    private float counterCooldownUntilV10;
    private bool counterResponseRequestedV10;
    private bool counterNearV10;
    private ChernobylCameraFX cameraFxV10;
    private Light2D coreLightV10;
    private Light2D globalFillLightV10;
    private readonly List<SpriteRenderer> arenaGridLinesV10 = new List<SpriteRenderer>();

    private void InitializeOverhaulV10()
    {
        cameraFxV10 = ChernobylCameraFX.CreateOrGet();
        SetupCoreLightV10();
        CreateArenaGridVisualV10();
    }


    private void CreateArenaGridVisualV10()
    {
        Bounds b = GetArenaBounds();
        GameObject root = new GameObject("CHN_FixedArenaGrid_6x4");
        root.hideFlags = HideFlags.DontSave;
        root.transform.SetParent(ownerRoom != null ? ownerRoom.transform : null, true);
        root.transform.position = Vector3.zero;

        // 격자는 전투 가능 영역의 끝까지 정확하게 그린다.
        // V10의 EnemySpawnArea 기반 경계는 좌/우/상/하 입장 스폰 지점을 잘라냈다.
        float minX = b.min.x;
        float maxX = b.max.x;
        float minY = b.min.y;
        float maxY = b.max.y;
        float w = maxX - minX;
        float h = maxY - minY;
        float lineWidth = 0.025f;
        Color lineColor = new Color(0.34f, 1f, 0.40f, 0.075f);

        for (int x = 0; x <= FixedGridColsV10; x++)
        {
            float px = Mathf.Lerp(minX, maxX, x / (float)FixedGridColsV10);
            GameObject line = new GameObject("GridV_" + x);
            line.transform.SetParent(root.transform, true);
            line.transform.position = new Vector3(px, b.center.y, 0f);
            line.transform.localScale = new Vector3(lineWidth, h, 1f);
            SpriteRenderer r = line.AddComponent<SpriteRenderer>();
            r.sprite = ChernobylRuntimeSprites.WhitePixel;
            r.color = lineColor;
            r.sortingOrder = 3;
            arenaGridLinesV10.Add(r);
        }

        for (int y = 0; y <= FixedGridRowsV10; y++)
        {
            float py = Mathf.Lerp(minY, maxY, y / (float)FixedGridRowsV10);
            GameObject line = new GameObject("GridH_" + y);
            line.transform.SetParent(root.transform, true);
            line.transform.position = new Vector3(b.center.x, py, 0f);
            line.transform.localScale = new Vector3(w, lineWidth, 1f);
            SpriteRenderer r = line.AddComponent<SpriteRenderer>();
            r.sprite = ChernobylRuntimeSprites.WhitePixel;
            r.color = lineColor;
            r.sortingOrder = 3;
            arenaGridLinesV10.Add(r);
        }
    }

    private void SetupCoreLightV10()
    {
        if (coreRenderer == null) return;

        // Sprite-Lit-Default를 사용하는 기존 맵에 Point Light2D 하나만 추가하면
        // 2D Light 패스가 활성화되면서 빛을 받지 않는 배경/벽이 검게 보일 수 있다.
        // 체르노빌 방 전체에는 중성 Global Light2D를 먼저 공급하고, 코어의 Point Light를
        // 그 위에 얹어서 원래 맵 가독성을 유지한다.
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
        // Global fill이 맵을 정상 밝기로 유지하므로 코어 라이트는 국소 강조만 담당한다.
        float baseIntensity = phase == 1 ? 0.22f : phase == 2 ? 0.32f : 0.46f;
        coreLightV10.intensity = baseIntensity + pulse * (phase == 1 ? 0.06f : phase == 2 ? 0.10f : 0.16f);
        coreLightV10.color = CurrentHazardColor();

        float gridAlpha = phase == 1 ? 0.075f : phase == 2 ? 0.105f : 0.145f;
        Color hazard = CurrentHazardColor();
        for (int i = 0; i < arenaGridLinesV10.Count; i++)
        {
            SpriteRenderer line = arenaGridLinesV10[i];
            if (line == null) continue;
            Color c = Color.Lerp(new Color(0.34f, 1f, 0.40f, 1f), hazard, 0.55f);
            c.a = gridAlpha + (phase >= 3 ? pulse * 0.028f : 0f);
            line.color = c;
        }
    }

    private void RegisterSustainedHitV10(int damage)
    {
        if (!combatStarted || dead) return;
        lastSustainedHitTimeV10 = Time.time;
        lastAttackDistanceV10 = Vector2.Distance(GetPlayerPosition(), transform.position);
        // 한 발의 큰 피해만으로 즉시 발동되지 않게 하고, 지속 명중은 살짝 빠르게 누적한다.
        sustainedAttackTimeV10 += Mathf.Clamp(damage * 0.0018f, 0f, 0.045f);
    }

    private void UpdateSustainedPressureV10()
    {
        if (!combatStarted || state != BossState.Combat || dead)
        {
            sustainedAttackTimeV10 = Mathf.MoveTowards(sustainedAttackTimeV10, 0f, Time.deltaTime * 2.4f);
            return;
        }

        bool recentlyHit = Time.time - lastSustainedHitTimeV10 <= 0.26f;
        if (recentlyHit && !counterResponseRequestedV10 && Time.time >= counterCooldownUntilV10)
            sustainedAttackTimeV10 += Time.deltaTime;
        else
            sustainedAttackTimeV10 = Mathf.MoveTowards(sustainedAttackTimeV10, 0f, Time.deltaTime * (recentlyHit ? 0.25f : 1.8f));

        float threshold = phase == 1 ? 3.15f : phase == 2 ? 2.75f : 2.35f;
        if (!counterResponseRequestedV10 && Time.time >= counterCooldownUntilV10 && sustainedAttackTimeV10 >= threshold)
        {
            counterNearV10 = lastAttackDistanceV10 <= 2.65f;
            counterResponseRequestedV10 = true;
            sustainedAttackTimeV10 = 0f;
            ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.1f, CurrentHazardColor(), 10 + phase * 4, 3.1f);
            if (cameraFxV10 != null) cameraFxV10.PulseCounter(0.55f);
        }
    }

    private IEnumerator RunCounterResponseV10()
    {
        if (!counterResponseRequestedV10 || dead) yield break;
        counterResponseRequestedV10 = false;
        counterCooldownUntilV10 = Time.time + (phase == 1 ? 6.5f : phase == 2 ? 5.7f : 4.9f);
        if (counterNearV10) yield return CorePurgeCounterRoutineV10();
        else yield return ContainmentCounterRoutineV10();
    }

    private IEnumerator CorePurgeCounterRoutineV10()
    {
        Color color = CurrentHazardColor();
        float radius = phase == 1 ? 1.95f : phase == 2 ? 2.15f : 2.32f;
        float warning = phase == 1 ? 0.88f : phase == 2 ? 0.70f : 0.56f;
        GameObject mark = ChernobylBossEffects.CreateCircleTelegraph(transform.position, radius, warning, color, 18);
        RegisterSpawnedObject(mark);
        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.08f, color, 16 + phase * 4, 3.8f);
        if (cameraFxV10 != null) cameraFxV10.PulseCounter(0.72f);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        ForgetAndDestroy(mark);
        ChernobylBossEffects.SpawnCircleBlast(transform.position, radius, color);
        DamagePlayerCircle(transform.position, radius, 1);
        PlaySfx(explosionSfx, 0.78f, phase >= 3 ? 0.86f : 0.92f);
        Shake(0.145f);
        if (cameraFxV10 != null) cameraFxV10.PulseCounter(1f);

        // 최종 단계는 중심에서 한 번 더 밀어내되 회피할 수 있는 간격을 둔다.
        if (phase >= 3 && !dead)
        {
            yield return WaitCombatSeconds(0.18f);
            ChernobylShockwave.Create(transform.position, 0.72f, 2.65f, 0.20f, 0.70f, 1, color);
        }
    }

    private IEnumerator ContainmentCounterRoutineV10()
    {
        List<GridCell> cells = BuildFixedGridV10();
        Vector3 playerPos = GetPlayerPosition();
        Bounds b = GetArenaBounds();
        float nx = Mathf.InverseLerp(b.min.x, b.max.x, playerPos.x);
        float ny = Mathf.InverseLerp(b.min.y, b.max.y, playerPos.y);
        int col = Mathf.Clamp(Mathf.FloorToInt(nx * FixedGridColsV10), 0, FixedGridColsV10 - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt(ny * FixedGridRowsV10), 0, FixedGridRowsV10 - 1);
        bool useColumn = Mathf.Abs(playerPos.x - transform.position.x) >= Mathf.Abs(playerPos.y - transform.position.y) * 0.85f;
        float warning = phase == 1 ? 0.72f : phase == 2 ? 0.58f : 0.46f;
        Color color = CurrentHazardColor();

        if (cameraFxV10 != null) cameraFxV10.PulseCounter(0.72f);
        if (useColumn)
        {
            yield return DetonateColumnV10(cells, col, warning, color, 0.60f);
            if (phase >= 2 && !dead)
            {
                int next = col < FixedGridColsV10 / 2 ? Mathf.Min(FixedGridColsV10 - 1, col + 1) : Mathf.Max(0, col - 1);
                yield return WaitCombatSeconds(0.10f);
                yield return DetonateColumnV10(cells, next, Mathf.Max(0.38f, warning - 0.10f), color, 0.56f);
            }
        }
        else
        {
            yield return DetonateRowV10(cells, row, warning, color, 0.60f);
            if (phase >= 2 && !dead)
            {
                int next = row < FixedGridRowsV10 / 2 ? Mathf.Min(FixedGridRowsV10 - 1, row + 1) : Mathf.Max(0, row - 1);
                yield return WaitCombatSeconds(0.10f);
                yield return DetonateRowV10(cells, next, Mathf.Max(0.38f, warning - 0.10f), color, 0.56f);
            }
        }
    }

    private PatternKind ChoosePatternV10()
    {
        List<PatternKind> pool = new List<PatternKind>();
        if (phase == 1)
        {
            pool.AddRange(new[] { PatternKind.TargetBlast, PatternKind.CrossBlast, PatternKind.CheckerA, PatternKind.CheckerB,
                PatternKind.SweepL2R, PatternKind.SweepR2L, PatternKind.SweepTopDown, PatternKind.SweepBottomUp });
        }
        else if (phase == 2)
        {
            pool.AddRange(new[] { PatternKind.CrossBlast, PatternKind.CoreShockwave, PatternKind.CheckerA, PatternKind.CheckerB,
                PatternKind.SweepL2R, PatternKind.SweepR2L, PatternKind.SweepTopDown, PatternKind.SweepBottomUp,
                PatternKind.CompressHorizontal, PatternKind.ExpandHorizontal, PatternKind.CompressVertical,
                PatternKind.ChainInvert, PatternKind.SafeShift });
        }
        else
        {
            pool.AddRange(new[] { PatternKind.TargetBlast, PatternKind.CrossBlast, PatternKind.CoreShockwave,
                PatternKind.CheckerA, PatternKind.CheckerB, PatternKind.SweepL2R, PatternKind.SweepR2L,
                PatternKind.SweepTopDown, PatternKind.SweepBottomUp, PatternKind.CompressHorizontal,
                PatternKind.ExpandHorizontal, PatternKind.CompressVertical, PatternKind.ExpandVertical,
                PatternKind.ChainInvert, PatternKind.SafeShift });
        }

        PatternKind last = recentPatternHistoryV10.Count > 0 ? recentPatternHistoryV10[recentPatternHistoryV10.Count - 1] : PatternKind.SparseGrid;
        int lastFamily = PatternFamilyV10(last);
        List<PatternKind> filtered = new List<PatternKind>();
        for (int i = 0; i < pool.Count; i++)
        {
            PatternKind p = pool[i];
            bool recentlyUsed = recentPatternHistoryV10.Contains(p);
            bool sameFamily = PatternFamilyV10(p) == lastFamily;
            if (!recentlyUsed && !sameFamily) filtered.Add(p);
        }
        if (filtered.Count < 3)
        {
            filtered.Clear();
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != last) filtered.Add(pool[i]);
        }
        if (filtered.Count == 0) filtered.AddRange(pool);

        PatternKind chosen = filtered[Random.Range(0, filtered.Count)];
        recentPatternHistoryV10.Add(chosen);
        while (recentPatternHistoryV10.Count > 4) recentPatternHistoryV10.RemoveAt(0);
        return chosen;
    }

    private int PatternFamilyV10(PatternKind pattern)
    {
        switch (pattern)
        {
            case PatternKind.SweepL2R:
            case PatternKind.SweepR2L:
            case PatternKind.SweepTopDown:
            case PatternKind.SweepBottomUp: return 1;
            case PatternKind.CheckerA:
            case PatternKind.CheckerB:
            case PatternKind.ChainInvert:
            case PatternKind.SafeShift: return 2;
            case PatternKind.CompressHorizontal:
            case PatternKind.ExpandHorizontal:
            case PatternKind.CompressVertical:
            case PatternKind.ExpandVertical: return 3;
            case PatternKind.TargetBlast:
            case PatternKind.CrossBlast: return 4;
            case PatternKind.CoreShockwave: return 5;
            default: return 0;
        }
    }

    private IEnumerator RunPatternV10(PatternKind pattern)
    {
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
            default: yield return RunPattern(pattern); break;
        }
    }

    private List<GridCell> BuildFixedGridV10()
    {
        Bounds b = GetArenaBounds();
        // 6x4 셀을 전체 전투영역 끝까지 채운다. 셀을 아주 살짝 겹쳐
        // 외곽/셀 경계가 의도치 않은 안전지대가 되는 것을 방지한다.
        float width = Mathf.Max(1f, b.size.x);
        float height = Mathf.Max(1f, b.size.y);
        float cellW = width / FixedGridColsV10;
        float cellH = height / FixedGridRowsV10;
        Vector2 size = new Vector2(cellW * 1.015f, cellH * 1.015f);
        float startX = b.min.x + cellW * 0.5f;
        float startY = b.min.y + cellH * 0.5f;
        List<GridCell> cells = new List<GridCell>(FixedGridColsV10 * FixedGridRowsV10);
        for (int y = 0; y < FixedGridRowsV10; y++)
            for (int x = 0; x < FixedGridColsV10; x++)
                cells.Add(new GridCell(new Vector3(startX + x * cellW, startY + y * cellH, 0f), size));
        return cells;
    }

    private List<GridCell> GetColumnV10(List<GridCell> cells, int col)
    {
        List<GridCell> result = new List<GridCell>(FixedGridRowsV10);
        for (int y = 0; y < FixedGridRowsV10; y++) result.Add(cells[y * FixedGridColsV10 + col]);
        return result;
    }

    private List<GridCell> GetRowV10(List<GridCell> cells, int row)
    {
        List<GridCell> result = new List<GridCell>(FixedGridColsV10);
        int start = row * FixedGridColsV10;
        for (int x = 0; x < FixedGridColsV10; x++) result.Add(cells[start + x]);
        return result;
    }

    private List<GridCell> GetCheckerV10(List<GridCell> cells, int parity)
    {
        List<GridCell> result = new List<GridCell>();
        for (int y = 0; y < FixedGridRowsV10; y++)
            for (int x = 0; x < FixedGridColsV10; x++)
                if (((x + y) & 1) == (parity & 1)) result.Add(cells[y * FixedGridColsV10 + x]);
        return result;
    }

    private IEnumerator CheckerRoutineV10(int parity)
    {
        List<GridCell> cells = BuildFixedGridV10();
        float warning = phase == 1 ? 0.96f : phase == 2 ? 0.70f : 0.54f;
        yield return TelegraphAndDetonateCells(GetCheckerV10(cells, parity), warning, CurrentHazardColor());
        if (phase >= 3 && !dead && Random.value < 0.45f)
        {
            yield return WaitCombatSeconds(0.14f);
            yield return TelegraphAndDetonateCells(GetCheckerV10(cells, 1 - parity), 0.46f, CurrentHazardColor());
        }
    }

    private IEnumerator SweepColumnsRoutineV10(bool leftToRight)
    {
        List<GridCell> cells = BuildFixedGridV10();
        Color color = CurrentHazardColor();
        float warning = phase == 1 ? 0.58f : phase == 2 ? 0.46f : 0.36f;
        for (int step = 0; step < FixedGridColsV10 && !dead; step++)
        {
            int col = leftToRight ? step : FixedGridColsV10 - 1 - step;
            yield return DetonateColumnV10(cells, col, warning, color, 0.42f);
            if (!dead) yield return WaitCombatSeconds(phase == 1 ? 0.06f : 0.03f);
        }
    }

    private IEnumerator SweepRowsRoutineV10(bool bottomToTop)
    {
        List<GridCell> cells = BuildFixedGridV10();
        Color color = CurrentHazardColor();
        float warning = phase == 1 ? 0.64f : phase == 2 ? 0.50f : 0.39f;
        for (int step = 0; step < FixedGridRowsV10 && !dead; step++)
        {
            int row = bottomToTop ? step : FixedGridRowsV10 - 1 - step;
            yield return DetonateRowV10(cells, row, warning, color, 0.45f);
            if (!dead) yield return WaitCombatSeconds(phase == 1 ? 0.07f : 0.035f);
        }
    }

    private IEnumerator HorizontalCompressionRoutineV10(bool inward)
    {
        List<GridCell> cells = BuildFixedGridV10();
        Color color = CurrentHazardColor();
        int[,] pairs = inward ? new int[,] { {0,5}, {1,4}, {2,3} } : new int[,] { {2,3}, {1,4}, {0,5} };
        float warning = phase == 2 ? 0.62f : 0.48f;
        for (int i = 0; i < 3 && !dead; i++)
        {
            List<GridCell> hazards = GetColumnV10(cells, pairs[i,0]);
            hazards.AddRange(GetColumnV10(cells, pairs[i,1]));
            yield return TelegraphAndDetonateCells(hazards, warning, color);
            if (!dead) yield return WaitCombatSeconds(0.08f);
        }
    }

    private IEnumerator VerticalCompressionRoutineV10(bool inward)
    {
        List<GridCell> cells = BuildFixedGridV10();
        Color color = CurrentHazardColor();
        int[,] pairs = inward ? new int[,] { {0,3}, {1,2} } : new int[,] { {1,2}, {0,3} };
        float warning = phase == 2 ? 0.66f : 0.50f;
        for (int i = 0; i < 2 && !dead; i++)
        {
            List<GridCell> hazards = GetRowV10(cells, pairs[i,0]);
            hazards.AddRange(GetRowV10(cells, pairs[i,1]));
            yield return TelegraphAndDetonateCells(hazards, warning, color);
            if (!dead) yield return WaitCombatSeconds(0.10f);
        }
    }

    private IEnumerator ChainInvertRoutineV10()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int first = Random.value < 0.5f ? 0 : 1;
        float warning = phase == 2 ? 0.68f : 0.52f;
        yield return TelegraphAndDetonateCells(GetCheckerV10(cells, first), warning, CurrentHazardColor());
        if (dead) yield break;
        yield return WaitCombatSeconds(phase == 2 ? 0.16f : 0.10f);
        yield return TelegraphAndDetonateCells(GetCheckerV10(cells, 1 - first), Mathf.Max(0.40f, warning - 0.12f), CurrentHazardColor());
        if (phase >= 3 && !dead && Random.value < 0.38f)
        {
            yield return WaitCombatSeconds(0.10f);
            yield return TelegraphAndDetonateCells(GetCheckerV10(cells, first), 0.42f, CurrentHazardColor());
        }
    }

    private IEnumerator SafeShiftRoutineV10()
    {
        List<GridCell> cells = BuildFixedGridV10();
        // 무작위 셀 생성이 아니라 미리 설계한 안전지대 패턴 중 하나를 선택한다.
        int[][] safeSetsPhase2 =
        {
            new[] { 1, 2, 7, 8 }, new[] { 3, 4, 9, 10 }, new[] { 13, 14, 19, 20 },
            new[] { 15, 16, 21, 22 }, new[] { 6, 7, 12, 13 }, new[] { 10, 11, 16, 17 }
        };
        int[][] safeSetsMeltdown =
        {
            new[] { 1, 8 }, new[] { 4, 9 }, new[] { 14, 19 }, new[] { 16, 21 },
            new[] { 6, 13 }, new[] { 10, 17 }, new[] { 2, 15 }, new[] { 8, 22 }
        };
        int[][] source = phase >= 3 ? safeSetsMeltdown : safeSetsPhase2;
        int firstIndex = Random.Range(0, source.Length);
        int secondIndex = (firstIndex + Random.Range(1, source.Length)) % source.Length;
        float warning = phase == 2 ? 0.82f : 0.62f;
        yield return DetonateAllExceptSafeV10(cells, source[firstIndex], warning);
        if (dead) yield break;
        yield return WaitCombatSeconds(phase == 2 ? 0.18f : 0.12f);
        yield return DetonateAllExceptSafeV10(cells, source[secondIndex], Mathf.Max(0.46f, warning - 0.12f));
    }

    private IEnumerator DetonateAllExceptSafeV10(List<GridCell> cells, int[] safe, float warning)
    {
        HashSet<int> safeSet = new HashSet<int>(safe);
        List<GridCell> hazards = new List<GridCell>(cells.Count - safeSet.Count);
        for (int i = 0; i < cells.Count; i++) if (!safeSet.Contains(i)) hazards.Add(cells[i]);
        yield return TelegraphAndDetonateCells(hazards, warning, CurrentHazardColor());
    }

    private IEnumerator DetonateColumnV10(List<GridCell> cells, int col, float warning, Color color, float cameraStrength)
    {
        if (cameraFxV10 != null) cameraFxV10.PulseSweep(cameraStrength);
        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.DirectionalShake(0.12f, 0.045f + phase * 0.008f, col < FixedGridColsV10 / 2 ? Vector2.right : Vector2.left);
        yield return TelegraphAndDetonateCells(GetColumnV10(cells, Mathf.Clamp(col, 0, FixedGridColsV10 - 1)), warning, color);
    }

    private IEnumerator DetonateRowV10(List<GridCell> cells, int row, float warning, Color color, float cameraStrength)
    {
        if (cameraFxV10 != null) cameraFxV10.PulseSweep(cameraStrength);
        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.DirectionalShake(0.12f, 0.045f + phase * 0.008f, row < FixedGridRowsV10 / 2 ? Vector2.up : Vector2.down);
        yield return TelegraphAndDetonateCells(GetRowV10(cells, Mathf.Clamp(row, 0, FixedGridRowsV10 - 1)), warning, color);
    }
}
