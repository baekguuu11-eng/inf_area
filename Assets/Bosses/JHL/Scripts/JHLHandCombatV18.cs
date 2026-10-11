using System.Collections;
using UnityEngine;

public sealed partial class JHLBossController
{
    private void PrepareHandsV18(JHLPatternKind pattern)
    {
        bool fist = pattern == JHLPatternKind.HandSlam || pattern == JHLPatternKind.DoubleTapSlam ||
            pattern == JHLPatternKind.TwinSlam || pattern == JHLPatternKind.SlamChain || pattern == JHLPatternKind.SidePunch;
        PoseHandV18(leftHand, fist ? JHLHandPoseV18.Fist : JHLHandPoseV18.Open, Vector2.up);
        PoseHandV18(rightHand, fist ? JHLHandPoseV18.Fist : JHLHandPoseV18.Open, Vector2.up);
    }

    private void PoseHandV18(Transform hand, JHLHandPoseV18 pose, Vector2 direction)
    {
        if (hand == null) return;
        JHLArticulatedHandV18 rig = hand.GetComponent<JHLArticulatedHandV18>();
        if (rig != null) rig.SetPose(pose, direction);
    }

    private IEnumerator GunVolleyV18(bool both, bool fan)
    {
        if (player == null || leftHand == null || rightHand == null) yield break;
        bool useLeft = Random.value < 0.5f;
        Bounds bounds = GetCombatBounds();
        Transform first = useLeft ? leftHand : rightHand;
        JHLPartMotion motion = useLeft ? leftMotion : rightMotion;
        motion.SetTarget(new Vector3(useLeft ? bounds.min.x + 0.9f : bounds.max.x - 0.9f,
            bounds.center.y + bounds.extents.y * 0.42f, 0f), JHLPartMotionState.Anticipation);
        if (both)
        {
            JHLPartMotion other = useLeft ? rightMotion : leftMotion;
            other.SetTarget(new Vector3(useLeft ? bounds.max.x - 0.9f : bounds.min.x + 0.9f,
                bounds.center.y + bounds.extents.y * 0.42f, 0f), JHLPartMotionState.Anticipation);
        }
        // Allow the moving hand and joint chain to reach a clear firing pose before lock.
        PoseHandV18(first, JHLHandPoseV18.FingerGun, ((Vector2)player.position - (Vector2)first.position).normalized);
        if (both) PoseHandV18(useLeft ? rightHand : leftHand, JHLHandPoseV18.FingerGun,
            ((Vector2)player.position - (Vector2)(useLeft ? rightHand.position : leftHand.position)).normalized);
        yield return new WaitForSeconds(0.38f);
        Transform[] hands = both ? new[] { leftHand, rightHand } : new[] { first };
        foreach (Transform hand in hands)
            PoseHandV18(hand, JHLHandPoseV18.FingerGun, ((Vector2)player.position - (Vector2)hand.position).normalized);
        yield return new WaitForSeconds(0.22f);
        if (bossAudio != null) bossAudio.PlayHandWindup();
        int volleys = fan ? 2 + phase : 6 + phase * 3;
        for (int shot = 0; shot < volleys && !dead && player != null; shot++)
        {
            foreach (Transform hand in hands)
            {
                JHLArticulatedHandV18 rig = hand.GetComponent<JHLArticulatedHandV18>();
                if (rig == null) continue;
                Vector2 origin = rig.Muzzle;
                Vector2 direction = rig.Direction;
                int rays = fan ? 5 : 1;
                for (int ray = 0; ray < rays; ray++)
                {
                    Vector2 dir = RotateVector(direction, fan ? (ray - 2) * 12f : (shot % 3 - 1) * 7f);
                    JHLProjectile bullet = JHLProjectile.Create(this, origin, dir, 8.5f + phase, 1);
                    if (bullet != null) bullet.SetLifetimeV17(1.45f);
                }
                if (combatEffects != null) combatEffects.SpawnImpact(origin, 0.16f, Color.white);
                if (bossAudio != null) bossAudio.PlayProjectile();
            }
            yield return new WaitForSeconds(fan ? 0.26f : phase == 1 ? 0.12f : phase == 2 ? 0.10f : 0.085f);
        }
        foreach (Transform hand in hands) PoseHandV18(hand, JHLHandPoseV18.Open, Vector2.up);
    }
}
