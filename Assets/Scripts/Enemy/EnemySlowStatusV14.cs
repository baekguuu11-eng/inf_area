using UnityEngine;

/// <summary>
/// V14 whip crowd-control status. The slow affects movement only; attack timings are unchanged.
/// Reapplying refreshes duration and keeps the stronger active slow.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySlowStatusV14 : MonoBehaviour
{
    private EnemyMotor motor;
    private float remaining;
    private float activeMultiplier = 1f;

    private void Awake()
    {
        motor = GetComponent<EnemyMotor>();
        if (motor == null) motor = GetComponentInParent<EnemyMotor>();
    }

    public void Apply(float speedMultiplier, float duration)
    {
        if (motor == null) return;
        float safeMultiplier = Mathf.Clamp(speedMultiplier, 0.25f, 1f);
        activeMultiplier = remaining > 0f ? Mathf.Min(activeMultiplier, safeMultiplier) : safeMultiplier;
        remaining = Mathf.Max(remaining, Mathf.Max(0.05f, duration));
        motor.SetExternalSpeedMultiplier(activeMultiplier);
    }

    private void Update()
    {
        if (remaining <= 0f) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            activeMultiplier = 1f;
            if (motor != null) motor.SetExternalSpeedMultiplier(1f);
        }
    }

    private void OnDisable()
    {
        remaining = 0f;
        activeMultiplier = 1f;
        if (motor != null) motor.SetExternalSpeedMultiplier(1f);
    }
}
