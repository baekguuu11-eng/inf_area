using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChernobylBossController
{
    private int patternPhaseV17;
    private readonly Queue<PatternKind> introductionsV17 = new Queue<PatternKind>();

    private IEnumerator BaitVolleyRoutineV17()
    {
        int count = phase == 1 ? 3 : phase == 2 ? 4 : 5;
        for (int i = 0; i < count && !dead; i++)
        {
            // Lock each position at warning creation; never chase after commitment.
            Vector3 target = ClampArenaPoint(GetPlayerPosition(), 0.6f);
            yield return ExecuteCircleTargetV11(target, phase == 1 ? 0.85f : 0.95f,
                phase == 1 ? 0.85f : phase == 2 ? 0.75f : 0.65f, 0.58f);
            if (transitionRequested || meltdownRequested) yield break;
            yield return WaitCombatSeconds(0.16f);
        }
    }

    private IEnumerator CoreRadiationRoutineV17() { yield return TargetBlastRoutine(); }

    private IEnumerator PipeCascadeRoutineV17()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows; GetGridDimensionsV11(out cols, out rows);
        int pc, pr; WorldToGridV11(GetPlayerPosition(), cols, rows, out pc, out pr);
        int row = Mathf.Clamp(pr, 0, rows - 1);
        bool fromLeft = pc < cols / 2;
        // One horizontal pipe; the rows above/below remain traversable.
        for (int step = 0; step < 3 && !dead; step++)
        {
            int band = fromLeft ? step : 2 - step;
            List<GridCell> hazards = new List<GridCell>();
            for (int x = 0; x < cols; x++) if (x * 3 / cols == band) hazards.Add(cells[row * cols + x]);
            yield return TelegraphAndDetonateCells(hazards,
                step == 0 ? 1.0f : phase == 1 ? 0.72f : phase == 2 ? 0.64f : 0.56f, CurrentHazardColor());
            if (transitionRequested || meltdownRequested) yield break;
            yield return WaitCombatSeconds(0.12f);
        }
    }

    private IEnumerator DirectionalVentRoutineV17(bool crossed)
    {
        Vector2 origin = transform.position;
        Vector2 aim = ((Vector2)GetPlayerPosition() - origin).normalized;
        if (aim.sqrMagnitude < 0.01f) aim = Vector2.down;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        int count = crossed ? 2 : 1;
        for (int i = 0; i < count && !dead; i++)
        {
            float a = angle + i * 90f;
            Vector2 direction = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            // Fixed narrow directional vent; lateral movement preserves an approach route.
            yield return OrientedLineBlastV11(origin + direction * 1.8f,
                new Vector2(3.6f, 0.9f), a, phase == 1 ? 1.05f : phase == 2 ? 0.95f : 0.85f);
            if (transitionRequested || meltdownRequested) yield break;
        }
        yield return WaitCombatSeconds(0.40f);
    }

    private IEnumerator FinalOverloadRoutineV17()
    {
        debugLastComboV11 = "V17 BAIT / PIPE / VENT";
        yield return BaitVolleyRoutineV17();
        if (dead || transitionRequested || meltdownRequested) yield break;
        yield return WaitCombatSeconds(0.30f);
        yield return PipeCascadeRoutineV17();
        if (dead || transitionRequested || meltdownRequested) yield break;
        yield return WaitCombatSeconds(0.30f);
        yield return DirectionalVentRoutineV17(false);
    }
}
