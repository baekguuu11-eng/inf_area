using System.Collections.Generic;
using UnityEngine;

public enum JHLPatternFamily
{
    HandSingle,
    HandArea,
    Projectile,
    Laser,
    SpaceControl,
    Combo,
    Ultimate
}

[DisallowMultipleComponent]
public sealed class JHLPatternDirector : MonoBehaviour
{
    private readonly Queue<JHLPatternKind> recentPatterns = new Queue<JHLPatternKind>();
    private readonly Queue<JHLPatternFamily> recentFamilies = new Queue<JHLPatternFamily>();

    private JHLPatternKind lastPatternV15;
    private bool hasLastV15;

    public JHLPatternFamily LastFamily { get; private set; } = JHLPatternFamily.HandSingle;

    public void ResetHistory()
    {
        hasLastV15 = false;
        recentPatterns.Clear();
        recentFamilies.Clear();
        LastFamily = JHLPatternFamily.HandSingle;
    }

    public void Remember(JHLPatternKind pattern)
    {
        JHLPatternFamily family = FamilyOf(pattern);
        LastFamily = family;
        lastPatternV15 = pattern; hasLastV15 = true;
        recentPatterns.Enqueue(pattern);
        recentFamilies.Enqueue(family);
        while (recentPatterns.Count > 5) recentPatterns.Dequeue();
        while (recentFamilies.Count > 5) recentFamilies.Dequeue();
    }

    public JHLPatternKind Select(
        int phase,
        float healthRatio,
        Vector2 playerPosition,
        Vector2 playerVelocity,
        Vector2 cameraCenter,
        float cameraHalfWidth,
        bool fullAccessUsed)
    {
        if (phase >= 3 && healthRatio <= 0.15f && !fullAccessUsed)
            return JHLPatternKind.FullAccess;

        List<JHLPatternKind> candidates = BuildPool(phase);
        float edge = Mathf.Abs(playerPosition.x - cameraCenter.x) / Mathf.Max(0.1f, cameraHalfWidth);
        float speed = playerVelocity.magnitude;

        float total = 0f;
        float[] weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            JHLPatternKind pattern = candidates[i];
            JHLPatternFamily family = FamilyOf(pattern);
            float weight = BaseWeight(pattern);

            int samePatternCount = Count(recentPatterns, pattern);
            int sameFamilyCount = Count(recentFamilies, family);
            if (samePatternCount > 0) weight *= samePatternCount >= 2 ? 0.035f : 0.20f;
            if (sameFamilyCount >= 2) weight *= sameFamilyCount >= 3 ? 0.22f : 0.46f;

            // Edge players: sweeping, tracking and side-entry attacks become more likely.
            if (edge > 0.68f)
            {
                if (pattern == JHLPatternKind.HandSweep || pattern == JHLPatternKind.EnhancedSweep ||
                    pattern == JHLPatternKind.ScissorSweep || pattern == JHLPatternKind.SidePunch ||
                    pattern == JHLPatternKind.TrackingThinBeam || pattern == JHLPatternKind.LaserSweep)
                    weight *= 1.75f;
                if (pattern == JHLPatternKind.Compression || pattern == JHLPatternKind.HandPrison || pattern == JHLPatternKind.PrisonBarrage)
                    weight *= 0.72f;
            }
            // Center campers: space-control and multi-target slam attacks get priority.
            else if (edge < 0.30f)
            {
                if (pattern == JHLPatternKind.Compression || pattern == JHLPatternKind.CrossSlam ||
                    pattern == JHLPatternKind.HandPrison || pattern == JHLPatternKind.PrisonBarrage ||
                    pattern == JHLPatternKind.TwinSlam || pattern == JHLPatternKind.CompressionBurst)
                    weight *= 1.55f;
            }

            // Slow player: aimed attacks punish staying still.
            if (speed < 0.7f)
            {
                if (pattern == JHLPatternKind.HandSlam || pattern == JHLPatternKind.DoubleTapSlam ||
                    pattern == JHLPatternKind.EnhancedSlam || pattern == JHLPatternKind.TripleAimLaser ||
                    pattern == JHLPatternKind.RapidLaserBurst || pattern == JHLPatternKind.StraightLaser)
                    weight *= 1.34f;
            }
            // Fast player: pattern geometry that cuts routes becomes more useful.
            else if (speed > 4.0f)
            {
                if (pattern == JHLPatternKind.SplitBeam || pattern == JHLPatternKind.HandSweep ||
                    pattern == JHLPatternKind.CrossSlam || pattern == JHLPatternKind.LaserCurtain ||
                    pattern == JHLPatternKind.SpiralBeam || pattern == JHLPatternKind.ScissorSweep)
                    weight *= 1.28f;
            }

            weights[i] = hasLastV15 && pattern == lastPatternV15 ? 0f : Mathf.Max(0.01f, weight);
            total += weights[i];
        }

