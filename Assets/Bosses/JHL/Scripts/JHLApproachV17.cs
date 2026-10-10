using System.Collections;
using UnityEngine;

public sealed partial class JHLBossController
{
    private float ApproachDurationV17()
    {
        if (player == null || face == null) return 0.8f;
        PlayerStats stats = player.GetComponent<PlayerStats>();
        float speed = stats != null ? Mathf.Max(0.1f, stats.MoveSpeed) : 5f;
        float distance = Mathf.Max(0f, Vector2.Distance(player.position, face.position) - faceSize.y * 0.45f - 0.6f);
        // Starting position matters. A long trip is bounded so slow builds still need positioning.
        return Mathf.Clamp(distance / speed + 0.28f, 0.20f, phase == 1 ? 0.55f : phase == 2 ? 0.42f : 0.32f);
    }

    private IEnumerator ApproachGateRoutineV17()
    {
        if (leftMotion == null || rightMotion == null) yield break;
        // Both hands stop short of the centre. Moving physical walls are not required.
        Bounds b = GetCombatBounds();
        float gapHalf = Mathf.Max(1.25f, handSize.x * 0.25f);
        float y = b.center.y;
        Vector3 leftTarget = new Vector3(b.center.x - gapHalf - handSize.x * 0.5f, y, 0f);
        Vector3 rightTarget = new Vector3(b.center.x + gapHalf + handSize.x * 0.5f, y, 0f);
        float warning = phase == 2 ? 1.15f : 1.0f;
        float leftEdge = b.center.x - gapHalf;
        float rightEdge = b.center.x + gapHalf;
        GameObject lw = CreateRectTelegraph(new Vector2((b.min.x + leftEdge) * 0.5f, y),
            new Vector2(Mathf.Max(0.1f, leftEdge - b.min.x), handSize.y), 0f,
            new Color(1f, 0.10f, 0.28f, 0.25f), warning);
        GameObject rw = CreateRectTelegraph(new Vector2((b.max.x + rightEdge) * 0.5f, y),
            new Vector2(Mathf.Max(0.1f, b.max.x - rightEdge), handSize.y), 0f,
            new Color(1f, 0.10f, 0.28f, 0.25f), warning);
        SetHandsSolid(false);
        leftMotion.SetTarget(new Vector3(b.min.x - handSize.x, y, 0f), JHLPartMotionState.Anticipation);
        rightMotion.SetTarget(new Vector3(b.max.x + handSize.x, y, 0f), JHLPartMotionState.Anticipation);
        if (bossAudio != null) bossAudio.PlayHandWindup();
        yield return new WaitForSeconds(warning);
        DestroyTracked(lw); DestroyTracked(rw);
        leftMotion.SetKinematicProfile(16f, 60f, 100f);
        rightMotion.SetKinematicProfile(16f, 60f, 100f);
        leftMotion.SetTarget(leftTarget, JHLPartMotionState.Attack);
        rightMotion.SetTarget(rightTarget, JHLPartMotionState.Attack);
        SetTrails(true, true);
        float elapsed = 0f;
        bool hit = false;
        float duration = ApproachDurationV17() + 0.45f;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.deltaTime;
            if (!hit) hit = TryDamagePlayerFromHandContact(leftHand, 1) || TryDamagePlayerFromHandContact(rightHand, 1);
            yield return null;
        }
        SetTrails(false, false);
    }

    private IEnumerator SweepBarrageRoutineV17()
    {
        bool sweepFromLeft = Random.value < 0.5f;
        auxiliaryPatternRoutine = StartCoroutine(IdleGunSupportV18(!sweepFromLeft));
        yield return HandSweepRoutine(sweepFromLeft, 0.90f, 1);
        while (!dead && auxiliaryPatternRoutine != null) yield return null;
    }

    private IEnumerator IdleGunSupportV18(bool useLeft)
    {
        Transform hand = useLeft ? leftHand : rightHand;
        if (hand == null) { auxiliaryPatternRoutine = null; yield break; }
        // Opposite hand owns the shooting channel; never move the sweeping hand twice.
        Vector2 direction = new Vector2(useLeft ? 0.35f : -0.35f, -1f).normalized;
        PoseHandV18(hand, JHLHandPoseV18.FingerGun, direction);
        yield return new WaitForSeconds(0.65f);
        JHLArticulatedHandV18 rig = hand.GetComponent<JHLArticulatedHandV18>();
        for (int i = 0; i < 4 + phase && !dead && rig != null; i++)
        {
            JHLProjectile bolt = JHLProjectile.Create(this, rig.Muzzle, rig.Direction, 8.5f + phase, 1);
            if (bolt != null) bolt.SetLifetimeV17(1.3f);
            if (bossAudio != null) bossAudio.PlayProjectile();
            yield return new WaitForSeconds(0.16f);
        }
        PoseHandV18(hand, JHLHandPoseV18.Open, Vector2.up);
        auxiliaryPatternRoutine = null;
    }
}
