using System.Collections;
using UnityEngine;

public sealed partial class JHLBossController
{
    // Retained null for old debug reset paths; JHL no longer attaches this component.
    private BossOpeningWindowV15 openingV15;
    private int attacksSinceComboV15;
    private float nextImpactV15;

    private void RegisterPressureV16(int damage)
    {
        if (!combatStarted || dead || state != BossState.Combat || Time.time < antiPressureCooldownUntil) return;
        if (Time.time > pressureWindowUntil)
        {
            meleePressureDamage = rangedPressureDamage = 0f;
            pressureWindowUntil = Time.time + 3.2f;
        }
        if (IsPlayerInFaceMeleeZone()) meleePressureDamage += damage;
        else rangedPressureDamage += damage;
        if (meleePressureDamage >= health.MaxHealth * 0.055f)
            RequestAntiPressureCounter(JHLPatternKind.RoarRepulse);
        else if (rangedPressureDamage >= health.MaxHealth * 0.08f)
            RequestAntiPressureCounter(JHLPatternKind.RoarRepulse);
    }

    private IEnumerator PressureAimV16()
    {
        yield return new WaitForSeconds(0.30f);
        if (!dead) yield return StraightLaserRoutine(0.95f, 0.34f, 1);
        auxiliaryPatternRoutine = null;
    }
}