        float roll = Random.value * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (weights[i] <= 0f) continue;
            roll -= weights[i];
            if (roll <= 0f) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    public static JHLPatternFamily FamilyOf(JHLPatternKind pattern)
    {
        switch (pattern)
        {
            case JHLPatternKind.HandSlam:
            case JHLPatternKind.DoubleTapSlam:
            case JHLPatternKind.EnhancedSlam:
            case JHLPatternKind.TwinSlam:
            case JHLPatternKind.SlamChain:
            case JHLPatternKind.QuadSlam:
                return JHLPatternFamily.HandSingle;

            case JHLPatternKind.HandSweep:
            case JHLPatternKind.EnhancedSweep:
            case JHLPatternKind.ScissorSweep:
            case JHLPatternKind.SidePunch:
            case JHLPatternKind.DiagonalCrossPunch:
            case JHLPatternKind.MovingGate:
                return JHLPatternFamily.HandArea;

            case JHLPatternKind.FingerBarrage:
            case JHLPatternKind.ProjectileFan:
            case JHLPatternKind.CrossfireBarrage:
            case JHLPatternKind.PrisonBarrage:
                return JHLPatternFamily.Projectile;

            case JHLPatternKind.StraightLaser:
            case JHLPatternKind.TripleAimLaser:
            case JHLPatternKind.MassiveCentralBeam:
            case JHLPatternKind.TrackingThinBeam:
            case JHLPatternKind.SplitBeam:
            case JHLPatternKind.LaserSweep:
            case JHLPatternKind.CrossLaser:
            case JHLPatternKind.LaserCurtain:
            case JHLPatternKind.SpiralBeam:
            case JHLPatternKind.RapidLaserBurst:
            case JHLPatternKind.BeamPinch:
            case JHLPatternKind.RemoteSuppression:
                return JHLPatternFamily.Laser;

            case JHLPatternKind.Compression:
            case JHLPatternKind.HandPrison:
            case JHLPatternKind.CompressionBurst:
            case JHLPatternKind.PredictiveBombardment:
                return JHLPatternFamily.SpaceControl;

            case JHLPatternKind.CrossSlam:
            case JHLPatternKind.FinalCompression:
            case JHLPatternKind.SweepLaserCombo:
            case JHLPatternKind.RoarRepulse:
                return JHLPatternFamily.Combo;

            case JHLPatternKind.FullAccess:
                return JHLPatternFamily.Ultimate;

            default:
                return JHLPatternFamily.HandSingle;
        }
    }

    private static List<JHLPatternKind> BuildPool(int phase)
    {
        if (phase <= 1) return new List<JHLPatternKind> {
            JHLPatternKind.HandSlam, JHLPatternKind.HandSweep, JHLPatternKind.StraightLaser, JHLPatternKind.FingerBarrage };
        if (phase == 2) return new List<JHLPatternKind> {
            JHLPatternKind.DoubleTapSlam, JHLPatternKind.HandSweep, JHLPatternKind.Compression,
            JHLPatternKind.StraightLaser, JHLPatternKind.FingerBarrage };
        return new List<JHLPatternKind> {
            JHLPatternKind.DoubleTapSlam, JHLPatternKind.HandSweep, JHLPatternKind.Compression,
            JHLPatternKind.MassiveCentralBeam, JHLPatternKind.TrackingThinBeam };
    }

    private static float BaseWeight(JHLPatternKind pattern)
    {
        switch (pattern)
        {
            case JHLPatternKind.HandSlam: return 0.92f;
            case JHLPatternKind.HandSweep: return 1.00f;
            case JHLPatternKind.StraightLaser: return 0.82f;
            case JHLPatternKind.FingerBarrage: return 0.82f;
            case JHLPatternKind.DoubleTapSlam: return 0.88f;
            case JHLPatternKind.SidePunch: return 0.96f;
            case JHLPatternKind.TripleAimLaser: return 0.78f;
            case JHLPatternKind.ProjectileFan: return 0.86f;
            case JHLPatternKind.EnhancedSlam: return 0.94f;
            case JHLPatternKind.EnhancedSweep: return 0.92f;
            case JHLPatternKind.Compression: return 0.90f;
            case JHLPatternKind.HandPrison: return 0.82f;
            case JHLPatternKind.CrossSlam: return 0.93f;
            case JHLPatternKind.TwinSlam: return 0.86f;
            case JHLPatternKind.SlamChain: return 0.80f;
            case JHLPatternKind.ScissorSweep: return 0.84f;
            case JHLPatternKind.LaserSweep: return 0.82f;
            case JHLPatternKind.CrossLaser: return 0.82f;
            case JHLPatternKind.PrisonBarrage: return 0.76f;
            case JHLPatternKind.CompressionBurst: return 0.70f;
            case JHLPatternKind.MassiveCentralBeam: return 0.88f;
            case JHLPatternKind.TrackingThinBeam: return 0.78f;
            case JHLPatternKind.SplitBeam: return 0.84f;
            case JHLPatternKind.LaserCurtain: return 0.78f;
            case JHLPatternKind.SpiralBeam: return 0.72f;
            case JHLPatternKind.CrossfireBarrage: return 0.80f;
            case JHLPatternKind.SweepLaserCombo: return 0.74f;
            case JHLPatternKind.QuadSlam: return 0.70f;
            case JHLPatternKind.RapidLaserBurst: return 0.72f;
            case JHLPatternKind.FinalCompression: return 0.68f;
            case JHLPatternKind.MovingGate: return 0.72f;
            case JHLPatternKind.DiagonalCrossPunch: return 0.76f;
            case JHLPatternKind.PredictiveBombardment: return 0.74f;
            case JHLPatternKind.BeamPinch: return 0.70f;
            case JHLPatternKind.RoarRepulse: return 0.01f;
            case JHLPatternKind.RemoteSuppression: return 0.01f;
            default: return 1f;
        }
    }

    private static int Count<T>(Queue<T> queue, T value)
    {
        int count = 0;
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        foreach (T item in queue)
            if (comparer.Equals(item, value)) count++;
        return count;
    }
}
